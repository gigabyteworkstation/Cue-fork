using System.Collections.Generic;
using UnityEngine;

namespace Cue
{
    // Authoring UI for the sound-graph engine: create/edit patches (trigger ->
    // node tree) and author Cuneiform scripts. Built only from widgets already
    // proven in SoundTab (combo boxes, sliders, checkboxes, show/hide) plus the
    // multiline TextBox for the script editor.
    //
    // The node tree is shown as an indented dropdown; selecting a node reveals
    // its parameter controls. Every GraphValue parameter gets a "source"
    // dropdown (constant / any signal) plus a value slider.
    class SoundGraphTab : Tab
    {
        private class NodeRef
        {
            public Sound.SoundNode node;
            public Sound.SoundNode parent;
            public int depth;
        }

        private readonly Person person_;
        private bool ignore_ = false;

        // ---- patches
        private VUI.ComboBox<Sound.SoundPatch> patches_;
        private VUI.TextBox patchName_;
        private VUI.ComboBox<string> patchTrigger_;
        private VUI.ComboBox<string> patchPart_;
        private VUI.ComboBox<string> patchOrifice_;
        private VUI.FloatTextSlider patchInterval_;
        private VUI.CheckBox patchEnabled_;
        private VUI.TextBox patchCustom_;

        // section selector: only one of these groups is visible at a time, so
        // each section gets the full panel height (VUI has no scroll container)
        private VUI.ComboBox<string> section_;
        private VUI.Panel patchGroup_, scriptGroup_;

        // ---- scripts
        private VUI.ComboBox<Sound.Script> scriptList_;
        private VUI.TextBox scriptName_;
        private VUI.CheckBox scriptEnabled_;
        private VUI.TextBox scriptSource_;
        private VUI.Label scriptInfo_;

        // ---- node tree
        private VUI.ComboBox<string> nodeList_;
        private VUI.ComboBox<string> nodeAddType_;
        private VUI.Label nodeTypeLabel_;
        private VUI.ComboBox<string> clipSet_;
        private VUI.CheckBox clipLoop_;

        // up to 3 GraphValue rows (intensity/gain/pitch or value/interval)
        private const int GvRows = 3;
        private VUI.Label[] gvLabel_ = new VUI.Label[GvRows];
        private VUI.ComboBox<string>[] gvSource_ = new VUI.ComboBox<string>[GvRows];
        private VUI.FloatTextSlider[] gvSlider_ = new VUI.FloatTextSlider[GvRows];
        private Sound.GraphValue[] gvBound_ = new Sound.GraphValue[GvRows];

        // envelope sliders
        private VUI.FloatTextSlider envAttack_, envHold_, envRelease_;

        // math node
        private VUI.ComboBox<string> mathOp_, mathTarget_;

        // conditional sub-panels (toggled whole, so their labels hide too)
        private VUI.Panel clipPanel_, envPanel_, mathPanel_;
        private VUI.Panel[] gvRowPanel_ = new VUI.Panel[GvRows];

        // last-seen engine list sizes, so the tab self-heals when data is loaded
        // AFTER the UI was built (the plugin builds its UI before CheckConfig
        // restores the saved graph)
        private int seenPatches_ = -1, seenSets_ = -1, seenScripts_ = -1;

        private readonly List<NodeRef> nodeRefs_ = new List<NodeRef>();
        private readonly List<string> partNames_ = new List<string>();
        private readonly List<string> sourceNames_ = new List<string>();

        public SoundGraphTab(Person p)
            : base("Graph", false)
        {
            person_ = p;
            BuildPartNames();
            Build();
            RefreshPatchList();
            ShowSelectedPatch();
            RefreshScriptList();
            ShowSelectedScript();
        }

        public override bool DebugOnly { get { return false; } }

        private Sound.SoundGraphEngine Engine
        {
            get { return person_.Sounds != null ? person_.Sounds.Graph : null; }
        }

        // ---------------------------------------------------------------------

        private void BuildPartNames()
        {
            partNames_.Add("Any");
            foreach (BodyPartType b in BodyPartType.Values)
                partNames_.Add(BodyPartType.ToString(b));
        }

        private List<string> TriggerNames()
        {
            var l = new List<string>();
            for (int i = 0; i < Sound.SoundRule.TriggerNames.Length; ++i)
                l.Add(Sound.SoundRule.TriggerNames[i]);
            l.Add("Always (start on load)");
            return l;
        }

        // source list for a GraphValue: constant + built-in signals
        private void RebuildSourceNames()
        {
            sourceNames_.Clear();
            sourceNames_.Add("(constant)");
            for (int i = 0; i < Sound.GVar.Names.Length; ++i)
                sourceNames_.Add(Sound.GVar.Names[i]);
        }

        // ---------------------------------------------------------------------

        private void Build()
        {
            Layout = new VUI.VerticalFlow(4);

            // section selector (one group visible at a time)
            var top = new VUI.Panel(new VUI.HorizontalFlow(6));
            top.Add(new VUI.Label("Section:"));
            section_ = top.Add(new VUI.ComboBox<string>(
                new string[] { "Patches", "Scripts" }, OnSection));
            section_.MinimumSize = new VUI.Size(180, VUI.Widget.DontCare);
            Add(top);

            patchGroup_  = new VUI.Panel(new VUI.VerticalFlow(3));
            scriptGroup_ = new VUI.Panel(new VUI.VerticalFlow(3));

            BuildPatchGroup();
            BuildScriptGroup();

            Add(patchGroup_);
            Add(scriptGroup_);

            ShowSection(0);
        }

        private void OnSection(int i) { ShowSection(i); }

        private void ShowSection(int i)
        {
            patchGroup_.Visible  = (i == 0);
            scriptGroup_.Visible = (i == 1);
        }

        private void BuildScriptGroup()
        {
            scriptGroup_.Add(new VUI.Label("Scripts", UnityEngine.FontStyle.Bold));

            var s1 = new VUI.Panel(new VUI.HorizontalFlow(6));
            scriptList_ = s1.Add(new VUI.ComboBox<Sound.Script>(OnScriptSelected));
            scriptList_.MinimumSize = new VUI.Size(240, VUI.Widget.DontCare);
            s1.Add(new VUI.Button("Add", OnAddScript));
            s1.Add(new VUI.Button("Remove", OnRemoveScript));
            scriptEnabled_ = s1.Add(new VUI.CheckBox("Enabled", OnScriptEnabled));
            scriptGroup_.Add(s1);

            var s2 = new VUI.Panel(new VUI.HorizontalFlow(6));
            s2.Add(new VUI.Label("Name:"));
            scriptName_ = s2.Add(new VUI.TextBox("", "script name", OnScriptName));
            scriptName_.MinimumSize = new VUI.Size(200, VUI.Widget.DontCare);
            scriptGroup_.Add(s2);

            scriptInfo_ = scriptGroup_.Add(new VUI.Label("--"));

            scriptSource_ = scriptGroup_.Add(new VUI.TextBox("", "code...", OnScriptSource));
            scriptSource_.Multiline = true;
            scriptSource_.MinimumSize = new VUI.Size(470, 230);
        }

        private void BuildPatchGroup()
        {
            patchGroup_.Add(new VUI.Label("Patches", UnityEngine.FontStyle.Bold));

            var p1 = new VUI.Panel(new VUI.HorizontalFlow(6));
            patches_ = p1.Add(new VUI.ComboBox<Sound.SoundPatch>(OnPatchSelected));
            patches_.MinimumSize = new VUI.Size(280, VUI.Widget.DontCare);
            p1.Add(new VUI.Button("Add", OnAddPatch));
            p1.Add(new VUI.Button("Remove", OnRemovePatch));
            patchGroup_.Add(p1);

            var p2 = new VUI.Panel(new VUI.HorizontalFlow(6));
            p2.Add(new VUI.Label("Name:"));
            patchName_ = p2.Add(new VUI.TextBox("", "patch name", OnPatchName));
            patchEnabled_ = p2.Add(new VUI.CheckBox("Enabled", OnPatchEnabled));
            patchGroup_.Add(p2);

            var p3 = new VUI.Panel(new VUI.HorizontalFlow(6));
            p3.Add(new VUI.Label("Trigger:"));
            patchTrigger_ = p3.Add(new VUI.ComboBox<string>(TriggerNames().ToArray(), OnPatchTrigger));
            patchTrigger_.MinimumSize = new VUI.Size(200, VUI.Widget.DontCare);
            p3.Add(new VUI.Label("Interval:"));
            patchInterval_ = p3.Add(new VUI.FloatTextSlider(0f, 0f, 3f, OnPatchInterval));
            patchGroup_.Add(p3);

            var p4 = new VUI.Panel(new VUI.HorizontalFlow(6));
            p4.Add(new VUI.Label("Body part:"));
            patchPart_ = p4.Add(new VUI.ComboBox<string>(partNames_.ToArray(), OnPatchPart));
            patchPart_.MinimumSize = new VUI.Size(160, VUI.Widget.DontCare);
            p4.Add(new VUI.Label("Orifice:"));
            patchOrifice_ = p4.Add(new VUI.ComboBox<string>(Sound.SoundRule.OrificeNames, OnPatchOrifice));
            patchOrifice_.MinimumSize = new VUI.Size(130, VUI.Widget.DontCare);
            patchGroup_.Add(p4);

            var p5 = new VUI.Panel(new VUI.HorizontalFlow(6));
            p5.Add(new VUI.Label("Custom trigger:"));
            patchCustom_ = p5.Add(new VUI.TextBox(
                "", "blank = use event above; else fired by a script trigger()", OnPatchCustom));
            patchCustom_.MinimumSize = new VUI.Size(280, VUI.Widget.DontCare);
            patchGroup_.Add(p5);

            // ---- node tree ----
            patchGroup_.Add(new VUI.Label("Node tree", UnityEngine.FontStyle.Bold));

            var n1 = new VUI.Panel(new VUI.HorizontalFlow(6));
            nodeList_ = n1.Add(new VUI.ComboBox<string>(new string[] { "(empty)" }, OnNodeSelected));
            nodeList_.MinimumSize = new VUI.Size(300, VUI.Widget.DontCare);
            nodeAddType_ = n1.Add(new VUI.ComboBox<string>(Sound.SoundNode.AllTypes, null));
            nodeAddType_.MinimumSize = new VUI.Size(120, VUI.Widget.DontCare);
            n1.Add(new VUI.Button("Add child", OnAddNode));
            n1.Add(new VUI.Button("Remove", OnRemoveNode));
            patchGroup_.Add(n1);

            nodeTypeLabel_ = patchGroup_.Add(new VUI.Label("node: (none)"));

            var n2 = new VUI.Panel(new VUI.HorizontalFlow(6));
            n2.Add(new VUI.Label("Set:"));
            clipSet_ = n2.Add(new VUI.ComboBox<string>(new string[] { "" }, OnClipSet));
            clipSet_.MinimumSize = new VUI.Size(200, VUI.Widget.DontCare);
            clipLoop_ = n2.Add(new VUI.CheckBox("Loop", OnClipLoop));
            clipPanel_ = n2;
            patchGroup_.Add(n2);

            for (int i = 0; i < GvRows; ++i)
            {
                int k = i;
                var row = new VUI.Panel(new VUI.HorizontalFlow(6));
                gvLabel_[k] = row.Add(new VUI.Label("param:"));
                gvSource_[k] = row.Add(new VUI.ComboBox<string>(new string[] { "(constant)" }, (idx) => OnGvSource(k, idx)));
                gvSource_[k].MinimumSize = new VUI.Size(160, VUI.Widget.DontCare);
                gvSlider_[k] = row.Add(new VUI.FloatTextSlider(1f, 0f, 2f, (f) => OnGvConst(k, f)));
                gvRowPanel_[k] = row;
                patchGroup_.Add(row);
            }

            var ne = new VUI.Panel(new VUI.HorizontalFlow(6));
            ne.Add(new VUI.Label("Env A/H/R:"));
            envAttack_  = ne.Add(new VUI.FloatTextSlider(0.02f, 0f, 3f, OnEnvAttack));
            envHold_    = ne.Add(new VUI.FloatTextSlider(0f, 0f, 10f, OnEnvHold));
            envRelease_ = ne.Add(new VUI.FloatTextSlider(0.1f, 0f, 5f, OnEnvRelease));
            envPanel_ = ne;
            patchGroup_.Add(ne);

            var nm = new VUI.Panel(new VUI.HorizontalFlow(6));
            nm.Add(new VUI.Label("Op:"));
            mathOp_ = nm.Add(new VUI.ComboBox<string>(Sound.MathOp.Names, OnMathOp));
            mathOp_.MinimumSize = new VUI.Size(120, VUI.Widget.DontCare);
            nm.Add(new VUI.Label("Modulates:"));
            mathTarget_ = nm.Add(new VUI.ComboBox<string>(Sound.MathTarget.Names, OnMathTarget));
            mathTarget_.MinimumSize = new VUI.Size(100, VUI.Widget.DontCare);
            mathPanel_ = nm;
            patchGroup_.Add(nm);
        }

        // ===================== PATCHES =====================

        private void RefreshPatchList()
        {
            if (Engine == null) return;
            ignore_ = true;
            patches_.SetItems(Engine.Patches);
            ignore_ = false;
            seenPatches_ = Engine.Patches.Count;
        }

        private void ShowSelectedPatch()
        {
            ignore_ = true;

            var p = patches_.Selected;
            if (p != null)
            {
                patchName_.Text = p.name;
                patchEnabled_.Checked = p.enabled;
                patchTrigger_.Select(TriggerToIndex(p.trigger));
                patchPart_.Select(PartToIndex(p.part));
                patchOrifice_.Select(p.orifice);
                patchInterval_.Value = p.minInterval;
                patchCustom_.Text = p.customTrigger;
            }

            ignore_ = false;

            RebuildNodeList();
            ShowSelectedNode();
        }

        private void OnPatchCustom(string s)
        {
            if (ignore_ || patches_.Selected == null) return;
            patches_.Selected.customTrigger = s;
            Save();
        }

        private int TriggerToIndex(int trigger)
        {
            if (trigger == Sound.SoundPatch.TriggerAlways)
                return Sound.SoundRule.TriggerNames.Length;  // the "Always" entry
            if (trigger >= 0 && trigger < Sound.SoundRule.TriggerNames.Length)
                return trigger;
            return 0;
        }

        private int IndexToTrigger(int i)
        {
            if (i >= Sound.SoundRule.TriggerNames.Length)
                return Sound.SoundPatch.TriggerAlways;
            return i;
        }

        private void OnPatchSelected(Sound.SoundPatch p) { if (!ignore_) ShowSelectedPatch(); }

        private void OnAddPatch()
        {
            if (Engine == null) return;
            var p = new Sound.SoundPatch { name = "patch" + (Engine.Patches.Count + 1) };
            Engine.Patches.Add(p);
            RefreshPatchList();
            patches_.Select(Engine.Patches.Count - 1);
            ShowSelectedPatch();
            Save();
        }

        private void OnRemovePatch()
        {
            if (Engine == null || patches_.Selected == null) return;
            Engine.Patches.Remove(patches_.Selected);
            RefreshPatchList();
            ShowSelectedPatch();
            Save();
        }

        private void OnPatchName(string s)
        {
            if (ignore_ || patches_.Selected == null) return;
            var cur = patches_.Selected;
            cur.name = s;
            int idx = Engine.Patches.IndexOf(cur);
            ignore_ = true;
            patches_.SetItems(Engine.Patches);
            if (idx >= 0) patches_.Select(idx);
            ignore_ = false;
            Save();
        }

        private void OnPatchEnabled(bool b)  { if (!ignore_ && patches_.Selected != null) { patches_.Selected.enabled = b; Relabel(); Save(); } }
        private void OnPatchInterval(float f) { if (!ignore_ && patches_.Selected != null) { patches_.Selected.minInterval = f; Save(); } }
        private void OnPatchOrifice(int i)    { if (!ignore_ && patches_.Selected != null) { patches_.Selected.orifice = i; Relabel(); Save(); } }

        private void OnPatchTrigger(int i)
        {
            if (ignore_ || patches_.Selected == null) return;
            patches_.Selected.trigger = IndexToTrigger(i);
            Relabel(); Save();
        }

        private void OnPatchPart(int i)
        {
            if (ignore_ || patches_.Selected == null) return;
            patches_.Selected.part = IndexToPart(i);
            Relabel(); Save();
        }

        private void Relabel()
        {
            if (Engine == null || patches_.Selected == null) return;
            int idx = Engine.Patches.IndexOf(patches_.Selected);
            ignore_ = true;
            patches_.SetItems(Engine.Patches);
            if (idx >= 0) patches_.Select(idx);
            ignore_ = false;
        }

        // ===================== NODE TREE =====================

        private void RebuildNodeList()
        {
            nodeRefs_.Clear();
            var p = patches_.Selected;
            if (p != null && p.root != null)
                Flatten(p.root, null, 0);

            var labels = new List<string>();
            if (nodeRefs_.Count == 0)
            {
                labels.Add("(empty - Add child to create root)");
            }
            else
            {
                for (int i = 0; i < nodeRefs_.Count; ++i)
                {
                    var nr = nodeRefs_[i];
                    labels.Add(new string(' ', nr.depth * 2) + Describe(nr.node));
                }
            }

            ignore_ = true;
            nodeList_.SetItems(labels);
            ignore_ = false;
        }

        private void Flatten(Sound.SoundNode node, Sound.SoundNode parent, int depth)
        {
            nodeRefs_.Add(new NodeRef { node = node, parent = parent, depth = depth });
            for (int i = 0; i < node.children.Count; ++i)
                Flatten(node.children[i], node, depth + 1);
        }

        private string Describe(Sound.SoundNode n)
        {
            string s = n.Type;
            var clip = n as Sound.ClipNode;
            if (clip != null)
                s += " [" + (string.IsNullOrEmpty(clip.set) ? "no set" : clip.set) + (clip.loop ? " loop" : "") + "]";
            return s;
        }

        private NodeRef CurrentNodeRef()
        {
            int i = nodeList_.SelectedIndex;
            if (i >= 0 && i < nodeRefs_.Count)
                return nodeRefs_[i];
            return null;
        }

        private void OnNodeSelected(int i) { if (!ignore_) ShowSelectedNode(); }

        private void OnAddNode()
        {
            var p = patches_.Selected;
            if (p == null) return;

            string type = nodeAddType_.Selected;
            if (string.IsNullOrEmpty(type)) type = "clip";
            var n = Sound.SoundNode.CreateByType(type);
            if (n == null) return;

            if (p.root == null)
            {
                p.root = n;
            }
            else
            {
                var nr = CurrentNodeRef();
                var parent = (nr != null) ? nr.node : p.root;
                parent.children.Add(n);
            }

            RebuildNodeList();
            ShowSelectedNode();
            Save();
        }

        private void OnRemoveNode()
        {
            var p = patches_.Selected;
            var nr = CurrentNodeRef();
            if (p == null || nr == null) return;

            if (nr.parent == null)
                p.root = null;             // removed the root
            else
                nr.parent.children.Remove(nr.node);

            RebuildNodeList();
            ShowSelectedNode();
            Save();
        }

        private void ShowSelectedNode()
        {
            ignore_ = true;

            // hide whole panels by default (so their labels hide too)
            clipPanel_.Visible = false;
            for (int i = 0; i < GvRows; ++i)
            {
                gvRowPanel_[i].Visible = false;
                gvBound_[i] = null;
            }
            envPanel_.Visible = false;
            mathPanel_.Visible = false;

            var nr = CurrentNodeRef();
            if (nr == null) { nodeTypeLabel_.Text = "node: (none)"; ignore_ = false; return; }

            nodeTypeLabel_.Text = "node: " + nr.node.Type;
            RebuildSourceNames();

            var clip = nr.node as Sound.ClipNode;
            var gain = nr.node as Sound.GainNode;
            var pitch = nr.node as Sound.PitchNode;
            var loop = nr.node as Sound.LoopNode;
            var env = nr.node as Sound.EnvelopeNode;
            var math = nr.node as Sound.MathNode;

            if (clip != null)
            {
                clipPanel_.Visible = true;
                clipSet_.SetItems(SetNames());
                clipSet_.Select(SetIndex(clip.set));
                clipLoop_.Checked = clip.loop;

                ConfigGv(0, "intensity:", clip.intensity, 0f, 1f);
                ConfigGv(1, "gain:", clip.gain, 0f, 2f);
                ConfigGv(2, "pitch:", clip.pitch, 0.5f, 2f);
            }
            else if (gain != null)
            {
                ConfigGv(0, "gain:", gain.gain, 0f, 2f);
            }
            else if (pitch != null)
            {
                ConfigGv(0, "pitch:", pitch.pitch, 0.5f, 2f);
            }
            else if (loop != null)
            {
                ConfigGv(0, "interval (s):", loop.interval, 0f, 3f);
            }
            else if (env != null)
            {
                envPanel_.Visible = true;
                envAttack_.Value = env.attack;
                envHold_.Value = env.hold;
                envRelease_.Value = env.release;
            }
            else if (math != null)
            {
                ConfigGv(0, "a:", math.a, -2f, 2f);
                ConfigGv(1, "b:", math.b, -2f, 2f);
                mathPanel_.Visible = true;
                mathOp_.Select(math.op);
                mathTarget_.Select(math.target);
            }

            ignore_ = false;
        }

        private void ConfigGv(int row, string label, Sound.GraphValue gv, float min, float max)
        {
            gvBound_[row] = gv;
            gvRowPanel_[row].Visible = true;   // shows the whole row (label included)

            gvLabel_[row].Text = label;
            gvSource_[row].SetItems(sourceNames_);
            gvSource_[row].Select(GvSourceIndex(gv));
            gvSlider_[row].Minimum = min;
            gvSlider_[row].Maximum = max;
            gvSlider_[row].Value = gv.constant;
        }

        // source index: 0=const, 1..N=signal id+1
        private int GvSourceIndex(Sound.GraphValue gv)
        {
            if (!string.IsNullOrEmpty(gv.varName))
            {
                int idx = sourceNames_.IndexOf(gv.varName);
                return (idx >= 0) ? idx : 0;
            }
            if (gv.varId >= 0)
                return gv.varId + 1;
            return 0;
        }

        private void OnGvSource(int row, int idx) { ApplyGvSource(gvBound_[row], idx); }

        // Maps a source-dropdown index to a GraphValue binding: 0=constant,
        // 1..N=built-in signal.
        private void ApplyGvSource(Sound.GraphValue gv, int idx)
        {
            if (ignore_ || gv == null) return;

            if (idx <= 0)
            {
                gv.varId = -1; gv.varName = null;
            }
            else if (idx <= Sound.GVar.Names.Length)
            {
                gv.varId = idx - 1; gv.varName = null;
            }
            Save();
        }

        private void OnGvConst(int row, float f)
        {
            if (ignore_ || gvBound_[row] == null) return;
            gvBound_[row].constant = f;
            Save();
        }

        private void OnClipSet(int i)
        {
            if (ignore_) return;
            var nr = CurrentNodeRef();
            var clip = (nr != null) ? nr.node as Sound.ClipNode : null;
            if (clip == null) return;
            var names = SetNames();
            clip.set = (i >= 0 && i < names.Count) ? names[i] : "";
            RebuildNodeList();
            Save();
        }

        private void OnClipLoop(bool b)
        {
            if (ignore_) return;
            var nr = CurrentNodeRef();
            var clip = (nr != null) ? nr.node as Sound.ClipNode : null;
            if (clip == null) return;
            clip.loop = b;
            RebuildNodeList();
            Save();
        }

        private void OnEnvAttack(float f)  { EnvSet(0, f); }
        private void OnEnvHold(float f)    { EnvSet(1, f); }
        private void OnEnvRelease(float f) { EnvSet(2, f); }

        private void OnMathOp(int i)
        {
            if (ignore_) return;
            var nr = CurrentNodeRef();
            var m = (nr != null) ? nr.node as Sound.MathNode : null;
            if (m != null) { m.op = i; Save(); }
        }

        private void OnMathTarget(int i)
        {
            if (ignore_) return;
            var nr = CurrentNodeRef();
            var m = (nr != null) ? nr.node as Sound.MathNode : null;
            if (m != null) { m.target = i; Save(); }
        }

        private void EnvSet(int which, float f)
        {
            if (ignore_) return;
            var nr = CurrentNodeRef();
            var env = (nr != null) ? nr.node as Sound.EnvelopeNode : null;
            if (env == null) return;
            if (which == 0) env.attack = f;
            else if (which == 1) env.hold = f;
            else env.release = f;
            Save();
        }

        private List<string> SetNames()
        {
            var l = new List<string>();
            var sets = Sound.SoundManager.Instance.Sets;
            for (int i = 0; i < sets.Count; ++i)
                l.Add(sets[i].Name);
            if (l.Count == 0) l.Add("");
            return l;
        }

        private int SetIndex(string name)
        {
            var names = SetNames();
            for (int i = 0; i < names.Count; ++i)
                if (names[i] == name) return i;
            return 0;
        }

        // ===================== helpers =====================

        private int PartToIndex(BodyPartType part)
        {
            if (part == BP.None) return 0;
            int i = 1;
            foreach (BodyPartType b in BodyPartType.Values) { if (b == part) return i; ++i; }
            return 0;
        }

        private BodyPartType IndexToPart(int i)
        {
            if (i <= 0) return BP.None;
            int n = 1;
            foreach (BodyPartType b in BodyPartType.Values) { if (n == i) return b; ++n; }
            return BP.None;
        }

        // ===================== SCRIPTS =====================

        private List<Sound.Script> ScriptList
        {
            get { return Engine != null ? Engine.Scripts : null; }
        }

        private void RefreshScriptList()
        {
            if (ScriptList == null) return;
            ignore_ = true;
            scriptList_.SetItems(ScriptList);
            ignore_ = false;
            seenScripts_ = ScriptList.Count;
        }

        // Self-heal: when the engine's data changes outside the UI (most
        // importantly when the saved graph loads after this tab was built),
        // rebuild the affected lists so they actually show the loaded data.
        protected override void DoUpdate(float s)
        {
            if (Engine == null) return;

            if (Engine.Patches.Count != seenPatches_)
            {
                RefreshPatchList();
                ShowSelectedPatch();
            }

            if (ScriptList != null && ScriptList.Count != seenScripts_)
            {
                RefreshScriptList();
                ShowSelectedScript();
            }

            int sets = Sound.SoundManager.Instance.Sets.Count;
            if (sets != seenSets_)
            {
                seenSets_ = sets;
                // refresh the clip-set dropdown for the currently-shown node
                ShowSelectedNode();
            }

            // live profiling readout for the selected script
            if (scriptGroup_.Visible && scriptList_.Selected != null)
                UpdateScriptInfo();
        }

        private void ShowSelectedScript()
        {
            ignore_ = true;
            var sc = scriptList_.Selected;
            if (sc != null)
            {
                scriptName_.Text = sc.name;
                scriptEnabled_.Checked = sc.enabled;
                scriptSource_.Text = sc.source;
            }
            else
            {
                scriptName_.Text = "";
                scriptSource_.Text = "";
            }
            UpdateScriptInfo();
            ignore_ = false;
        }

        private void UpdateScriptInfo()
        {
            var sc = scriptList_.Selected;
            if (sc == null) { scriptInfo_.Text = "--"; return; }

            string err = sc.Error;
            if (!string.IsNullOrEmpty(err))
                scriptInfo_.Text = "ERROR: " + err;
            else
                scriptInfo_.Text = "instr/frame: " + sc.lastInstr +
                    "   vars: " + sc.totalVars +
                    "   mem: " + FormatBytes(sc.memBytes) +
                    "   " + sc.lastMicros.ToString("0.0") + " us";
        }

        private static string FormatBytes(int b)
        {
            if (b < 1024) return b + " B";
            return (b / 1024f).ToString("0.0") + " KB";
        }

        private void OnScriptSelected(Sound.Script s) { if (!ignore_) ShowSelectedScript(); }

        private void OnAddScript()
        {
            if (ScriptList == null) return;
            ScriptList.Add(new Sound.Script { name = "script" + (ScriptList.Count + 1), source = "" });
            RefreshScriptList();
            scriptList_.Select(ScriptList.Count - 1);
            ShowSelectedScript();
            Save();
        }

        private void OnRemoveScript()
        {
            if (ScriptList == null || scriptList_.Selected == null) return;
            ScriptList.Remove(scriptList_.Selected);
            RefreshScriptList();
            ShowSelectedScript();
            Save();
        }

        private void OnScriptName(string s)
        {
            if (ignore_ || scriptList_.Selected == null) return;
            var cur = scriptList_.Selected;
            cur.name = s;
            int idx = ScriptList.IndexOf(cur);
            ignore_ = true;
            scriptList_.SetItems(ScriptList);
            if (idx >= 0) scriptList_.Select(idx);
            ignore_ = false;
            Save();
        }

        private void OnScriptEnabled(bool b)
        {
            if (ignore_ || scriptList_.Selected == null) return;
            scriptList_.Selected.enabled = b;
            Save();
        }

        private void OnScriptSource(string s)
        {
            if (ignore_ || scriptList_.Selected == null) return;
            scriptList_.Selected.source = s;
            scriptList_.Selected.Compile();   // surface errors immediately
            UpdateScriptInfo();
            Save();
        }

        private void Save()
        {
            // Force the event detector to re-resolve its tracked body parts now
            // (e.g. you just set an impact patch to the vagina) instead of
            // waiting for the next periodic rebuild.
            if (person_.Sounds != null)
                person_.Sounds.RulesChanged();

            Cue.Instance.SaveLater();
        }
    }
}

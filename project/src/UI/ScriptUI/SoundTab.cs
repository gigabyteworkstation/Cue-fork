using System.Collections.Generic;
using UnityEngine;

namespace Cue
{
    // UI for the sound event system: manage sound sets (folder or assetbundle
    // sources, 3/5 intensity bands) and per-person trigger rules (impact,
    // penetration entry/exit, deep thrust, tongue, fingering, orgasm).
    class SoundTab : Tab
    {
        private readonly Person person_;

        // sets section
        private VUI.ComboBox<Sound.SoundSet> sets_;
        private VUI.TextBox setName_;
        private VUI.TextBox setSource_;
        private VUI.ComboBox<string> setType_;
        private VUI.TextBox setIntensity_;
        private VUI.Label setStatus_;

        // rules section
        private VUI.ComboBox<Sound.SoundRule> rules_;
        private VUI.ComboBox<string> ruleTrigger_;
        private VUI.ComboBox<string> rulePart_;
        private VUI.ComboBox<string> ruleOrifice_;
        private VUI.ComboBox<string> ruleSet_;
        private VUI.FloatTextSlider ruleVolume_;
        private VUI.FloatTextSlider rulePitch_;
        private VUI.FloatTextSlider ruleJitter_;
        private VUI.FloatTextSlider ruleIntVol_;
        private VUI.FloatTextSlider ruleVelPitch_;
        private VUI.FloatTextSlider ruleInterval_;
        private VUI.FloatTextSlider ruleDepth_;
        private VUI.FloatTextSlider ruleMinSpeed_;
        private VUI.FloatTextSlider ruleMaxSpeed_;
        private VUI.CheckBox ruleEnabled_;

        private VUI.FloatTextSlider masterVolume_;

        private VUI.Button[] testBtns_ = new VUI.Button[8];
        private float[] testInt_ = new float[8];

        private List<string> partNames_ = new List<string>();
        private bool ignore_ = false;

        public SoundTab(Person p)
            : base("Sounds", false)
        {
            person_ = p;
            BuildPartNames();
            Build();
            RefreshSets();
            RefreshRules();
        }

        public override bool DebugOnly { get { return false; } }

        private Sound.SoundManager Manager
        {
            get { return Sound.SoundManager.Instance; }
        }

        private Sound.SoundEventsEngine Engine
        {
            get { return person_.Sounds; }
        }

        private void BuildPartNames()
        {
            partNames_.Add("Any");
            foreach (BodyPartType b in BodyPartType.Values)
                partNames_.Add(BodyPartType.ToString(b));
        }

        private void Build()
        {
            Layout = new VUI.VerticalFlow(8);

            // ---- master volume -------------------------------------------
            var mv = new VUI.Panel(new VUI.HorizontalFlow(8));
            mv.Add(new VUI.Label("Master volume:"));
            masterVolume_ = mv.Add(new VUI.FloatTextSlider(
                Manager.MasterVolume, 0f, 2f, OnMasterVolume));
            masterVolume_.MinimumSize = new VUI.Size(200, VUI.Widget.DontCare);
            Add(mv);

            // ---- sound sets ----------------------------------------------
            Add(new VUI.Label("Sound sets", UnityEngine.FontStyle.Bold));

            var sp1 = new VUI.Panel(new VUI.HorizontalFlow(8));
            sets_ = sp1.Add(new VUI.ComboBox<Sound.SoundSet>(OnSetSelected));
            sets_.MinimumSize = new VUI.Size(280, VUI.Widget.DontCare);
            sp1.Add(new VUI.Button("Add set", OnAddSet));
            sp1.Add(new VUI.Button("Remove", OnRemoveSet));
            Add(sp1);

            var sp2 = new VUI.Panel(new VUI.HorizontalFlow(8));
            sp2.Add(new VUI.Label("Name:"));
            setName_ = sp2.Add(new VUI.TextBox("", "set name", OnSetName));
            Add(sp2);

            var sp2b = new VUI.Panel(new VUI.HorizontalFlow(8));
            sp2b.Add(new VUI.Label("Intensities:"));
            setIntensity_ = sp2b.Add(new VUI.TextBox(
                "", "blank = none (random); e.g.  soft, medium, hard", OnSetIntensity));
            setIntensity_.MinimumSize = new VUI.Size(360, VUI.Widget.DontCare);
            Add(sp2b);

            var sp3 = new VUI.Panel(new VUI.HorizontalFlow(8));
            sp3.Add(new VUI.Label("Source:"));
            setSource_ = sp3.Add(new VUI.TextBox("", "folder or .assetbundle path", OnSetSource));
            // Fix both min AND max so a long picked path can't expand the box and
            // shove the type dropdown / Browse button off the row. The path still
            // scrolls horizontally inside the fixed-width field.
            setSource_.MinimumSize = new VUI.Size(330, VUI.Widget.DontCare);
            setSource_.MaximumSize = new VUI.Size(330, VUI.Widget.DontCare);
            setType_ = sp3.Add(new VUI.ComboBox<string>(
                new string[] { "Folder", "AssetBundle" }, OnSetType));
            // Without an explicit minimum the closed combobox can collapse to
            // nothing once a value is picked.
            setType_.MinimumSize = new VUI.Size(150, VUI.Widget.DontCare);
            sp3.Add(new VUI.Button("Browse...", OnBrowse));
            Add(sp3);

            // Test buttons are rebuilt per set: one per intensity name, or a
            // single "Test (random)" when the set has no intensities.
            var sp4 = new VUI.Panel(new VUI.HorizontalFlow(8));
            sp4.Add(new VUI.Button("Reload clips", OnReloadSet));
            for (int i = 0; i < testBtns_.Length; ++i)
            {
                int k = i;
                testBtns_[k] = sp4.Add(new VUI.Button("Test", () => OnTest(testInt_[k])));
            }
            setStatus_ = sp4.Add(new VUI.Label(""));
            Add(sp4);

            Add(new VUI.Spacer(10));

            // ---- rules ----------------------------------------------------
            Add(new VUI.Label("Trigger rules", UnityEngine.FontStyle.Bold));

            var rp1 = new VUI.Panel(new VUI.HorizontalFlow(8));
            rules_ = rp1.Add(new VUI.ComboBox<Sound.SoundRule>(OnRuleSelected));
            rules_.MinimumSize = new VUI.Size(420, VUI.Widget.DontCare);
            rp1.Add(new VUI.Button("Add rule", OnAddRule));
            rp1.Add(new VUI.Button("Remove", OnRemoveRule));
            Add(rp1);

            var rp2 = new VUI.Panel(new VUI.HorizontalFlow(8));
            rp2.Add(new VUI.Label("Trigger:"));
            ruleTrigger_ = rp2.Add(new VUI.ComboBox<string>(
                Sound.SoundRule.TriggerNames, OnRuleTrigger));
            ruleTrigger_.MinimumSize = new VUI.Size(200, VUI.Widget.DontCare);
            ruleEnabled_ = rp2.Add(new VUI.CheckBox("Enabled", OnRuleEnabled));
            Add(rp2);

            var rp3 = new VUI.Panel(new VUI.HorizontalFlow(8));
            rp3.Add(new VUI.Label("Body part:"));
            rulePart_ = rp3.Add(new VUI.ComboBox<string>(partNames_.ToArray(), OnRulePart));
            rulePart_.MinimumSize = new VUI.Size(170, VUI.Widget.DontCare);
            rp3.Add(new VUI.Label("Orifice:"));
            ruleOrifice_ = rp3.Add(new VUI.ComboBox<string>(
                Sound.SoundRule.OrificeNames, OnRuleOrifice));
            ruleOrifice_.MinimumSize = new VUI.Size(140, VUI.Widget.DontCare);
            Add(rp3);

            var rp4 = new VUI.Panel(new VUI.HorizontalFlow(8));
            rp4.Add(new VUI.Label("Sound set:"));
            // NOTE: the index-callback-only ComboBox ctor routes through the
            // (ItemType[], IndexCallback) overload with a null array and throws
            // in new List<>(null); always hand it a real array.
            ruleSet_ = rp4.Add(new VUI.ComboBox<string>(
                SetNamesWithNone().ToArray(), OnRuleSet));
            ruleSet_.MinimumSize = new VUI.Size(250, VUI.Widget.DontCare);
            Add(rp4);

            var gl = new VUI.GridLayout(4, 4);
            gl.HorizontalSpacing = 12;
            var grid = new VUI.Panel(gl);

            grid.Add(new VUI.Label("Volume:"));
            ruleVolume_ = grid.Add(new VUI.FloatTextSlider(1f, 0f, 2f, OnRuleVolume));
            grid.Add(new VUI.Label("Pitch:"));
            rulePitch_ = grid.Add(new VUI.FloatTextSlider(1f, 0.5f, 2f, OnRulePitch));

            grid.Add(new VUI.Label("Pitch jitter:"));
            ruleJitter_ = grid.Add(new VUI.FloatTextSlider(0.05f, 0f, 0.5f, OnRuleJitter));
            grid.Add(new VUI.Label("Vel→volume:"));
            ruleIntVol_ = grid.Add(new VUI.FloatTextSlider(1f, 0f, 1f, OnRuleIntVol));

            grid.Add(new VUI.Label("Vel→pitch:"));
            ruleVelPitch_ = grid.Add(new VUI.FloatTextSlider(0f, 0f, 1f, OnRuleVelPitch));
            grid.Add(new VUI.Label("Min interval:"));
            ruleInterval_ = grid.Add(new VUI.FloatTextSlider(0.15f, 0f, 5f, OnRuleInterval));

            grid.Add(new VUI.Label("Min speed (m/s):"));
            ruleMinSpeed_ = grid.Add(new VUI.FloatTextSlider(0.4f, 0f, 5f, OnRuleMinSpeed));
            grid.Add(new VUI.Label("Max speed (m/s):"));
            ruleMaxSpeed_ = grid.Add(new VUI.FloatTextSlider(2.8f, 0.1f, 8f, OnRuleMaxSpeed));

            grid.Add(new VUI.Label("Depth thresh:"));
            ruleDepth_ = grid.Add(new VUI.FloatTextSlider(0.8f, 0.1f, 1f, OnRuleDepth));

            Add(grid);
        }

        // ---- sets handlers ----------------------------------------------

        // Rebuilds the dropdown item lists. MUST NOT be called from the set
        // selection handler: SetItems resets the selection to index 0, which is
        // exactly what made the dropdown snap back to set1.
        private void RebuildSetList()
        {
            ignore_ = true;
            sets_.SetItems(Manager.Sets);
            ruleSet_.SetItems(SetNamesWithNone());
            ignore_ = false;
        }

        // Mirrors the currently-selected set into the editor fields. Safe to
        // call on selection change because it never touches the item list.
        private void ShowSelectedSet()
        {
            ignore_ = true;

            var s = sets_.Selected;
            if (s != null)
            {
                setName_.Text = s.Name;
                setSource_.Text = s.Source;
                setType_.Select(s.SourceType);
                setIntensity_.Text = s.IntensityCSV;
                setStatus_.Text = s.Status;
            }
            else
            {
                setName_.Text = "";
                setSource_.Text = "";
                setIntensity_.Text = "";
                setStatus_.Text = "";
            }

            ConfigureTestButtons(s);

            ignore_ = false;
        }

        // One test button per intensity band (labelled with the band name), or a
        // single "Test (random)" when the set has no intensities. Hidden buttons
        // are skipped by the flow layout, so the row reflows cleanly.
        private void ConfigureTestButtons(Sound.SoundSet s)
        {
            for (int i = 0; i < testBtns_.Length; ++i)
            {
                if (testBtns_[i] != null)
                    testBtns_[i].Visible = false;
            }

            if (s == null)
                return;

            if (s.HasIntensities)
            {
                var names = s.IntensityNames;
                int count = System.Math.Min(names.Count, testBtns_.Length);
                for (int i = 0; i < count; ++i)
                {
                    testInt_[i] = (count <= 1) ? 0.5f : (i + 0.5f) / count;
                    testBtns_[i].Text = "Test: " + names[i];
                    testBtns_[i].Visible = true;
                }
            }
            else
            {
                testInt_[0] = 0.5f;
                testBtns_[0].Text = "Test (random)";
                testBtns_[0].Visible = true;
            }
        }

        private void RefreshSets()
        {
            RebuildSetList();
            ShowSelectedSet();
        }

        private List<string> SetNamesWithNone()
        {
            var names = new List<string>();
            names.Add("(none)");
            for (int i = 0; i < Manager.Sets.Count; ++i)
                names.Add(Manager.Sets[i].Name);
            return names;
        }

        private void OnMasterVolume(float f)
        {
            if (ignore_) return;
            Manager.MasterVolume = f;
            Cue.Instance.SaveLater();
        }

        private void OnSetSelected(Sound.SoundSet s)
        {
            if (ignore_) return;
            ShowSelectedSet();
        }

        private void OnAddSet()
        {
            if (ignore_) return;
            var s = Manager.Add("set" + (Manager.Sets.Count + 1));
            RebuildSetList();
            sets_.Select(Manager.Sets.IndexOf(s));
            ShowSelectedSet();
            Cue.Instance.SaveLater();
        }

        private void OnRemoveSet()
        {
            if (ignore_) return;
            Manager.Remove(sets_.Selected);
            RebuildSetList();
            ShowSelectedSet();
            Cue.Instance.SaveLater();
        }

        private void OnSetName(string s)
        {
            if (ignore_ || sets_.Selected == null) return;
            var cur = sets_.Selected;
            cur.Name = s;

            int idx = Manager.Sets.IndexOf(cur);
            ignore_ = true;
            sets_.SetItems(Manager.Sets);
            ruleSet_.SetItems(SetNamesWithNone());
            if (idx >= 0) sets_.Select(idx);   // keep it selected, no re-entry
            ignore_ = false;

            Cue.Instance.SaveLater();
        }

        private void OnSetSource(string s)
        {
            if (ignore_ || sets_.Selected == null) return;
            sets_.Selected.Source = s;
            Cue.Instance.SaveLater();
        }

        private void OnSetType(int i)
        {
            if (ignore_ || sets_.Selected == null) return;
            sets_.Selected.SourceType = i;
            Cue.Instance.SaveLater();
        }

        private void OnSetIntensity(string s)
        {
            if (ignore_ || sets_.Selected == null) return;
            sets_.Selected.IntensityCSV = s;
            sets_.Selected.Reload();
            setStatus_.Text = sets_.Selected.Status;
            Cue.Instance.SaveLater();
        }

        private void OnBrowse()
        {
            var set = sets_.Selected;
            if (set == null) return;

            if (set.SourceType == Sound.SoundSet.SourceBundle)
            {
                Cue.Instance.Sys.LoadFileDialog("assetbundle", (path) =>
                {
                    if (string.IsNullOrEmpty(path)) return;
                    set.Source = path;
                    set.Reload();
                    ShowSelectedSet();
                    Cue.Instance.SaveLater();
                });
            }
            else
            {
                // VaM has no folder picker; pick any audio file and use its
                // directory
                Cue.Instance.Sys.LoadFileDialog("wav", (path) =>
                {
                    if (string.IsNullOrEmpty(path)) return;

                    int cut = path.LastIndexOfAny(new char[] { '/', '\\' });
                    if (cut > 0)
                        path = path.Substring(0, cut);

                    set.Source = path;
                    set.Reload();
                    ShowSelectedSet();
                    Cue.Instance.SaveLater();
                });
            }
        }

        private void OnReloadSet()
        {
            if (sets_.Selected == null) return;
            sets_.Selected.Reload();
            ShowSelectedSet();
        }

        private void OnTest(float intensity)
        {
            var set = sets_.Selected;
            if (set == null) return;

            var head = person_.Body.Get(BP.Head);
            var pos = (head != null)
                ? Sys.Vam.U.ToUnity(head.Position)
                : Sys.Vam.U.ToUnity(person_.Position);

            Manager.Play(set.Name, pos, intensity, 1f, 1f, 0.05f, 1f, 0f);
        }

        // ---- rules handlers ----------------------------------------------

        private void RebuildRuleList()
        {
            if (Engine == null) return;
            ignore_ = true;
            rules_.SetItems(Engine.Rules);
            ignore_ = false;
        }

        // Mirrors the selected rule into the editor controls; never rebuilds the
        // rule list (that would reset the selection back to rule 0).
        private void ShowSelectedRule()
        {
            ignore_ = true;

            var r = rules_.Selected;
            if (r != null)
            {
                ruleTrigger_.Select(r.trigger);
                rulePart_.Select(PartToIndex(r.part));
                ruleOrifice_.Select(r.orifice);
                SelectRuleSet(r.set);
                ruleVolume_.Value = r.volume;
                rulePitch_.Value = r.pitch;
                ruleJitter_.Value = r.pitchJitter;
                ruleIntVol_.Value = r.intensityToVolume;
                ruleVelPitch_.Value = r.velToPitch;
                ruleInterval_.Value = r.minInterval;
                ruleDepth_.Value = r.depthThreshold;
                ruleMinSpeed_.Value = r.minSpeed;
                ruleMaxSpeed_.Value = r.maxSpeed;
                ruleEnabled_.Checked = r.enabled;
            }

            ignore_ = false;
        }

        private void RefreshRules()
        {
            RebuildRuleList();
            ShowSelectedRule();
        }

        private int PartToIndex(BodyPartType part)
        {
            if (part == BP.None) return 0;

            int i = 1;
            foreach (BodyPartType b in BodyPartType.Values)
            {
                if (b == part) return i;
                ++i;
            }

            return 0;
        }

        private BodyPartType IndexToPart(int i)
        {
            if (i <= 0) return BP.None;

            int n = 1;
            foreach (BodyPartType b in BodyPartType.Values)
            {
                if (n == i) return b;
                ++n;
            }

            return BP.None;
        }

        private void SelectRuleSet(string name)
        {
            var names = SetNamesWithNone();
            ruleSet_.SetItems(names);

            int idx = 0;
            for (int i = 1; i < names.Count; ++i)
            {
                if (names[i] == name)
                {
                    idx = i;
                    break;
                }
            }

            ruleSet_.Select(idx);
        }

        private void OnRuleSelected(Sound.SoundRule r)
        {
            if (ignore_) return;
            ShowSelectedRule();
        }

        private void OnAddRule()
        {
            if (Engine == null) return;
            Engine.Rules.Add(new Sound.SoundRule());
            Engine.RulesChanged();
            RebuildRuleList();
            rules_.Select(Engine.Rules.Count - 1);
            ShowSelectedRule();
            Cue.Instance.SaveLater();
        }

        private void OnRemoveRule()
        {
            if (Engine == null || rules_.Selected == null) return;
            Engine.Rules.Remove(rules_.Selected);
            Engine.RulesChanged();
            RebuildRuleList();
            ShowSelectedRule();
            Cue.Instance.SaveLater();
        }

        private void ChangedRule()
        {
            if (Engine != null)
                Engine.RulesChanged();
            Cue.Instance.SaveLater();
        }

        private void OnRuleTrigger(int i)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.trigger = i;
            ChangedRule();
            RelabelRules();
        }

        private void OnRulePart(int i)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.part = IndexToPart(i);
            ChangedRule();
            RelabelRules();
        }

        private void OnRuleOrifice(int i)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.orifice = i;
            ChangedRule();
            RelabelRules();
        }

        private void OnRuleSet(int i)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.set = (i <= 0) ? "" : Manager.Sets[i - 1].Name;
            ChangedRule();
            RelabelRules();
        }

        private void OnRuleVolume(float f)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.volume = f;
            ChangedRule();
        }

        private void OnRulePitch(float f)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.pitch = f;
            ChangedRule();
        }

        private void OnRuleJitter(float f)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.pitchJitter = f;
            ChangedRule();
        }

        private void OnRuleIntVol(float f)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.intensityToVolume = f;
            ChangedRule();
        }

        private void OnRuleVelPitch(float f)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.velToPitch = f;
            ChangedRule();
        }

        private void OnRuleMinSpeed(float f)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.minSpeed = f;
            ChangedRule();
        }

        private void OnRuleMaxSpeed(float f)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.maxSpeed = f;
            ChangedRule();
        }

        // Re-labels the rule dropdown after a change that alters a rule's
        // ToString (trigger/part/orifice/set), keeping the same rule selected.
        // Fully ignore_-guarded so the rebuild can't re-enter the editor through
        // the selection callback (the controls already hold the new values).
        private void RelabelRules()
        {
            if (Engine == null || rules_.Selected == null) return;

            int idx = Engine.Rules.IndexOf(rules_.Selected);

            ignore_ = true;
            rules_.SetItems(Engine.Rules);
            if (idx >= 0) rules_.Select(idx);
            ignore_ = false;
        }

        private void OnRuleInterval(float f)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.minInterval = f;
            ChangedRule();
        }

        private void OnRuleDepth(float f)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.depthThreshold = f;
            ChangedRule();
        }

        private void OnRuleEnabled(bool b)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.enabled = b;
            ChangedRule();
        }

        protected override void DoUpdate(float s)
        {
            var set = sets_.Selected;
            if (set != null)
                setStatus_.Text = set.Status;
        }
    }
}

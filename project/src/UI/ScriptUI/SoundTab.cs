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
        private VUI.ComboBox<Cue.Sound.SoundSet> sets_;
        private VUI.TextBox setName_;
        private VUI.TextBox setSource_;
        private VUI.ComboBox<string> setType_;
        private VUI.ComboBox<string> setBands_;
        private VUI.Label setStatus_;

        // rules section
        private VUI.ComboBox<Cue.Sound.SoundRule> rules_;
        private VUI.ComboBox<string> ruleTrigger_;
        private VUI.ComboBox<string> rulePart_;
        private VUI.ComboBox<string> ruleOrifice_;
        private VUI.ComboBox<string> ruleSet_;
        private VUI.FloatTextSlider ruleVolume_;
        private VUI.FloatTextSlider rulePitch_;
        private VUI.FloatTextSlider ruleJitter_;
        private VUI.FloatTextSlider ruleIntVol_;
        private VUI.FloatTextSlider ruleInterval_;
        private VUI.FloatTextSlider ruleDepth_;
        private VUI.CheckBox ruleEnabled_;

        private VUI.FloatTextSlider masterVolume_;

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

        private Cue.Sound.SoundManager Manager
        {
            get { return Cue.Sound.SoundManager.Instance; }
        }

        private Cue.Sound.SoundEventsEngine Engine
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
            sets_ = sp1.Add(new VUI.ComboBox<Cue.Sound.SoundSet>(OnSetSelected));
            sets_.MinimumSize = new VUI.Size(280, VUI.Widget.DontCare);
            sp1.Add(new VUI.Button("Add set", OnAddSet));
            sp1.Add(new VUI.Button("Remove", OnRemoveSet));
            Add(sp1);

            var sp2 = new VUI.Panel(new VUI.HorizontalFlow(8));
            sp2.Add(new VUI.Label("Name:"));
            setName_ = sp2.Add(new VUI.TextBox("", "set name", OnSetName));
            sp2.Add(new VUI.Label("Bands:"));
            setBands_ = sp2.Add(new VUI.ComboBox<string>(
                new string[] { "3", "5" }, OnSetBands));
            Add(sp2);

            var sp3 = new VUI.Panel(new VUI.HorizontalFlow(8));
            sp3.Add(new VUI.Label("Source:"));
            setSource_ = sp3.Add(new VUI.TextBox("", "folder or .assetbundle path", OnSetSource));
            setSource_.MinimumSize = new VUI.Size(330, VUI.Widget.DontCare);
            setType_ = sp3.Add(new VUI.ComboBox<string>(
                new string[] { "Folder", "AssetBundle" }, OnSetType));
            sp3.Add(new VUI.Button("Browse...", OnBrowse));
            Add(sp3);

            var sp4 = new VUI.Panel(new VUI.HorizontalFlow(8));
            sp4.Add(new VUI.Button("Reload clips", OnReloadSet));
            sp4.Add(new VUI.Button("Test (soft)", () => OnTest(0.1f)));
            sp4.Add(new VUI.Button("Test (medium)", () => OnTest(0.5f)));
            sp4.Add(new VUI.Button("Test (hard)", () => OnTest(0.95f)));
            setStatus_ = sp4.Add(new VUI.Label(""));
            Add(sp4);

            Add(new VUI.Spacer(10));

            // ---- rules ----------------------------------------------------
            Add(new VUI.Label("Trigger rules", UnityEngine.FontStyle.Bold));

            var rp1 = new VUI.Panel(new VUI.HorizontalFlow(8));
            rules_ = rp1.Add(new VUI.ComboBox<Cue.Sound.SoundRule>(OnRuleSelected));
            rules_.MinimumSize = new VUI.Size(420, VUI.Widget.DontCare);
            rp1.Add(new VUI.Button("Add rule", OnAddRule));
            rp1.Add(new VUI.Button("Remove", OnRemoveRule));
            Add(rp1);

            var rp2 = new VUI.Panel(new VUI.HorizontalFlow(8));
            rp2.Add(new VUI.Label("Trigger:"));
            ruleTrigger_ = rp2.Add(new VUI.ComboBox<string>(
                Cue.Sound.SoundRule.TriggerNames, OnRuleTrigger));
            ruleEnabled_ = rp2.Add(new VUI.CheckBox("Enabled", OnRuleEnabled));
            Add(rp2);

            var rp3 = new VUI.Panel(new VUI.HorizontalFlow(8));
            rp3.Add(new VUI.Label("Body part:"));
            rulePart_ = rp3.Add(new VUI.ComboBox<string>(partNames_.ToArray(), OnRulePart));
            rp3.Add(new VUI.Label("Orifice:"));
            ruleOrifice_ = rp3.Add(new VUI.ComboBox<string>(
                Cue.Sound.SoundRule.OrificeNames, OnRuleOrifice));
            Add(rp3);

            var rp4 = new VUI.Panel(new VUI.HorizontalFlow(8));
            rp4.Add(new VUI.Label("Sound set:"));
            ruleSet_ = rp4.Add(new VUI.ComboBox<string>(OnRuleSet));
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
            grid.Add(new VUI.Label("Intensity→vol:"));
            ruleIntVol_ = grid.Add(new VUI.FloatTextSlider(1f, 0f, 1f, OnRuleIntVol));

            grid.Add(new VUI.Label("Min interval:"));
            ruleInterval_ = grid.Add(new VUI.FloatTextSlider(0.15f, 0f, 5f, OnRuleInterval));
            grid.Add(new VUI.Label("Depth thresh:"));
            ruleDepth_ = grid.Add(new VUI.FloatTextSlider(0.8f, 0.1f, 1f, OnRuleDepth));

            Add(grid);
        }

        // ---- sets handlers ----------------------------------------------

        private void RefreshSets()
        {
            ignore_ = true;

            sets_.SetItems(Manager.Sets);
            ruleSet_.SetItems(SetNamesWithNone());

            var s = sets_.Selected;
            if (s != null)
            {
                setName_.Text = s.Name;
                setSource_.Text = s.Source;
                setType_.Select(s.SourceType);
                setBands_.Select(s.BandCount == 5 ? 1 : 0);
                setStatus_.Text = s.Status;
            }
            else
            {
                setName_.Text = "";
                setSource_.Text = "";
                setStatus_.Text = "";
            }

            ignore_ = false;
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

        private void OnSetSelected(Cue.Sound.SoundSet s)
        {
            if (ignore_) return;
            RefreshSets();
        }

        private void OnAddSet()
        {
            if (ignore_) return;
            var s = Manager.Add("set" + (Manager.Sets.Count + 1));
            RefreshSets();
            sets_.Select(Manager.Sets.IndexOf(s));
            Cue.Instance.SaveLater();
        }

        private void OnRemoveSet()
        {
            if (ignore_) return;
            Manager.Remove(sets_.Selected);
            RefreshSets();
            Cue.Instance.SaveLater();
        }

        private void OnSetName(string s)
        {
            if (ignore_ || sets_.Selected == null) return;
            sets_.Selected.Name = s;
            RefreshSets();
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

        private void OnSetBands(int i)
        {
            if (ignore_ || sets_.Selected == null) return;
            sets_.Selected.BandCount = (i == 1) ? 5 : 3;
            sets_.Selected.Reload();
            Cue.Instance.SaveLater();
        }

        private void OnBrowse()
        {
            var set = sets_.Selected;
            if (set == null) return;

            if (set.SourceType == Cue.Sound.SoundSet.SourceBundle)
            {
                Cue.Instance.Sys.LoadFileDialog("assetbundle", (path) =>
                {
                    if (string.IsNullOrEmpty(path)) return;
                    set.Source = path;
                    set.Reload();
                    RefreshSets();
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
                    RefreshSets();
                    Cue.Instance.SaveLater();
                });
            }
        }

        private void OnReloadSet()
        {
            if (sets_.Selected == null) return;
            sets_.Selected.Reload();
            RefreshSets();
        }

        private void OnTest(float intensity)
        {
            var set = sets_.Selected;
            if (set == null) return;

            var head = person_.Body.Get(BP.Head);
            var pos = (head != null)
                ? Sys.Vam.U.ToUnity(head.Position)
                : Sys.Vam.U.ToUnity(person_.Position);

            Manager.Play(set.Name, pos, intensity, 1f, 1f, 0.05f, 1f);
        }

        // ---- rules handlers ----------------------------------------------

        private void RefreshRules()
        {
            if (Engine == null) return;

            ignore_ = true;

            rules_.SetItems(Engine.Rules);

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
                ruleInterval_.Value = r.minInterval;
                ruleDepth_.Value = r.depthThreshold;
                ruleEnabled_.Checked = r.enabled;
            }

            ignore_ = false;
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

        private void OnRuleSelected(Cue.Sound.SoundRule r)
        {
            if (ignore_) return;
            RefreshRules();
        }

        private void OnAddRule()
        {
            if (Engine == null) return;
            Engine.Rules.Add(new Cue.Sound.SoundRule());
            Engine.RulesChanged();
            RefreshRules();
            rules_.Select(Engine.Rules.Count - 1);
            Cue.Instance.SaveLater();
        }

        private void OnRemoveRule()
        {
            if (Engine == null || rules_.Selected == null) return;
            Engine.Rules.Remove(rules_.Selected);
            Engine.RulesChanged();
            RefreshRules();
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
        }

        private void OnRulePart(int i)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.part = IndexToPart(i);
            ChangedRule();
        }

        private void OnRuleOrifice(int i)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.orifice = i;
            ChangedRule();
        }

        private void OnRuleSet(int i)
        {
            if (ignore_ || rules_.Selected == null) return;
            rules_.Selected.set = (i <= 0) ? "" : Manager.Sets[i - 1].Name;
            ChangedRule();
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

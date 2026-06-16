using System.Collections.Generic;
using UnityEngine;

namespace Cue
{
    // Sound set management: create the clip banks (folder or assetbundle, with
    // optional named intensity bands) that the sound graph's clip nodes draw
    // from. The old per-rule trigger system has been removed -- the graph engine
    // (see the "Graph" tab) is now the only event->sound path.
    class SoundTab : Tab
    {
        private readonly Person person_;

        private VUI.ComboBox<Sound.SoundSet> sets_;
        private VUI.TextBox setName_;
        private VUI.TextBox setSource_;
        private VUI.ComboBox<string> setType_;
        private VUI.TextBox setIntensity_;
        private VUI.Label setStatus_;
        private VUI.FloatTextSlider masterVolume_;

        private VUI.Button[] testBtns_ = new VUI.Button[8];
        private float[] testInt_ = new float[8];

        private bool ignore_ = false;

        public SoundTab(Person p)
            : base("Sound Sets", false)
        {
            person_ = p;
            Build();
            RefreshSetList();
            ShowSelectedSet();
        }

        public override bool DebugOnly { get { return false; } }

        private Sound.SoundManager Manager
        {
            get { return Sound.SoundManager.Instance; }
        }

        private void Build()
        {
            Layout = new VUI.VerticalFlow(8);

            var mv = new VUI.Panel(new VUI.HorizontalFlow(8));
            mv.Add(new VUI.Label("Master volume:"));
            masterVolume_ = mv.Add(new VUI.FloatTextSlider(
                Manager.MasterVolume, 0f, 2f, OnMasterVolume));
            masterVolume_.MinimumSize = new VUI.Size(200, VUI.Widget.DontCare);
            Add(mv);

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
            setSource_.MinimumSize = new VUI.Size(330, VUI.Widget.DontCare);
            setSource_.MaximumSize = new VUI.Size(330, VUI.Widget.DontCare);
            setType_ = sp3.Add(new VUI.ComboBox<string>(
                new string[] { "Folder", "AssetBundle" }, OnSetType));
            setType_.MinimumSize = new VUI.Size(150, VUI.Widget.DontCare);
            sp3.Add(new VUI.Button("Browse...", OnBrowse));
            Add(sp3);

            var sp4 = new VUI.Panel(new VUI.HorizontalFlow(8));
            sp4.Add(new VUI.Button("Reload clips", OnReloadSet));
            for (int i = 0; i < testBtns_.Length; ++i)
            {
                int k = i;
                testBtns_[k] = sp4.Add(new VUI.Button("Test", () => OnTest(testInt_[k])));
            }
            setStatus_ = sp4.Add(new VUI.Label(""));
            Add(sp4);
        }

        // ---- set list / selection ---------------------------------------

        private void RefreshSetList()
        {
            ignore_ = true;
            sets_.SetItems(Manager.Sets);
            ignore_ = false;
        }

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

        private void ConfigureTestButtons(Sound.SoundSet s)
        {
            for (int i = 0; i < testBtns_.Length; ++i)
                if (testBtns_[i] != null) testBtns_[i].Visible = false;

            if (s == null) return;

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

        // ---- handlers ----------------------------------------------------

        private void OnMasterVolume(float f)
        {
            if (ignore_) return;
            Manager.MasterVolume = f;
            Cue.Instance.SaveLater();
        }

        private void OnSetSelected(Sound.SoundSet s) { if (!ignore_) ShowSelectedSet(); }

        private void OnAddSet()
        {
            if (ignore_) return;
            var s = Manager.Add("set" + (Manager.Sets.Count + 1));
            RefreshSetList();
            sets_.Select(Manager.Sets.IndexOf(s));
            ShowSelectedSet();
            Cue.Instance.SaveLater();
        }

        private void OnRemoveSet()
        {
            if (ignore_) return;
            Manager.Remove(sets_.Selected);
            RefreshSetList();
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
            if (idx >= 0) sets_.Select(idx);
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
                Cue.Instance.Sys.LoadFileDialog("wav", (path) =>
                {
                    if (string.IsNullOrEmpty(path)) return;
                    int cut = path.LastIndexOfAny(new char[] { '/', '\\' });
                    if (cut > 0) path = path.Substring(0, cut);
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

        protected override void DoUpdate(float s)
        {
            var set = sets_.Selected;
            if (set != null)
                setStatus_.Text = set.Status;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cue
{
    class ArousalSystemTab : Tab
    {
        private readonly Person person_;

        private VUI.IntTextSlider    seedSlider_;
        private VUI.FloatTextSlider  sensitivitySlider_;
        private VUI.FloatTextSlider  responsivenessSlider_;
        private VUI.FloatTextSlider  staminaSlider_;
        private VUI.FloatTextSlider  inhibitionSlider_;

        private VUI.Label stateLabel_;
        private VUI.Label barrierLabel_;
        private VUI.Label biasLabel_;
        
        private VUI.Label urgeLabel_;
        private VUI.Label edgeLabel_;
        private VUI.Label stimLabel_;
        private VUI.Label frustLabel_;
        
        private VUI.Label traitDepthLabel_;
        private VUI.Label traitSpeedLabel_;
        private VUI.Label traitResistLabel_;

        private VUI.Label learnDepthLabel_;
        private VUI.Label learnPaceLabel_;
        private VUI.Label depthLabel_;
        private VUI.Label speedLabel_;
        private VUI.Label velLabel_;
        private VUI.Label dwellLabel_;
        private VUI.Label sourceLabel_;

        private VUI.ListView<string> debugList_;

        private bool ignore_ = false;

        public ArousalSystemTab(Person p)
            : base("Arousal", false)
        {
            person_ = p;
            Build();
        }

        public override bool DebugOnly { get { return false; } }

        private ArousalSystem System { get { return person_.ArousalSystem; } }

        private void Build()
        {
            Layout = new VUI.BorderLayout(10);

            var seedPanel = new VUI.Panel(new VUI.HorizontalFlow(8));
            seedPanel.Add(new VUI.Label("Personality seed:"));
            seedSlider_ = seedPanel.Add(new VUI.IntTextSlider(0, int.MaxValue, OnSeed));
            seedSlider_.MinimumSize = new VUI.Size(180, VUI.Widget.DontCare);
            seedPanel.Add(new VUI.Button("Randomise", OnRandomiseSeed));

            var gl = new VUI.GridLayout(2, 4);
            gl.HorizontalSpacing = 20;
            gl.HorizontalStretch = new List<bool>() { false, true };
            var grid = new VUI.Panel(gl);

            stateLabel_       = AddRow(grid, "Dominant State:");
            barrierLabel_     = AddRow(grid, "Climax Barrier:");
            biasLabel_        = AddRow(grid, "Bias (Speed / Depth):");
            traitResistLabel_ = AddRow(grid, "Climax Resist trait:");
            
            urgeLabel_        = AddRow(grid, "Urge:");
            edgeLabel_        = AddRow(grid, "Edge:");
            stimLabel_        = AddRow(grid, "Total stim:");
            frustLabel_       = AddRow(grid, "Frustration:");
            
            learnDepthLabel_  = AddRow(grid, "Learned Depth:");
            learnPaceLabel_   = AddRow(grid, "Learned Pace:");
            
            depthLabel_       = AddRow(grid, "Pen depth:");
            speedLabel_       = AddRow(grid, "Pen speed:");
            velLabel_         = AddRow(grid, "Velocity:");
            dwellLabel_       = AddRow(grid, "Dwell time:");
            sourceLabel_      = AddRow(grid, "Source:");

            var personaGrid = new VUI.Panel(new VUI.GridLayout(2, 4)
            {
                HorizontalSpacing = 20,
                HorizontalStretch = new List<bool>() { false, true }
            });

            sensitivitySlider_    = AddSliderRow(personaGrid, "Sensitivity:",    0f, 2f, OnSensitivity);
            responsivenessSlider_ = AddSliderRow(personaGrid, "Responsiveness:", 0f, 2f, OnResponsiveness);
            staminaSlider_        = AddSliderRow(personaGrid, "Stamina:",        0f, 2f, OnStamina);
            inhibitionSlider_     = AddSliderRow(personaGrid, "Inhibition:",     0f, 1f, OnInhibition);

            debugList_ = new VUI.ListView<string>();
            debugList_.Font     = VUI.Style.Theme.MonospaceFont;
            debugList_.FontSize = 20;

            var top = new VUI.Panel(new VUI.VerticalFlow(8));
            top.Add(seedPanel);
            top.Add(new VUI.Label("Live State & AI Adapter"));
            top.Add(grid);
            top.Add(new VUI.Label("Personality"));
            top.Add(personaGrid);

            Add(top,        VUI.BorderLayout.Top);
            Add(debugList_, VUI.BorderLayout.Center);
        }

        private VUI.Label AddRow(VUI.Panel p, string caption)
        {
            p.Add(new VUI.Label(caption));
            var v = p.Add(new VUI.Label("—"));
            return v;
        }

        private VUI.FloatTextSlider AddSliderRow(VUI.Panel p, string caption,
            float min, float max, VUI.FloatTextSlider.ValueCallback cb)
        {
            p.Add(new VUI.Label(caption));
            var s = p.Add(new VUI.FloatTextSlider(min, max, cb));
            s.MinimumSize = new VUI.Size(180, VUI.Widget.DontCare);
            return s;
        }

        private void OnSeed(int seed)
        {
            if (ignore_ || System == null) return;
            System.seed_ = seed;
        }

        private void OnRandomiseSeed()
        {
            if (System == null) return;
            try
            {
                ignore_ = true;
                int s = (int)(Time.realtimeSinceStartup * 1000f);
                System.seed_       = s;
                seedSlider_.Value = s;
            }
            finally { ignore_ = false; }
        }

        private void OnSensitivity(float v)
        {
            if (ignore_ || System?.brain_ == null) return;
            System.brain_.Personality.Sensitivity = v;
        }

        private void OnResponsiveness(float v)
        {
            if (ignore_ || System?.brain_ == null) return;
            System.brain_.Personality.Responsiveness = v;
        }

        private void OnStamina(float v)
        {
            if (ignore_ || System?.brain_ == null) return;
            System.brain_.Personality.Stamina = v;
        }

        private void OnInhibition(float v)
        {
            if (ignore_ || System?.brain_ == null) return;
            System.brain_.Personality.Inhibition = v;
        }

        protected override void DoUpdate(float s)
        {
            if (System == null)
            {
                stateLabel_.Text = "not initialised";
                return;
            }

            var brain = System.brain_;
            var pen   = System.PenStats;
            var p     = brain.Personality;

            try
            {
                ignore_ = true;
                if (seedSlider_.Value != System.seed_)
                    seedSlider_.Value = System.seed_;

                if (Math.Abs(sensitivitySlider_.Value    - p.Sensitivity)    > 0.001f)
                    sensitivitySlider_.Value = p.Sensitivity;
                if (Math.Abs(responsivenessSlider_.Value - p.Responsiveness) > 0.001f)
                    responsivenessSlider_.Value = p.Responsiveness;
                if (Math.Abs(staminaSlider_.Value        - p.Stamina)        > 0.001f)
                    staminaSlider_.Value = p.Stamina;
                if (Math.Abs(inhibitionSlider_.Value     - p.Inhibition)     > 0.001f)
                    inhibitionSlider_.Value = p.Inhibition;
            }
            finally { ignore_ = false; }

            stateLabel_.Text       = brain.StateString;

            // Climax Barrier Logic text
            if (person_.Mood.Get(MoodType.Excited) > 0.85f) {
                if (brain.IsStalling)
                    barrierLabel_.Text = "FAILING (Stalled)";
                else
                    barrierLabel_.Text = "BREAKING THROUGH";
            } else {
                barrierLabel_.Text = "Inactive (Not peaking)";
            }

            biasLabel_.Text        = $"{brain.SpeedSens:0.00}x / {brain.DepthSens:0.00}x";
            traitResistLabel_.Text = brain.ClimaxResist.ToString("0.00") + " Req";

            urgeLabel_.Text        = brain.UrgeIntensity.ToString("0.00");
            edgeLabel_.Text        = brain.EdgeFactor.ToString("0.00");
            stimLabel_.Text        = brain.TotalStim.ToString("0.00");
            frustLabel_.Text       = brain.Frustration.ToString("0.00");
            
            learnDepthLabel_.Text  = brain.LearnedDepth.ToString("0.00");
            learnPaceLabel_.Text   = brain.LearnedPace.ToString("0.00") + "s";
            
            depthLabel_.Text       = pen.NormalisedDepth.ToString("0.00");
            speedLabel_.Text       = pen.NormalisedSpeed.ToString("0.00");
            velLabel_.Text         = pen.SmoothedVelocity.ToString("+0.00;-0.00; 0.00");
            dwellLabel_.Text       = pen.DwellTime.ToString("0.0") + "s";
            sourceLabel_.Text      = pen.DetectedAtomName ?? "none";

            debugList_.SetItems(System.Debug());
        }
    }
}
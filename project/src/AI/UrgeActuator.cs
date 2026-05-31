using System;
using SimpleJSON;
using UnityEngine;
using Cue;

namespace Cue
{
    public class UrgeActuator
    {
        private const float UpdateInterval = 0.1f;
        private const float UrgeThreshold  = 0.35f;
        private const float MaxForcedMag   = 0.6f;

        private readonly Person person_;
        private readonly Logger log_;

        private float elapsed_         = 0f;
        private bool  triggerActive_   = false;
        private int   forcedPersonIdx_ = -1;

        public UrgeActuator(Person p)
        {
            person_          = p;
            log_             = new Logger(Logger.Object, p, "urge");
            forcedPersonIdx_ = -1;
        }

        public void Update(float s, ArousalBrain brain, PenetrationStats pen)
        {
            elapsed_ += s;
            if (elapsed_ < UpdateInterval) return;
            elapsed_ = 0f;

            float urge   = brain.UrgeIntensity;
            bool  active = pen.Active;

            float orgasmBoost = brain.OrgasmState.Active ? brain.OrgasmState.ContractionPhase * 0.4f : 0f;
            float effectiveUrge = Mathf.Clamp01(urge + orgasmBoost);

            if (active && effectiveUrge > UrgeThreshold)
            {
                float mag = (effectiveUrge - UrgeThreshold) / (1f - UrgeThreshold) * MaxForcedMag;

                if (!triggerActive_)
                {
                    log_.Verbose("urge trigger start mag=" + mag.ToString("0.00"));
                    person_.Body.Get(BP.Vagina).AddForcedTrigger(forcedPersonIdx_, BP.None, mag);
                    triggerActive_ = true;
                }
                else
                {
                    person_.Body.Get(BP.Vagina).RemoveForcedTrigger(forcedPersonIdx_, BP.None);
                    person_.Body.Get(BP.Vagina).AddForcedTrigger(forcedPersonIdx_, BP.None, mag);
                }
            }
            else if (triggerActive_)
            {
                log_.Verbose("urge trigger stop");
                person_.Body.Get(BP.Vagina).RemoveForcedTrigger(forcedPersonIdx_, BP.None);
                triggerActive_ = false;
            }
        }

        public void Destroy()
        {
            if (triggerActive_)
            {
                person_.Body.Get(BP.Vagina).RemoveForcedTrigger(forcedPersonIdx_, BP.None);
                triggerActive_ = false;
            }
        }

        public void Debug(DebugLines debug)
        {
            debug.Add("triggerActive", triggerActive_.ToString());
        }
    }


    public class ArousalSystem
    {
        private const float ScanInterval = 3f;

        private readonly Person       person_;
        public           ArousalBrain brain_;
        private readonly UrgeActuator actuator_;
        public           int          seed_;
        private          float        scanElapsed_ = 0f;

        private readonly DildoLanguage.PenetrationReader pen_;

        public ArousalSystem(Person p, int seed = 0)
        {
            person_   = p;
            seed_     = seed;
            pen_      = new DildoLanguage.PenetrationReader(p);
            brain_    = new ArousalBrain(p, seed);
            actuator_ = new UrgeActuator(p);
        }

        public void SetPenetratorAtom(Sys.IAtom a)
        {
            pen_.SetPenetratorAtom(a);
        }

        public PenetrationStats PenStats
        {
            get { return pen_.Stats; }
        }

        public void NotifyOrgasmBegun()
        {
            brain_.NotifyOrgasmBegun();
        }

        public void NotifyOrgasmEnded()
        {
            brain_.NotifyOrgasmEnded();
        }

        public void Update(float s)
        {
            scanElapsed_ += s;
            if (scanElapsed_ >= ScanInterval)
            {
                scanElapsed_ = 0f;
                brain_.SetSeed(seed_);
                ScanForPenetrator();
            }

            pen_.Update(s);
            brain_.Update(s, pen_.Stats);
            actuator_.Update(s, brain_, pen_.Stats);
        }

        private void ScanForPenetrator()
        {
            var atoms = Cue.Instance.Sys.GetAtoms();
            for (int i = 0; i < atoms.Count; ++i)
            {
                var a = atoms[i] as Sys.Vam.VamAtom;
                if (a == null) continue;
                if (a.Atom == null) continue;
                if (a.Atom.type != "CustomUnityAsset") continue;

                pen_.SetPenetratorAtom(a);
                return;
            }

            pen_.SetPenetratorAtom(null);
        }

        public void Destroy()
        {
            actuator_.Destroy();
        }

        public int Seed
        {
            get { return seed_; }
            set { seed_ = value; brain_.SetSeed(value); }
        }

        public JSONClass ToJSON()
        {
            return brain_.ToJSON();
        }

        public void Load(JSONClass o)
        {
            if (o == null)
                return;

            int seed = seed_;
            if (J.OptInt(o, "seed", ref seed))
                seed_ = seed;

            brain_.Load(o);
        }

        public string[] Debug()
        {
            var lines = new DebugLines();
            lines.Add("── Brain ──", "");
            foreach (var l in brain_.Debug()) lines.Add(l, "");
            lines.Add("", "");
            lines.Add("── Penetration ──", "");
            if (pen_.Available)
                pen_.Debug(lines);
            else
                lines.Add("dl.available", "false");
            lines.Add("", "");
            lines.Add("── Actuator ──", "");
            actuator_.Debug(lines);
            return lines.MakeArray();
        }
    }
}
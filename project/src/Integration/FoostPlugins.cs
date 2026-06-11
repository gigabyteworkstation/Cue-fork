using UnityEngine;

namespace Cue
{
    // Integration with Foost.SexyFluids when it's loaded on the same Person
    // atom as Cue. Drives the squirt simulation from Cue's orgasm pipeline:
    //  - normal orgasm  -> "squirt:start" (one envelope-driven orgasm)
    //  - perpetual      -> "squirt:startEndless" until the orgasm state ends
    //  - aftershocks    -> "squirt:burst" pulses
    // Whether a person squirts at all (and how often) is gated by the seeded
    // SquirtPropensity orgasm trait, so it's per-personality like everything
    // else.
    public class SexyFluidsIntegration
    {
        private const string PluginName = "SexyFluids";
        private const float CheckInterval = 5f;

        private readonly Person person_;
        private readonly Logger log_;

        private Sys.Vam.ActionParameter squirtStart_;
        private Sys.Vam.ActionParameter squirtEndless_;
        private Sys.Vam.ActionParameter squirtStop_;
        private Sys.Vam.FloatParameter squirtBurst_;

        private float checkElapsed_ = 0f;
        private bool available_ = false;
        private bool endlessActive_ = false;

        public SexyFluidsIntegration(Person p)
        {
            person_ = p;
            log_ = new Logger(Logger.Integration, p, "sexyFluids");

            squirtStart_   = new Sys.Vam.ActionParameter(p, PluginName, "squirt:start");
            squirtEndless_ = new Sys.Vam.ActionParameter(p, PluginName, "squirt:startEndless");
            squirtStop_    = new Sys.Vam.ActionParameter(p, PluginName, "squirt:stop");
            squirtBurst_   = new Sys.Vam.FloatParameter(p, PluginName, "squirt:burst");

            CheckAvailable();
        }

        public bool Available { get { return available_; } }

        private void CheckAvailable()
        {
            available_ = squirtStart_.Check();
        }

        public void Update(float s)
        {
            checkElapsed_ += s;
            if (checkElapsed_ >= CheckInterval)
            {
                checkElapsed_ = 0f;
                CheckAvailable();
            }
        }

        private bool WantsSquirt()
        {
            var brain = person_.ArousalSystem?.brain_;
            if (brain == null) return false;

            // squirting is a per-seed propensity, not universal
            return brain.OrgasmTraits.SquirtPropensity > 0.45f;
        }

        public void OnOrgasm(bool perpetual)
        {
            if (!available_ || !WantsSquirt())
                return;

            if (perpetual)
            {
                log_.Info("starting endless squirt");
                squirtEndless_.Fire();
                endlessActive_ = true;
            }
            else
            {
                log_.Info("starting squirt");
                squirtStart_.Fire();
            }
        }

        public void OnAftershock(float magnitude)
        {
            if (!available_ || !WantsSquirt())
                return;

            if (squirtBurst_.Check())
                squirtBurst_.Value = 0.5f + Mathf.Clamp01(magnitude) * 2.0f;
        }

        public void OnOrgasmEnded()
        {
            if (endlessActive_ && available_)
            {
                log_.Info("stopping endless squirt");
                squirtStop_.Fire();
                endlessActive_ = false;
            }
        }

        public void Debug(DebugLines debug)
        {
            debug.Add("sexyFluids", available_ ? "available" : "not found");
            debug.Add("  endless", endlessActive_.ToString());
            debug.Add("  wants", WantsSquirt().ToString());
        }
    }


    // Light integration with Skynet.OrificeDynamics on the same Person atom:
    // presence detection plus the few generically-named parameters it exposes.
    // Used as a realism hint (gape/stretch state slightly boosts penetration
    // sound intensity); kept deliberately minimal since the plugin exposes
    // little by stable names.
    public class OrificeDynamicsIntegration
    {
        private const string PluginName = "OrificeDynamics";
        private const float CheckInterval = 5f;

        private readonly Person person_;
        private readonly Logger log_;

        private Sys.Vam.FloatParameter genitalPlaneWidth_;
        private Sys.Vam.FloatParameter genitalPlaneHeight_;

        private float checkElapsed_ = 0f;
        private bool available_ = false;

        public OrificeDynamicsIntegration(Person p)
        {
            person_ = p;
            log_ = new Logger(Logger.Integration, p, "orificeDynamics");

            genitalPlaneWidth_  = new Sys.Vam.FloatParameter(p, PluginName, "Genital Plane Width");
            genitalPlaneHeight_ = new Sys.Vam.FloatParameter(p, PluginName, "Genital Plane Height");

            available_ = genitalPlaneWidth_.Check();
        }

        public bool Available { get { return available_; } }

        public void Update(float s)
        {
            checkElapsed_ += s;
            if (checkElapsed_ >= CheckInterval)
            {
                checkElapsed_ = 0f;
                available_ = genitalPlaneWidth_.Check();
            }
        }

        // Rough 0..1 "stretch" hint from the plane parameters; 0 when the
        // plugin is missing.
        public float StretchHint
        {
            get
            {
                if (!available_) return 0f;
                float w = genitalPlaneWidth_.Value;
                float h = genitalPlaneHeight_.Value;
                return Mathf.Clamp01((w + h) * 0.5f);
            }
        }

        public void Debug(DebugLines debug)
        {
            debug.Add("orificeDynamics", available_ ? "available" : "not found");
            if (available_)
                debug.Add("  stretchHint", StretchHint.ToString("0.00"));
        }
    }


    // Bundles the per-person third-party integrations so Person only needs a
    // single field and update call.
    public class FoostIntegrations
    {
        private readonly SexyFluidsIntegration sexyFluids_;
        private readonly OrificeDynamicsIntegration orificeDynamics_;

        public FoostIntegrations(Person p)
        {
            sexyFluids_ = new SexyFluidsIntegration(p);
            orificeDynamics_ = new OrificeDynamicsIntegration(p);
        }

        public SexyFluidsIntegration SexyFluids { get { return sexyFluids_; } }
        public OrificeDynamicsIntegration OrificeDynamics { get { return orificeDynamics_; } }

        public void Update(float s)
        {
            sexyFluids_.Update(s);
            orificeDynamics_.Update(s);
        }

        public void Debug(DebugLines debug)
        {
            sexyFluids_.Debug(debug);
            orificeDynamics_.Debug(debug);
        }
    }
}

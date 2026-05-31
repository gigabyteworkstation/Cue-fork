using SimpleJSON;
using System;
using UnityEngine;

namespace Cue.VamMoan2
{
    public class OneLinerScheduler
    {
        private readonly Voice voice_;
        private readonly Logger log_;

        private float[] cooldowns_ = new float[11];
        private float[] elapsed_   = new float[11];

        private float globalElapsed_          = 0f;
        private float currentGlobalCooldown_  = 25f;

        private bool  wasOrgasming_    = false;
        private bool  oneLinerPending_ = false;

        private float lastTotalStim_   = 0f;
        private float reactionBaseline_= 0f;
        private float reactionCooldown_= 0f;

        public OneLinerScheduler(Voice v)
        {
            voice_ = v;
            log_   = new Logger(Logger.Integration, v.Person, "vamm2.oneliners");

            for (int i = 1; i <= 10; i++)
            {
                cooldowns_[i] = 60f;
                elapsed_[i]   = 60f;
            }

            cooldowns_[10] = 30f;
        }

        public void Load(JSONClass o, bool inherited)
        {
            if (o == null || !o.HasKey("oneliners")) return;

            var arr = o["oneliners"].AsArray;
            if (arr == null) return;

            foreach (JSONNode item in arr)
            {
                var io = item.AsObject;
                if (io == null) continue;

                int slot      = 0;
                float cooldown = J.OptFloat(io, "cooldown", 45f);

                J.OptInt(io, "slot", ref slot);

                if (slot >= 1 && slot <= 10)
                {
                    cooldowns_[slot] = cooldown;
                    elapsed_[slot]   = cooldown;
                }
            }
        }

        public void Update(float s)
        {
            globalElapsed_ += s;
            for (int i = 1; i <= 10; i++) elapsed_[i] += s;
            if (reactionCooldown_ > 0f) reactionCooldown_ -= s;

            Person person    = voice_.Person;
            bool   orgasming = (person.Mood.State == Mood.OrgasmState);
            var    brain     = person.ArousalSystem?.brain_;

            if (brain == null) return;

            float inhibition    = brain.Personality.Inhibition;
            float responsiveness = brain.Personality.Responsiveness;

            float targetPause = Mathf.Lerp(0.45f, 0.02f, brain.UrgeIntensity);
            voice_.SetPauseBetweenMoans(targetPause);

            if (orgasming)
            {
                wasOrgasming_    = true;
                oneLinerPending_ = false;
                return;
            }

            if (wasOrgasming_ && !orgasming)
            {
                wasOrgasming_ = false;
                elapsed_[10]  = cooldowns_[10];
                TryFire(10, GetNaturalIntensity(brain), immediate: false);
                return;
            }

            float currentStim = brain.TotalStim;
            lastTotalStim_    = currentStim;

            // Reaction detection ------------------------------------------------
            // The old code compared a single-frame delta of an already heavily
            // smoothed signal against a large threshold, so genuine spikes almost
            // never crossed it, and it was further gated behind "only if a
            // one-liner is pending or we're in the first few seconds" -- which is
            // why reactions felt janky and rarely fired when they should.
            //
            // Instead we track a slowly-following baseline and fire when the live
            // stimulation surges meaningfully above its own recent average (a real
            // "that caught me off guard" moment), regardless of what else is going
            // on. Inhibition lowers the odds; responsiveness sharpens them.
            reactionBaseline_ = Mathf.Lerp(reactionBaseline_, currentStim, s * 0.6f);
            float surge = currentStim - reactionBaseline_;

            float surgeThreshold = 0.10f - (responsiveness * 0.03f);

            if (surge > surgeThreshold && currentStim > 0.12f && reactionCooldown_ <= 0f)
            {
                float chance = Mathf.Clamp01(1f - inhibition * 0.6f);
                if (UnityEngine.Random.value < chance)
                {
                    log_.Info("stim surge -> reaction (surge=" + surge.ToString("0.00") + ")");
                    voice_.PlayReactionImmediate(GetNaturalIntensity(brain));

                    oneLinerPending_  = false;               // interrupt any pending line
                    reactionCooldown_ = Mathf.Lerp(3.5f, 1.5f, responsiveness * 0.5f);
                    reactionBaseline_ = currentStim;         // avoid re-firing same surge
                    return;
                }
            }

            if (oneLinerPending_ && globalElapsed_ > 6f)
                oneLinerPending_ = false;

            if (globalElapsed_ < currentGlobalCooldown_ || oneLinerPending_) return;

            int  natInt           = GetNaturalIntensity(brain);
            bool suppressedByShyness = (UnityEngine.Random.value < inhibition * 0.8f);

            if (brain.States.Stalled > 0.5f)
            {
                int edgingInt = (UnityEngine.Random.value > 0.5f) ? 2 : 3;
                if (TryFire(10, edgingInt, immediate: false)) return;
            }

            if (brain.States.Edging > 0.85f && brain.BarrierStatus <= 1.05f)
            {
                if (TryFire(9, 3, immediate: false)) return;
            }

            if (suppressedByShyness)
            {
                ResetGlobalCooldown(inhibition);
                return;
            }

            if (brain.IsPenetrated && brain.CurrentVelocity > 0.8f && brain.SpeedSens < 0.7f && brain.States.Enjoying < 0.4f)
            {
                if (TryFire(8, natInt, immediate: false)) return;
            }

            if (brain.IsPenetrated && (brain.States.Craving > 0.6f || brain.States.Frustrated > 0.5f))
            {
                if (brain.SpeedSens >= brain.DepthSens && brain.CurrentVelocity < 0.4f)
                {
                    if (TryFire(6, natInt, immediate: false)) return;
                }
                else if (brain.DepthSens > brain.SpeedSens && brain.CurrentDepth < 0.5f)
                {
                    int forcedInt = brain.States.Frustrated > 0.8f ? 4 : natInt;
                    if (TryFire(7, forcedInt, immediate: false)) return;
                }
            }

            if (brain.IsPenetrated && brain.Familiarity > 0.85f && brain.States.Enjoying > 0.7f)
            {
                int slot = (UnityEngine.Random.value > 0.5f) ? 2 : 3;
                if (TryFire(slot, 3, immediate: false)) return;
            }

            if (brain.IsPenetrated && currentStim > 0.85f && brain.States.Responding > 0.6f)
            {
                int slot = (UnityEngine.Random.value > 0.5f) ? 4 : 5;
                if (TryFire(slot, natInt, immediate: false)) return;
            }

            if (brain.States.Craving > 0.4f || brain.States.Frustrated > 0.3f)
            {
                if (!brain.IsPenetrated)
                {
                    if (TryFire(1, 0, immediate: false)) return;
                }
                else if (brain.UrgeIntensity > 0.5f)
                {
                    if (TryFire(1, 4, immediate: false)) return;
                }
            }
        }

        private int GetNaturalIntensity(ArousalBrain brain)
        {
            float a = Math.Max(brain.UrgeIntensity, voice_.Person.Mood.Get(MoodType.Excited));
            return (int)Mathf.Clamp(Mathf.Floor(a / 0.2f), 0f, 4f);
        }

        private void ResetGlobalCooldown(float inhibition)
        {
            currentGlobalCooldown_ = 25f + (inhibition * 35f) + (UnityEngine.Random.value * 15f);
            globalElapsed_         = 0f;
        }

        private bool TryFire(int slot, int intensity, bool immediate)
        {
            if (elapsed_[slot] < cooldowns_[slot]) return false;

            elapsed_[slot] = 0f;

            float inhibition = voice_.Person.ArousalSystem.brain_.Personality.Inhibition;
            ResetGlobalCooldown(inhibition);

            log_.Info($"one-liner slot {slot} (Forced Int: {intensity}) immediate={immediate}");

            if (immediate)
                voice_.PlayOneLinerImmediate(slot, intensity);
            else
            {
                voice_.ScheduleOneLiner(slot, intensity);
                oneLinerPending_ = true;
            }

            return true;
        }
    }


    public sealed class Voice : IVoice
    {
        private const string PluginName         = "VAMMoan2TeaserPlugin.VAMMoan2Teaser";
        private const float  VoiceCheckInterval = 2f;

        private float    initMoanRatio_      = 0.2f;
        private float    initMouthNoseRatio_ = 0.15f;
        private bool     sfxPelvicSlap_      = false;
        private bool     sfxSquishes_        = false;
        private bool     sfxBlowjob_         = false;
        private bool     sfxSpank_           = false;
        private bool     allowVA_            = true;

        private JSONClass cachedOptions_ = null;

        private struct Params
        {
            public Sys.Vam.StringChooserParameter character;
            public Sys.Vam.StringChooserParameter changeState;
            public Sys.Vam.FloatParameter         setArousal;
            public Sys.Vam.FloatParameter         setMoanRatio;
            public Sys.Vam.FloatParameter         setMouthNoseRatio;
            public Sys.Vam.FloatParameter         volume;
            public Sys.Vam.FloatParameter         moanRatio;
            public Sys.Vam.FloatParameter         mouthNoseRatio;
            public Sys.Vam.BoolParameter          breathingEnabled;
            public Sys.Vam.BoolParameter          audioDrivenJaw;
            public Sys.Vam.BoolParameter          lipSync;

            public Sys.Vam.BoolParameter          enablePelvicSlap;
            public Sys.Vam.BoolParameter          enableSquishes;
            public Sys.Vam.BoolParameter          enableBlowjob;
            public Sys.Vam.BoolParameter          enableSpank;
            public Sys.Vam.StringChooserParameter scheduleOneLiner;
            public Sys.Vam.ActionParameter        scheduleOneLinerSet;
            public Sys.Vam.StringChooserParameter playOneLinerImmediate;
            public Sys.Vam.ActionParameter        playOneLinerSetImmediate;
            public Sys.Vam.BoolParameter          allowVAPlayback;
            public Sys.Vam.StringParameter        currentState;

            public Sys.Vam.FloatParameter         setTriggerIntensity;
            public Sys.Vam.StringChooserParameter playSoundTypeImmediate;
            public Sys.Vam.FloatParameter         setPauseBetweenMoans;
        }

        private Person            person_       = null;
        private Logger            log_;
        private Params            p_;
        private float             checkElapsed_ = 0f;
        private string            warning_      = "";
        private float             oldVolume_    = 0.5f;
        private OneLinerScheduler oneLiners_    = null;

        private bool  inOrgasm_              = false;
        private bool  isPerpetualOrgasm_     = false;
        private float orgasmIntensityTarget_ = 1f;
        private float orgasmArousalValue_    = 1f;

        private float dynMoanRatio_     = -1f;
        private float lastSetMoanRatio_ = -1f;

        private Voice() {}

        public Voice(JSONClass o)
        {
            Load(o, false);
        }

        public void Load(JSONClass o, bool inherited)
        {
            if (o == null) return;

            J.OptFloat(o, "moanRatio",      ref initMoanRatio_);
            J.OptFloat(o, "mouthNoseRatio", ref initMouthNoseRatio_);
            J.OptBool(o,  "sfxPelvicSlap",  ref sfxPelvicSlap_);
            J.OptBool(o,  "sfxSquishes",    ref sfxSquishes_);
            J.OptBool(o,  "sfxBlowjob",     ref sfxBlowjob_);
            J.OptBool(o,  "sfxSpank",       ref sfxSpank_);
            J.OptBool(o,  "allowVA",        ref allowVA_);

            cachedOptions_ = o;

            if (oneLiners_ != null)
                oneLiners_.Load(o, inherited);
        }

        public IVoice Clone()
        {
            var b = new Voice();
            b.CopyFrom(this);
            return b;
        }

        private void CopyFrom(Voice v)
        {
            initMoanRatio_      = v.initMoanRatio_;
            initMouthNoseRatio_ = v.initMouthNoseRatio_;
            sfxPelvicSlap_      = v.sfxPelvicSlap_;
            sfxSquishes_        = v.sfxSquishes_;
            sfxBlowjob_         = v.sfxBlowjob_;
            sfxSpank_           = v.sfxSpank_;
            allowVA_            = v.allowVA_;
            cachedOptions_      = v.cachedOptions_;
        }

        public void Init(Person p)
        {
            person_ = p;
            log_    = new Logger(Logger.Integration, p, "vamm2");

            p_.character              = SCP("VAMMCharacter");
            p_.changeState            = SCP("Change State");
            p_.setArousal             = FP("Set Arousal");
            p_.setMoanRatio           = FP("Set Moan Ratio");
            p_.setMouthNoseRatio      = FP("Set Mouth/Nose Ratio");
            p_.volume                 = FP("VAMMVolume");
            p_.moanRatio              = FP("VAMMMoaningRatio");
            p_.mouthNoseRatio         = FP("VAMMBreathingMouthNoseRatio");
            p_.breathingEnabled       = BP("VAMMBreathingEnabled");
            p_.audioDrivenJaw         = BP("VAMMAudioDrivenJawEnabled");
            p_.lipSync                = BP("VAMMLipsyncEnabled");
            p_.enablePelvicSlap       = BP("enablePelvicSlap");
            p_.enableSquishes         = BP("enableSquishes");
            p_.enableBlowjob          = BP("enableBlowjob");
            p_.enableSpank            = BP("enableSpank");
            p_.scheduleOneLiner       = SCP("Schedule One Liner");
            p_.scheduleOneLinerSet    = AP("Schedule One Liner Set");
            p_.playOneLinerImmediate  = SCP("Play One Liner Immediate");
            p_.playOneLinerSetImmediate = AP("Play One Liner Set Immediate");
            p_.allowVAPlayback        = BP("Allow VA Playback");
            p_.currentState           = SP("VAMMCurrentState");

            p_.setTriggerIntensity    = FP("Set Trigger Intensity");
            p_.playSoundTypeImmediate = SCP("Play Sound Type Immediate");
            p_.setPauseBetweenMoans   = FP("Set Pause Between Moans");

            p_.moanRatio.Value         = initMoanRatio_;
            p_.mouthNoseRatio.Value    = initMouthNoseRatio_;
            p_.setMoanRatio.Value      = initMoanRatio_;
            p_.setMouthNoseRatio.Value = initMouthNoseRatio_;
            p_.enablePelvicSlap.Value  = sfxPelvicSlap_;
            p_.enableSquishes.Value    = sfxSquishes_;
            p_.enableBlowjob.Value     = sfxBlowjob_;
            p_.enableSpank.Value       = sfxSpank_;
            p_.allowVAPlayback.Value   = allowVA_;
            p_.breathingEnabled.Value  = true;
            p_.audioDrivenJaw.Value    = false;
            p_.lipSync.Value           = true;

            oldVolume_ = 0.5f;

            oneLiners_ = new OneLinerScheduler(this);
            if (cachedOptions_ != null)
                oneLiners_.Load(cachedOptions_, false);

            CheckVersion();
        }

        public Person Person { get { return person_; } }
        public string Name   { get { return "vammoan2"; } }

        public bool Muted
        {
            set
            {
                if (value) { oldVolume_ = p_.volume.Value; p_.volume.Value = 0; }
                else        p_.volume.Value = oldVolume_;
            }
        }

        public bool MouthEnabled
        {
            get { return p_.audioDrivenJaw.Value; }
            set { p_.audioDrivenJaw.Value = value; }
        }

        public bool LipsyncEnabled
        {
            get { return p_.lipSync.Value; }
            set { p_.lipSync.Value = value; }
        }

        public bool ChestEnabled
        {
            get { return p_.breathingEnabled.Value; }
            set { p_.breathingEnabled.Value = value; }
        }

        public string Warning { get { return warning_; } }

        public void Update(float s)
        {
            checkElapsed_ += s;
            if (checkElapsed_ >= VoiceCheckInterval)
            {
                checkElapsed_ = 0f;
                CheckVersion();
            }

            if (!inOrgasm_)
            {
                oneLiners_?.Update(s);
                UpdateDynamicMoanRatio(s);
            }
        }

        // Continuously shifts the breathing/moaning balance with arousal: she
        // breathes more when calm and moans more as she builds, starting from
        // the user-configured baseline ratio instead of staying fixed at it.
        private void UpdateDynamicMoanRatio(float s)
        {
            var brain = person_?.ArousalSystem?.brain_;
            if (brain == null) return;

            float arousal = Mathf.Max(person_.Mood.Get(MoodType.Excited), brain.UrgeIntensity);
            float target  = Mathf.Lerp(initMoanRatio_, 0.9f, arousal);

            if (dynMoanRatio_ < 0f)
                dynMoanRatio_ = target;

            dynMoanRatio_ = Mathf.Lerp(dynMoanRatio_, target, s * 1.5f);

            if (Mathf.Abs(dynMoanRatio_ - lastSetMoanRatio_) > 0.02f)
            {
                SetMoanRatio(dynMoanRatio_);
                lastSetMoanRatio_ = dynMoanRatio_;
            }
        }

        public void SetMoaning(float v)    { p_.setArousal.Value = v; ChangeState("Moaning"); }
        // Breathing/Silent/Kissing only happen well outside an orgasm, so they
        // double as a safety net that re-arms reactions if the orgasm latch is
        // somehow still set (SetMoaning is deliberately excluded -- it's also
        // used for the dips *within* an orgasm wave).
        public void SetBreathing()         { ExitOrgasm(); ChangeState("Breathing Idle"); }
        public void SetSilent()            { ExitOrgasm(); ChangeState("Disabled"); }
        public void SetKissing()           { ExitOrgasm(); ChangeState("Kissing"); }

        public void SetOrgasm()
        {
            ChangeState("Orgasm");
        }

        public void SetOrgasmImmediate()
        {
            inOrgasm_           = true;
            isPerpetualOrgasm_  = false;
            orgasmArousalValue_ = 1f;

            p_.setArousal.Value = 1f;
            ChangeStateImmediate("Orgasm");
        }

        public void SetPerpetualOrgasm()
        {
            inOrgasm_          = true;
            isPerpetualOrgasm_ = true;

            p_.setArousal.Value = 1f;
            ChangeStateImmediate("Orgasm Perpetual");
        }

        public void DriveOrgasmIntensity(float vocalDrive)
        {
            if (!inOrgasm_) return;

            orgasmIntensityTarget_ = Mathf.Clamp01(vocalDrive);

            p_.setArousal.Value = orgasmIntensityTarget_;

            if (!isPerpetualOrgasm_)
            {
                string targetState = orgasmIntensityTarget_ > 0.05f ? "Orgasm" : "Moaning";
                string currentVammState = p_.currentState?.Value ?? "";

                if (orgasmIntensityTarget_ > 0.05f && currentVammState != "Orgasm")
                    ChangeStateImmediate("Orgasm");
                else if (orgasmIntensityTarget_ <= 0.05f && currentVammState == "Orgasm")
                    ChangeState("Moaning");
            }
        }

        public void ExitOrgasm()
        {
            inOrgasm_ = false;
            isPerpetualOrgasm_ = false;
        }

        public void SetBJ(float v)
        {
            p_.setArousal.Value = v;
            ChangeState("Blowjob");
        }

        public void SetPauseBetweenMoans(float pause)
        {
            if (p_.setPauseBetweenMoans != null)
                p_.setPauseBetweenMoans.Value = pause;
        }

        public void SetTriggerIntensity(int intensity)
        {
            if (p_.setTriggerIntensity != null)
                p_.setTriggerIntensity.Value = intensity;
        }

        public void PlayReactionImmediate(int intensity)
        {
            if (p_.playSoundTypeImmediate != null)
            {
                SetTriggerIntensity(intensity);
                p_.playSoundTypeImmediate.Value = "Reaction";
                SetTriggerIntensity(-1);
            }
        }

        public void ScheduleOneLiner(int slot, int intensity)
        {
            if (slot < 1 || slot > 10) return;

            SetTriggerIntensity(intensity);
            p_.scheduleOneLiner.Value = slot.ToString();
            p_.scheduleOneLinerSet.Fire();
            SetTriggerIntensity(-1);
        }

        public void PlayOneLinerImmediate(int slot, int intensity)
        {
            if (slot < 1 || slot > 10) return;

            SetTriggerIntensity(intensity);
            p_.playOneLinerImmediate.Value = slot.ToString();
            p_.playOneLinerSetImmediate.Fire();
            SetTriggerIntensity(-1);
        }

        public void ScheduleOneLiner(int slot) { ScheduleOneLiner(slot, -1); }
        public void PlayOneLinerImmediate(int slot) { PlayOneLinerImmediate(slot, -1); }

        public void SetMoanRatio(float v)      { p_.setMoanRatio.Value      = U.Clamp(v, 0, 1); }
        public void SetMouthNoseRatio(float v) { p_.setMouthNoseRatio.Value = U.Clamp(v, 0, 1); }

        public bool SFXPelvicSlap { get { return p_.enablePelvicSlap.Value; } set { p_.enablePelvicSlap.Value = value; } }
        public bool SFXSquishes   { get { return p_.enableSquishes.Value;   } set { p_.enableSquishes.Value   = value; } }
        public bool SFXBlowjob    { get { return p_.enableBlowjob.Value;    } set { p_.enableBlowjob.Value    = value; } }
        public bool SFXSpank      { get { return p_.enableSpank.Value;      } set { p_.enableSpank.Value      = value; } }

        public void Debug(DebugLines debug)
        {
            debug.Add("provider",       "vammoan2");
            debug.Add("currentState",   p_.currentState?.Value ?? "?");
            debug.Add("inOrgasm",       inOrgasm_.ToString());
            debug.Add("isPerpetual",    isPerpetualOrgasm_.ToString());
            debug.Add("orgasmDrive",    orgasmIntensityTarget_.ToString("0.00"));
            debug.Add("moanRatio",      p_.moanRatio.Value.ToString("0.00"));
            debug.Add("mouthNoseRatio", p_.mouthNoseRatio.Value.ToString("0.00"));
            debug.Add("sfx",
                "psl=" + sfxPelvicSlap_ +
                " sqs=" + sfxSquishes_ +
                " bj="  + sfxBlowjob_  +
                " spk=" + sfxSpank_);
        }

        public void Destroy() {}

        private void ChangeState(string state)
        {
            if (p_.changeState.Value != state)
            {
                log_.Info("state -> " + state);
                p_.changeState.Value = state;
            }
        }

        private void ChangeStateImmediate(string state)
        {
            log_.Info("state immediate -> " + state);
            p_.changeState.Value = state;
        }

        private void CheckVersion()
        {
            warning_ = p_.currentState.Check() ? "" : "VAMMoan2 missing";
        }

        private Sys.Vam.BoolParameter          BP(string n)  { return new Sys.Vam.BoolParameter(person_, PluginName, n); }
        private Sys.Vam.FloatParameter         FP(string n)  { return new Sys.Vam.FloatParameter(person_, PluginName, n); }
        private Sys.Vam.StringChooserParameter SCP(string n) { return new Sys.Vam.StringChooserParameter(person_, PluginName, n); }
        private Sys.Vam.ActionParameter        AP(string n)  { return new Sys.Vam.ActionParameter(person_, PluginName, n); }
        private Sys.Vam.StringParameter        SP(string n)  { return new Sys.Vam.StringParameter(person_, PluginName, n); }

        public override string ToString()
        {
            return "VAMMoan2 state=" + (p_.currentState?.Value ?? "?") +
                   " moanRatio=" + p_.moanRatio.Value.ToString("0.00");
        }
    }
}
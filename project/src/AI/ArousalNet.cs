using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Cue
{
    public class ArousalPersonality
    {
        public float Sensitivity    = 1.0f;
        public float Responsiveness = 1.0f;
        public float Stamina        = 1.0f;
        public float Inhibition     = 0.0f;

        public static ArousalPersonality Default()
        {
            return new ArousalPersonality();
        }

        public static ArousalPersonality Sensitive()
        {
            return new ArousalPersonality
            {
                Sensitivity    = 1.6f,
                Responsiveness = 1.4f,
                Stamina        = 0.7f,
                Inhibition     = 0.0f
            };
        }

        public static ArousalPersonality Reserved()
        {
            return new ArousalPersonality
            {
                Sensitivity    = 0.6f,
                Responsiveness = 0.5f,
                Stamina        = 1.4f,
                Inhibition     = 0.5f
            };
        }

        public ArousalPersonality Clone()
        {
            return new ArousalPersonality
            {
                Sensitivity    = Sensitivity,
                Responsiveness = Responsiveness,
                Stamina        = Stamina,
                Inhibition     = Inhibition
            };
        }
    }

    public class OrgasmTraits
    {
        public readonly float OrgasmVocality;
        public readonly float OrgasmDuration;
        public readonly float OrgasmIntensityPeak;
        public readonly float OrgasmBuildSharpness;
        public readonly float OrgasmContractionRate;
        public readonly float OrgasmContractionDepth;
        public readonly float OrgasmSensitivityFlood;
        public readonly float OrgasmAftershockCount;
        public readonly float OrgasmAftershockDecay;
        public readonly float OrgasmDelayThreshold;
        public readonly float MultiOrgasmPropensity;
        public readonly bool  IsMult;
        public readonly float SquirtPropensity;
        public readonly float BodyTensionMax;
        public readonly float PostOrgasmHypersensitivity;

        public OrgasmTraits(int seed)
        {
            var rng = new System.Random((int)(seed ^ 0xABCD1234));

            OrgasmVocality              = Rand(rng, 0.3f, 1.0f);
            OrgasmDuration              = Rand(rng, 4.0f, 18.0f);
            OrgasmIntensityPeak         = Rand(rng, 0.6f, 1.0f);
            OrgasmBuildSharpness        = Rand(rng, 0.5f, 3.0f);
            OrgasmContractionRate       = Rand(rng, 0.8f, 4.0f);
            OrgasmContractionDepth      = Rand(rng, 0.2f, 0.9f);
            OrgasmSensitivityFlood      = Rand(rng, 0.4f, 1.0f);
            OrgasmAftershockCount       = Rand(rng, 0.0f, 4.0f);
            OrgasmAftershockDecay       = Rand(rng, 0.2f, 0.8f);
            OrgasmDelayThreshold        = Rand(rng, 0.0f, 0.4f);
            MultiOrgasmPropensity       = Rand(rng, 0.0f, 1.0f);
            IsMult                      = MultiOrgasmPropensity > 0.65f;
            SquirtPropensity            = Rand(rng, 0.0f, 1.0f);
            BodyTensionMax              = Rand(rng, 0.4f, 1.0f);
            PostOrgasmHypersensitivity  = Rand(rng, 0.2f, 1.0f);
        }

        private static float Rand(System.Random rng, float lo, float hi)
        {
            return (float)(rng.NextDouble() * (hi - lo) + lo);
        }
    }

    public class ArousalStates
    {
        public float Idle         = 1f;
        public float Resisting    = 0f;
        public float WarmingUp    = 0f;
        public float Responding   = 0f;
        public float Enjoying     = 0f;
        public float Craving      = 0f;
        public float Peaking      = 0f;
        public float Edging       = 0f;
        public float Desperate    = 0f;
        public float Desensitized = 0f;
        public float Frustrated   = 0f;
        public float Stalled      = 0f;
        public float OrgasmWave   = 0f;

        public string GetDominantState()
        {
            string dom = "Idle";
            float max = Idle;

            if (Resisting > max)    { max = Resisting;    dom = "Resisting"; }
            if (WarmingUp > max)    { max = WarmingUp;    dom = "WarmingUp"; }
            if (Responding > max)   { max = Responding;   dom = "Responding"; }
            if (Enjoying > max)     { max = Enjoying;     dom = "Enjoying"; }
            if (Craving > max)      { max = Craving;      dom = "Craving"; }
            if (Peaking > max)      { max = Peaking;      dom = "Peaking"; }
            if (Edging > max)       { max = Edging;       dom = "Edging"; }
            if (Desperate > max)    { max = Desperate;    dom = "Desperate"; }
            if (Stalled > max)      { max = Stalled;      dom = "Stalled"; }
            if (Desensitized > max) { max = Desensitized; dom = "Desensitized"; }
            if (Frustrated > max)   { max = Frustrated;   dom = "Frustrated"; }
            if (OrgasmWave > max)   { max = OrgasmWave;   dom = "OrgasmWave"; }

            return dom;
        }
    }

    class SeededTraits
    {
        public readonly float DepthSensitivity;
        public readonly float SpeedSensitivity;
        public readonly float PullSensitivity;
        public readonly float RhythmBonus;
        public readonly float RhythmLearningRate;
        public readonly float DepthThreshold;
        public readonly float DepthBonusGain;
        public readonly float SigmoidSteepness;
        public readonly float ClimaxResistance;
        public readonly float ClimaxThreshold;
        public readonly float EdgeThreshold;
        public readonly float EdgeBuildRate;
        public readonly float EdgeDecayRate;
        public readonly float MomentumDecay;
        public readonly float ZoneTouchWeight;
        public readonly float RestingBias;
        public readonly float HabituationRate;
        public readonly float FrustrationRate;
        public readonly float AdaptationRate;
        public readonly float NoiseSeed;
        public readonly float ProceduralVariance;
        public readonly float GirthSensitivity;
        public readonly float AngleSensitivity;
        public readonly float DwellPleasure;
        public readonly float PaceVarianceTolerrance;

        // --- round 2: dynamics / orgasm-difficulty traits -------------------
        // Appended at the end on purpose: the RNG is consumed sequentially, so
        // adding draws here keeps every trait above identical for a given seed.
        public readonly float Orgasmicity;       // how readily climax tips over (low = easy arouse, hard finish)
        public readonly float NoveltyCraving;    // how fast she bores of a repeated pace and craves change
        public readonly float PaceChangeReward;  // re-excitement gained from a pace/depth change
        public readonly float PreferredDepth;    // innate favourite depth
        public readonly float PreferredPace;     // innate favourite thrust interval (s)
        public readonly float ClimaxBuildRate;   // base speed the climax gate fills
        public readonly float ArousalFalloff;    // base detumescence rate when above the sustainable ceiling
        public readonly float BoredomRate;        // how fast boredom accumulates

        public SeededTraits(int seed)
        {
            var rng = new System.Random(seed);

            DepthSensitivity         = Rand(rng, 0.40f, 1.80f);
            SpeedSensitivity         = Rand(rng, 0.40f, 1.80f);
            PullSensitivity          = Rand(rng, 0.20f, 1.00f);
            RhythmBonus              = Rand(rng, 1.00f, 2.20f);
            RhythmLearningRate       = Rand(rng, 0.05f, 0.35f);
            DepthThreshold           = Rand(rng, 0.35f, 0.75f);
            DepthBonusGain           = Rand(rng, 1.20f, 2.50f);
            SigmoidSteepness         = Rand(rng, 3.00f, 9.00f);
            ClimaxResistance         = Rand(rng, 0.60f, 0.95f);
            ClimaxThreshold          = Rand(rng, 0.85f, 0.92f);
            EdgeThreshold            = Rand(rng, 0.60f, 0.82f);
            EdgeBuildRate            = Rand(rng, 0.08f, 0.25f);
            EdgeDecayRate            = Rand(rng, 0.01f, 0.05f);
            MomentumDecay            = Rand(rng, 0.08f, 0.30f);
            ZoneTouchWeight          = Rand(rng, 0.30f, 0.90f);
            RestingBias              = Rand(rng, -0.15f, 0.15f);
            HabituationRate          = Rand(rng, 0.02f, 0.08f);
            FrustrationRate          = Rand(rng, 0.05f, 0.15f);
            AdaptationRate           = Rand(rng, 0.02f, 0.10f);
            NoiseSeed                = Rand(rng, 0.0f, 1000.0f);
            ProceduralVariance       = Rand(rng, 0.05f, 0.22f);
            GirthSensitivity         = Rand(rng, 0.3f, 1.5f);
            AngleSensitivity         = Rand(rng, 0.1f, 0.8f);
            DwellPleasure            = Rand(rng, 0.1f, 0.9f);
            PaceVarianceTolerrance   = Rand(rng, 0.1f, 0.6f);

            Orgasmicity              = Rand(rng, 0.15f, 1.00f);
            NoveltyCraving           = Rand(rng, 0.10f, 0.95f);
            PaceChangeReward         = Rand(rng, 0.20f, 0.80f);
            PreferredDepth           = Rand(rng, 0.30f, 0.95f);
            PreferredPace            = Rand(rng, 0.30f, 1.40f);
            ClimaxBuildRate          = Rand(rng, 0.18f, 0.55f);
            ArousalFalloff           = Rand(rng, 0.04f, 0.14f);
            BoredomRate              = Rand(rng, 0.03f, 0.12f);
        }

        private static float Rand(System.Random rng, float lo, float hi)
        {
            return (float)(rng.NextDouble() * (hi - lo) + lo);
        }
    }


    public class OrgasmState
    {
        private OrgasmTraits traits_;
        private bool active_ = false;
        private float elapsed_ = 0f;
        private float wavePhase_ = 0f;
        private int aftershocksFired_ = 0;
        private float aftershockTimer_ = 0f;
        private float currentIntensity_ = 0f;
        private float contractionPhase_ = 0f;
        private float nextAftershockIn_ = 0f;

        public bool  Active          { get { return active_; } }
        public float Intensity       { get { return currentIntensity_; } }
        public float WavePhase       { get { return wavePhase_; } }
        public float ContractionPhase { get { return contractionPhase_; } }
        public bool  IsAftershock    { get { return aftershocksFired_ > 0 && elapsed_ > traits_.OrgasmDuration; } }
        public float PostSensitivity { get { return active_ ? traits_.PostOrgasmHypersensitivity * currentIntensity_ : 0f; } }

        public OrgasmState(OrgasmTraits t)
        {
            traits_ = t;
        }

        public void Begin()
        {
            active_           = true;
            elapsed_          = 0f;
            wavePhase_        = 0f;
            aftershocksFired_ = 0;
            aftershockTimer_  = 0f;
            contractionPhase_ = 0f;
            currentIntensity_ = 0f;
            nextAftershockIn_ = UnityEngine.Random.Range(1.5f, 4.0f);
        }

        public void End()
        {
            active_           = false;
            currentIntensity_ = 0f;
        }

        public float Update(float s)
        {
            if (!active_) return 0f;

            elapsed_ += s;

            float dur = traits_.OrgasmDuration;

            float normalised = Mathf.Clamp01(elapsed_ / dur);

            float build  = 1f - Mathf.Pow(1f - normalised, traits_.OrgasmBuildSharpness);
            float fade   = Mathf.Pow(1f - normalised, 1.2f);
            float envlp  = build * fade;

            wavePhase_        += s * traits_.OrgasmContractionRate;
            contractionPhase_  = (Mathf.Sin(wavePhase_) * 0.5f + 0.5f) * traits_.OrgasmContractionDepth;

            currentIntensity_  = envlp * traits_.OrgasmIntensityPeak + contractionPhase_ * 0.15f;
            currentIntensity_  = Mathf.Clamp01(currentIntensity_);

            float vocalDrive = currentIntensity_ * traits_.OrgasmVocality;

            if (elapsed_ >= dur)
            {
                int maxShocks = Mathf.RoundToInt(traits_.OrgasmAftershockCount);
                if (aftershocksFired_ < maxShocks)
                {
                    aftershockTimer_ += s;
                    if (aftershockTimer_ >= nextAftershockIn_)
                    {
                        aftershockTimer_   = 0f;
                        aftershocksFired_++;
                        float shockDecay   = Mathf.Pow(traits_.OrgasmAftershockDecay, aftershocksFired_);
                        currentIntensity_  = 0.35f * shockDecay * traits_.OrgasmIntensityPeak;
                        nextAftershockIn_  = UnityEngine.Random.Range(2.0f, 6.0f);
                    }
                    else
                    {
                        currentIntensity_ = 0f;
                    }
                }
                else
                {
                    End();
                    return 0f;
                }
            }

            return vocalDrive;
        }

        public void SetTraits(OrgasmTraits t)
        {
            traits_ = t;
        }
    }


    public class ProceduralArousalLayer
    {
        private float phase_       = 0f;
        private float phase2_      = 0f;
        private float amplitude_   = 0f;
        private float targetAmp_   = 0f;
        private float freq1_       = 0f;
        private float freq2_       = 0f;
        private readonly float variance_;

        public float Value { get; private set; }

        public ProceduralArousalLayer(float noiseSeed, float variance)
        {
            variance_ = variance;
            phase_    = noiseSeed * 6.2831853f;
            phase2_   = noiseSeed * 9.4247780f;
            freq1_    = 0.07f + noiseSeed * 0.05f;
            freq2_    = 0.13f + noiseSeed * 0.08f;
        }

        public void Update(float s, float excitement, bool active)
        {
            targetAmp_ = active ? excitement * variance_ : 0f;
            amplitude_ = Mathf.Lerp(amplitude_, targetAmp_, s * 0.8f);

            phase_  += s * freq1_;
            phase2_ += s * freq2_;

            float wave1 = Mathf.Sin(phase_);
            float wave2 = Mathf.Sin(phase2_) * 0.5f;
            float composite = (wave1 + wave2) / 1.5f;

            Value = composite * amplitude_;
        }
    }


    public class ArousalBrain
    {
        private const float UpdateInterval  = 0.20f;
        private const float SmoothRate      = 0.18f;
        private const float RhythmTolerance = 0.25f;

        private readonly Person             person_;
        private readonly Logger             log_;
        private          SeededTraits       traits_;
        private          ArousalPersonality personality_;
        private          OrgasmTraits       orgasmTraits_;
        private          OrgasmState        orgasmState_;
        private          ProceduralArousalLayer proceduralLayer_;

        private int   seed_;
        private float elapsed_      = 0f;
        private float elapsedTotal_ = 0f;

        private float arousalDeltaBias_ = 0f;
        private float urgeIntensity_    = 0f;
        private float edgeFactor_       = 0f;
        private float totalStim_        = 0f;
        private float habituation_      = 0f;
        private float frustration_      = 0f;

        private float physicalDrive_  = 1f;
        private float emotionalDrive_ = 1f;
        private float pendingBurst_   = 0f;

        private bool  isStalling_          = false;
        private float currentBarrierStatus_ = 1f;

        public float CurrentDepth    { get; private set; }
        public float CurrentVelocity { get; private set; }
        public float CurrentGirth    { get; private set; }
        public bool  IsPenetrated    { get; private set; }
        public float ProceduralNoise { get; private set; }
        public float OrgasmVocalDrive { get; private set; }

        public ArousalStates States { get; private set; } = new ArousalStates();

        private float momentumAccum_    = 0f;
        private float edgePressure_     = 0f;
        private float dwellSaturation_  = 0f;
        private float lastVelocity_     = 0f;
        private float lastDepth_        = 0f;
        private float lastBurstTime_    = 0f;

        private float learnedDepth_     = 0.5f;
        private float learnedPace_      = 1.0f;
        private float familiarityBonus_ = 0f;

        // --- round 2: boredom / novelty / climax gate ----------------------
        private float boredom_         = 0f;   // 0..1, rises with repetition, lowers arousal & blocks climax
        private float warmth_          = 0f;   // 0..1, slow build-up from sustained engagement, raises the plateau
        private float climaxReadiness_ = 0f;   // 0..1, must fill before she can actually orgasm
        private float climaxGateCap_   = 0.90f;// hard ceiling on arousal until readiness fills (breathes)
        private float falloffRate_     = 0f;   // current detumescence rate, consumed by Mood
        private float varietySignal_   = 0f;   // 0..1, how much the pace/depth is currently changing
        private float noveltyPulse_    = 0f;   // transient excitement from a fresh change
        private float paceEma_         = 0f;   // slow average pace, for variety detection
        private float depthEma_        = 0f;   // slow average depth
        private float lastExcitement_  = 0f;   // cached for fuzzy-state calc
        private int   orgasmCount_      = 0;    // how many times she's finished this session

        private float girthContrib_     = 0f;
        private float angleContrib_     = 0f;
        private float dwellPleasure_    = 0f;

        private const int RhythmBufSize = 8;
        private float[]   rhythmIntervals_ = new float[RhythmBufSize];
        private int       rhythmHead_      = 0;
        private int       rhythmCount_     = 0;
        private float     timeSinceThrust_ = 0f;
        private float     currentPace_     = 0f;
        private float     rhythmScore_     = 0f;

        private DebugLines debugLines_ = new DebugLines();

        private bool personalitySeeded_ = false;

        public ArousalBrain(Person p, int seed, ArousalPersonality personality = null)
        {
            person_          = p;
            log_             = new Logger(Logger.Object, p, "arousalBrain");
            seed_            = seed;
            traits_          = new SeededTraits(seed);
            orgasmTraits_    = new OrgasmTraits(seed);
            orgasmState_     = new OrgasmState(orgasmTraits_);
            proceduralLayer_ = new ProceduralArousalLayer(traits_.NoiseSeed, traits_.ProceduralVariance);

            learnedDepth_ = traits_.PreferredDepth;
            learnedPace_  = traits_.PreferredPace;

            if (personality != null)
            {
                personality_       = personality;
                personalitySeeded_ = false;
            }
            else
            {
                // Give every person a distinct, stable high-level temperament
                // derived from their (persisted) seed, so two people no longer
                // respond identically.
                personality_       = DerivePersonality(seed);
                personalitySeeded_ = true;
            }
        }

        public void SetSeed(int seed)
        {
            if (seed_ != seed)
            {
                seed_            = seed;
                traits_          = new SeededTraits(seed);
                orgasmTraits_    = new OrgasmTraits(seed);
                orgasmState_.SetTraits(orgasmTraits_);
                proceduralLayer_ = new ProceduralArousalLayer(traits_.NoiseSeed, traits_.ProceduralVariance);

                if (personalitySeeded_)
                    personality_ = DerivePersonality(seed);

                // A fresh seed is a fresh person: wipe everything she has
                // "learned" / accumulated so old preferences and fatigue don't
                // bleed into the new personality.
                ResetLearnedState();
            }
        }

        // Clears all runtime memory and accumulators back to a cold start.
        // Innate preferences are re-seeded from the (new) traits.
        public void ResetLearnedState()
        {
            arousalDeltaBias_ = 0f;
            urgeIntensity_    = 0f;
            edgeFactor_       = 0f;
            totalStim_        = 0f;
            habituation_      = 0f;
            frustration_      = 0f;
            momentumAccum_    = 0f;
            edgePressure_     = 0f;
            dwellSaturation_  = 0f;
            familiarityBonus_ = 0f;
            girthContrib_     = 0f;
            angleContrib_     = 0f;
            dwellPleasure_    = 0f;
            rhythmScore_      = 0f;
            rhythmCount_      = 0;
            rhythmHead_       = 0;

            boredom_          = 0f;
            warmth_           = 0f;
            climaxReadiness_  = 0f;
            climaxGateCap_    = 0.90f;
            falloffRate_      = 0f;
            varietySignal_    = 0f;
            noveltyPulse_     = 0f;
            paceEma_          = 0f;
            depthEma_         = 0f;
            lastExcitement_   = 0f;
            orgasmCount_      = 0;

            learnedDepth_     = traits_.PreferredDepth;
            learnedPace_      = traits_.PreferredPace;
        }

        public void SetPersonality(ArousalPersonality p)
        {
            personality_       = p ?? ArousalPersonality.Default();
            personalitySeeded_ = false;
        }

        private static ArousalPersonality DerivePersonality(int seed)
        {
            var rng = new System.Random(seed ^ 0x5F3759DF);

            return new ArousalPersonality
            {
                Sensitivity    = 0.60f + (float)rng.NextDouble() * 1.10f,  // 0.60..1.70
                Responsiveness = 0.50f + (float)rng.NextDouble() * 1.10f,  // 0.50..1.60
                Stamina        = 0.60f + (float)rng.NextDouble() * 1.10f,  // 0.60..1.70
                Inhibition     = (float)rng.NextDouble() * 0.50f           // 0.00..0.50
            };
        }

        // Serialises the brain's slow-moving runtime memory so a saved scene
        // resumes mid-arousal instead of snapping back to a cold start. The
        // seed is included so the per-person traits stay identical across a
        // save/load or a plugin reset.
        public JSONClass ToJSON()
        {
            var o = new JSONClass();

            o.Add("seed",         new JSONData(seed_));
            o.Add("habituation",  new JSONData(habituation_));
            o.Add("frustration",  new JSONData(frustration_));
            o.Add("edgePressure", new JSONData(edgePressure_));
            o.Add("edgeFactor",   new JSONData(edgeFactor_));
            o.Add("urge",         new JSONData(urgeIntensity_));
            o.Add("totalStim",    new JSONData(totalStim_));
            o.Add("deltaBias",    new JSONData(arousalDeltaBias_));
            o.Add("momentum",     new JSONData(momentumAccum_));
            o.Add("dwell",        new JSONData(dwellSaturation_));
            o.Add("learnedDepth", new JSONData(learnedDepth_));
            o.Add("learnedPace",  new JSONData(learnedPace_));
            o.Add("familiarity",  new JSONData(familiarityBonus_));
            o.Add("boredom",      new JSONData(boredom_));
            o.Add("warmth",       new JSONData(warmth_));
            o.Add("climaxReady",  new JSONData(climaxReadiness_));
            o.Add("orgasmCount",  new JSONData(orgasmCount_));

            return o;
        }

        public void Load(JSONClass o)
        {
            if (o == null)
                return;

            int seed = seed_;
            if (J.OptInt(o, "seed", ref seed))
                SetSeed(seed);

            J.OptFloat(o, "habituation",  ref habituation_);
            J.OptFloat(o, "frustration",  ref frustration_);
            J.OptFloat(o, "edgePressure", ref edgePressure_);
            J.OptFloat(o, "edgeFactor",   ref edgeFactor_);
            J.OptFloat(o, "urge",         ref urgeIntensity_);
            J.OptFloat(o, "totalStim",    ref totalStim_);
            J.OptFloat(o, "deltaBias",    ref arousalDeltaBias_);
            J.OptFloat(o, "momentum",     ref momentumAccum_);
            J.OptFloat(o, "dwell",        ref dwellSaturation_);
            J.OptFloat(o, "learnedDepth", ref learnedDepth_);
            J.OptFloat(o, "learnedPace",  ref learnedPace_);
            J.OptFloat(o, "familiarity",  ref familiarityBonus_);
            J.OptFloat(o, "boredom",      ref boredom_);
            J.OptFloat(o, "warmth",       ref warmth_);
            J.OptFloat(o, "climaxReady",  ref climaxReadiness_);
            J.OptInt(o,   "orgasmCount",  ref orgasmCount_);
        }

        public void GetExcitementFactors(out float physicalMul, out float emotionalMul, out float burst)
        {
            physicalMul  = physicalDrive_;
            emotionalMul = emotionalDrive_;
            burst        = pendingBurst_;
            pendingBurst_ = 0f;
        }

        public int                Seed             { get { return seed_; } }
        public float              ArousalDeltaBias { get { return arousalDeltaBias_; } }
        public float              UrgeIntensity    { get { return urgeIntensity_; } }
        public float              EdgeFactor       { get { return edgeFactor_; } }
        public float              TotalStim        { get { return totalStim_; } }
        public float              Habituation      { get { return habituation_; } }
        public float              Frustration      { get { return frustration_; } }
        public float              ThrustAccum      { get { return momentumAccum_; } }
        public float              DwellSaturation  { get { return dwellSaturation_; } }
        public float              LearnedDepth     { get { return learnedDepth_; } }
        public float              LearnedPace      { get { return learnedPace_; } }
        public float              Familiarity      { get { return familiarityBonus_; } }
        public bool               IsStalling       { get { return isStalling_; } }
        public float              BarrierStatus    { get { return currentBarrierStatus_; } }
        public float              DepthSens        { get { return traits_.DepthSensitivity; } }
        public float              SpeedSens        { get { return traits_.SpeedSensitivity; } }
        public float              ClimaxResist     { get { return traits_.ClimaxResistance; } }
        public OrgasmTraits       OrgasmTraits     { get { return orgasmTraits_; } }
        public OrgasmState        OrgasmState      { get { return orgasmState_; } }
        public ArousalPersonality Personality      { get { return personality_; } }
        public string             StateString      { get { return States.GetDominantState(); } }

        // Instantaneous bodily drive (0..1): how hard the body is being worked
        // right now. Fed into the excitement *rate* so vigorous, well-matched
        // stimulation actually moves arousal quickly.
        public float PhysicalArousalDrive
        {
            get { return Mathf.Clamp01(totalStim_ * 0.70f + urgeIntensity_ * 0.45f + edgeFactor_ * 0.25f); }
        }

        // The arousal level the *current* stimulation can sustain (0..1). This
        // lifts the excitement ceiling so toy/dildo scenes (which the legacy
        // zone system barely registers) can build and climax, while collapsing
        // to ~0 when nothing is happening so idle people don't drift upward.
        public float ArousalCeiling
        {
            get
            {
                if (!IsPenetrated && urgeIntensity_ < 0.05f && totalStim_ < 0.05f && warmth_ < 0.05f)
                    return 0f;

                float ceil = Mathf.Clamp01(
                    urgeIntensity_ * 0.60f + totalStim_ * 0.45f
                    + edgeFactor_ * 0.30f + warmth_ * 0.50f);

                // Boredom drains the sustainable level a little while penetrated
                // (noticeable, but it must not park her at a low standstill --
                // its real bite is on the climax gate, not the plateau height).
                ceil *= (1f - boredom_ * 0.18f);

                // Hold strictly below 1.0 until the climax gate opens (Update).
                return Mathf.Min(ceil, climaxGateCap_);
            }
        }

        // Detumescence rate (per second) Mood should use when arousal is above
        // the currently sustainable ceiling -- replaces the old fixed -0.01/s.
        public float FalloffRate     { get { return falloffRate_; } }
        public float Boredom         { get { return boredom_; } }
        public float ClimaxReadiness { get { return climaxReadiness_; } }
        public float VarietySignal   { get { return varietySignal_; } }
        public int   OrgasmCount     { get { return orgasmCount_; } }

        // Called by Mood when an orgasm starts: counts it, drops readiness, and
        // -- after a couple of repeats on the same pattern -- drifts her learned
        // preferences so the identical motion becomes less effective (she wants
        // something new).
        public void OnOrgasm()
        {
            orgasmCount_++;
            climaxReadiness_ = 0f;
            edgePressure_    = 0f;
            frustration_     = 0f;

            if (orgasmCount_ >= 2)
            {
                float drift = traits_.NoveltyCraving * 0.25f;
                float sign  = (UnityEngine.Random.value < 0.5f) ? -1f : 1f;

                learnedDepth_ = Mathf.Clamp01(learnedDepth_ + sign * drift);
                learnedPace_  = Mathf.Max(0.1f, learnedPace_ * (1f + sign * drift));
                boredom_      = Mathf.Min(1f, boredom_ + traits_.NoveltyCraving * 0.20f);
            }
        }

        // She should kick straight into a sustained/perpetual orgasm vocal loop.
        public bool ShouldPerpetualOrgasm()
        {
            return orgasmTraits_.IsMult
                && orgasmTraits_.MultiOrgasmPropensity > 0.70f
                && IsPenetrated
                && totalStim_ > 0.55f;
        }

        // She's a multi-orgasmic type and stimulation is still strong enough to
        // roll straight into another wave instead of dropping into afterglow.
        public bool ShouldContinueOrgasm()
        {
            return orgasmTraits_.IsMult
                && IsPenetrated
                && totalStim_ > 0.50f
                && urgeIntensity_ > 0.55f;
        }

        public void NotifyOrgasmBegun()
        {
            orgasmState_.Begin();
        }

        public void NotifyOrgasmEnded()
        {
            orgasmState_.End();
        }

        public void Update(float s, PenetrationStats pen)
        {
            elapsed_         += s;
            elapsedTotal_    += s;
            timeSinceThrust_ += s;

            float vel    = pen.SmoothedVelocity;
            float absVel = Mathf.Abs(vel);
            bool  active = pen.Active;

            CurrentDepth    = pen.NormalisedDepth;
            CurrentVelocity = absVel;
            CurrentGirth    = pen.NormalisedDepth > 0.01f ? Mathf.Clamp01(pen.InsertionDepthMetres * 2.5f) : 0f;
            IsPenetrated    = active;

            OrgasmVocalDrive = orgasmState_.Update(s);

            proceduralLayer_.Update(s, person_.Mood.Get(MoodType.Excited), active);
            ProceduralNoise = proceduralLayer_.Value;

            if (vel > 0.05f && lastVelocity_ <= 0.05f && active)
                RecordThrust();

            bool deepThrust = (pen.NormalisedDepth > traits_.DepthThreshold && lastDepth_ <= traits_.DepthThreshold && vel > 0.15f);
            if (deepThrust && active && (elapsedTotal_ - lastBurstTime_ > 2.0f))
            {
                pendingBurst_  += 0.15f * personality_.Sensitivity;
                lastBurstTime_  = elapsedTotal_;
            }

            lastVelocity_ = vel;
            lastDepth_    = pen.NormalisedDepth;

            if (vel > 0f && active)
            {
                float gain     = vel * s * personality_.Responsiveness;
                momentumAccum_ = Mathf.Min(momentumAccum_ + gain, 1f);
            }
            else
            {
                momentumAccum_ = Mathf.Max(momentumAccum_ - s * traits_.MomentumDecay, 0f);
            }

            if (active)
                dwellSaturation_ = Mathf.Min(dwellSaturation_ + s * 0.008f * personality_.Stamina, 1f);
            else
                dwellSaturation_ = Mathf.Max(dwellSaturation_ - s * 0.05f, 0f);

            UpdateRhythm(s);

            if (elapsed_ < UpdateInterval) return;
            float dt  = elapsed_;
            elapsed_  = 0f;

            float excitement = person_.Mood.Get(MoodType.Excited);

            float pushStim = 0f;
            if (vel > 0.01f && active)
            {
                float depthPart = pen.NormalisedDepth * traits_.DepthSensitivity * 0.5f;
                float speedPart = absVel * traits_.SpeedSensitivity * 0.5f;
                float synergy   = (pen.NormalisedDepth * absVel) * 0.5f;
                pushStim        = Mathf.Clamp01(depthPart + speedPart + synergy);
            }

            float pullStim = (vel < -0.01f && active)
                ? Mathf.Clamp01(pen.NormalisedDepth * absVel * traits_.PullSensitivity) : 0f;

            float rawStim = Mathf.Max(pushStim, pullStim);

            if (pen.NormalisedDepth > traits_.DepthThreshold && active)
                rawStim *= traits_.DepthBonusGain;

            if (active && CurrentGirth > 0.1f)
            {
                girthContrib_ = Mathf.Lerp(girthContrib_, CurrentGirth * traits_.GirthSensitivity * 0.3f, dt * 2f);
                rawStim      += girthContrib_;
            }
            else
            {
                girthContrib_ = Mathf.Lerp(girthContrib_, 0f, dt * 1.5f);
            }

            if (active && pen.ApproachAngleDegrees > 0f)
            {
                float angleFactor = Mathf.Clamp01(pen.ApproachAngleDegrees / 45f);
                angleContrib_     = angleFactor * traits_.AngleSensitivity * 0.2f;
                rawStim          += angleContrib_;
            }
            else
            {
                angleContrib_ = 0f;
            }

            if (active && absVel < 0.05f && pen.NormalisedDepth > 0.3f)
            {
                dwellPleasure_ = Mathf.Lerp(dwellPleasure_, traits_.DwellPleasure * pen.NormalisedDepth * 0.4f, dt * 0.5f);
                rawStim       += dwellPleasure_;
            }
            else
            {
                dwellPleasure_ = Mathf.Lerp(dwellPleasure_, 0f, dt * 2f);
            }

            float rhythmMul    = 1f + rhythmScore_ * (traits_.RhythmBonus - 1f);
            float stimResponse = Sigmoid(rawStim * personality_.Sensitivity, traits_.SigmoidSteepness);
            float momMul       = 1f + momentumAccum_ * personality_.Responsiveness * 0.8f;

            int maxP = Math.Max(1, Cue.Instance.ActivePersons.Length);
            float zoneTouch = (ZoneSources(SS.Genitals) + ZoneSources(SS.Breasts) + ZoneSources(SS.Mouth))
                / (float)(maxP * 3) * traits_.ZoneTouchWeight * personality_.Sensitivity;

            if (States.Enjoying > 0.5f && active)
            {
                learnedDepth_ = Lerp(learnedDepth_, pen.NormalisedDepth, dt * traits_.AdaptationRate);
                if (currentPace_ > 0.1f)
                    learnedPace_ = Lerp(learnedPace_, currentPace_, dt * traits_.AdaptationRate);
            }

            float depthMatch       = 1f - Mathf.Abs(pen.NormalisedDepth - learnedDepth_);
            float paceMatch        = currentPace_ > 0.1f ? 1f - Mathf.Min(Mathf.Abs(currentPace_ - learnedPace_) / learnedPace_, 1f) : 0f;
            familiarityBonus_      = (depthMatch * 0.5f + paceMatch * 0.5f);

            float adaptedStim = stimResponse * rhythmMul * momMul * (1f + familiarityBonus_ * 0.2f) + zoneTouch;

            float proceduralContrib = ProceduralNoise;
            adaptedStim += proceduralContrib;

            if (active && rawStim > 0.1f)
            {
                float repetitiveness = rhythmScore_ * dwellSaturation_;
                habituation_ = Mathf.Min(habituation_ + dt * traits_.HabituationRate * repetitiveness, 1f);
            }
            else if (!active || absVel < 0.01f)
            {
                habituation_ = Mathf.Max(habituation_ - dt * traits_.HabituationRate * 2.0f, 0f);
            }

            // --- variety / boredom -----------------------------------------
            // Detect how much the *pattern* is actually changing. A steady,
            // unvarying pace at high engagement breeds boredom (scaled by
            // NoveltyCraving), which cools her down now and blocks the climax
            // gate later. A genuine change injects a novelty pulse that
            // re-excites and relieves boredom -- this is what makes "change it
            // up" matter.
            paceEma_  = Lerp(paceEma_,  currentPace_, dt * 0.30f);
            depthEma_ = Lerp(depthEma_, CurrentDepth, dt * 0.30f);

            float paceDev = currentPace_ > 0.05f
                ? Mathf.Min(Mathf.Abs(currentPace_ - paceEma_) / Mathf.Max(paceEma_, 0.1f), 1f)
                : 0f;
            float depthDev   = Mathf.Abs(CurrentDepth - depthEma_);
            float varietyRaw = Mathf.Clamp01(paceDev * 0.7f + depthDev * 1.5f);

            if (varietyRaw - varietySignal_ > 0.25f)
            {
                noveltyPulse_ = traits_.PaceChangeReward;
                boredom_      = Mathf.Max(0f, boredom_ - 0.25f);
            }

            varietySignal_ = Lerp(varietySignal_, varietyRaw, dt * 1.5f);
            noveltyPulse_  = Mathf.Max(0f, noveltyPulse_ - dt * 1.2f);

            // Boredom relaxes toward a target set by how steady + repetitive the
            // current stimulation is (scaled by NoveltyCraving). It rises slowly
            // and falls quickly, and is deliberately bounded well below 1 so it
            // never crushes arousal to a standstill -- its main job is to make
            // *finishing* hard (it gates climaxReadiness below), not to flatten
            // the plateau.
            float boredomTarget;
            if (active && dwellSaturation_ > 0.15f)
            {
                float steadiness = Mathf.Clamp01(1f - varietySignal_ * 1.5f);
                boredomTarget = steadiness * traits_.NoveltyCraving * 0.80f
                              * (0.5f + rhythmScore_ * 0.5f);
            }
            else
            {
                boredomTarget = 0f;
            }

            float boredomSpeed = (boredomTarget > boredom_)
                ? traits_.BoredomRate * 2.5f   // builds gradually
                : 0.40f;                        // fades quickly on variety / rest
            boredom_ = Mathf.MoveTowards(boredom_, boredomTarget, dt * boredomSpeed);
            boredom_ = Mathf.Clamp01(boredom_);

            // --- consolidate stimulation -----------------------------------
            float habituationPenalty = 1f - (habituation_ * 0.4f);
            float boredomPenalty     = 1f - (boredom_ * 0.6f);
            float targetTotalStim    = Mathf.Clamp01(
                (adaptedStim + noveltyPulse_ * 0.30f) * habituationPenalty * boredomPenalty);
            totalStim_               = Lerp(totalStim_, targetTotalStim, SmoothRate);

            // --- warmth: slow arousal build-up -----------------------------
            // Real arousal accumulates with sustained engagement. Without it the
            // plateau is capped by *instantaneous* stim, so moderate dildo motion
            // parks her near 0.5. Warmth creeps up while she's actively worked
            // and feeds the ceiling, so steady play climbs over ~30s to a high
            // plateau (the climax gate still keeps it below 1.0 until ready).
            if (active && totalStim_ > 0.20f)
                warmth_ = Mathf.Min(
                    warmth_ + dt * 0.030f * personality_.Responsiveness * (0.5f + totalStim_), 1f);
            else
                warmth_ = Mathf.Max(warmth_ - dt * 0.05f, 0f);

            isStalling_          = false;
            currentBarrierStatus_ = 1f;

            if (excitement > traits_.ClimaxThreshold && active)
            {
                float threshold = traits_.ClimaxResistance;

                if (targetTotalStim < threshold)
                {
                    float penalty         = 1f - (threshold - targetTotalStim);
                    currentBarrierStatus_ = Mathf.Max(0.05f, penalty * penalty * penalty);
                    isStalling_           = true;

                    frustration_ = Mathf.Min(frustration_ + dt * traits_.FrustrationRate * 5f, 1f);
                    pendingBurst_ -= dt * 0.1f;
                }
                else
                {
                    currentBarrierStatus_ = 1.2f + (targetTotalStim - threshold);
                    pendingBurst_        += dt * 0.2f * personality_.Sensitivity;
                    frustration_          = Mathf.Max(frustration_ - dt * traits_.FrustrationRate * 4f, 0f);
                }
            }
            else
            {
                if (!active || targetTotalStim < 0.3f)
                {
                    if (edgePressure_ > 0.2f || urgeIntensity_ > 0.6f)
                        frustration_ = Mathf.Min(frustration_ + dt * traits_.FrustrationRate, 1f);
                    else
                        frustration_ = Mathf.Max(frustration_ - dt * traits_.FrustrationRate * 0.5f, 0f);
                }
                else
                {
                    frustration_ = Mathf.Max(frustration_ - dt * traits_.FrustrationRate * 3f, 0f);
                }
            }

            float inhibitionDamper = 1f - personality_.Inhibition;
            float targetDelta      = Mathf.Clamp((targetTotalStim * 2f - 1f + traits_.RestingBias) * inhibitionDamper, -1f, 1f);

            float targetUrge = Mathf.Clamp01(
                targetTotalStim * personality_.Sensitivity
                + momentumAccum_ * 0.3f
                + frustration_ * 0.2f
                - personality_.Inhibition * 0.4f);

            physicalDrive_  = (1f + targetTotalStim * 0.8f + momentumAccum_ * 0.4f) * currentBarrierStatus_;
            emotionalDrive_ = (1f + urgeIntensity_ * 0.5f + frustration_ * 0.3f + familiarityBonus_ * 0.4f - personality_.Inhibition * 0.5f) * Mathf.Max(currentBarrierStatus_, 0.5f);

            float effectiveArousal = Mathf.Max(excitement, urgeIntensity_);
            if (active && effectiveArousal >= traits_.EdgeThreshold)
            {
                float overThresh      = (effectiveArousal - traits_.EdgeThreshold) / (1f - traits_.EdgeThreshold);
                float buildMultiplier = 1f + overThresh;
                edgePressure_         = Mathf.Min(edgePressure_ + dt * traits_.EdgeBuildRate * targetTotalStim * buildMultiplier, 1f);
            }
            else
            {
                float decayMod = 1f - (frustration_ * 0.8f);
                edgePressure_  = Mathf.Max(edgePressure_ - dt * traits_.EdgeDecayRate * decayMod, 0f);
            }

            float targetEdge = Mathf.Clamp01(edgePressure_);

            arousalDeltaBias_ = Lerp(arousalDeltaBias_, targetDelta, SmoothRate);
            urgeIntensity_    = Lerp(urgeIntensity_,    targetUrge,  SmoothRate);
            edgeFactor_       = Lerp(edgeFactor_,       targetEdge,  SmoothRate * 0.5f);

            // --- climax gate -----------------------------------------------
            // Arousal can't actually reach orgasm until this readiness fills.
            // It builds from being near the top WITH variety and WITHOUT
            // boredom, scaled by Orgasmicity -- so a low-Orgasmicity person will
            // hover at the edge almost indefinitely unless you change things up.
            lastExcitement_ = excitement;

            float nearTop = Mathf.Clamp01(
                (effectiveArousal - traits_.EdgeThreshold) /
                Mathf.Max(1f - traits_.EdgeThreshold, 0.05f));

            if (active && nearTop > 0f)
            {
                float varietyFactor = 0.40f + varietySignal_ * 1.20f;  // steady~0.4 .. varied~1.6
                float notBored      = 1f - boredom_ * 0.90f;
                float build = traits_.ClimaxBuildRate * personality_.Sensitivity
                            * traits_.Orgasmicity * varietyFactor * notBored
                            * targetTotalStim * nearTop;
                climaxReadiness_ += dt * build;
            }
            else
            {
                climaxReadiness_ -= dt * 0.15f;
            }
            climaxReadiness_ = Mathf.Clamp01(climaxReadiness_);

            // The plateau the body holds at: opens above 1.0 only once ready,
            // and -- while still building -- "breathes" with procedural noise +
            // boredom so high arousal is never a flat line (this kills the
            // constant-moan problem). The breathing/boredom suppression fades as
            // readiness fills, so a fully-ready body reliably tips over.
            float plateau = Mathf.Lerp(0.90f, 1.05f, climaxReadiness_);
            float breathe = 1f - climaxReadiness_;
            plateau -= Mathf.Abs(ProceduralNoise) * 0.12f * breathe;
            plateau -= boredom_ * 0.08f * breathe;
            climaxGateCap_ = Mathf.Clamp(plateau, 0.55f, 1.05f);

            // Detumescence rate Mood applies when arousal sits above what the
            // current (cooling) stimulation can sustain. Boredom makes her cool
            // off noticeably faster.
            falloffRate_ = traits_.ArousalFalloff * (1f + boredom_ * 1.0f) * (1.2f - totalStim_ * 0.6f);

            UpdateFuzzyStates();
        }

        private void UpdateFuzzyStates()
        {
            States.Idle         = Mathf.Clamp01(1f - totalStim_ - urgeIntensity_);
            States.Resisting    = Mathf.Clamp01((personality_.Inhibition * 2f) - totalStim_);
            States.WarmingUp    = Mathf.Clamp01(urgeIntensity_ * 2f) * (1f - States.Enjoying);
            States.Responding   = Mathf.Clamp01(totalStim_ * 1.5f);
            States.Enjoying     = Mathf.Clamp01((urgeIntensity_ - 0.2f) * 2f);
            States.Craving      = Mathf.Clamp01((urgeIntensity_ - 0.6f) * 2.5f);
            // nearTop = how close arousal is to the climax threshold.
            float nearTop = Mathf.Clamp01(
                (lastExcitement_ - traits_.EdgeThreshold) /
                Mathf.Max(1f - traits_.EdgeThreshold, 0.05f));

            // Edging is genuinely "held near the top but not allowed to finish":
            // high arousal with the climax gate still shut. This makes the state
            // reachable (it used to be raw edgeFactor and almost never lit up).
            States.Edging       = Mathf.Clamp01(
                nearTop * (1f - climaxReadiness_) * (0.6f + edgePressure_ * 0.8f)
                + edgeFactor_ * 0.3f);

            // Peaking is the opposite: near the top AND ready to go over.
            States.Peaking      = Mathf.Clamp01(nearTop * climaxReadiness_ * 1.6f);

            States.Desperate    = Mathf.Clamp01(States.Edging * (frustration_ + boredom_) * 1.5f);
            States.Stalled      = isStalling_ ? 1f : Mathf.Clamp01(boredom_ * nearTop * 1.5f);
            States.Desensitized = Mathf.Max(habituation_, boredom_);
            States.Frustrated   = Mathf.Clamp01(frustration_ + boredom_ * 0.4f);
            States.OrgasmWave   = orgasmState_.Active ? orgasmState_.Intensity : 0f;
        }

        public string[] Debug()
        {
            debugLines_.Clear();
            debugLines_.Add("dom_state",     StateString);
            debugLines_.Add("delta",         arousalDeltaBias_.ToString("0.00"));
            debugLines_.Add("urge",          urgeIntensity_.ToString("0.00"));
            debugLines_.Add("edge",          edgeFactor_.ToString("0.00"));
            debugLines_.Add("totalStim",     totalStim_.ToString("0.00"));
            debugLines_.Add("momentum",      momentumAccum_.ToString("0.00"));
            debugLines_.Add("habituation",   habituation_.ToString("0.00"));
            debugLines_.Add("frustration",   frustration_.ToString("0.00"));
            debugLines_.Add("boredom",       boredom_.ToString("0.00"));
            debugLines_.Add("warmth",        warmth_.ToString("0.00"));
            debugLines_.Add("variety",       varietySignal_.ToString("0.00"));
            debugLines_.Add("climaxReady",   climaxReadiness_.ToString("0.00"));
            debugLines_.Add("gateCap",       climaxGateCap_.ToString("0.00"));
            debugLines_.Add("falloff",       falloffRate_.ToString("0.000"));
            debugLines_.Add("orgasmCount",   orgasmCount_.ToString());
            debugLines_.Add("barrierStat",   currentBarrierStatus_.ToString("0.00"));
            debugLines_.Add("procNoise",     ProceduralNoise.ToString("0.00"));
            debugLines_.Add("girthContrib",  girthContrib_.ToString("0.00"));
            debugLines_.Add("angleContrib",  angleContrib_.ToString("0.00"));
            debugLines_.Add("dwellPleasure", dwellPleasure_.ToString("0.00"));
            debugLines_.Add("", "");
            debugLines_.Add("orgasm:", "");
            debugLines_.Add("  active",      orgasmState_.Active.ToString());
            debugLines_.Add("  intensity",   orgasmState_.Intensity.ToString("0.00"));
            debugLines_.Add("  contraction", orgasmState_.ContractionPhase.ToString("0.00"));
            debugLines_.Add("  isAftershock",orgasmState_.IsAftershock.ToString());
            debugLines_.Add("  vocalDrive",  OrgasmVocalDrive.ToString("0.00"));
            debugLines_.Add("", "");
            debugLines_.Add("learning/AI:", "");
            debugLines_.Add("  familiarity", familiarityBonus_.ToString("0.00"));
            debugLines_.Add("  learnDepth",  learnedDepth_.ToString("0.00"));
            debugLines_.Add("  learnPace",   learnedPace_.ToString("0.00") + "s");
            debugLines_.Add("", "");
            debugLines_.Add("states:", "");
            debugLines_.Add("  enjoying",    States.Enjoying.ToString("0.00"));
            debugLines_.Add("  craving",     States.Craving.ToString("0.00"));
            debugLines_.Add("  edging",      States.Edging.ToString("0.00"));
            debugLines_.Add("  stalled",     States.Stalled.ToString("0.00"));
            debugLines_.Add("  frust",       States.Frustrated.ToString("0.00"));
            debugLines_.Add("  orgasmWave",  States.OrgasmWave.ToString("0.00"));
            debugLines_.Add("", "");
            debugLines_.Add("traits:", "");
            debugLines_.Add("  depthSens",   traits_.DepthSensitivity.ToString("0.00"));
            debugLines_.Add("  speedSens",   traits_.SpeedSensitivity.ToString("0.00"));
            debugLines_.Add("  girthSens",   traits_.GirthSensitivity.ToString("0.00"));
            debugLines_.Add("  climaxRes",   traits_.ClimaxResistance.ToString("0.00"));
            debugLines_.Add("  procVar",     traits_.ProceduralVariance.ToString("0.00"));
            debugLines_.Add("  orgasmicity", traits_.Orgasmicity.ToString("0.00"));
            debugLines_.Add("  noveltyCrav", traits_.NoveltyCraving.ToString("0.00"));
            debugLines_.Add("  paceReward",  traits_.PaceChangeReward.ToString("0.00"));
            debugLines_.Add("  climaxBuild", traits_.ClimaxBuildRate.ToString("0.00"));
            debugLines_.Add("  arousalFall", traits_.ArousalFalloff.ToString("0.00"));
            debugLines_.Add("orgasmTraits:", "");
            debugLines_.Add("  duration",    orgasmTraits_.OrgasmDuration.ToString("0.0"));
            debugLines_.Add("  vocality",    orgasmTraits_.OrgasmVocality.ToString("0.00"));
            debugLines_.Add("  contRate",    orgasmTraits_.OrgasmContractionRate.ToString("0.00"));
            debugLines_.Add("  multiOrg",    orgasmTraits_.IsMult.ToString());
            debugLines_.Add("  postHyper",   orgasmTraits_.PostOrgasmHypersensitivity.ToString("0.00"));
            return debugLines_.MakeArray();
        }

        private void RecordThrust()
        {
            if (rhythmCount_ > 0)
            {
                rhythmIntervals_[rhythmHead_] = timeSinceThrust_;
                rhythmHead_ = (rhythmHead_ + 1) % RhythmBufSize;
                if (rhythmCount_ < RhythmBufSize)
                    rhythmCount_++;
            }
            else
            {
                rhythmCount_ = 1;
            }

            timeSinceThrust_ = 0f;
        }

        private void UpdateRhythm(float s)
        {
            if (rhythmCount_ < 2)
            {
                rhythmScore_ = 0f;
                return;
            }

            float sum = 0f;
            int   n   = rhythmCount_ - 1;

            for (int i = 0; i < n; ++i)
                sum += rhythmIntervals_[i];

            currentPace_ = sum / n;
            if (currentPace_ < 0.01f)
            {
                rhythmScore_ = 0f;
                return;
            }

            float varSum = 0f;
            for (int i = 0; i < n; ++i)
            {
                float diff = (rhythmIntervals_[i] - currentPace_) / currentPace_;
                varSum    += diff * diff;
            }

            float cv     = (float)Math.Sqrt(varSum / n);
            float target = Mathf.Clamp01(1f - cv / RhythmTolerance / 4f);

            if (timeSinceThrust_ > currentPace_ * 2f)
                target = 0f;

            rhythmScore_ = Lerp(rhythmScore_, target, traits_.RhythmLearningRate);
        }

        private static float Sigmoid(float x, float k)
        {
            float shifted = x - 0.5f;
            float e       = (float)Math.Exp(-k * shifted);
            return 1f / (1f + e);
        }

        private int ZoneSources(ZoneType z)
        {
            var zone = person_.Body.Zone(z);
            return zone?.ActiveSources ?? 0;
        }

        private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
    }
}
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Cue.Sound
{
    // One user-configured mapping: "when X happens (optionally filtered to a
    // body part / orifice), play a clip from sound set S with these volume and
    // pitch characteristics".
    public class SoundRule
    {
        public const int TriggerImpact       = 0;
        public const int TriggerPenEntry     = 1;
        public const int TriggerPenExit      = 2;
        public const int TriggerDeepThrust   = 3;
        public const int TriggerTongue       = 4;
        public const int TriggerFingerEntry  = 5;
        public const int TriggerFingerExit   = 6;
        public const int TriggerOrgasm       = 7;
        public const int TriggerThrustIn     = 8;   // continuous, while sliding inward
        public const int TriggerThrustOut    = 9;   // continuous, while sliding outward
        public const int TriggerCount        = 10;

        public static readonly string[] TriggerNames = new string[]
        {
            "Impact on body part", "Penetration entry", "Penetration exit",
            "Deep thrust", "Tongue/throat contact", "Fingering entry",
            "Fingering exit", "Orgasm start",
            "Thrusting IN (wet loop)", "Thrusting OUT (wet loop)"
        };

        public const int OrificeAny    = 0;
        public const int OrificeVagina = 1;
        public const int OrificeAnus   = 2;
        public const int OrificeMouth  = 3;

        public static readonly string[] OrificeNames = new string[]
        {
            "Any", "Vagina", "Anus", "Mouth"
        };

        public int trigger = TriggerImpact;
        public BodyPartType part = BP.None;      // Impact filter; None = any tracked part
        public int orifice = OrificeAny;         // penetration triggers filter
        public string set = "";
        public float volume = 1.0f;
        public float pitch = 1.0f;
        public float pitchJitter = 0.05f;
        public float intensityToVolume = 1.0f;   // 0 = constant volume, 1 = fully attenuated
        public float velToPitch = 0.0f;          // 0 = constant pitch, 1 = hard hits ring up to +0.5
        public float minInterval = 0.15f;
        public float depthThreshold = 0.8f;      // DeepThrust / Tongue
        public float minSpeed = 0.4f;            // m/s -> intensity 0 (impact / fingering)
        public float maxSpeed = 2.8f;            // m/s -> intensity 1 (impact / fingering)
        public bool enabled = true;

        // Maps a raw closing/approach speed (m/s) to a 0..1 intensity using this
        // rule's min/max speed window. Used by the impact and fingering
        // detectors so each rule can have its own sensitivity.
        public float SpeedToIntensity(float speed)
        {
            float span = maxSpeed - minSpeed;
            if (span < 0.01f) span = 0.01f;
            return Mathf.Clamp01((speed - minSpeed) / span);
        }

        // runtime
        public float lastFire = -1000f;
        public bool armed = false;               // for threshold-crossing triggers

        public bool MatchesOrifice(string name)
        {
            if (orifice == OrificeAny) return true;
            if (string.IsNullOrEmpty(name)) return false;

            switch (orifice)
            {
                case OrificeVagina: return name == "Vagina";
                case OrificeAnus:   return name == "Anus";
                case OrificeMouth:  return name == "Mouth";
            }

            return true;
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("trigger",  new JSONData(trigger));
            o.Add("part",     new JSONData(part.Int));
            o.Add("orifice",  new JSONData(orifice));
            o.Add("set",      set);
            o.Add("volume",   new JSONData(volume));
            o.Add("pitch",    new JSONData(pitch));
            o.Add("jitter",   new JSONData(pitchJitter));
            o.Add("intVol",   new JSONData(intensityToVolume));
            o.Add("velPitch", new JSONData(velToPitch));
            o.Add("interval", new JSONData(minInterval));
            o.Add("depth",    new JSONData(depthThreshold));
            o.Add("minSpeed", new JSONData(minSpeed));
            o.Add("maxSpeed", new JSONData(maxSpeed));
            o.Add("enabled",  new JSONData(enabled));
            return o;
        }

        public static SoundRule FromJSON(JSONClass o)
        {
            if (o == null) return null;

            var r = new SoundRule();
            J.OptInt(o,   "trigger",  ref r.trigger);

            int p = BP.None.Int;
            J.OptInt(o,   "part",     ref p);
            r.part = BodyPartType.CreateInternal(p);

            J.OptInt(o,   "orifice",  ref r.orifice);
            r.set = J.OptString(o, "set", "");
            J.OptFloat(o, "volume",   ref r.volume);
            J.OptFloat(o, "pitch",    ref r.pitch);
            J.OptFloat(o, "jitter",   ref r.pitchJitter);
            J.OptFloat(o, "intVol",   ref r.intensityToVolume);
            J.OptFloat(o, "velPitch", ref r.velToPitch);
            J.OptFloat(o, "interval", ref r.minInterval);
            J.OptFloat(o, "depth",    ref r.depthThreshold);
            J.OptFloat(o, "minSpeed", ref r.minSpeed);
            J.OptFloat(o, "maxSpeed", ref r.maxSpeed);
            bool e = true;
            J.OptBool(o,  "enabled",  ref e);
            r.enabled = e;
            return r;
        }

        public override string ToString()
        {
            string s = TriggerNames[U.Clamp(trigger, 0, TriggerCount - 1)];

            if (trigger == TriggerImpact && part != BP.None)
                s += " (" + BodyPartType.ToString(part) + ")";
            else if (trigger != TriggerImpact && trigger != TriggerOrgasm && orifice != OrificeAny)
                s += " (" + OrificeNames[orifice] + ")";

            s += " -> " + (string.IsNullOrEmpty(set) ? "(no set)" : set);

            if (!enabled)
                s += " [off]";

            return s;
        }
    }


    // Per-person event detector. Watches body-part proximity for impacts
    // (slaps, pokes), penetration stats for entry/exit/deep-thrust/tongue
    // events, and hand-to-genital motion for fingering. All hot-path work is
    // simple vector arithmetic on pre-resolved body parts: no allocation, no
    // storable lookups per frame.
    public class SoundEventsEngine
    {
        private const float RebuildInterval   = 2.0f;
        private const float ImpactRadius      = 0.13f;
        private const float ImpactMinSpeed    = 0.15f;   // global gate; per-rule minSpeed filters further
        private const float ImpactCooldown    = 0.18f;
        private const float FingerEnterDist   = 0.055f;
        private const float FingerExitDist    = 0.095f;
        private const float ThrustMoveSpeed   = 0.03f;   // m/s; min motion to count as sliding

        // body parts that can be impact targets
        private static readonly BodyPartType[] TargetParts = new BodyPartType[]
        {
            BP.Head, BP.Chest, BP.Belly, BP.Hips,
            BP.LeftBreast, BP.RightBreast,
            BP.LeftGlute, BP.RightGlute,
            BP.LeftThigh, BP.RightThigh
        };

        private readonly Person person_;
        private readonly Logger log_;
        private readonly List<SoundRule> rules_ = new List<SoundRule>();

        private float rebuildElapsed_ = RebuildInterval;

        // impact targets (on this person)
        private BodyPart[] targets_ = new BodyPart[0];
        private BodyPartType[] targetTypes_ = new BodyPartType[0];

        // probes (things that can hit this person)
        private BodyPart[] probes_ = new BodyPart[0];
        private bool penProbe_ = false;          // last slot is the penetrator atom
        private Sys.IAtom penAtom_ = null;

        // pair state, [target * probeCount + probe]
        private float[] pairDist_ = new float[0];
        private float[] pairCooldown_ = new float[0];

        // fingering probes: hands of self + others
        private BodyPart[] hands_ = new BodyPart[0];
        private bool[] handInside_ = new bool[0];
        private float[] handDist_ = new float[0];
        private UnityEngine.Vector3[] handLastPos_ = new UnityEngine.Vector3[0];
        private BodyPart vagina_ = null;
        private BodyPart lips_ = null;

        private bool lastPenActive_ = false;
        private float lastPenVel_ = 0f;
        private float lastPenDepth_ = 0f;
        private float entrySpeed_ = 0f;

        private int impactsFired_ = 0;
        private int eventsFired_ = 0;

        private readonly SoundGraphEngine graph_;

        public SoundEventsEngine(Person p)
        {
            person_ = p;
            log_ = new Logger(Logger.Object, p, "sounds");
            CreateDefaultRules();
            graph_ = new SoundGraphEngine(p);
        }

        public List<SoundRule> Rules { get { return rules_; } }
        public SoundGraphEngine Graph { get { return graph_; } }

        // The node-graph engine is now the only event->sound path; the legacy
        // per-rule firing below is dead (kept only so this stays a small, safe
        // change -- it can be fully deleted later).
        private static bool GraphMode { get { return true; } }

        private void CreateDefaultRules()
        {
            rules_.Clear();

            var slapL = new SoundRule();
            slapL.trigger = SoundRule.TriggerImpact;
            slapL.part = BP.LeftGlute;
            rules_.Add(slapL);

            var slapR = new SoundRule();
            slapR.trigger = SoundRule.TriggerImpact;
            slapR.part = BP.RightGlute;
            rules_.Add(slapR);

            var entry = new SoundRule();
            entry.trigger = SoundRule.TriggerPenEntry;
            rules_.Add(entry);

            var exit = new SoundRule();
            exit.trigger = SoundRule.TriggerPenExit;
            exit.intensityToVolume = 0.4f;
            rules_.Add(exit);

            var deep = new SoundRule();
            deep.trigger = SoundRule.TriggerDeepThrust;
            deep.minInterval = 0.25f;
            rules_.Add(deep);

            var thrustIn = new SoundRule();
            thrustIn.trigger = SoundRule.TriggerThrustIn;
            thrustIn.minInterval = 0.10f;   // dense stream of wet sounds
            thrustIn.velToPitch = 0.3f;
            rules_.Add(thrustIn);

            var thrustOut = new SoundRule();
            thrustOut.trigger = SoundRule.TriggerThrustOut;
            thrustOut.minInterval = 0.10f;
            thrustOut.velToPitch = 0.3f;
            rules_.Add(thrustOut);

            var fingerIn = new SoundRule();
            fingerIn.trigger = SoundRule.TriggerFingerEntry;
            fingerIn.minSpeed = 0.05f;   // fingers move much slower than slaps
            fingerIn.maxSpeed = 1.2f;
            rules_.Add(fingerIn);

            var fingerOut = new SoundRule();
            fingerOut.trigger = SoundRule.TriggerFingerExit;
            fingerOut.intensityToVolume = 0.3f;
            rules_.Add(fingerOut);
        }

        public void Update(float s)
        {
            rebuildElapsed_ += s;
            if (rebuildElapsed_ >= RebuildInterval)
            {
                rebuildElapsed_ = 0f;
                Rebuild();
            }

            UpdateImpacts(s);
            UpdatePenetration(s);
            UpdateFingering(s);

            // The detectors above feed events to whichever engine is active; the
            // graph engine also needs a per-frame tick to advance running
            // instances and refresh its signal table.
            if (GraphMode)
                graph_.Update(s);
        }

        // Drives the scripts' `on fixed { }` hook at the physics timestep.
        public void FixedUpdate(float s)
        {
            if (GraphMode)
                graph_.FixedUpdate(s);
        }

        // Resolves body-part references and pair-state arrays. Runs every
        // couple of seconds, never per frame.
        private void Rebuild()
        {
            var body = person_.Body;

            vagina_ = body.Get(BP.Vagina);
            lips_ = body.Get(BP.Lips);

            // Impact targets come from the GRAPH PATCHES now (the rule list is
            // dead). Track exactly the parts that impact-trigger patches ask for
            // -- including the vagina/anus etc. which aren't in the generic
            // TargetParts fallback -- so "impact on <part>" actually fires there.
            bool anyPart = false;
            var wanted = new List<BodyPartType>();

            var patches = graph_.Patches;
            for (int i = 0; i < patches.Count; ++i)
            {
                var p = patches[i];
                if (!p.enabled || p.trigger != SoundRule.TriggerImpact)
                    continue;

                if (p.part == BP.None)
                    anyPart = true;
                else if (!wanted.Contains(p.part))
                    wanted.Add(p.part);
            }

            if (anyPart)
            {
                wanted.Clear();
                for (int i = 0; i < TargetParts.Length; ++i)
                    wanted.Add(TargetParts[i]);
            }

            var ts = new List<BodyPart>();
            var tt = new List<BodyPartType>();
            for (int i = 0; i < wanted.Count; ++i)
            {
                var bp = body.Get(wanted[i]);
                if (bp != null)
                {
                    ts.Add(bp);
                    tt.Add(wanted[i]);
                }
            }
            targets_ = ts.ToArray();
            targetTypes_ = tt.ToArray();

            // probes: other persons' hands and feet, plus the penetrator atom
            var ps = new List<BodyPart>();
            var hs = new List<BodyPart>();

            var self = person_;
            var all = Cue.Instance.ActivePersons;

            for (int i = 0; i < all.Length; ++i)
            {
                var p = all[i];

                var lh = p.Body.Get(BP.LeftHand);
                var rh = p.Body.Get(BP.RightHand);

                if (lh != null) hs.Add(lh);
                if (rh != null) hs.Add(rh);

                if (p == self)
                    continue;

                if (lh != null) ps.Add(lh);
                if (rh != null) ps.Add(rh);

                var lf = p.Body.Get(BP.LeftFoot);
                var rf = p.Body.Get(BP.RightFoot);
                if (lf != null) ps.Add(lf);
                if (rf != null) ps.Add(rf);
            }

            probes_ = ps.ToArray();
            penAtom_ = null;
            penProbe_ = false;

            int probeCount = probes_.Length + 1;  // +1 reserved for penetrator
            int pairCount = targets_.Length * probeCount;

            if (pairDist_.Length != pairCount)
            {
                pairDist_ = new float[pairCount];
                pairCooldown_ = new float[pairCount];
                for (int i = 0; i < pairCount; ++i)
                    pairDist_[i] = 10f;
            }

            // fingering probe arrays
            if (hands_.Length != hs.Count)
            {
                handInside_ = new bool[hs.Count];
                handDist_ = new float[hs.Count];
                handLastPos_ = new UnityEngine.Vector3[hs.Count];
                for (int i = 0; i < hs.Count; ++i)
                {
                    handDist_[i] = 10f;
                    handLastPos_[i] = Sys.Vam.U.ToUnity(hs[i].Position);
                }
            }
            hands_ = hs.ToArray();

            // penetrator atom for body-poke impacts (dildo hitting the body)
            var atoms = Cue.Instance.Sys.GetAtoms();
            for (int i = 0; i < atoms.Count; ++i)
            {
                var a = atoms[i] as Sys.Vam.VamAtom;
                if (a == null || a.Atom == null) continue;
                if (a.Atom.type != "CustomUnityAsset") continue;
                penAtom_ = a;
                penProbe_ = true;
                break;
            }
        }

        private void UpdateImpacts(float s)
        {
            if (targets_.Length == 0)
                return;

            int probeCount = probes_.Length + 1;

            for (int t = 0; t < targets_.Length; ++t)
            {
                var tp = Sys.Vam.U.ToUnity(targets_[t].Position);

                for (int p = 0; p < probeCount; ++p)
                {
                    UnityEngine.Vector3 pp;

                    if (p < probes_.Length)
                    {
                        pp = Sys.Vam.U.ToUnity(probes_[p].Position);
                    }
                    else if (penProbe_ && penAtom_ != null)
                    {
                        pp = Sys.Vam.U.ToUnity(penAtom_.Position);
                    }
                    else
                    {
                        continue;
                    }

                    int idx = t * probeCount + p;

                    float dist = UnityEngine.Vector3.Distance(tp, pp);
                    float last = pairDist_[idx];
                    pairDist_[idx] = dist;

                    if (pairCooldown_[idx] > 0f)
                    {
                        pairCooldown_[idx] -= s;
                        continue;
                    }

                    if (s <= 0f)
                        continue;

                    float closing = (last - dist) / s;   // m/s, positive = approaching

                    // Fire on a clean crossing into the contact shell, or when a
                    // fast move tunnelled straight to deep inside it in one frame
                    // (anti-tunnel). Per-rule minSpeed decides what's loud enough.
                    bool crossedIn = (dist < ImpactRadius && last >= ImpactRadius);
                    bool tunnelled = (dist < ImpactRadius * 0.6f && closing > 0f &&
                                      last >= ImpactRadius * 0.6f);

                    if ((crossedIn || tunnelled) && closing >= ImpactMinSpeed)
                    {
                        var mid = (tp + pp) * 0.5f;
                        FireImpact(targetTypes_[t], mid, closing);  // pass raw m/s
                        pairCooldown_[idx] = ImpactCooldown;
                    }
                }
            }
        }

        private void UpdatePenetration(float s)
        {
            var arousal = person_.ArousalSystem;
            if (arousal == null) return;

            var pen = arousal.PenStats;
            if (pen == null) return;

            string orifice = pen.OrificeName;
            float now = Cue.Instance.Sys.RealtimeSinceStartup;

            UnityEngine.Vector3 pos = OrificePosition(orifice);

            if (pen.Active && !lastPenActive_)
            {
                entrySpeed_ = Mathf.Clamp01(pen.NormalisedSpeed * 1.5f + 0.15f);
                Fire(SoundRule.TriggerPenEntry, orifice, pos, entrySpeed_, now);
            }
            else if (!pen.Active && lastPenActive_)
            {
                Fire(SoundRule.TriggerPenExit, orifice, pos, 0.45f, now);
            }

            if (pen.Active)
            {
                // deep-thrust: depth crosses the rule threshold while moving in
                for (int i = 0; i < rules_.Count; ++i)
                {
                    var r = rules_[i];
                    if (!r.enabled) continue;

                    if (r.trigger == SoundRule.TriggerDeepThrust)
                    {
                        if (pen.NormalisedDepth >= r.depthThreshold &&
                            lastPenDepth_ < r.depthThreshold &&
                            pen.SmoothedVelocity > 0.1f &&
                            r.MatchesOrifice(orifice))
                        {
                            float intensity = Mathf.Clamp01(
                                pen.NormalisedSpeed * 0.7f + pen.NormalisedDepth * 0.4f);
                            FireRule(r, pos, intensity, now);
                        }
                    }
                    else if (r.trigger == SoundRule.TriggerTongue)
                    {
                        // tongue/throat: deep contact inside the mouth
                        if (orifice == "Mouth" &&
                            pen.NormalisedDepth >= r.depthThreshold &&
                            lastPenDepth_ < r.depthThreshold)
                        {
                            float intensity = Mathf.Clamp01(0.3f + pen.NormalisedSpeed);
                            var mouthPos = (lips_ != null)
                                ? Sys.Vam.U.ToUnity(lips_.Position) : pos;
                            FireRule(r, mouthPos, intensity, now);
                        }
                    }
                }

                // Continuous wet sliding: while the penetrator is moving, fire
                // the matching directional rule. SmoothedVelocity > 0 is inward,
                // < 0 is outward, so IN and OUT can drive dedicated sounds. The
                // per-rule minInterval throttles it into a steady stream of
                // randomised wet one-shots (set a small interval for a "loop"),
                // and the speed becomes the intensity so volume/pitch track the
                // thrust velocity.
                float v   = pen.SmoothedVelocity;
                float spd = Mathf.Clamp01(pen.NormalisedSpeed);

                if (v > ThrustMoveSpeed)
                    Fire(SoundRule.TriggerThrustIn, orifice, pos, spd, now);
                else if (v < -ThrustMoveSpeed)
                    Fire(SoundRule.TriggerThrustOut, orifice, pos, spd, now);

                // Also raise an Impact event ON THE ORIFICE'S BODY PART at the
                // start of each inward stroke. The impact proximity detector
                // can't see this (it measures the dildo's root, which never
                // reaches the orifice), so penetration drives it directly --
                // making "Impact on body part: Vagina" fire while penetrating.
                bool inwardStart = (v > ThrustMoveSpeed && lastPenVel_ <= ThrustMoveSpeed);
                if (inwardStart)
                {
                    var opart = OrificePart(orifice);
                    if (opart != BP.None)
                        FireImpact(opart, pos, Mathf.Abs(v) * 3f);
                }
                lastPenVel_ = v;

                lastPenDepth_ = pen.NormalisedDepth;
            }
            else
            {
                lastPenDepth_ = 0f;
            }

            lastPenActive_ = pen.Active;
        }

        private BodyPartType OrificePart(string orifice)
        {
            if (orifice == "Vagina") return BP.Vagina;
            if (orifice == "Anus")   return BP.Anus;
            if (orifice == "Mouth")  return BP.Lips;
            return BP.None;
        }

        private void UpdateFingering(float s)
        {
            if (vagina_ == null || hands_.Length == 0 || s <= 0f)
                return;

            var vp = Sys.Vam.U.ToUnity(vagina_.Position);
            float now = Cue.Instance.Sys.RealtimeSinceStartup;

            for (int i = 0; i < hands_.Length && i < handInside_.Length; ++i)
            {
                var hp = Sys.Vam.U.ToUnity(hands_[i].Position);
                var move = hp - handLastPos_[i];
                handLastPos_[i] = hp;

                float dist = UnityEngine.Vector3.Distance(vp, hp);
                handDist_[i] = dist;

                float moveLen = move.magnitude;
                float speed = moveLen / s;

                // "Actually going in": the hand must be close AND moving toward
                // the orifice along its approach (not just sliding past nearby).
                // This is a far better proxy than raw distance and is robust to
                // the orifice control's rotation convention.
                bool movingIn = false;
                if (moveLen > 1e-4f)
                {
                    var toOrifice = vp - hp;
                    if (toOrifice.sqrMagnitude > 1e-6f)
                        movingIn = UnityEngine.Vector3.Dot(
                            move / moveLen, toOrifice.normalized) > 0.5f;
                }

                if (!handInside_[i] && dist < FingerEnterDist && movingIn)
                {
                    handInside_[i] = true;
                    // entry: hand speed -> per-rule intensity bands
                    FireMapped(SoundRule.TriggerFingerEntry, "Vagina", vp, speed, now);
                }
                else if (handInside_[i] && dist > FingerExitDist)
                {
                    handInside_[i] = false;
                    // exit: single fixed intensity
                    Fire(SoundRule.TriggerFingerExit, "Vagina", vp, 0.4f, now);
                }
            }
        }

        // Like Fire(), but maps a raw approach speed (m/s) to each matching
        // rule's own intensity window (used for fingering entry, where the user
        // wants graded intensities driven by how fast the finger goes in).
        private void FireMapped(
            int trigger, string orifice, UnityEngine.Vector3 pos,
            float rawSpeed, float now)
        {
            if (GraphMode)
            {
                graph_.OnEvent(trigger, new SignalArgs
                {
                    pos = pos, intensity = Mathf.Clamp01(rawSpeed / 1.2f),
                    part = BP.None, orifice = orifice
                });
                return;
            }

            for (int i = 0; i < rules_.Count; ++i)
            {
                var r = rules_[i];
                if (!r.enabled || r.trigger != trigger)
                    continue;
                if (orifice != null && !r.MatchesOrifice(orifice))
                    continue;
                if (rawSpeed < r.minSpeed)
                    continue;

                FireRule(r, pos, r.SpeedToIntensity(rawSpeed), now);
            }
        }

        // Called by Mood when an orgasm begins.
        public void OnOrgasm(float intensity)
        {
            var hips = person_.Body.Get(BP.Hips);
            var pos = (hips != null)
                ? Sys.Vam.U.ToUnity(hips.Position)
                : Sys.Vam.U.ToUnity(person_.Position);

            Fire(SoundRule.TriggerOrgasm, null, pos,
                Mathf.Clamp01(intensity),
                Cue.Instance.Sys.RealtimeSinceStartup);
        }

        private void FireImpact(BodyPartType part, UnityEngine.Vector3 pos, float rawSpeed)
        {
            if (GraphMode)
            {
                graph_.OnEvent(SoundRule.TriggerImpact, new SignalArgs
                {
                    pos = pos, intensity = Mathf.Clamp01(rawSpeed / 3f),
                    part = part, orifice = null
                });
                return;
            }

            float now = Cue.Instance.Sys.RealtimeSinceStartup;

            for (int i = 0; i < rules_.Count; ++i)
            {
                var r = rules_[i];
                if (!r.enabled || r.trigger != SoundRule.TriggerImpact)
                    continue;
                if (r.part != BP.None && r.part != part)
                    continue;
                if (rawSpeed < r.minSpeed)
                    continue;

                float intensity = r.SpeedToIntensity(rawSpeed);
                if (FireRule(r, pos, intensity, now))
                    ++impactsFired_;
            }
        }

        private void Fire(
            int trigger, string orifice, UnityEngine.Vector3 pos,
            float intensity, float now)
        {
            if (GraphMode)
            {
                graph_.OnEvent(trigger, new SignalArgs
                {
                    pos = pos, intensity = intensity, part = BP.None, orifice = orifice
                });
                return;
            }

            for (int i = 0; i < rules_.Count; ++i)
            {
                var r = rules_[i];
                if (!r.enabled || r.trigger != trigger)
                    continue;
                if (orifice != null && !r.MatchesOrifice(orifice))
                    continue;

                FireRule(r, pos, intensity, now);
            }
        }

        private bool FireRule(SoundRule r, UnityEngine.Vector3 pos, float intensity, float now)
        {
            if (now - r.lastFire < r.minInterval)
                return false;
            if (string.IsNullOrEmpty(r.set))
                return false;

            r.lastFire = now;

            bool ok = SoundManager.Instance.Play(
                r.set, pos, intensity,
                r.volume, r.pitch, r.pitchJitter, r.intensityToVolume, r.velToPitch);

            if (ok)
                ++eventsFired_;

            return ok;
        }

        private UnityEngine.Vector3 OrificePosition(string orifice)
        {
            BodyPart bp = null;

            if (orifice == "Mouth")
                bp = lips_;
            else if (orifice == "Anus")
                bp = person_.Body.Get(BP.Anus);
            else
                bp = vagina_;

            if (bp != null)
                return Sys.Vam.U.ToUnity(bp.Position);

            return Sys.Vam.U.ToUnity(person_.Position);
        }

        public void RulesChanged()
        {
            // forces target/probe rebuild next frame
            rebuildElapsed_ = RebuildInterval;
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            var a = new JSONArray();
            for (int i = 0; i < rules_.Count; ++i)
                a.Add(rules_[i].ToJSON());
            o.Add("rules", a);

            if (graph_ != null)
                o.Add("graph", graph_.ToJSON());

            return o;
        }

        public void Load(JSONClass o)
        {
            if (o == null)
                return;

            if (o.HasKey("rules"))
            {
                rules_.Clear();

                var a = o["rules"].AsArray;
                if (a != null)
                {
                    foreach (JSONNode n in a)
                    {
                        var r = SoundRule.FromJSON(n.AsObject);
                        if (r != null)
                            rules_.Add(r);
                    }
                }

                RulesChanged();
            }

            if (o.HasKey("graph") && graph_ != null)
                graph_.Load(o["graph"].AsObject);
        }

        public void Debug(DebugLines debug)
        {
            debug.Add("targets",       targets_.Length.ToString());
            debug.Add("probes",        (probes_.Length + (penProbe_ ? 1 : 0)).ToString());
            debug.Add("hands",         hands_.Length.ToString());
            debug.Add("rules",         rules_.Count.ToString());
            debug.Add("impactsFired",  impactsFired_.ToString());
            debug.Add("eventsFired",   eventsFired_.ToString());
        }
    }
}

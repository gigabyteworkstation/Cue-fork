using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Cue.Sound
{
    // Event payload handed to the graph when a detector fires.
    public struct SignalArgs
    {
        public UnityEngine.Vector3 pos;
        public float intensity;
        public BodyPartType part;   // impact filter
        public string orifice;      // "Vagina"/"Anus"/"Mouth" or null
    }


    // A graph entry point: "when <trigger> happens (optionally filtered by body
    // part / orifice), instantiate this node tree." Triggers reuse the detector
    // taxonomy (SoundRule.Trigger*) plus TriggerAlways for ambiences/loops that
    // start on load and run forever.
    public class SoundPatch
    {
        public const int TriggerAlways = 100;

        public string name = "patch";
        public int trigger = SoundRule.TriggerImpact;
        public BodyPartType part = BP.None;
        public int orifice = SoundRule.OrificeAny;
        public float minInterval = 0.0f;
        public bool enabled = true;
        public SoundNode root = null;

        public float lastFire = -1000f;   // runtime

        public bool MatchesOrifice(string name_)
        {
            if (orifice == SoundRule.OrificeAny) return true;
            if (string.IsNullOrEmpty(name_)) return false;
            switch (orifice)
            {
                case SoundRule.OrificeVagina: return name_ == "Vagina";
                case SoundRule.OrificeAnus:   return name_ == "Anus";
                case SoundRule.OrificeMouth:  return name_ == "Mouth";
            }
            return true;
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("name", name);
            o.Add("trigger", new JSONData(trigger));
            o.Add("part", new JSONData(part.Int));
            o.Add("orifice", new JSONData(orifice));
            o.Add("interval", new JSONData(minInterval));
            o.Add("enabled", new JSONData(enabled));
            if (root != null)
                o.Add("root", root.ToJSON());
            return o;
        }

        public static SoundPatch FromJSON(JSONClass o)
        {
            if (o == null) return null;
            var p = new SoundPatch();
            p.name = J.OptString(o, "name", "patch");
            J.OptInt(o, "trigger", ref p.trigger);
            int part = BP.None.Int; J.OptInt(o, "part", ref part);
            p.part = BodyPartType.CreateInternal(part);
            J.OptInt(o, "orifice", ref p.orifice);
            J.OptFloat(o, "interval", ref p.minInterval);
            bool e = true; J.OptBool(o, "enabled", ref e); p.enabled = e;
            if (o.HasKey("root"))
                p.root = SoundNode.FromJSON(o["root"]);
            return p;
        }
    }


    // Per-person graph runtime. Holds the live signal table (auto-filled from
    // the person's own state each frame, so most variables need no detector
    // plumbing), the patch list, and the set of currently-running instances.
    public class SoundGraphEngine
    {
        private const int MaxRunning = 48;

        private readonly Person person_;
        private readonly float[] vars_ = new float[GVar.TableCount];
        private readonly List<SoundPatch> patches_ = new List<SoundPatch>();
        private readonly System.Random rng_;
        private readonly PhysicsProbeManager probes_;

        private class Running
        {
            public SoundContext ctx;
            public NodeInstance root;
            public SoundPatch patch;
        }
        private readonly List<Running> running_ = new List<Running>();

        private bool startedAlways_ = false;
        private float elapsed_ = 0f;

        public SoundGraphEngine(Person p)
        {
            person_ = p;
            rng_ = new System.Random(System.Guid.NewGuid().GetHashCode());
            probes_ = new PhysicsProbeManager(p);
            CreateDefaultPatches();
        }

        public List<SoundPatch> Patches { get { return patches_; } }
        public float[] Vars { get { return vars_; } }
        public int RunningCount { get { return running_.Count; } }
        public PhysicsProbeManager Probes { get { return probes_; } }

        // ---- signals & events --------------------------------------------

        public void SetVar(int id, float v)
        {
            if (id >= 0 && id < GVar.TableCount)
                vars_[id] = v;
        }

        public void OnEvent(int trigger, SignalArgs args)
        {
            float now = elapsed_;

            for (int i = 0; i < patches_.Count; ++i)
            {
                var p = patches_[i];
                if (!p.enabled || p.trigger != trigger || p.root == null)
                    continue;

                if (p.trigger == SoundRule.TriggerImpact &&
                    p.part != BP.None && p.part != args.part)
                    continue;

                if (!p.MatchesOrifice(args.orifice))
                    continue;

                if (now - p.lastFire < p.minInterval)
                    continue;

                p.lastFire = now;
                Spawn(p, args.pos, args.intensity);
            }
        }

        private void Spawn(SoundPatch p, UnityEngine.Vector3 pos, float intensity)
        {
            if (running_.Count >= MaxRunning)
            {
                // drop the oldest to make room
                running_[0].root.Stop(true);
                running_.RemoveAt(0);
            }

            var ctx = new SoundContext
            {
                Vars = vars_,
                Custom = probes_.Outputs,
                Intensity = Mathf.Clamp01(intensity),
                Position = pos,
                Now = elapsed_,
                Person = person_,
                Rng = rng_
            };

            var inst = p.root.Create(ctx);
            if (inst == null) return;

            running_.Add(new Running { ctx = ctx, root = inst, patch = p });
        }

        // ---- update -------------------------------------------------------

        public void Update(float s)
        {
            elapsed_ += s;

            probes_.Update(s);
            RefreshSignals();

            if (!startedAlways_)
            {
                startedAlways_ = true;
                for (int i = 0; i < patches_.Count; ++i)
                {
                    if (patches_[i].enabled && patches_[i].trigger == SoundPatch.TriggerAlways)
                        Spawn(patches_[i], Sys.Vam.U.ToUnity(person_.Position), 1f);
                }
            }

            for (int i = running_.Count - 1; i >= 0; --i)
            {
                var r = running_[i];
                r.ctx.Now = elapsed_;
                r.root.Update(s);
                if (r.root.Finished)
                    running_.RemoveAt(i);
            }
        }

        // Auto-populates the continuous signal variables from the person's own
        // state, so graphs work without any per-variable detector wiring.
        private void RefreshSignals()
        {
            vars_[GVar.Time] = elapsed_;
            vars_[GVar.Arousal] = person_.Mood.Get(MoodType.Excited);

            var brain = person_.ArousalSystem != null ? person_.ArousalSystem.brain_ : null;
            if (brain != null)
            {
                vars_[GVar.Urge] = brain.UrgeIntensity;
                vars_[GVar.Boredom] = brain.Boredom;
                vars_[GVar.PenGirth] = brain.CurrentGirth;
            }

            // clear per-orifice each frame; the active one is filled below
            vars_[GVar.OralActive] = 0f; vars_[GVar.OralDepth] = 0f; vars_[GVar.OralVelocity] = 0f;
            vars_[GVar.AnalActive] = 0f; vars_[GVar.AnalDepth] = 0f; vars_[GVar.AnalVelocity] = 0f;
            vars_[GVar.VagActive]  = 0f; vars_[GVar.VagDepth]  = 0f; vars_[GVar.VagVelocity]  = 0f;

            var pen = (person_.ArousalSystem != null) ? person_.ArousalSystem.PenStats : null;
            if (pen != null && pen.Active)
            {
                float depth = Mathf.Clamp01(pen.NormalisedDepth);
                float speed = Mathf.Clamp01(pen.NormalisedSpeed);
                float v = pen.SmoothedVelocity;

                vars_[GVar.PenActive] = 1f;
                vars_[GVar.PenVelocity] = speed;
                vars_[GVar.PenDepth] = depth;
                vars_[GVar.PenDir] = (v > 0.03f) ? 1f : (v < -0.03f) ? -1f : 0f;

                // route into the per-orifice signals (DildoLanguage reports the
                // orifice name; this is the reliable per-orifice source)
                switch (pen.OrificeName)
                {
                    case "Mouth":
                        vars_[GVar.OralActive] = 1f; vars_[GVar.OralDepth] = depth; vars_[GVar.OralVelocity] = speed;
                        break;
                    case "Anus":
                        vars_[GVar.AnalActive] = 1f; vars_[GVar.AnalDepth] = depth; vars_[GVar.AnalVelocity] = speed;
                        break;
                    case "Vagina":
                        vars_[GVar.VagActive] = 1f; vars_[GVar.VagDepth] = depth; vars_[GVar.VagVelocity] = speed;
                        break;
                }
            }
            else
            {
                vars_[GVar.PenActive] = 0f;
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < running_.Count; ++i)
                running_[i].root.Stop(true);
            running_.Clear();
            startedAlways_ = false;
        }

        // ---- defaults & serialization ------------------------------------

        private void CreateDefaultPatches()
        {
            patches_.Clear();

            patches_.Add(ImpactPatch("slap L", BP.LeftGlute, "slaps"));
            patches_.Add(ImpactPatch("slap R", BP.RightGlute, "slaps"));
            patches_.Add(EventPatch("entry", SoundRule.TriggerPenEntry, "wet", 1f));
            patches_.Add(EventPatch("exit",  SoundRule.TriggerPenExit,  "wet", 0.6f));
            patches_.Add(ThrustPatch("slide in",  SoundRule.TriggerThrustIn,  "wet_in"));
            patches_.Add(ThrustPatch("slide out", SoundRule.TriggerThrustOut, "wet_out"));
            patches_.Add(EventPatch("finger in", SoundRule.TriggerFingerEntry, "fingering", 1f));
            patches_.Add(EventPatch("orgasm", SoundRule.TriggerOrgasm, "orgasm", 1f));
        }

        private static SoundPatch ImpactPatch(string name, BodyPartType part, string set)
        {
            var p = new SoundPatch { name = name, trigger = SoundRule.TriggerImpact, part = part };
            p.root = new ClipNode { set = set, intensity = GraphValue.Var(GVar.EventIntensity) };
            return p;
        }

        private static SoundPatch EventPatch(string name, int trigger, string set, float gain)
        {
            var p = new SoundPatch { name = name, trigger = trigger };
            p.root = new ClipNode
            {
                set = set,
                intensity = GraphValue.Var(GVar.EventIntensity),
                gain = GraphValue.Const(gain)
            };
            return p;
        }

        private static SoundPatch ThrustPatch(string name, int trigger, string set)
        {
            var p = new SoundPatch { name = name, trigger = trigger, minInterval = 0.10f };
            // pitch rises with stroke velocity (variable -> curve), a small taste
            // of what the graph can do that the old rules could not express.
            var pitch = GraphValue.Var(GVar.PenVelocity,
                new Curve { type = CurveType.Linear, inMin = 0f, inMax = 1f, outMin = 0.95f, outMax = 1.25f });
            p.root = new ClipNode
            {
                set = set,
                intensity = GraphValue.Var(GVar.PenVelocity),
                pitch = pitch
            };
            return p;
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            var a = new JSONArray();
            for (int i = 0; i < patches_.Count; ++i)
                a.Add(patches_[i].ToJSON());
            o.Add("patches", a);
            o.Add("probes", probes_.ToJSON());
            return o;
        }

        public void Load(JSONClass o)
        {
            if (o == null || !o.HasKey("patches"))
                return;

            StopAll();
            patches_.Clear();

            var a = o["patches"].AsArray;
            if (a != null)
            {
                foreach (JSONNode n in a)
                {
                    var p = SoundPatch.FromJSON(n.AsObject);
                    if (p != null)
                        patches_.Add(p);
                }
            }

            if (o.HasKey("probes"))
                probes_.Load(o["probes"].AsObject);
        }
    }
}

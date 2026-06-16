using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Cue.Sound
{
    // Per-frame context handed to every probe. The penetrator position is
    // resolved once per frame by the manager so N probes don't each rescan.
    public struct ProbeEvalContext
    {
        public Person person;
        public UnityEngine.Vector3 penetratorPos;
        public bool hasPenetrator;
        public float dt;
    }


    // A point in space a probe can reference: one of this person's body parts,
    // or the active penetrator (dildo / CUA).
    public class ProbePoint
    {
        public const int BodyPart   = 0;
        public const int Penetrator = 1;

        public int kind = BodyPart;
        public BodyPartType part = BP.Hips;

        public UnityEngine.Vector3 Resolve(ProbeEvalContext c, out bool ok)
        {
            if (kind == Penetrator)
            {
                ok = c.hasPenetrator;
                return c.penetratorPos;
            }

            var bp = c.person.Body.Get(part);
            if (bp != null) { ok = true; return Sys.Vam.U.ToUnity(bp.Position); }

            ok = false;
            return UnityEngine.Vector3.zero;
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("kind", new JSONData(kind));
            o.Add("part", new JSONData(part.Int));
            return o;
        }

        public static ProbePoint FromJSON(JSONNode n)
        {
            var o = n as JSONClass;
            if (o == null) return new ProbePoint();
            var p = new ProbePoint();
            J.OptInt(o, "kind", ref p.kind);
            int pt = BP.Hips.Int; J.OptInt(o, "part", ref pt);
            p.part = BodyPartType.CreateInternal(pt);
            return p;
        }

        public static ProbePoint Part(BodyPartType bp) { return new ProbePoint { kind = BodyPart, part = bp }; }
        public static ProbePoint Pen()                 { return new ProbePoint { kind = Penetrator }; }
    }


    // ---- Probe base -------------------------------------------------------
    // A physical query that writes one value into a named custom variable each
    // frame. Probes must bound their own per-frame work (cheap ones do it all;
    // the future mesh probe will stride internally), so the manager can simply
    // evaluate every enabled probe per frame without freezing.
    public abstract class PhysicsProbe
    {
        public string name = "probe";   // also the custom-variable name it writes
        public bool enabled = true;

        public abstract string Type { get; }
        public abstract float Evaluate(ProbeEvalContext c);

        protected virtual void WriteJSON(JSONClass o) { }
        protected virtual void ReadJSON(JSONClass o) { }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("type", Type);
            o.Add("name", name);
            o.Add("enabled", new JSONData(enabled));
            WriteJSON(o);
            return o;
        }

        public static PhysicsProbe FromJSON(JSONNode n)
        {
            var o = n as JSONClass;
            if (o == null || !o.HasKey("type")) return null;

            PhysicsProbe p = CreateByType(o["type"].Value);
            if (p == null) return null;

            p.name = J.OptString(o, "name", "probe");
            bool e = true; J.OptBool(o, "enabled", ref e); p.enabled = e;
            p.ReadJSON(o);
            return p;
        }

        public override string ToString()
        {
            return name + "  [" + Type + "]" + (enabled ? "" : " (off)");
        }

        public static PhysicsProbe CreateByType(string t)
        {
            switch (t)
            {
                case "distance": return new DistanceProbe();
                case "velocity": return new VelocityProbe();
                case "raycast":  return new RaycastProbe();
                case "overlap":  return new OverlapProbe();
            }
            return null;
        }

        public static readonly string[] AllTypes = new string[]
        {
            "distance", "velocity", "raycast", "overlap"
        };
    }


    // ---- distance between two points (metres) ----------------------------
    public class DistanceProbe : PhysicsProbe
    {
        public ProbePoint a = ProbePoint.Pen();
        public ProbePoint b = ProbePoint.Part(BP.Lips);

        public override string Type { get { return "distance"; } }

        public override float Evaluate(ProbeEvalContext c)
        {
            bool oka, okb;
            var pa = a.Resolve(c, out oka);
            var pb = b.Resolve(c, out okb);
            if (!oka || !okb) return 999f;
            return UnityEngine.Vector3.Distance(pa, pb);
        }

        protected override void WriteJSON(JSONClass o) { o.Add("a", a.ToJSON()); o.Add("b", b.ToJSON()); }
        protected override void ReadJSON(JSONClass o)
        {
            if (o.HasKey("a")) a = ProbePoint.FromJSON(o["a"]);
            if (o.HasKey("b")) b = ProbePoint.FromJSON(o["b"]);
        }
    }


    // ---- speed of a point (m/s) ------------------------------------------
    public class VelocityProbe : PhysicsProbe
    {
        public ProbePoint a = ProbePoint.Part(BP.RightHand);

        private UnityEngine.Vector3 last_;
        private bool has_ = false;

        public override string Type { get { return "velocity"; } }

        public override float Evaluate(ProbeEvalContext c)
        {
            bool ok;
            var p = a.Resolve(c, out ok);
            if (!ok || c.dt <= 0f) { last_ = p; has_ = ok; return 0f; }

            float v = 0f;
            if (has_)
                v = UnityEngine.Vector3.Distance(p, last_) / c.dt;

            last_ = p;
            has_ = true;
            return v;
        }

        protected override void WriteJSON(JSONClass o) { o.Add("a", a.ToJSON()); }
        protected override void ReadJSON(JSONClass o)
        {
            if (o.HasKey("a")) a = ProbePoint.FromJSON(o["a"]);
        }
    }


    // ---- raycast/segment cast: hit distance from->to (metres) ------------
    public class RaycastProbe : PhysicsProbe
    {
        public ProbePoint from = ProbePoint.Pen();
        public ProbePoint to   = ProbePoint.Part(BP.Lips);
        public float maxDist = 0f;   // 0 => use the from->to distance

        public override string Type { get { return "raycast"; } }

        public override float Evaluate(ProbeEvalContext c)
        {
            bool okf, okt;
            var pf = from.Resolve(c, out okf);
            var pt = to.Resolve(c, out okt);
            if (!okf || !okt) return 999f;

            var dir = pt - pf;
            float dist = dir.magnitude;
            if (dist < 1e-4f) return 0f;
            dir /= dist;

            float range = (maxDist > 0f) ? maxDist : dist;

            RaycastHit hit;
            if (Physics.Raycast(pf, dir, out hit, range,
                    Physics.AllLayers, QueryTriggerInteraction.Collide))
                return hit.distance;

            return range;
        }

        protected override void WriteJSON(JSONClass o)
        {
            o.Add("from", from.ToJSON());
            o.Add("to", to.ToJSON());
            o.Add("maxDist", new JSONData(maxDist));
        }
        protected override void ReadJSON(JSONClass o)
        {
            if (o.HasKey("from")) from = ProbePoint.FromJSON(o["from"]);
            if (o.HasKey("to"))   to   = ProbePoint.FromJSON(o["to"]);
            J.OptFloat(o, "maxDist", ref maxDist);
        }
    }


    // ---- overlap: number of colliders within radius of a point -----------
    public class OverlapProbe : PhysicsProbe
    {
        public ProbePoint at = ProbePoint.Part(BP.Vagina);
        public float radius = 0.05f;

        private readonly Collider[] hits_ = new Collider[16];

        public override string Type { get { return "overlap"; } }

        public override float Evaluate(ProbeEvalContext c)
        {
            bool ok;
            var p = at.Resolve(c, out ok);
            if (!ok) return 0f;

            int n = Physics.OverlapSphereNonAlloc(
                p, radius, hits_, Physics.AllLayers, QueryTriggerInteraction.Collide);
            return n;
        }

        protected override void WriteJSON(JSONClass o)
        {
            o.Add("at", at.ToJSON());
            o.Add("radius", new JSONData(radius));
        }
        protected override void ReadJSON(JSONClass o)
        {
            if (o.HasKey("at")) at = ProbePoint.FromJSON(o["at"]);
            J.OptFloat(o, "radius", ref radius);
        }
    }


    // ---- Manager ----------------------------------------------------------
    // Owns the probe list, resolves the shared per-frame state once, evaluates
    // each enabled probe, and publishes results into the outputs dictionary that
    // the graph reads as custom variables.
    public class PhysicsProbeManager
    {
        private readonly Person person_;
        private readonly List<PhysicsProbe> probes_ = new List<PhysicsProbe>();
        private readonly Dictionary<string, float> outputs_ = new Dictionary<string, float>();

        public PhysicsProbeManager(Person p)
        {
            person_ = p;
            CreateDefaults();
        }

        public List<PhysicsProbe> Probes { get { return probes_; } }
        public Dictionary<string, float> Outputs { get { return outputs_; } }

        public void Update(float s)
        {
            var c = new ProbeEvalContext { person = person_, dt = s };
            c.penetratorPos = FindPenetrator(out c.hasPenetrator);

            for (int i = 0; i < probes_.Count; ++i)
            {
                var p = probes_[i];
                if (!p.enabled || string.IsNullOrEmpty(p.name))
                    continue;

                outputs_[p.name] = p.Evaluate(c);
            }
        }

        private UnityEngine.Vector3 FindPenetrator(out bool found)
        {
            var atoms = Cue.Instance.Sys.GetAtoms();
            for (int i = 0; i < atoms.Count; ++i)
            {
                var a = atoms[i] as Sys.Vam.VamAtom;
                if (a == null || a.Atom == null) continue;
                if (a.Atom.type != "CustomUnityAsset") continue;
                found = true;
                return Sys.Vam.U.ToUnity(a.Position);
            }
            found = false;
            return UnityEngine.Vector3.zero;
        }

        private void CreateDefaults()
        {
            // Intentionally empty: probes are created by the user in the UI.
            probes_.Clear();
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            var a = new JSONArray();
            for (int i = 0; i < probes_.Count; ++i)
                a.Add(probes_[i].ToJSON());
            o.Add("probes", a);
            return o;
        }

        public void Load(JSONClass o)
        {
            if (o == null || !o.HasKey("probes")) return;

            probes_.Clear();
            var a = o["probes"].AsArray;
            if (a != null)
            {
                foreach (JSONNode n in a)
                {
                    var p = PhysicsProbe.FromJSON(n.AsObject);
                    if (p != null) probes_.Add(p);
                }
            }
        }
    }
}

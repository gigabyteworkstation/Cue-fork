using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Cue.Sound
{
    // ---- Signal variables -------------------------------------------------
    // The wiring between "what's happening" and "what plays" (RAGE's audio
    // variables). Detectors publish these every frame into the per-person table;
    // graph nodes read them through GraphValue. They are deliberately generic
    // (velocity, depth, arousal, time, random) so graphs can drive ambiences and
    // arbitrary scenarios, not just sexual interactions.
    public static class GVar
    {
        public const int PenActive   = 0;   // 0/1 penetrated
        public const int PenVelocity = 1;   // |stroke speed| 0..1
        public const int PenDepth    = 2;   // 0..1
        public const int PenDir      = 3;   // +1 sliding in, -1 out, 0 still
        public const int PenGirth    = 4;   // 0..1
        public const int Arousal     = 5;   // mood excited 0..1
        public const int Urge        = 6;   // brain urge 0..1
        public const int Boredom     = 7;   // brain boredom 0..1
        public const int Time        = 8;   // seconds since graph start

        // Per-orifice penetration (the "primary" pen.* above mirrors whichever
        // is active). Each is active 0/1, plus its own depth and velocity, so a
        // graph can react differently to oral vs anal vs vaginal.
        public const int OralActive    = 9;
        public const int OralDepth     = 10;
        public const int OralVelocity  = 11;
        public const int AnalActive    = 12;
        public const int AnalDepth     = 13;
        public const int AnalVelocity  = 14;
        public const int VagActive     = 15;
        public const int VagDepth      = 16;
        public const int VagVelocity   = 17;

        public const int TableCount  = 18;  // stored in the per-person table

        // "virtual" ids resolved from per-instance context, not the table
        public const int EventIntensity = 18;  // intensity of the triggering event
        public const int Random         = 19;  // fresh 0..1 each read

        public const int Count = 20;

        public static readonly string[] Names = new string[]
        {
            "pen.active", "pen.velocity", "pen.depth", "pen.dir", "pen.girth",
            "arousal", "urge", "boredom", "time",
            "oral.active", "oral.depth", "oral.velocity",
            "anal.active", "anal.depth", "anal.velocity",
            "vaginal.active", "vaginal.depth", "vaginal.velocity",
            "event.intensity", "random"
        };

        public static int FromName(string n)
        {
            for (int i = 0; i < Names.Length; ++i)
                if (Names[i] == n) return i;
            return -1;
        }
    }


    // Per-person signal table plus the bits of per-instance state a running
    // graph needs. Passed by reference down the node tree.
    public class SoundContext
    {
        public float[] Vars;                  // global signals (size GVar.TableCount)
        public Dictionary<string, float> Custom; // probe outputs, by user-chosen name
        public float Intensity = 1f;          // triggering event intensity
        public UnityEngine.Vector3 Position;  // where to play
        public float Now = 0f;                // realtime seconds
        public Person Person = null;
        public System.Random Rng = null;

        public float GetCustom(string name)
        {
            float v;
            if (Custom != null && name != null && Custom.TryGetValue(name, out v))
                return v;
            return 0f;
        }

        public float GetVar(int id)
        {
            if (id == GVar.EventIntensity)
                return Intensity;

            if (id == GVar.Random)
                return (Rng != null) ? (float)Rng.NextDouble() : UnityEngine.Random.value;

            if (id >= 0 && id < GVar.TableCount && Vars != null)
                return Vars[id];

            return 0f;
        }
    }


    // ---- Curve ------------------------------------------------------------
    // Remaps an input range to an output range with a shaping function (RAGE's
    // audioengine/curve.h: linear / exponential / inverse-exp / s-curve).
    public enum CurveType { Linear = 0, Exp = 1, InvExp = 2, SCurve = 3 }

    public class Curve
    {
        public CurveType type = CurveType.Linear;
        public float inMin = 0f, inMax = 1f, outMin = 0f, outMax = 1f;

        public float Evaluate(float x)
        {
            float t = (inMax == inMin) ? 0f : Mathf.Clamp01((x - inMin) / (inMax - inMin));

            switch (type)
            {
                case CurveType.Exp:    t = t * t;                 break;
                case CurveType.InvExp: t = 1f - (1f - t) * (1f - t); break;
                case CurveType.SCurve: t = t * t * (3f - 2f * t); break;
            }

            return outMin + (outMax - outMin) * t;
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("type",   new JSONData((int)type));
            o.Add("inMin",  new JSONData(inMin));
            o.Add("inMax",  new JSONData(inMax));
            o.Add("outMin", new JSONData(outMin));
            o.Add("outMax", new JSONData(outMax));
            return o;
        }

        public static Curve FromJSON(JSONClass o)
        {
            if (o == null) return null;
            var c = new Curve();
            int t = 0; J.OptInt(o, "type", ref t); c.type = (CurveType)t;
            J.OptFloat(o, "inMin",  ref c.inMin);
            J.OptFloat(o, "inMax",  ref c.inMax);
            J.OptFloat(o, "outMin", ref c.outMin);
            J.OptFloat(o, "outMax", ref c.outMax);
            return c;
        }
    }


    // ---- GraphValue -------------------------------------------------------
    // A parameter that is either a constant or a signal variable, optionally
    // remapped through a curve. This is what makes node parameters "live".
    public class GraphValue
    {
        public float  constant = 1f;
        public int    varId = -1;       // built-in signal id, -1 => use constant
        public string varName = null;   // custom (probe) variable name; wins over varId
        public Curve  curve = null;      // optional remap of the variable

        public GraphValue() { }
        public GraphValue(float c) { constant = c; }

        public float Get(SoundContext ctx)
        {
            float v;

            if (!string.IsNullOrEmpty(varName))
                v = ctx.GetCustom(varName);
            else if (varId >= 0)
                v = ctx.GetVar(varId);
            else
                return constant;

            if (curve != null)
                v = curve.Evaluate(v);
            return v;
        }

        public static GraphValue Const(float c) { return new GraphValue(c); }

        public static GraphValue Var(int id, Curve c = null)
        {
            return new GraphValue { varId = id, curve = c };
        }

        public static GraphValue Custom_(string name, Curve c = null)
        {
            return new GraphValue { varName = name, curve = c };
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("k", new JSONData(constant));
            o.Add("v", new JSONData(varId));
            if (!string.IsNullOrEmpty(varName))
                o.Add("vn", varName);
            if (curve != null)
                o.Add("curve", curve.ToJSON());
            return o;
        }

        public static GraphValue FromJSON(JSONNode n)
        {
            var o = n as JSONClass;
            if (o == null) return new GraphValue();

            var g = new GraphValue();
            J.OptFloat(o, "k", ref g.constant);
            J.OptInt(o, "v", ref g.varId);
            g.varName = J.OptString(o, "vn", null);
            if (o.HasKey("curve"))
                g.curve = Curve.FromJSON(o["curve"].AsObject);
            return g;
        }
    }
}

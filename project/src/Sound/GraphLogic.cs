using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Cue.Sound
{
    // Comparison operators for logic triggers (const ints, never an enum).
    public static class CmpOp
    {
        public const int Greater = 0;
        public const int Less    = 1;
        public const int GEqual  = 2;
        public const int LEqual  = 3;
        public const int Equal   = 4;
        public const int NEqual  = 5;

        public static readonly string[] Names = new string[]
        { "a > b", "a < b", "a >= b", "a <= b", "a == b", "a != b" };

        public static bool Apply(int op, float a, float b)
        {
            switch (op)
            {
                case Greater: return a > b;
                case Less:    return a < b;
                case GEqual:  return a >= b;
                case LEqual:  return a <= b;
                case Equal:   return Mathf.Abs(a - b) < 1e-4f;
                case NEqual:  return Mathf.Abs(a - b) >= 1e-4f;
            }
            return false;
        }
    }

    public static class LogicKind
    {
        public const int Assign  = 0;   // outVar = a OP b
        public const int Trigger = 1;   // fire triggerName when (a CMP b) becomes true

        public static readonly string[] Names = new string[] { "set variable", "fire trigger" };
    }


    // One per-frame logic rule: either assigns a computed value to a named
    // variable, or fires a named custom trigger on the rising edge of a
    // comparison. This is the "save variables / compare / calculate" layer.
    public class LogicOp
    {
        public bool enabled = true;
        public int kind = LogicKind.Assign;
        public GraphValue a = GraphValue.Const(0f);
        public GraphValue b = GraphValue.Const(0f);
        public int op  = MathOp.Mul;      // for Assign
        public int cmp = CmpOp.Greater;   // for Trigger
        public string outVar = "";        // Assign target variable
        public string triggerName = "";   // Trigger to fire

        public bool lastCond = false;     // runtime (rising-edge memory)

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("enabled", new JSONData(enabled));
            o.Add("kind", new JSONData(kind));
            o.Add("a", a.ToJSON());
            o.Add("b", b.ToJSON());
            o.Add("op", new JSONData(op));
            o.Add("cmp", new JSONData(cmp));
            o.Add("outVar", outVar);
            o.Add("triggerName", triggerName);
            return o;
        }

        public static LogicOp FromJSON(JSONClass o)
        {
            if (o == null) return null;
            var l = new LogicOp();
            bool e = true; J.OptBool(o, "enabled", ref e); l.enabled = e;
            J.OptInt(o, "kind", ref l.kind);
            if (o.HasKey("a")) l.a = GraphValue.FromJSON(o["a"]);
            if (o.HasKey("b")) l.b = GraphValue.FromJSON(o["b"]);
            J.OptInt(o, "op", ref l.op);
            J.OptInt(o, "cmp", ref l.cmp);
            l.outVar = J.OptString(o, "outVar", "");
            l.triggerName = J.OptString(o, "triggerName", "");
            return l;
        }

        public override string ToString()
        {
            if (kind == LogicKind.Trigger)
                return "fire '" + triggerName + "' when " + CmpOp.Names[U.Clamp(cmp, 0, 5)] +
                       (enabled ? "" : "  [off]");

            return (string.IsNullOrEmpty(outVar) ? "?" : outVar) + " = " +
                   MathOp.Names[U.Clamp(op, 0, MathOp.Names.Length - 1)] +
                   (enabled ? "" : "  [off]");
        }
    }
}

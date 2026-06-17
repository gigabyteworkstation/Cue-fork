using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Cue.Sound
{
    // Kept for the math NODE (binary op of two GraphValues). The logic layer
    // below is now formula-driven instead of using these.
    public static class CmpOp
    {
        public const int Greater = 0, Less = 1, GEqual = 2, LEqual = 3, Equal = 4, NEqual = 5;
        public static readonly string[] Names = new string[]
        { "a > b", "a < b", "a >= b", "a <= b", "a == b", "a != b" };
    }

    // Modulator kinds. A modulator is evaluated every frame and writes one named
    // variable (or fires a trigger). The value comes from a literal formula, so
    // anything in the value table can drive anything -- "modulate everything".
    public static class LogicKind
    {
        public const int Set      = 0;   // out = formula
        public const int Trigger  = 1;   // fire trigger on rising edge of formula
        public const int Slew     = 2;   // out slews toward formula at `rate`/sec
        public const int Smooth   = 3;   // out = lerp(out, formula, dt*rate)
        public const int Envelope = 4;   // formula triggers an A/H/R envelope -> out

        public static readonly string[] Names = new string[]
        { "set = formula", "trigger when", "slew toward", "smooth toward", "envelope (A/H/R)" };
    }


    // One modulator. Formula is the input/condition; depending on kind it either
    // assigns a variable, fires a trigger, or runs a stateful slew/smooth/
    // envelope. No C# enums (VaM-safe).
    public class LogicOp
    {
        public bool enabled = true;
        public int kind = LogicKind.Set;
        public string outVar = "";        // Set/Slew/Smooth/Envelope output
        public string triggerName = "";   // Trigger output
        public string formula = "";       // value / condition / input

        public float rate = 5f;           // slew units/sec, or smooth lerp rate
        public float attack = 0.05f, hold = 0f, release = 0.3f;  // envelope

        // ---- runtime ----
        private Expr expr_ = null;
        private string exprFrom_ = null;
        private bool lastCond_ = false;
        private float val_ = 0f;
        private bool valInit_ = false;
        private bool envOn_ = false, envRel_ = false;
        private float envT_ = 0f, envRelT_ = 0f;

        private float F(SoundContext ctx)
        {
            if (expr_ == null || exprFrom_ != formula)
            {
                expr_ = Expr.Parse(formula);
                exprFrom_ = formula;
            }
            return expr_.Eval(ctx);
        }

        public void Eval(SoundContext ctx, float dt, Dictionary<string, float> vars, Action<string> fire)
        {
            switch (kind)
            {
                case LogicKind.Set:
                {
                    if (!string.IsNullOrEmpty(outVar))
                        vars[outVar] = F(ctx);
                    break;
                }

                case LogicKind.Trigger:
                {
                    bool c = F(ctx) != 0f;
                    if (c && !lastCond_ && fire != null)
                        fire(triggerName);
                    lastCond_ = c;
                    break;
                }

                case LogicKind.Slew:
                {
                    float t = F(ctx);
                    if (!valInit_) { val_ = t; valInit_ = true; }
                    val_ = Mathf.MoveTowards(val_, t, rate * dt);
                    if (!string.IsNullOrEmpty(outVar)) vars[outVar] = val_;
                    break;
                }

                case LogicKind.Smooth:
                {
                    float t = F(ctx);
                    if (!valInit_) { val_ = t; valInit_ = true; }
                    val_ = Mathf.Lerp(val_, t, Mathf.Clamp01(dt * rate));
                    if (!string.IsNullOrEmpty(outVar)) vars[outVar] = val_;
                    break;
                }

                case LogicKind.Envelope:
                {
                    bool c = F(ctx) != 0f;
                    if (c && !lastCond_) { envOn_ = true; envRel_ = false; envT_ = 0f; envRelT_ = 0f; }
                    lastCond_ = c;

                    float env = 0f;
                    if (envOn_)
                    {
                        envT_ += dt;
                        if (envT_ < attack)
                            env = (attack > 0f) ? envT_ / attack : 1f;
                        else if (!envRel_)
                        {
                            env = 1f;
                            bool holdDone = (hold > 0f) ? (envT_ >= attack + hold) : !c;
                            if (holdDone) { envRel_ = true; envRelT_ = 0f; }
                        }
                        else
                        {
                            envRelT_ += dt;
                            env = (release > 0f) ? Mathf.Clamp01(1f - envRelT_ / release) : 0f;
                            if (env <= 0.001f) envOn_ = false;
                        }
                    }

                    if (!string.IsNullOrEmpty(outVar)) vars[outVar] = Mathf.Clamp01(env);
                    break;
                }
            }
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("enabled", new JSONData(enabled));
            o.Add("kind", new JSONData(kind));
            o.Add("outVar", outVar);
            o.Add("triggerName", triggerName);
            o.Add("formula", formula);
            o.Add("rate", new JSONData(rate));
            o.Add("attack", new JSONData(attack));
            o.Add("hold", new JSONData(hold));
            o.Add("release", new JSONData(release));
            return o;
        }

        public static LogicOp FromJSON(JSONClass o)
        {
            if (o == null) return null;
            var l = new LogicOp();
            bool e = true; J.OptBool(o, "enabled", ref e); l.enabled = e;
            J.OptInt(o, "kind", ref l.kind);
            l.outVar = J.OptString(o, "outVar", "");
            l.triggerName = J.OptString(o, "triggerName", "");
            l.formula = J.OptString(o, "formula", "");
            J.OptFloat(o, "rate", ref l.rate);
            J.OptFloat(o, "attack", ref l.attack);
            J.OptFloat(o, "hold", ref l.hold);
            J.OptFloat(o, "release", ref l.release);
            return l;
        }

        public override string ToString()
        {
            string k = LogicKind.Names[U.Clamp(kind, 0, LogicKind.Names.Length - 1)];
            string target = (kind == LogicKind.Trigger)
                ? ("'" + triggerName + "'")
                : (string.IsNullOrEmpty(outVar) ? "?" : outVar);
            return target + ": " + k + (enabled ? "" : "  [off]");
        }
    }
}

using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Cue.Sound
{
    // A tiny hand-written, SPIRV-ish stack VM with a compiled bytecode program.
    // Source is lexed -> parsed -> compiled to opcodes once, then executed every
    // frame on pre-sized arenas (no per-frame allocation = "top tier memory").
    //
    // Language v1:
    //   // comment
    //   let x = 0                 // script variable (persists across frames)
    //   x = $arousal * 2 + sin($time)
    //   $wobble = clamp01(x)      // $name reads/writes the shared value table
    //   if ($pen.velocity > 0.5) { fire fast } else { x = 0 }
    //   while (x < 10) { x = x + 1 }
    //   on fixed { ... }          // runs in FixedUpdate (physics)
    //   on late  { ... }          // runs in LateUpdate
    //   (top-level statements run every Update / Heartbeat)
    //
    // No C# enums anywhere (VaM's runtime compiler crashes on them).

    public static class Op
    {
        public const int PUSHC  = 0;   // push consts[a]
        public const int LOADL  = 1;   // push vars[a]
        public const int STOREL = 2;   // vars[a] = pop
        public const int LOADV  = 3;   // push resolve(names[a])  (signal / table var)
        public const int SETV   = 4;   // table[names[a]] = pop
        public const int FIRE   = 5;   // fire(names[a])
        public const int CALL   = 6;   // builtin a, argc b -> push result
        public const int PUSHS  = 7;   // push strs[a] onto the string stack
        public const int HCALL  = 8;   // host fn a, fargc b, sargc c -> push float result
        public const int ADD = 10, SUB = 11, MUL = 12, DIV = 13, MOD = 14, POW = 15, NEG = 16;
        public const int LT = 20, GT = 21, LE = 22, GE = 23, EQ = 24, NE = 25, AND = 26, OR = 27, NOT = 28;
        public const int JMP = 30, JZ = 31;   // jump / jump-if-zero to addr a
        public const int POP = 40, HALT = 41;
    }

    // The language's identity. Scripts are authored as text and saved as
    // ".cune" files (Cuneiform: the oldest writing system, and it contains
    // "Cue"). One place to rename it all.
    public static class CuneLang
    {
        public const string Name      = "Cuneiform";
        public const string Short     = "Cune";
        public const string Extension = "cune";   // *.cune
    }

    public static class Hook
    {
        public const int Update = 0;   // Heartbeat
        public const int Fixed  = 1;   // PostSimulation / physics
        public const int Late   = 2;   // RenderStepped
        public const int Count  = 3;
        public static readonly string[] Names = new string[] { "update", "fixed", "late" };
    }


    // The compiled program: one bytecode segment per hook, plus the pooled
    // constants/names and the script's variable count.
    public class ScriptProgram
    {
        public float[] consts = new float[0];
        public string[] names = new string[0];
        public string[] strs = new string[0];   // string literal pool (host args)
        public int numVars = 0;
        public int[][] code = new int[Hook.Count][];  // per-hook bytecode
        public string error = null;

        public bool Ok { get { return error == null; } }
        public bool HasHook(int h) { return code[h] != null && code[h].Length > 0; }
    }


    // One script: source text, its compiled program, runtime variable slots, and
    // per-frame profiling counters.
    public class Script
    {
        public string name = "script";
        public string source = "";
        public bool enabled = true;

        private ScriptProgram prog_ = null;
        private string compiledFrom_ = null;
        private float[] vars_ = new float[0];

        // profiling
        public int lastInstr = 0;        // opcodes executed last frame
        public int totalVars = 0;        // variable slots (memory)
        public float lastMicros = 0f;    // last run time
        public int memBytes = 0;         // estimated steady-state footprint

        // Resolves `import "name"` to another script's source. Set by the engine
        // before compiling so scripts can pull in shared libraries / globals.
        public Func<string, string> Imports = null;

        public string Error { get { return (prog_ != null) ? prog_.error : null; } }

        public void Compile()
        {
            prog_ = Compiler.Compile(source, Imports);
            compiledFrom_ = source;
            vars_ = new float[Mathf.Max(1, prog_.numVars)];
            totalVars = prog_.numVars;
            memBytes = EstimateBytes();
        }

        // Rough but honest steady-state memory: the pools + all code segments +
        // the persistent variable arena (the shared 256-float VM stack is not
        // counted here since it's one ThreadStatic buffer shared by every script).
        private int EstimateBytes()
        {
            if (prog_ == null) return 0;

            int b = 0;
            b += prog_.consts.Length * 4;
            b += vars_.Length * 4;

            for (int i = 0; i < prog_.names.Length; ++i)
                b += 16 + (prog_.names[i] != null ? prog_.names[i].Length * 2 : 0);
            for (int i = 0; i < prog_.strs.Length; ++i)
                b += 16 + (prog_.strs[i] != null ? prog_.strs[i].Length * 2 : 0);

            for (int h = 0; h < Hook.Count; ++h)
                if (prog_.code[h] != null) b += prog_.code[h].Length * 4;

            return b;
        }

        public void RunHook(int hook, SoundContext ctx, Dictionary<string, float> table, Action<string> fire)
        {
            if (!enabled) return;
            if (prog_ == null || compiledFrom_ != source)
                Compile();
            if (!prog_.Ok || !prog_.HasHook(hook))
                return;

            float t0 = Time.realtimeSinceStartup;
            lastInstr = VM.Run(prog_, prog_.code[hook], vars_, ctx, table, fire);
            lastMicros = (Time.realtimeSinceStartup - t0) * 1e6f;
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("name", name);
            o.Add("source", source);
            o.Add("enabled", new JSONData(enabled));
            return o;
        }

        public static Script FromJSON(JSONClass o)
        {
            if (o == null) return null;
            var s = new Script();
            s.name = J.OptString(o, "name", "script");
            s.source = J.OptString(o, "source", "");
            bool e = true; J.OptBool(o, "enabled", ref e); s.enabled = e;
            return s;
        }

        public override string ToString()
        {
            string err = Error;
            return name + (string.IsNullOrEmpty(err) ? "" : "  !ERR") + (enabled ? "" : "  [off]");
        }
    }


    // ---- VM ---------------------------------------------------------------
    public static class VM
    {
        private const int StackSize = 256;
        private const int StrStackSize = 64;
        [ThreadStatic] private static float[] stack_;
        [ThreadStatic] private static string[] sstack_;

        public static int Run(ScriptProgram p, int[] code, float[] vars,
            SoundContext ctx, Dictionary<string, float> table, Action<string> fire)
        {
            if (stack_ == null) stack_ = new float[StackSize];
            if (sstack_ == null) sstack_ = new string[StrStackSize];
            var st = stack_;
            var ss = sstack_;
            int ssp = 0;
            int sp = 0;
            int ip = 0;
            int instr = 0;
            int n = code.Length;
            int guard = 0;

            while (ip < n)
            {
                if (++guard > 1000000) break;  // runaway protection
                instr++;

                int op = code[ip++];
                switch (op)
                {
                    case Op.PUSHC:  st[sp++] = p.consts[code[ip++]]; break;
                    case Op.LOADL:  st[sp++] = vars[code[ip++]]; break;
                    case Op.STOREL: vars[code[ip++]] = st[--sp]; break;
                    case Op.LOADV:  { string nm = p.names[code[ip++]]; st[sp++] = (ctx != null) ? ctx.Resolve(nm) : 0f; break; }
                    case Op.SETV:   { string nm = p.names[code[ip++]]; if (table != null) table[nm] = st[--sp]; else sp--; break; }
                    case Op.FIRE:   { string nm = p.names[code[ip++]]; if (fire != null) fire(nm); break; }
                    case Op.CALL:   { int f = code[ip++]; int argc = code[ip++]; sp = CallBuiltin(f, st, sp, argc); break; }
                    case Op.PUSHS:  { if (ssp < StrStackSize) ss[ssp++] = p.strs[code[ip++]]; else ip++; break; }
                    case Op.HCALL:
                    {
                        int f = code[ip++], fargc = code[ip++], sargc = code[ip++];
                        sp  -= fargc; if (sp  < 0) sp  = 0;
                        ssp -= sargc; if (ssp < 0) ssp = 0;
                        var ha = new Host.Args {
                            s = ss, sb = ssp, sn = sargc,
                            f = st, fb = sp,  fn = fargc,
                            ctx = ctx, table = table, fire = fire
                        };
                        st[sp++] = Host.Call(f, ha);
                        break;
                    }

                    case Op.ADD: st[sp - 2] = st[sp - 2] + st[sp - 1]; sp--; break;
                    case Op.SUB: st[sp - 2] = st[sp - 2] - st[sp - 1]; sp--; break;
                    case Op.MUL: st[sp - 2] = st[sp - 2] * st[sp - 1]; sp--; break;
                    case Op.DIV: { float b = st[sp - 1]; st[sp - 2] = (Math.Abs(b) > 1e-9f) ? st[sp - 2] / b : 0f; sp--; break; }
                    case Op.MOD: { float b = st[sp - 1]; st[sp - 2] = (Math.Abs(b) > 1e-9f) ? st[sp - 2] % b : 0f; sp--; break; }
                    case Op.POW: st[sp - 2] = (float)Math.Pow(st[sp - 2], st[sp - 1]); sp--; break;
                    case Op.NEG: st[sp - 1] = -st[sp - 1]; break;

                    case Op.LT: st[sp - 2] = (st[sp - 2] <  st[sp - 1]) ? 1f : 0f; sp--; break;
                    case Op.GT: st[sp - 2] = (st[sp - 2] >  st[sp - 1]) ? 1f : 0f; sp--; break;
                    case Op.LE: st[sp - 2] = (st[sp - 2] <= st[sp - 1]) ? 1f : 0f; sp--; break;
                    case Op.GE: st[sp - 2] = (st[sp - 2] >= st[sp - 1]) ? 1f : 0f; sp--; break;
                    case Op.EQ: st[sp - 2] = (Math.Abs(st[sp - 2] - st[sp - 1]) < 1e-5f) ? 1f : 0f; sp--; break;
                    case Op.NE: st[sp - 2] = (Math.Abs(st[sp - 2] - st[sp - 1]) >= 1e-5f) ? 1f : 0f; sp--; break;
                    case Op.AND: st[sp - 2] = (st[sp - 2] != 0f && st[sp - 1] != 0f) ? 1f : 0f; sp--; break;
                    case Op.OR:  st[sp - 2] = (st[sp - 2] != 0f || st[sp - 1] != 0f) ? 1f : 0f; sp--; break;
                    case Op.NOT: st[sp - 1] = (st[sp - 1] == 0f) ? 1f : 0f; break;

                    case Op.JMP: ip = code[ip]; break;
                    case Op.JZ:  { int addr = code[ip++]; if (st[--sp] == 0f) ip = addr; break; }
                    case Op.POP:  sp--; break;
                    case Op.HALT: ip = n; break;
                }

                if (sp < 0) sp = 0;
                if (sp >= StackSize - 2) break;  // overflow guard
            }

            return instr;
        }

        private static int CallBuiltin(int f, float[] st, int sp, int argc)
        {
            float a = argc > 0 ? st[sp - argc] : 0f;
            float b = argc > 1 ? st[sp - argc + 1] : 0f;
            float c = argc > 2 ? st[sp - argc + 2] : 0f;
            sp -= argc;
            st[sp++] = Builtins.Call(f, a, b, c);
            return sp;
        }
    }


    public static class Builtins
    {
        public static readonly string[] Names = new string[]
        {
            "sin","cos","tan","abs","sqrt","floor","ceil","round","sign","exp","log",
            "frac","min","max","pow","mod","clamp","clamp01","lerp","step","smooth","pi","rand"
        };

        public static int Find(string n)
        {
            for (int i = 0; i < Names.Length; ++i) if (Names[i] == n) return i;
            return -1;
        }

        public static float Call(int f, float a, float b, float c)
        {
            switch (f)
            {
                case 0:  return (float)Math.Sin(a);
                case 1:  return (float)Math.Cos(a);
                case 2:  return (float)Math.Tan(a);
                case 3:  return Math.Abs(a);
                case 4:  return (a > 0f) ? (float)Math.Sqrt(a) : 0f;
                case 5:  return (float)Math.Floor(a);
                case 6:  return (float)Math.Ceiling(a);
                case 7:  return (float)Math.Round(a);
                case 8:  return Math.Sign(a);
                case 9:  return (float)Math.Exp(a);
                case 10: return (a > 0f) ? (float)Math.Log(a) : 0f;
                case 11: return a - (float)Math.Floor(a);
                case 12: return Math.Min(a, b);
                case 13: return Math.Max(a, b);
                case 14: return (float)Math.Pow(a, b);
                case 15: return (Math.Abs(b) > 1e-9f) ? a % b : 0f;
                case 16: return (a < b) ? b : (a > c ? c : a);          // clamp
                case 17: return (a < 0f) ? 0f : (a > 1f ? 1f : a);      // clamp01
                case 18: return a + (b - a) * (c < 0f ? 0f : (c > 1f ? 1f : c));  // lerp
                case 19: return (a >= b) ? 1f : 0f;                     // step
                case 20: { float t = (Math.Abs(b - a) < 1e-9f) ? 0f : (c - a) / (b - a); t = t < 0 ? 0 : (t > 1 ? 1 : t); return t * t * (3f - 2f * t); }
                case 21: return (float)Math.PI;
                case 22: return UnityEngine.Random.value;
            }
            return 0f;
        }
    }
}

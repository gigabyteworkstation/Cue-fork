using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cue.Sound
{
    // The bridge that lets Cuneiform scripts reach into Cue. A host function has
    // a name, a typed signature (string args, which must be a leading prefix,
    // then float args), and an implementation. The compiler looks functions up
    // by name to type-check call sites; the VM dispatches HCALL through Call().
    //
    // This is the single extension point for "let the language talk to Cue":
    // adding a capability = one Reg(...) line here, no compiler/VM changes.
    public static class Host
    {
        public const int KFloat = 0, KString = 1;

        // Argument view handed to a host function. A by-value struct over the
        // VM's two stacks -> zero allocation per call. S(i)/F(i) clamp so a
        // function can read optional trailing floats that weren't supplied.
        public struct Args
        {
            public string[] s; public int sb, sn;
            public float[]  f; public int fb, fn;
            public SoundContext ctx;
            public Dictionary<string, float> table;
            public Action<string> fire;

            public string S(int i) { return (i >= 0 && i < sn) ? s[sb + i] : ""; }
            public float  F(int i) { return (i >= 0 && i < fn) ? f[fb + i] : 0f; }
            public float  F(int i, float def) { return (i >= 0 && i < fn) ? f[fb + i] : def; }
        }

        private class Fn
        {
            public string name;
            public int[] kinds;       // per-arg kind, KString prefix then KFloat
            public int reqStrings;    // leading string args (all required)
            public Func<Args, float> invoke;
            public string help;
        }

        private static readonly List<Fn> fns_ = new List<Fn>();

        static Host()
        {
            // --- sound -----------------------------------------------------
            Reg("play", "s f f f", PlayHere,
                "play(set, intensity=0, vol=1, pitch=1) at this person");
            Reg("playat", "s f f f f f", PlayAt,
                "playat(set, intensity, x, y, z, ...) at a world point");

            // --- value table ----------------------------------------------
            Reg("get", "s", GetVar, "get(\"name\") -> shared table / signal value");
            Reg("set", "s f", SetVar, "set(\"name\", value) writes the shared table");
            Reg("trigger", "s", Trigger, "trigger(\"name\") fires a custom trigger");
        }

        private static void Reg(string name, string sig, Func<Args, float> fn, string help)
        {
            var kinds = ParseSig(sig);
            int req = 0;
            for (int i = 0; i < kinds.Length; ++i)
            {
                if (kinds[i] == KString) req++;
                else break;   // strings are a leading prefix; stop at first float
            }
            fns_.Add(new Fn { name = name, kinds = kinds, reqStrings = req, invoke = fn, help = help });
        }

        private static int[] ParseSig(string sig)
        {
            if (string.IsNullOrEmpty(sig)) return new int[0];
            var parts = sig.Split(' ');
            var list = new List<int>();
            for (int i = 0; i < parts.Length; ++i)
            {
                var p = parts[i].Trim();
                if (p.Length == 0) continue;
                list.Add(p == "s" ? KString : KFloat);
            }
            return list.ToArray();
        }

        // ---- compiler-facing -------------------------------------------------

        public static int Find(string name)
        {
            for (int i = 0; i < fns_.Count; ++i)
                if (fns_[i].name == name) return i;
            return -1;
        }

        public static int   Arity(int id)      { return (id >= 0 && id < fns_.Count) ? fns_[id].kinds.Length : 0; }
        public static int   ReqStrings(int id) { return (id >= 0 && id < fns_.Count) ? fns_[id].reqStrings : 0; }
        public static int   KindAt(int id, int argi)
        {
            if (id < 0 || id >= fns_.Count) return KFloat;
            var k = fns_[id].kinds;
            return (argi >= 0 && argi < k.Length) ? k[argi] : KFloat;   // extra args are floats
        }

        public static IEnumerable<string> Listing()
        {
            for (int i = 0; i < fns_.Count; ++i)
                yield return fns_[i].help;
        }

        // ---- VM-facing -------------------------------------------------------

        public static float Call(int id, Args a)
        {
            if (id < 0 || id >= fns_.Count) return 0f;
            try { return fns_[id].invoke(a); }
            catch (Exception) { return 0f; }
        }

        // ---- implementations -------------------------------------------------

        private static float PlayHere(Args a)
        {
            var ctx = a.ctx;
            if (ctx == null || ctx.Person == null) return 0f;
            var pos = Sys.Vam.U.ToUnity(ctx.Person.Position);
            return DoPlay(a.S(0), a.F(0), a.F(1, 1f), a.F(2, 1f), pos);
        }

        private static float PlayAt(Args a)
        {
            var pos = new UnityEngine.Vector3(a.F(1), a.F(2), a.F(3));
            return DoPlay(a.S(0), a.F(0), a.F(4, 1f), a.F(5, 1f), pos);
        }

        private static float DoPlay(string set, float intensity, float vol, float pitch, UnityEngine.Vector3 pos)
        {
            if (string.IsNullOrEmpty(set)) return 0f;
            if (vol <= 0f) vol = 1f;
            if (pitch <= 0f) pitch = 1f;

            bool ok = SoundManager.Instance.Play(
                set, pos, Mathf.Clamp01(intensity),
                vol, pitch, 0f, 0f, 0f);
            return ok ? 1f : 0f;
        }

        private static float GetVar(Args a)
        {
            string nm = a.S(0);
            if (a.ctx != null) return a.ctx.Resolve(nm);
            float v; return (a.table != null && a.table.TryGetValue(nm, out v)) ? v : 0f;
        }

        private static float SetVar(Args a)
        {
            if (a.table != null) a.table[a.S(0)] = a.F(0);
            return a.F(0);
        }

        private static float Trigger(Args a)
        {
            if (a.fire != null) a.fire(a.S(0));
            return 0f;
        }
    }
}

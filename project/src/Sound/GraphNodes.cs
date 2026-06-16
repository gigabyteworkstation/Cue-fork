using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Cue.Sound
{
    // ---- Node base --------------------------------------------------------
    // A graph node is a serializable descriptor (data). At trigger time it spins
    // up a NodeInstance (runtime). Container nodes recurse into child instances.
    // This descriptor/instance split (like a behaviour tree) keeps the saved
    // graph immutable while many instances of it can run concurrently.
    public abstract class SoundNode
    {
        public List<SoundNode> children = new List<SoundNode>();

        public abstract string Type { get; }
        public abstract NodeInstance Create(SoundContext ctx);

        protected virtual void WriteJSON(JSONClass o) { }
        protected virtual void ReadJSON(JSONClass o) { }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("type", Type);
            WriteJSON(o);

            if (children.Count > 0)
            {
                var a = new JSONArray();
                for (int i = 0; i < children.Count; ++i)
                    a.Add(children[i].ToJSON());
                o.Add("children", a);
            }

            return o;
        }

        public static SoundNode FromJSON(JSONNode n)
        {
            var o = n as JSONClass;
            if (o == null || !o.HasKey("type"))
                return null;

            SoundNode node = CreateByType(o["type"].Value);
            if (node == null)
                return null;

            node.ReadJSON(o);

            if (o.HasKey("children"))
            {
                var a = o["children"].AsArray;
                if (a != null)
                {
                    foreach (JSONNode cn in a)
                    {
                        var c = FromJSON(cn);
                        if (c != null)
                            node.children.Add(c);
                    }
                }
            }

            return node;
        }

        public static SoundNode CreateByType(string t)
        {
            switch (t)
            {
                case "clip":     return new ClipNode();
                case "gain":     return new GainNode();
                case "pitch":    return new PitchNode();
                case "envelope": return new EnvelopeNode();
                case "random":   return new RandomNode();
                case "layer":    return new LayerNode();
                case "sequence": return new SequenceNode();
                case "loop":     return new LoopNode();
                case "math":     return new MathNode();
            }
            return null;
        }

        public static readonly string[] AllTypes = new string[]
        {
            "clip", "gain", "pitch", "math", "envelope", "random", "layer", "sequence", "loop"
        };

        protected NodeInstance CreateChild(int i, SoundContext ctx)
        {
            if (i < 0 || i >= children.Count || children[i] == null)
                return null;
            return children[i].Create(ctx);
        }
    }


    // ---- Instance base ----------------------------------------------------
    public abstract class NodeInstance
    {
        protected readonly SoundContext ctx;

        // Multipliers pushed down from parent modulators (the combine chain).
        public float gainMul = 1f;
        public float pitchMul = 1f;

        public bool Finished = false;

        protected NodeInstance(SoundContext c) { ctx = c; }

        public abstract void Update(float s);
        public abstract void Stop(bool immediate);
    }


    // ---- clip (leaf source) ----------------------------------------------
    // Plays a clip drawn from a SoundSet (folder/bundle, with the set's own
    // intensity bands). One-shot or looping. Its gain/pitch GraphValues are the
    // base settings; parent modulators scale them through gainMul/pitchMul.
    public class ClipNode : SoundNode
    {
        public string set = "";
        public GraphValue intensity = GraphValue.Var(GVar.EventIntensity);
        public GraphValue gain  = GraphValue.Const(1f);
        public GraphValue pitch = GraphValue.Const(1f);
        public bool loop = false;

        public override string Type { get { return "clip"; } }

        public override NodeInstance Create(SoundContext ctx)
        {
            return new ClipInstance(ctx, this);
        }

        protected override void WriteJSON(JSONClass o)
        {
            o.Add("set", set);
            o.Add("intensity", intensity.ToJSON());
            o.Add("gain", gain.ToJSON());
            o.Add("pitch", pitch.ToJSON());
            o.Add("loop", new JSONData(loop));
        }

        protected override void ReadJSON(JSONClass o)
        {
            set = J.OptString(o, "set", "");
            if (o.HasKey("intensity")) intensity = GraphValue.FromJSON(o["intensity"]);
            if (o.HasKey("gain"))      gain      = GraphValue.FromJSON(o["gain"]);
            if (o.HasKey("pitch"))     pitch     = GraphValue.FromJSON(o["pitch"]);
            bool l = false; J.OptBool(o, "loop", ref l); loop = l;
        }
    }

    public class ClipInstance : NodeInstance
    {
        private readonly ClipNode node_;
        private Voice voice_;
        private float baseVol_;
        private float basePitch_;
        private float elapsed_ = 0f;
        private readonly bool loop_;

        public ClipInstance(SoundContext ctx, ClipNode n) : base(ctx)
        {
            node_ = n;
            loop_ = n.loop;

            var setObj = SoundManager.Instance.Find(n.set);
            var clip = (setObj != null) ? setObj.Pick(n.intensity.Get(ctx)) : null;

            baseVol_   = n.gain.Get(ctx);
            basePitch_ = n.pitch.Get(ctx);

            if (clip == null)
            {
                Finished = true;
                return;
            }

            voice_ = SoundManager.Instance.Player.PlayVoice(
                clip, ctx.Position,
                baseVol_ * SoundManager.Instance.MasterVolume,
                basePitch_, loop_);

            if (voice_ == null)
                Finished = true;
        }

        public override void Update(float s)
        {
            if (Finished) return;

            elapsed_ += s;

            // re-read live params so variable-driven gain/pitch track signals
            baseVol_   = node_.gain.Get(ctx);
            basePitch_ = node_.pitch.Get(ctx);

            if (voice_ != null && voice_.Valid)
            {
                voice_.SetVolume(baseVol_ * gainMul * SoundManager.Instance.MasterVolume);
                voice_.SetPitch(basePitch_ * pitchMul);
                voice_.SetPosition(ctx.Position);
            }

            if (!loop_)
            {
                // one-shot finished: source stopped (small grace so we don't
                // trip on the frame before Play() registers as playing)
                if (elapsed_ > 0.1f && (voice_ == null || !voice_.IsPlaying))
                    Finished = true;
            }
        }

        public override void Stop(bool immediate)
        {
            if (voice_ != null)
                voice_.Stop();
            Finished = true;
        }
    }


    // ---- gain / pitch (modulators, one child) ----------------------------
    public class GainNode : SoundNode
    {
        public GraphValue gain = GraphValue.Const(1f);
        public override string Type { get { return "gain"; } }
        public override NodeInstance Create(SoundContext ctx)
        {
            return new ModulateInstance(ctx, CreateChild(0, ctx), gain, null);
        }
        protected override void WriteJSON(JSONClass o) { o.Add("gain", gain.ToJSON()); }
        protected override void ReadJSON(JSONClass o)
        {
            if (o.HasKey("gain")) gain = GraphValue.FromJSON(o["gain"]);
        }
    }

    public class PitchNode : SoundNode
    {
        public GraphValue pitch = GraphValue.Const(1f);
        public override string Type { get { return "pitch"; } }
        public override NodeInstance Create(SoundContext ctx)
        {
            return new ModulateInstance(ctx, CreateChild(0, ctx), null, pitch);
        }
        protected override void WriteJSON(JSONClass o) { o.Add("pitch", pitch.ToJSON()); }
        protected override void ReadJSON(JSONClass o)
        {
            if (o.HasKey("pitch")) pitch = GraphValue.FromJSON(o["pitch"]);
        }
    }

    // Shared modulator instance: scales the child's gain and/or pitch each frame
    // by a live GraphValue, combining with whatever this node was handed.
    public class ModulateInstance : NodeInstance
    {
        private readonly NodeInstance child_;
        private readonly GraphValue gain_;
        private readonly GraphValue pitch_;

        public ModulateInstance(SoundContext ctx, NodeInstance child,
            GraphValue gain, GraphValue pitch) : base(ctx)
        {
            child_ = child;
            gain_ = gain;
            pitch_ = pitch;
            if (child_ == null) Finished = true;
        }

        public override void Update(float s)
        {
            if (Finished || child_ == null) { Finished = true; return; }

            child_.gainMul  = gainMul  * ((gain_  != null) ? gain_.Get(ctx)  : 1f);
            child_.pitchMul = pitchMul * ((pitch_ != null) ? pitch_.Get(ctx) : 1f);
            child_.Update(s);

            Finished = child_.Finished;
        }

        public override void Stop(bool immediate)
        {
            if (child_ != null) child_.Stop(immediate);
            Finished = true;
        }
    }


    // ---- envelope (attack / hold / release over gain) --------------------
    public class EnvelopeNode : SoundNode
    {
        public float attack = 0.02f;
        public float hold = 0.0f;     // 0 == hold until child finishes
        public float release = 0.1f;

        public override string Type { get { return "envelope"; } }
        public override NodeInstance Create(SoundContext ctx)
        {
            return new EnvelopeInstance(ctx, CreateChild(0, ctx), this);
        }
        protected override void WriteJSON(JSONClass o)
        {
            o.Add("attack", new JSONData(attack));
            o.Add("hold", new JSONData(hold));
            o.Add("release", new JSONData(release));
        }
        protected override void ReadJSON(JSONClass o)
        {
            J.OptFloat(o, "attack", ref attack);
            J.OptFloat(o, "hold", ref hold);
            J.OptFloat(o, "release", ref release);
        }
    }

    public class EnvelopeInstance : NodeInstance
    {
        private readonly NodeInstance child_;
        private readonly EnvelopeNode node_;
        private float t_ = 0f;
        private bool releasing_ = false;
        private float releaseT_ = 0f;

        public EnvelopeInstance(SoundContext ctx, NodeInstance child, EnvelopeNode n) : base(ctx)
        {
            child_ = child;
            node_ = n;
            if (child_ == null) Finished = true;
        }

        public override void Update(float s)
        {
            if (Finished || child_ == null) { Finished = true; return; }

            t_ += s;

            float env;
            if (t_ < node_.attack)
            {
                env = (node_.attack > 0f) ? (t_ / node_.attack) : 1f;
            }
            else if (!releasing_)
            {
                env = 1f;

                // start releasing once the hold elapses, or (hold==0) once the
                // child reports finished
                bool holdDone = (node_.hold > 0f)
                    ? (t_ >= node_.attack + node_.hold)
                    : child_.Finished;

                if (holdDone)
                {
                    releasing_ = true;
                    releaseT_ = 0f;
                }
            }
            else
            {
                releaseT_ += s;
                env = (node_.release > 0f)
                    ? Mathf.Clamp01(1f - releaseT_ / node_.release)
                    : 0f;
            }

            child_.gainMul  = gainMul * Mathf.Clamp01(env);
            child_.pitchMul = pitchMul;
            child_.Update(s);

            if (releasing_ && env <= 0.001f)
            {
                child_.Stop(false);
                Finished = true;
            }
        }

        public override void Stop(bool immediate)
        {
            if (immediate)
            {
                if (child_ != null) child_.Stop(true);
                Finished = true;
            }
            else
            {
                releasing_ = true;
            }
        }
    }


    // ---- random (pick one child) -----------------------------------------
    public class RandomNode : SoundNode
    {
        public override string Type { get { return "random"; } }
        public override NodeInstance Create(SoundContext ctx)
        {
            if (children.Count == 0)
                return new EmptyInstance(ctx);

            int i = (ctx.Rng != null)
                ? ctx.Rng.Next(children.Count)
                : UnityEngine.Random.Range(0, children.Count);

            return new PassthroughInstance(ctx, CreateChild(i, ctx));
        }
    }


    // ---- layer (all children at once = multitrack) -----------------------
    public class LayerNode : SoundNode
    {
        public override string Type { get { return "layer"; } }
        public override NodeInstance Create(SoundContext ctx)
        {
            var list = new List<NodeInstance>();
            for (int i = 0; i < children.Count; ++i)
            {
                var ni = CreateChild(i, ctx);
                if (ni != null) list.Add(ni);
            }
            return new LayerInstance(ctx, list);
        }
    }

    public class LayerInstance : NodeInstance
    {
        private readonly List<NodeInstance> kids_;

        public LayerInstance(SoundContext ctx, List<NodeInstance> kids) : base(ctx)
        {
            kids_ = kids;
            if (kids_.Count == 0) Finished = true;
        }

        public override void Update(float s)
        {
            if (Finished) return;

            bool allDone = true;
            for (int i = 0; i < kids_.Count; ++i)
            {
                var k = kids_[i];
                if (k.Finished) continue;
                k.gainMul = gainMul;
                k.pitchMul = pitchMul;
                k.Update(s);
                if (!k.Finished) allDone = false;
            }
            Finished = allDone;
        }

        public override void Stop(bool immediate)
        {
            for (int i = 0; i < kids_.Count; ++i)
                kids_[i].Stop(immediate);
            Finished = true;
        }
    }


    // ---- sequence (children in order) ------------------------------------
    public class SequenceNode : SoundNode
    {
        public override string Type { get { return "sequence"; } }
        public override NodeInstance Create(SoundContext ctx)
        {
            return new SequenceInstance(ctx, this);
        }
    }

    public class SequenceInstance : NodeInstance
    {
        private readonly SequenceNode node_;
        private NodeInstance cur_;
        private int idx_ = -1;

        public SequenceInstance(SoundContext ctx, SequenceNode n) : base(ctx)
        {
            node_ = n;
            Advance();
        }

        private void Advance()
        {
            idx_++;
            if (idx_ >= node_.children.Count)
            {
                cur_ = null;
                Finished = true;
                return;
            }
            cur_ = node_.children[idx_].Create(ctx);
        }

        public override void Update(float s)
        {
            if (Finished) return;
            if (cur_ == null) { Advance(); if (Finished) return; }

            cur_.gainMul = gainMul;
            cur_.pitchMul = pitchMul;
            cur_.Update(s);

            if (cur_.Finished)
                Advance();
        }

        public override void Stop(bool immediate)
        {
            if (cur_ != null) cur_.Stop(immediate);
            Finished = true;
        }
    }


    // ---- loop (repeat child while active, with a gap) --------------------
    public class LoopNode : SoundNode
    {
        public GraphValue interval = GraphValue.Const(0f);  // gap between repeats (s)
        public override string Type { get { return "loop"; } }
        public override NodeInstance Create(SoundContext ctx)
        {
            return new LoopInstance(ctx, this);
        }
        protected override void WriteJSON(JSONClass o) { o.Add("interval", interval.ToJSON()); }
        protected override void ReadJSON(JSONClass o)
        {
            if (o.HasKey("interval")) interval = GraphValue.FromJSON(o["interval"]);
        }
    }

    public class LoopInstance : NodeInstance
    {
        private readonly LoopNode node_;
        private NodeInstance cur_;
        private float wait_ = 0f;
        private bool stopping_ = false;

        public LoopInstance(SoundContext ctx, LoopNode n) : base(ctx)
        {
            node_ = n;
            Restart();
        }

        private void Restart()
        {
            cur_ = (node_.children.Count > 0) ? node_.children[0].Create(ctx) : null;
        }

        public override void Update(float s)
        {
            if (Finished) return;

            if (cur_ != null && !cur_.Finished)
            {
                cur_.gainMul = gainMul;
                cur_.pitchMul = pitchMul;
                cur_.Update(s);
                return;
            }

            if (stopping_)
            {
                Finished = true;
                return;
            }

            // child finished: wait the interval, then restart
            wait_ += s;
            if (wait_ >= node_.interval.Get(ctx))
            {
                wait_ = 0f;
                Restart();
                if (cur_ == null) Finished = true;
            }
        }

        public override void Stop(bool immediate)
        {
            stopping_ = true;
            if (cur_ != null) cur_.Stop(immediate);
            if (immediate) Finished = true;
        }
    }


    // ---- math (modulate child's gain or pitch by a computed value) -------
    // Like gain/pitch, but the multiplier is a binary operation of two live
    // GraphValues (so you can drive a pitch by, say, pen.velocity * arousal).
    public static class MathOp
    {
        public const int Add = 0;
        public const int Sub = 1;
        public const int Mul = 2;
        public const int Div = 3;
        public const int Min = 4;
        public const int Max = 5;

        public static readonly string[] Names = new string[]
        { "a + b", "a - b", "a * b", "a / b", "min(a,b)", "max(a,b)" };

        public static float Apply(int op, float a, float b)
        {
            switch (op)
            {
                case Add: return a + b;
                case Sub: return a - b;
                case Mul: return a * b;
                case Div: return (Mathf.Abs(b) > 1e-6f) ? a / b : 0f;
                case Min: return Mathf.Min(a, b);
                case Max: return Mathf.Max(a, b);
            }
            return a;
        }
    }

    public static class MathTarget
    {
        public const int Gain = 0;
        public const int Pitch = 1;
        public static readonly string[] Names = new string[] { "gain", "pitch" };
    }

    public class MathNode : SoundNode
    {
        public GraphValue a = GraphValue.Const(1f);
        public GraphValue b = GraphValue.Const(1f);
        public int op = MathOp.Mul;
        public int target = MathTarget.Pitch;

        public override string Type { get { return "math"; } }
        public override NodeInstance Create(SoundContext ctx)
        {
            return new MathInstance(ctx, CreateChild(0, ctx), this);
        }
        protected override void WriteJSON(JSONClass o)
        {
            o.Add("a", a.ToJSON());
            o.Add("b", b.ToJSON());
            o.Add("op", new JSONData(op));
            o.Add("target", new JSONData(target));
        }
        protected override void ReadJSON(JSONClass o)
        {
            if (o.HasKey("a")) a = GraphValue.FromJSON(o["a"]);
            if (o.HasKey("b")) b = GraphValue.FromJSON(o["b"]);
            J.OptInt(o, "op", ref op);
            J.OptInt(o, "target", ref target);
        }
    }

    public class MathInstance : NodeInstance
    {
        private readonly NodeInstance child_;
        private readonly MathNode node_;

        public MathInstance(SoundContext ctx, NodeInstance child, MathNode n) : base(ctx)
        {
            child_ = child;
            node_ = n;
            if (child_ == null) Finished = true;
        }

        public override void Update(float s)
        {
            if (Finished || child_ == null) { Finished = true; return; }

            float v = MathOp.Apply(node_.op, node_.a.Get(ctx), node_.b.Get(ctx));

            child_.gainMul  = gainMul  * ((node_.target == MathTarget.Gain)  ? v : 1f);
            child_.pitchMul = pitchMul * ((node_.target == MathTarget.Pitch) ? v : 1f);
            child_.Update(s);

            Finished = child_.Finished;
        }

        public override void Stop(bool immediate)
        {
            if (child_ != null) child_.Stop(immediate);
            Finished = true;
        }
    }


    // ---- helper instances ------------------------------------------------
    public class PassthroughInstance : NodeInstance
    {
        private readonly NodeInstance child_;
        public PassthroughInstance(SoundContext ctx, NodeInstance child) : base(ctx)
        {
            child_ = child;
            if (child_ == null) Finished = true;
        }
        public override void Update(float s)
        {
            if (Finished || child_ == null) { Finished = true; return; }
            child_.gainMul = gainMul;
            child_.pitchMul = pitchMul;
            child_.Update(s);
            Finished = child_.Finished;
        }
        public override void Stop(bool immediate)
        {
            if (child_ != null) child_.Stop(immediate);
            Finished = true;
        }
    }

    public class EmptyInstance : NodeInstance
    {
        public EmptyInstance(SoundContext ctx) : base(ctx) { Finished = true; }
        public override void Update(float s) { Finished = true; }
        public override void Stop(bool immediate) { Finished = true; }
    }
}

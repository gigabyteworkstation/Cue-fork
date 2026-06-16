using UnityEngine;

namespace Cue.Sound
{
    // A pool of world-positioned AudioSources for one-shot playback. Every
    // play is positioned exactly at the event (impact point, orifice, ...) so
    // 3D attenuation/panning is correct, and because each concurrent play uses
    // its own source, sounds layer freely (slap over moan over squelch).
    //
    // Pooling instead of new AudioSource per hit: identical audible result,
    // zero steady-state allocation, no GameObject churn.
    // A live, controllable handle to a pooled AudioSource. Returned by
    // PlayVoice for the graph runtime, which needs to modulate volume/pitch over
    // time (envelopes, LFOs) and hold looping ambiences. A generation stamp
    // makes the handle safe: once the pool steals/reuses the slot, the old
    // handle's generation no longer matches and all its calls become no-ops.
    public class Voice
    {
        private readonly SoundPlayer player_;
        private readonly int index_;
        private readonly int gen_;

        internal Voice(SoundPlayer p, int index, int gen)
        {
            player_ = p;
            index_ = index;
            gen_ = gen;
        }

        public bool Valid     { get { return player_ != null && player_.VoiceValid(index_, gen_); } }
        public bool IsPlaying { get { return Valid && player_.VoiceSource(index_, gen_).isPlaying; } }

        public void SetVolume(float v)
        {
            var s = Source();
            if (s != null) s.volume = Mathf.Clamp(v, 0f, 2f);
        }

        public void SetPitch(float p)
        {
            var s = Source();
            if (s != null) s.pitch = Mathf.Clamp(p, 0.05f, 4f);
        }

        public void SetPosition(UnityEngine.Vector3 pos)
        {
            var s = Source();
            if (s != null) s.transform.position = pos;
        }

        public void Stop()
        {
            var s = Source();
            if (s != null) { s.loop = false; s.Stop(); }
        }

        private AudioSource Source()
        {
            return (player_ == null) ? null : player_.VoiceSource(index_, gen_);
        }
    }


    public class SoundPlayer
    {
        private const int PoolSize = 24;

        private GameObject root_ = null;
        private AudioSource[] sources_ = null;
        private float[] startTimes_ = null;
        private int[] gen_ = null;
        private int next_ = 0;

        private void EnsurePool()
        {
            if (root_ != null)
                return;

            root_ = new GameObject("cue!soundPool");
            UnityEngine.Object.DontDestroyOnLoad(root_);

            sources_ = new AudioSource[PoolSize];
            startTimes_ = new float[PoolSize];
            gen_ = new int[PoolSize];

            for (int i = 0; i < PoolSize; ++i)
            {
                var go = new GameObject("src" + i);
                go.transform.SetParent(root_.transform, false);

                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.loop = false;
                a.spatialBlend = 1.0f;             // fully 3D
                a.rolloffMode = AudioRolloffMode.Logarithmic;
                a.minDistance = 0.4f;
                a.maxDistance = 18.0f;
                a.dopplerLevel = 0f;
                a.spread = 25f;

                sources_[i] = a;
                startTimes_[i] = 0f;
                gen_[i] = 1;
            }
        }

        // Picks a slot (free, else oldest) and bumps its generation so any
        // outstanding Voice handle on that slot is invalidated.
        private int AcquireSlot()
        {
            EnsurePool();

            int chosen = -1;
            float oldest = float.MaxValue;
            int oldestIdx = 0;

            for (int n = 0; n < PoolSize; ++n)
            {
                int i = (next_ + n) % PoolSize;

                if (!sources_[i].isPlaying)
                {
                    chosen = i;
                    break;
                }

                if (startTimes_[i] < oldest)
                {
                    oldest = startTimes_[i];
                    oldestIdx = i;
                }
            }

            if (chosen < 0)
                chosen = oldestIdx;

            next_ = (chosen + 1) % PoolSize;
            gen_[chosen]++;
            return chosen;
        }

        // Returns a controllable handle (looping optional). Used by the graph
        // runtime; returns null when nothing could be played.
        public Voice PlayVoice(
            AudioClip clip, UnityEngine.Vector3 pos, float volume, float pitch, bool loop)
        {
            if (clip == null)
                return null;

            int i = AcquireSlot();
            var src = sources_[i];

            src.transform.position = pos;
            src.volume = Mathf.Clamp(volume, 0f, 2f);
            src.pitch = Mathf.Clamp(pitch, 0.05f, 4f);
            src.loop = loop;
            src.clip = clip;
            src.Play();

            startTimes_[i] = Time.unscaledTime;
            return new Voice(this, i, gen_[i]);
        }

        internal bool VoiceValid(int index, int gen)
        {
            return gen_ != null && index >= 0 && index < PoolSize && gen_[index] == gen;
        }

        internal AudioSource VoiceSource(int index, int gen)
        {
            return VoiceValid(index, gen) ? sources_[index] : null;
        }

        public bool Play(AudioClip clip, UnityEngine.Vector3 pos, float volume, float pitch)
        {
            if (clip == null || volume <= 0.001f)
                return false;

            int i = AcquireSlot();
            var src = sources_[i];

            src.transform.position = pos;
            src.volume = Mathf.Clamp(volume, 0f, 2f);
            src.pitch = Mathf.Clamp(pitch, 0.3f, 2.5f);
            src.loop = false;
            src.clip = clip;
            src.Play();

            startTimes_[i] = Time.unscaledTime;
            return true;
        }

        public void StopAll()
        {
            if (sources_ == null)
                return;

            for (int i = 0; i < PoolSize; ++i)
            {
                if (sources_[i] != null && sources_[i].isPlaying)
                {
                    sources_[i].loop = false;
                    sources_[i].Stop();
                }
            }
        }

        public void Destroy()
        {
            StopAll();

            if (root_ != null)
            {
                UnityEngine.Object.Destroy(root_);
                root_ = null;
                sources_ = null;
                gen_ = null;
            }
        }
    }
}

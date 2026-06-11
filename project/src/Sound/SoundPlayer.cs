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
    public class SoundPlayer
    {
        private const int PoolSize = 16;

        private GameObject root_ = null;
        private AudioSource[] sources_ = null;
        private float[] startTimes_ = null;
        private int next_ = 0;

        private void EnsurePool()
        {
            if (root_ != null)
                return;

            root_ = new GameObject("cue!soundPool");
            UnityEngine.Object.DontDestroyOnLoad(root_);

            sources_ = new AudioSource[PoolSize];
            startTimes_ = new float[PoolSize];

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
            }
        }

        public bool Play(AudioClip clip, UnityEngine.Vector3 pos, float volume, float pitch)
        {
            if (clip == null || volume <= 0.001f)
                return false;

            EnsurePool();

            // round-robin scan for a free source; if all are busy, steal the
            // oldest so fresh impacts always win
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

            var src = sources_[chosen];
            src.transform.position = pos;
            src.volume = Mathf.Clamp(volume, 0f, 2f);
            src.pitch = Mathf.Clamp(pitch, 0.3f, 2.5f);
            src.clip = clip;
            src.Play();

            startTimes_[chosen] = Time.unscaledTime;
            return true;
        }

        public void StopAll()
        {
            if (sources_ == null)
                return;

            for (int i = 0; i < PoolSize; ++i)
            {
                if (sources_[i] != null && sources_[i].isPlaying)
                    sources_[i].Stop();
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
            }
        }
    }
}

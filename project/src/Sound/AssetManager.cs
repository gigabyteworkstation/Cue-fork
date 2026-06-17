using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cue.Sound
{
    // Central, cached loader for runtime assets. Two sources:
    //   - audio files (.wav/.ogg/.mp3) via VaM's URLAudioClipManager
    //   - asset bundles via MeshVR.AssetLoader (audio clips are indexed by name;
    //     the raw AssetBundle is exposed so future host functions can pull other
    //     asset types out of it).
    //
    // Everything is loaded once and cached by path, so repeated requests are
    // free. Loads are asynchronous (clips stream in, bundles use a callback);
    // Update() drains finished streams. Cune scripts drive the whole thing
    // through host functions (loadclip / loadbundle / playfile / playbundle).
    public class AssetManager
    {
        private static AssetManager instance_;
        public static AssetManager Instance
        {
            get { if (instance_ == null) instance_ = new AssetManager(); return instance_; }
        }

        // ---- audio files -------------------------------------------------
        private readonly Dictionary<string, AudioClip> clips_ = new Dictionary<string, AudioClip>();
        private readonly List<NamedAudioClip> pending_ = new List<NamedAudioClip>();
        private readonly List<string> pendingPaths_ = new List<string>();

        // ---- asset bundles -----------------------------------------------
        private class Bundle
        {
            public string path;
            public bool ready;
            public bool failed;
            // Held as UnityEngine.Object because AssetBundle (in the split
            // AssetBundleModule assembly) is not a nameable type in VaM's
            // compiler — we only ever touch it via the request property.
            public UnityEngine.Object bundle;
            public readonly List<string> clipNames = new List<string>();
            public readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        }
        private readonly Dictionary<string, Bundle> bundles_ = new Dictionary<string, Bundle>();

        public int ClipCount    { get { return clips_.Count; } }
        public int BundleCount  { get { return bundles_.Count; } }
        public int PendingCount { get { return pending_.Count; } }

        // ---- audio files --------------------------------------------------

        // Returns a cached clip, queuing an async load on first request. Returns
        // null until the clip has streamed in (call again next frame).
        public AudioClip GetClip(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            AudioClip c;
            if (clips_.TryGetValue(path, out c))
                return c;

            for (int i = 0; i < pendingPaths_.Count; ++i)
                if (pendingPaths_[i] == path) return null;   // already streaming

            try
            {
                var nac = URLAudioClipManager.singleton.GetClip(path);
                if (nac == null)
                {
                    URLAudioClipManager.singleton.QueueFilePath(path);
                    nac = URLAudioClipManager.singleton.GetClip(path);
                }

                if (nac != null)
                {
                    if (nac.sourceClip != null)
                        clips_[path] = nac.sourceClip;
                    else
                    {
                        pending_.Add(nac);
                        pendingPaths_.Add(path);
                    }
                }
            }
            catch (Exception) { }

            clips_.TryGetValue(path, out c);
            return c;
        }

        public bool ClipReady(string path)
        {
            return !string.IsNullOrEmpty(path) && clips_.ContainsKey(path);
        }

        // ---- bundles ------------------------------------------------------

        // Queues a bundle load (cached). Safe to call every frame; only the
        // first call does work.
        public void LoadBundle(string path)
        {
            if (string.IsNullOrEmpty(path) || bundles_.ContainsKey(path))
                return;

            var b = new Bundle { path = path, ready = false };
            bundles_[path] = b;

            try
            {
                var req = new MeshVR.AssetLoader.AssetBundleFromFileRequest();
                req.path = path;
                req.callback = (r) => OnBundle(b, r);
                MeshVR.AssetLoader.QueueLoadAssetBundleFromFile(req);
            }
            catch (Exception) { b.failed = true; }
        }

        private void OnBundle(Bundle b, MeshVR.AssetLoader.AssetBundleFromFileRequest r)
        {
            try
            {
                if (r.assetBundle == null) { b.failed = true; return; }
                b.bundle = r.assetBundle;   // implicit upcast; type name never spelled

                var clips = r.assetBundle.LoadAllAssets<AudioClip>();
                if (clips != null)
                {
                    for (int i = 0; i < clips.Length; ++i)
                    {
                        b.clips[clips[i].name] = clips[i];
                        b.clipNames.Add(clips[i].name);
                    }
                    b.clipNames.Sort(StringComparer.OrdinalIgnoreCase);
                }

                b.ready = true;
            }
            catch (Exception) { b.failed = true; }
        }

        public bool BundleReady(string path)
        {
            Bundle b;
            return bundles_.TryGetValue(path, out b) && b.ready;
        }

        public AudioClip GetBundleClip(string path, string clip)
        {
            Bundle b;
            if (!bundles_.TryGetValue(path, out b) || !b.ready) return null;
            AudioClip c;
            return b.clips.TryGetValue(clip, out c) ? c : null;
        }

        // Raw bundle object (as UnityEngine.Object) for non-audio assets — the
        // seam for future host functions; callers cast as needed.
        public UnityEngine.Object GetBundle(string path)
        {
            Bundle b;
            return (bundles_.TryGetValue(path, out b) && b.ready) ? b.bundle : null;
        }

        // The names of every audio clip a loaded bundle contains, so the UI /
        // scripts can discover what's inside it. Empty until the bundle is ready.
        public List<string> BundleClipNames(string path)
        {
            Bundle b;
            return (bundles_.TryGetValue(path, out b)) ? b.clipNames : EmptyNames;
        }
        private static readonly List<string> EmptyNames = new List<string>();

        public int BundleClipCount(string path)
        {
            Bundle b;
            return (bundles_.TryGetValue(path, out b)) ? b.clipNames.Count : 0;
        }

        public bool BundleHasClip(string path, string clip)
        {
            Bundle b;
            return bundles_.TryGetValue(path, out b) && b.clips.ContainsKey(clip);
        }

        // Bundle paths currently cached (for the Assets UI panel).
        public IEnumerable<string> BundlePaths { get { return bundles_.Keys; } }
        public bool BundleFailed(string path)
        {
            Bundle b;
            return bundles_.TryGetValue(path, out b) && b.failed;
        }

        // ---- lifecycle ----------------------------------------------------

        public void Update()
        {
            if (pending_.Count == 0) return;

            for (int i = pending_.Count - 1; i >= 0; --i)
            {
                var nac = pending_[i];
                if (nac != null && nac.sourceClip != null)
                {
                    clips_[pendingPaths_[i]] = nac.sourceClip;
                    pending_.RemoveAt(i);
                    pendingPaths_.RemoveAt(i);
                }
            }
        }

        public void UnloadBundle(string path)
        {
            if (!bundles_.ContainsKey(path)) return;
            bundles_.Remove(path);
            try { MeshVR.AssetLoader.DoneWithAssetBundleFromFile(path); }
            catch (Exception) { }
        }

        public void Clear()
        {
            clips_.Clear();
            pending_.Clear();
            pendingPaths_.Clear();

            foreach (var kv in bundles_)
            {
                try { MeshVR.AssetLoader.DoneWithAssetBundleFromFile(kv.Key); }
                catch (Exception) { }
            }
            bundles_.Clear();
        }
    }
}

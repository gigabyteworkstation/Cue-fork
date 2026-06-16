using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;
using MVR.FileManagementSecure;

namespace Cue.Sound
{
    // A bank of audio clips grouped into intensity bands (3 or 5). Clips come
    // either from a folder of .wav/.ogg/.mp3 files or from an .assetbundle
    // containing AudioClips.
    //
    // Band assignment: a filename containing an explicit hint wins
    // ("soft"/"low"/"_1" -> first band, "med"/"mid" -> middle, "hard"/"high"/
    // "fast" -> last, "_2".."_5" -> that band). Files without hints are
    // distributed evenly across bands in sorted order, so a plain folder of
    // "slap01..slap12" still gives a sensible soft->hard spread.
    public class SoundSet
    {
        public const int SourceFolder = 0;
        public const int SourceBundle = 1;

        private string name_;
        private string source_ = "";
        private int    sourceType_ = SourceFolder;

        // User-defined intensity bands. Empty == no intensity grouping: every
        // clip goes into one pool and Pick() returns a random clip regardless
        // of intensity. Otherwise the count is the number of bands and the
        // names are matched against filenames to assign clips.
        private List<string> intensityNames_ = new List<string>() { "soft", "medium", "hard" };

        private List<AudioClip>[] bands_ = null;
        private List<NamedAudioClip> pending_ = null;  // folder clips still streaming in
        private List<string> pendingBandHint_ = null;  // parallel: original filename
        private bool   bundleRequested_ = false;
        private string loadError_ = "";
        private int    lastBand_ = -1;
        private int    lastIndex_ = -1;

        public SoundSet(string name)
        {
            name_ = name;
            AllocBands();
        }

        public string Name       { get { return name_; }       set { name_ = value; } }
        public string Source     { get { return source_; }     set { source_ = value; } }
        public int    SourceType { get { return sourceType_; } set { sourceType_ = value; } }
        public string LoadError  { get { return loadError_; } }

        // Number of clip pools: one per intensity name, or a single pool when no
        // intensities are configured.
        public int BandCount
        {
            get { return Mathf.Max(1, intensityNames_.Count); }
        }

        public bool HasIntensities
        {
            get { return intensityNames_.Count > 0; }
        }

        public List<string> IntensityNames
        {
            get { return intensityNames_; }
        }

        // Comma-separated for the UI: "soft, medium, hard" (blank == none).
        public string IntensityCSV
        {
            get { return string.Join(", ", intensityNames_.ToArray()); }
            set
            {
                intensityNames_ = new List<string>();
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split(',');
                    for (int i = 0; i < parts.Length; ++i)
                    {
                        var t = parts[i].Trim();
                        if (t.Length > 0)
                            intensityNames_.Add(t);
                    }
                }
                AllocBands();
            }
        }

        public int TotalClips
        {
            get
            {
                int n = 0;
                for (int i = 0; i < bands_.Length; ++i)
                    n += bands_[i].Count;
                return n;
            }
        }

        public string Status
        {
            get
            {
                if (loadError_ != "")
                    return loadError_;

                string s = "";
                for (int i = 0; i < bands_.Length; ++i)
                {
                    if (i > 0) s += "/";
                    s += bands_[i].Count.ToString();
                }

                int p = (pending_ != null) ? pending_.Count : 0;
                if (p > 0)
                    s += $" (+{p} loading)";

                return s;
            }
        }

        private void AllocBands()
        {
            int n = BandCount;
            bands_ = new List<AudioClip>[n];
            for (int i = 0; i < n; ++i)
                bands_[i] = new List<AudioClip>();
        }

        // Picks a random clip for the given 0..1 intensity, avoiding repeating
        // the previous pick when the band has more than one clip. Falls back to
        // neighbouring bands when the target band is empty.
        public AudioClip Pick(float intensity)
        {
            if (bands_ == null || bands_.Length == 0) return null;

            int n = bands_.Length;

            // Single pool (no intensities configured): pure random.
            int band = (n == 1)
                ? 0
                : Mathf.Clamp((int)(Mathf.Clamp01(intensity) * n), 0, n - 1);

            for (int tries = 0; tries < n; ++tries)
            {
                int b = band - tries;
                if (b < 0) b = band + tries;
                if (b < 0 || b >= n || bands_[b].Count == 0)
                    continue;

                var list = bands_[b];
                int i = UnityEngine.Random.Range(0, list.Count);

                if (list.Count > 1 && b == lastBand_ && i == lastIndex_)
                    i = (i + 1) % list.Count;

                lastBand_ = b;
                lastIndex_ = i;
                return list[i];
            }

            return null;
        }

        public void Reload()
        {
            AllocBands();
            pending_ = null;
            pendingBandHint_ = null;
            loadError_ = "";
            bundleRequested_ = false;

            if (string.IsNullOrEmpty(source_))
                return;

            if (sourceType_ == SourceBundle)
                LoadBundle();
            else
                LoadFolder();
        }

        private void LoadFolder()
        {
            try
            {
                var files = new List<string>();
                AddFiles(files, "*.wav");
                AddFiles(files, "*.ogg");
                AddFiles(files, "*.mp3");

                if (files.Count == 0)
                {
                    loadError_ = "no audio files in folder";
                    return;
                }

                files.Sort(StringComparer.OrdinalIgnoreCase);

                pending_ = new List<NamedAudioClip>();
                pendingBandHint_ = new List<string>();

                for (int i = 0; i < files.Count; ++i)
                {
                    string path = files[i];

                    var nac = URLAudioClipManager.singleton.GetClip(path);
                    if (nac == null)
                    {
                        URLAudioClipManager.singleton.QueueFilePath(path);
                        nac = URLAudioClipManager.singleton.GetClip(path);
                    }

                    if (nac != null)
                    {
                        pending_.Add(nac);
                        pendingBandHint_.Add(path);
                    }
                }

                if (pending_.Count == 0)
                    loadError_ = "failed to queue audio files";
            }
            catch (Exception e)
            {
                loadError_ = "folder load failed: " + e.Message;
            }
        }

        private void AddFiles(List<string> into, string pattern)
        {
            var fs = FileManagerSecure.GetFiles(source_, pattern);
            if (fs != null)
            {
                for (int i = 0; i < fs.Length; ++i)
                    into.Add(fs[i]);
            }
        }

        private void LoadBundle()
        {
            try
            {
                bundleRequested_ = true;

                var request = new MeshVR.AssetLoader.AssetBundleFromFileRequest();
                request.path = source_;
                request.callback = OnBundleLoaded;

                MeshVR.AssetLoader.QueueLoadAssetBundleFromFile(request);
            }
            catch (Exception e)
            {
                loadError_ = "bundle load failed: " + e.Message;
            }
        }

        private void OnBundleLoaded(MeshVR.AssetLoader.AssetBundleFromFileRequest request)
        {
            try
            {
                if (request.assetBundle == null)
                {
                    loadError_ = "assetbundle failed to load";
                    return;
                }

                var clips = request.assetBundle.LoadAllAssets<AudioClip>();
                if (clips == null || clips.Length == 0)
                {
                    loadError_ = "no AudioClips in bundle";
                    return;
                }

                var names = new List<string>();
                var byName = new Dictionary<string, AudioClip>();
                for (int i = 0; i < clips.Length; ++i)
                {
                    names.Add(clips[i].name);
                    byName[clips[i].name] = clips[i];
                }
                names.Sort(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < names.Count; ++i)
                    AddClip(byName[names[i]], names[i], i, names.Count);
            }
            catch (Exception e)
            {
                loadError_ = "bundle read failed: " + e.Message;
            }
        }

        // Folder clips stream in asynchronously; this drains them as their
        // AudioClip becomes available. Cheap no-op once everything's loaded.
        public void Update()
        {
            if (pending_ == null || pending_.Count == 0)
                return;

            int total = pending_.Count + TotalClips;

            for (int i = pending_.Count - 1; i >= 0; --i)
            {
                var nac = pending_[i];
                if (nac.sourceClip != null)
                {
                    AddClip(nac.sourceClip, pendingBandHint_[i], TotalClips, total);
                    pending_.RemoveAt(i);
                    pendingBandHint_.RemoveAt(i);
                }
            }

            if (pending_.Count == 0)
            {
                pending_ = null;
                pendingBandHint_ = null;
            }
        }

        private void AddClip(AudioClip clip, string name, int orderIndex, int orderTotal)
        {
            int nb = BandCount;

            // No intensities -> single pool, everything in band 0.
            if (intensityNames_.Count == 0)
            {
                bands_[0].Add(clip);
                return;
            }

            int band = GuessBand(name);

            if (band < 0)
            {
                // no hint: distribute evenly in sorted order
                if (orderTotal <= 0) orderTotal = 1;
                band = Mathf.Clamp(orderIndex * nb / orderTotal, 0, nb - 1);
            }

            bands_[band].Add(clip);
        }

        // Matches a filename against the configured intensity names (substring,
        // case-insensitive), then against _1.._N / -1..-N numeric suffixes.
        // Returns -1 when nothing matches so the caller can distribute evenly.
        private int GuessBand(string name)
        {
            string n = name.ToLowerInvariant();

            for (int i = 0; i < intensityNames_.Count; ++i)
            {
                string key = intensityNames_[i].ToLowerInvariant().Trim();
                if (key.Length > 0 && n.Contains(key))
                    return i;
            }

            for (int b = intensityNames_.Count; b >= 1; --b)
            {
                if (n.Contains("_" + b) || n.Contains("-" + b))
                    return b - 1;
            }

            return -1;
        }

        public void Destroy()
        {
            if (bundleRequested_ && !string.IsNullOrEmpty(source_))
            {
                try { MeshVR.AssetLoader.DoneWithAssetBundleFromFile(source_); }
                catch (Exception) { }
            }

            AllocBands();
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("name",       name_);
            o.Add("source",     source_);
            o.Add("sourceType", new JSONData(sourceType_));

            var arr = new JSONArray();
            for (int i = 0; i < intensityNames_.Count; ++i)
                arr.Add(new JSONData(intensityNames_[i]));
            o.Add("intensities", arr);

            return o;
        }

        public static SoundSet FromJSON(JSONClass o)
        {
            if (o == null || !o.HasKey("name"))
                return null;

            var s = new SoundSet(o["name"].Value);
            s.source_ = J.OptString(o, "source", "");
            int st = SourceFolder;
            J.OptInt(o, "sourceType", ref st);
            s.sourceType_ = st;

            if (o.HasKey("intensities"))
            {
                var names = new List<string>();
                var arr = o["intensities"].AsArray;
                if (arr != null)
                {
                    foreach (JSONNode n in arr)
                        names.Add(n.Value);
                }
                s.intensityNames_ = names;
                s.AllocBands();
            }
            else if (o.HasKey("bands"))
            {
                // back-compat: old 3/5 numeric band count
                int b = 3;
                J.OptInt(o, "bands", ref b);
                s.IntensityCSV = (b == 5)
                    ? "soft, light, medium, hard, intense"
                    : "soft, medium, hard";
            }

            s.Reload();
            return s;
        }

        public override string ToString()
        {
            return name_;
        }
    }


    // Global registry of sound sets plus the pooled positional player. Owned
    // by Cue; sets are saved with the scene (via the live saver) so the whole
    // configuration round-trips.
    public class SoundManager
    {
        private static SoundManager instance_ = null;

        private readonly List<SoundSet> sets_ = new List<SoundSet>();
        private readonly SoundPlayer player_ = new SoundPlayer();
        private float masterVolume_ = 1.0f;

        // Master switch between the legacy per-rule engine and the new sound
        // graph runtime (beta). Off by default so behaviour is unchanged until
        // the user opts in; persisted with the scene.
        private bool graphEnabled_ = false;

        public static SoundManager Instance
        {
            get
            {
                if (instance_ == null)
                    instance_ = new SoundManager();
                return instance_;
            }
        }

        public List<SoundSet> Sets        { get { return sets_; } }
        public SoundPlayer    Player      { get { return player_; } }

        public float MasterVolume
        {
            get { return masterVolume_; }
            set { masterVolume_ = U.Clamp(value, 0, 2); }
        }

        public bool GraphEnabled
        {
            get { return graphEnabled_; }
            set { graphEnabled_ = value; }
        }

        public SoundSet Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < sets_.Count; ++i)
            {
                if (sets_[i].Name == name)
                    return sets_[i];
            }

            return null;
        }

        public SoundSet Add(string name)
        {
            var s = new SoundSet(name);
            sets_.Add(s);
            return s;
        }

        public void Remove(SoundSet s)
        {
            if (s == null) return;
            s.Destroy();
            sets_.Remove(s);
        }

        // Plays a clip from `setName` at `pos` with intensity-driven volume and
        // pitch. Returns false when nothing could be played (missing set, no
        // clips yet, pool exhausted).
        public bool Play(
            string setName, UnityEngine.Vector3 pos, float intensity,
            float volumeScale, float pitchBase, float pitchJitter,
            float intensityToVolume, float velToPitch)
        {
            var set = Find(setName);
            if (set == null) return false;

            var clip = set.Pick(intensity);
            if (clip == null) return false;

            float vol = volumeScale * masterVolume_
                * Mathf.Lerp(1f, 0.35f + 0.65f * Mathf.Clamp01(intensity), intensityToVolume);

            float pitch = pitchBase
                + (UnityEngine.Random.value * 2f - 1f) * pitchJitter
                + Mathf.Clamp01(intensity) * velToPitch * 0.5f;  // velocity raises pitch

            return player_.Play(clip, pos, Mathf.Clamp(vol, 0f, 2f), pitch);
        }

        public void Update(float s)
        {
            for (int i = 0; i < sets_.Count; ++i)
                sets_[i].Update();
        }

        public void OnPluginState(bool enabled)
        {
            if (!enabled)
                player_.StopAll();
        }

        public JSONClass ToJSON()
        {
            var o = new JSONClass();
            o.Add("masterVolume", new JSONData(masterVolume_));
            o.Add("graphEnabled", new JSONData(graphEnabled_));

            var a = new JSONArray();
            for (int i = 0; i < sets_.Count; ++i)
                a.Add(sets_[i].ToJSON());
            o.Add("sets", a);

            return o;
        }

        public void Load(JSONClass o)
        {
            if (o == null) return;

            float mv = masterVolume_;
            if (J.OptFloat(o, "masterVolume", ref mv))
                masterVolume_ = mv;

            bool ge = graphEnabled_;
            if (J.OptBool(o, "graphEnabled", ref ge))
                graphEnabled_ = ge;

            for (int i = 0; i < sets_.Count; ++i)
                sets_[i].Destroy();
            sets_.Clear();

            var a = o["sets"].AsArray;
            if (a != null)
            {
                foreach (JSONNode n in a)
                {
                    var s = SoundSet.FromJSON(n.AsObject);
                    if (s != null)
                        sets_.Add(s);
                }
            }
        }
    }
}

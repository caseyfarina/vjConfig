using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectionMapper
{
    /// <summary>
    /// Serializable data for a single surface (JSON-friendly).
    /// </summary>
    [System.Serializable]
    public class SurfaceData
    {
        public string name;
        public int targetDisplay;
        public int sourceMode;
        public string sourceCameraPath;
        public int renderResolutionX;
        public int renderResolutionY;
        public float[] cornersFlat; // 8 floats: TL.x, TL.y, TR.x, TR.y, BR.x, BR.y, BL.x, BL.y
        public float cropX, cropY, cropW, cropH; // Source UV crop rect
        public float featherL, featherR, featherB, featherT; // Edge feather widths
        public int aaQuality;
        public float brightness;
        public float gamma;
        public bool enabled;

        public static SurfaceData FromSurface(ProjectionSurface s)
        {
            var d = new SurfaceData();
            d.name = s.name;
            d.targetDisplay = s.targetDisplay;
            d.sourceMode = (int)s.sourceMode;
            d.sourceCameraPath = s.sourceCameraPath;
            d.renderResolutionX = s.renderResolution.x;
            d.renderResolutionY = s.renderResolution.y;
            d.cornersFlat = new float[8];
            for (int i = 0; i < 4; i++)
            {
                d.cornersFlat[i * 2] = s.corners[i].x;
                d.cornersFlat[i * 2 + 1] = s.corners[i].y;
            }
            d.aaQuality = (int)s.aaQuality;
            d.cropX = s.sourceCropUV.x;
            d.cropY = s.sourceCropUV.y;
            d.cropW = s.sourceCropUV.width;
            d.cropH = s.sourceCropUV.height;
            d.featherL = s.edgeFeather.x;
            d.featherR = s.edgeFeather.y;
            d.featherB = s.edgeFeather.z;
            d.featherT = s.edgeFeather.w;
            d.brightness = s.brightness;
            d.gamma = s.gamma;
            d.enabled = s.enabled;
            return d;
        }

        public ProjectionSurface ToSurface()
        {
            var s = new ProjectionSurface();
            s.name = name;
            s.targetDisplay = targetDisplay;
            s.sourceMode = (SurfaceSourceMode)sourceMode;
            s.sourceCameraPath = sourceCameraPath;
            s.renderResolution = new Vector2Int(renderResolutionX, renderResolutionY);
            if (cornersFlat != null && cornersFlat.Length == 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    s.corners[i] = new Vector2(cornersFlat[i * 2], cornersFlat[i * 2 + 1]);
                }
            }
            s.aaQuality = (AAQuality)aaQuality;
            s.sourceCropUV = new Rect(cropX, cropY, cropW, cropH);
            // Handle legacy data where crop was not saved (all zeros)
            if (s.sourceCropUV.width <= 0f || s.sourceCropUV.height <= 0f)
                s.sourceCropUV = new Rect(0f, 0f, 1f, 1f);
            s.edgeFeather = new Vector4(featherL, featherR, featherB, featherT);
            s.brightness = brightness;
            s.gamma = gamma;
            s.enabled = enabled;
            s.dirty = true;
            return s;
        }
    }

    /// <summary>
    /// Serializable profile containing all surfaces.
    /// </summary>
    [System.Serializable]
    public class ProfileData
    {
        public string profileName;
        public List<SurfaceData> surfaces = new List<SurfaceData>();
    }

    /// <summary>
    /// Collection of all saved profiles.
    /// </summary>
    [System.Serializable]
    public class ProfileCollection
    {
        public string lastUsedProfile = "Default";
        public List<ProfileData> profiles = new List<ProfileData>();
    }

    /// <summary>
    /// Handles saving/loading projection mapping profiles to/from JSON files.
    /// </summary>
    public static class ProjectionPersistence
    {
        private const string FileName = "ProjectionMapper_Profiles.json";

        public static string GetFilePath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }

        /// <summary>
        /// Save all profiles to disk.
        /// </summary>
        public static void SaveCollection(ProfileCollection collection)
        {
            string json = JsonUtility.ToJson(collection, true);
            string path = GetFilePath();

            try
            {
                File.WriteAllText(path, json);
                Debug.Log($"[ProjectionMapper] Saved profiles to: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProjectionMapper] Failed to save profiles: {e.Message}");
            }
        }

        /// <summary>
        /// Load all profiles from disk. Returns a new default collection if file doesn't exist.
        /// </summary>
        public static ProfileCollection LoadCollection()
        {
            string path = GetFilePath();

            if (!File.Exists(path))
            {
                Debug.Log("[ProjectionMapper] No saved profiles found, creating defaults.");
                var collection = new ProfileCollection();
                var defaultProfile = new ProfileData { profileName = "Default" };
                collection.profiles.Add(defaultProfile);
                return collection;
            }

            try
            {
                string json = File.ReadAllText(path);
                var collection = JsonUtility.FromJson<ProfileCollection>(json);
                if (collection == null || collection.profiles == null || collection.profiles.Count == 0)
                {
                    Debug.LogWarning("[ProjectionMapper] Invalid profile data, creating defaults.");
                    collection = new ProfileCollection();
                    var defaultProfile = new ProfileData { profileName = "Default" };
                    collection.profiles.Add(defaultProfile);
                }
                Debug.Log($"[ProjectionMapper] Loaded {collection.profiles.Count} profiles from: {path}");
                return collection;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProjectionMapper] Failed to load profiles: {e.Message}");
                var collection = new ProfileCollection();
                var defaultProfile = new ProfileData { profileName = "Default" };
                collection.profiles.Add(defaultProfile);
                return collection;
            }
        }

        /// <summary>
        /// Save the current surfaces to a specific profile in the collection.
        /// </summary>
        public static void SaveProfile(ProfileCollection collection, string profileName, List<ProjectionSurface> surfaces)
        {
            ProfileData profile = collection.profiles.Find(p => p.profileName == profileName);
            if (profile == null)
            {
                profile = new ProfileData { profileName = profileName };
                collection.profiles.Add(profile);
            }

            profile.surfaces.Clear();
            foreach (var s in surfaces)
            {
                profile.surfaces.Add(SurfaceData.FromSurface(s));
            }

            collection.lastUsedProfile = profileName;
            SaveCollection(collection);
        }

        /// <summary>
        /// Load surfaces from a specific profile.
        /// </summary>
        public static List<ProjectionSurface> LoadProfile(ProfileCollection collection, string profileName)
        {
            var result = new List<ProjectionSurface>();
            ProfileData profile = collection.profiles.Find(p => p.profileName == profileName);
            if (profile == null) return result;

            foreach (var d in profile.surfaces)
            {
                result.Add(d.ToSurface());
            }

            return result;
        }
    }
}

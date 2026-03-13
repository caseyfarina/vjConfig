using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;

public static class AssignDeckVolumeProfiles
{
    public static void Execute()
    {
        AssignProfile("--- Stage A ---/LocalVolume_A",
                      "Assets/VJSystem/PresetLibraries/DeckA_VolumeProfile.asset");
        AssignProfile("--- Stage B ---/LocalVolume_B",
                      "Assets/VJSystem/PresetLibraries/DeckB_VolumeProfile.asset");

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[AssignDeckVolumeProfiles] Done.");
    }

    static void AssignProfile(string goPath, string profilePath)
    {
        var go = GameObject.Find(goPath);
        if (go == null) { Debug.LogError($"[AssignDeckVolumeProfiles] Not found: {goPath}"); return; }

        var vol = go.GetComponent<Volume>();
        if (vol == null) { Debug.LogError($"[AssignDeckVolumeProfiles] No Volume on {goPath}"); return; }

        // Load or create profile asset
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            Debug.Log($"[AssignDeckVolumeProfiles] Created profile at {profilePath}");
        }

        // Populate effects if missing
        PopulateProfile(profile, profilePath);

        // Assign shared profile and mark dirty
        vol.sharedProfile = profile;

        EditorUtility.SetDirty(go);
        Debug.Log($"[AssignDeckVolumeProfiles] Profile assigned to {goPath}");
    }

    static void PopulateProfile(VolumeProfile profile, string profilePath)
    {
        bool dirty = false;

        dirty |= EnsureEffect<Bloom>(profile, profilePath, fx =>
        {
            fx.active = false;
            fx.threshold.overrideState = true;  fx.threshold.value = 0.5f;
            fx.scatter.overrideState   = true;  fx.scatter.value   = 0.7f;
            fx.intensity.overrideState = true;  fx.intensity.value = 0f;
        });

        dirty |= EnsureEffect<ScreenSpaceLensFlare>(profile, profilePath, fx =>
        {
            fx.active = false;
            fx.intensity.overrideState               = true; fx.intensity.value               = 0f;
            fx.firstFlareIntensity.overrideState     = true; fx.firstFlareIntensity.value     = 0f;
            fx.secondaryFlareIntensity.overrideState = true; fx.secondaryFlareIntensity.value = 0f;
            fx.warpedFlareIntensity.overrideState    = true; fx.warpedFlareIntensity.value    = 0f;
            fx.streaksIntensity.overrideState        = true; fx.streaksIntensity.value        = 0f;
        });

        if (dirty) EditorUtility.SetDirty(profile);
    }

    static bool EnsureEffect<T>(VolumeProfile profile, string profilePath,
                                 System.Action<T> configure)
        where T : VolumeComponent
    {
        // If already present, just reconfigure it
        if (profile.TryGet<T>(out var existing))
        {
            configure(existing);
            EditorUtility.SetDirty(existing);
            return true;
        }

        var fx = profile.Add<T>(overrides: true);
        configure(fx);
        AssetDatabase.AddObjectToAsset(fx, profilePath);
        return true;
    }
}

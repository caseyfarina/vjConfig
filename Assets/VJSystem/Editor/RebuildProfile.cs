using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RebuildProfile
{
    public static void Execute()
    {
        const string profilePath = "Assets/Settings/VJ_VolumeProfile.asset";

        // Delete and recreate
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);

        // Add built-in overrides AND save as sub-assets
        AddAndSave<DepthOfField>(profile, profilePath, comp =>
        {
            comp.mode.Override(DepthOfFieldMode.Bokeh);
            comp.focusDistance.Override(10f);
        });

        AddAndSave<Bloom>(profile, profilePath, comp =>
        {
            comp.intensity.Override(0.5f);
            comp.threshold.Override(1f);
        });

        AddAndSave<Vignette>(profile, profilePath, comp =>
        {
            comp.intensity.Override(0.3f);
        });

        // Custom volume types
        var asm = System.Reflection.Assembly.Load("Assembly-CSharp");

        AddCustomVolume(profile, profilePath, asm, "VJSystem.PixelSortVolume");
        AddCustomVolume(profile, profilePath, asm, "VJSystem.ChromaticDisplacementVolume");

        // Save
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // Verify
        profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        Debug.Log($"[RebuildProfile] Component count: {profile.components.Count}");
        foreach (var c in profile.components)
            Debug.Log($"[RebuildProfile]   {c.GetType().Name} active={c.active} isSub={AssetDatabase.IsSubAsset(c)}");

        Debug.Log($"[RebuildProfile] TryGet<DepthOfField>: {profile.TryGet<DepthOfField>(out _)}");
        Debug.Log($"[RebuildProfile] TryGet<Bloom>: {profile.TryGet<Bloom>(out _)}");
        Debug.Log($"[RebuildProfile] TryGet<Vignette>: {profile.TryGet<Vignette>(out _)}");

        // Verify total sub-assets
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(profilePath);
        Debug.Log($"[RebuildProfile] Total objects at path: {allAssets.Length}");

        // Re-assign to volume
        var volumeGO = GameObject.Find("Global Volume");
        if (volumeGO != null)
        {
            var volume = volumeGO.GetComponent<Volume>();
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[RebuildProfile] Done.");
    }

    static void AddAndSave<T>(VolumeProfile profile, string profilePath, System.Action<T> configure = null)
        where T : VolumeComponent
    {
        // Create the component as a ScriptableObject
        var comp = ScriptableObject.CreateInstance<T>();
        comp.name = typeof(T).Name;
        comp.active = true;

        // Enable all overrides
        foreach (var param in comp.parameters)
            param.overrideState = true;

        // Configure specific values
        configure?.Invoke(comp);

        // Add to profile's component list
        profile.components.Add(comp);

        // Save as sub-asset so it persists on disk
        AssetDatabase.AddObjectToAsset(comp, profilePath);

        Debug.Log($"[RebuildProfile] Added {typeof(T).Name}");
    }

    static void AddCustomVolume(VolumeProfile profile, string profilePath, System.Reflection.Assembly asm, string typeName)
    {
        var type = asm?.GetType(typeName);
        if (type == null) { Debug.LogWarning($"[RebuildProfile] Type not found: {typeName}"); return; }

        var comp = (VolumeComponent)ScriptableObject.CreateInstance(type);
        comp.name = type.Name;
        foreach (var param in comp.parameters) param.overrideState = true;
        profile.components.Add(comp);
        AssetDatabase.AddObjectToAsset(comp, profilePath);
        Debug.Log($"[RebuildProfile] Added {type.Name}");
    }
}

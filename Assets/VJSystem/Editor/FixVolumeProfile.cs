using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FixVolumeProfile
{
    public static void Execute()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/VJ_VolumeProfile.asset");
        if (profile == null) { Debug.LogError("[Fix] VJ_VolumeProfile.asset not found"); return; }

        // Use profile.Add<T>() which properly creates sub-assets
        if (!profile.Has<DepthOfField>())
        {
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(10f);
            Debug.Log("[Fix] Added DepthOfField");
        }

        if (!profile.Has<Bloom>())
        {
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.5f);
            bloom.threshold.Override(1f);
            Debug.Log("[Fix] Added Bloom");
        }

        if (!profile.Has<Vignette>())
        {
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            Debug.Log("[Fix] Added Vignette");
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // Verify
        Debug.Log($"[Fix] Profile now has {profile.components.Count} components:");
        foreach (var comp in profile.components)
            Debug.Log($"  {comp.GetType().Name} active={comp.active}");

        if (profile.TryGet<DepthOfField>(out var d))
            Debug.Log($"[Fix] TryGet<DepthOfField> OK: mode={d.mode.value}");
        else
            Debug.LogError("[Fix] TryGet<DepthOfField> STILL FAILED");

        // Fix Volume reference in scene
        var volumeGO = GameObject.Find("Global Volume");
        if (volumeGO != null)
        {
            var volume = volumeGO.GetComponent<Volume>();
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log("[Fix] Done.");
    }
}

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class DebugVolumeProfile
{
    public static void Execute()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/VJ_VolumeProfile.asset");
        if (profile == null) { Debug.LogError("Profile not found"); return; }

        Debug.Log($"[Debug] Profile has {profile.components.Count} components:");
        foreach (var comp in profile.components)
        {
            Debug.Log($"  Type: {comp.GetType().FullName}, Active: {comp.active}, Name: {comp.name}");
        }

        // Check sub-assets
        var subAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/VJ_VolumeProfile.asset");
        Debug.Log($"[Debug] Sub-assets count: {subAssets.Length}");
        foreach (var sub in subAssets)
        {
            Debug.Log($"  SubAsset: {sub.GetType().FullName} name={sub.name}");
        }

        // Try TryGet
        if (profile.TryGet<DepthOfField>(out var dof))
            Debug.Log($"[Debug] TryGet<DepthOfField> SUCCESS: mode={dof.mode.value}");
        else
            Debug.LogError("[Debug] TryGet<DepthOfField> FAILED");

        if (profile.TryGet<Bloom>(out var bloom))
            Debug.Log($"[Debug] TryGet<Bloom> SUCCESS: intensity={bloom.intensity.value}");
        else
            Debug.LogError("[Debug] TryGet<Bloom> FAILED");
    }
}

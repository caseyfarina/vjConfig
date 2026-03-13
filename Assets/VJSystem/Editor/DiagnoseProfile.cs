using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class DiagnoseProfile
{
    public static void Execute()
    {
        var profilePath = "Assets/Settings/VJ_VolumeProfile.asset";

        // Load the asset
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null) { Debug.LogError("[Diag] Profile not found!"); return; }

        Debug.Log($"[Diag] Profile: {profile.name}, instanceID={profile.GetInstanceID()}");
        Debug.Log($"[Diag] Component count: {profile.components.Count}");

        for (int i = 0; i < profile.components.Count; i++)
        {
            var c = profile.components[i];
            if (c == null)
            {
                Debug.LogError($"[Diag]   [{i}] NULL component!");
                continue;
            }
            Debug.Log($"[Diag]   [{i}] {c.GetType().FullName} name='{c.name}' active={c.active} instanceID={c.GetInstanceID()}");

            // Check if it's a proper sub-asset
            var assetPath = AssetDatabase.GetAssetPath(c);
            Debug.Log($"[Diag]        assetPath='{assetPath}' isSubAsset={AssetDatabase.IsSubAsset(c)}");
        }

        // Check all sub-assets on this profile
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(profilePath);
        Debug.Log($"[Diag] Total objects at path: {subAssets.Length}");
        foreach (var sub in subAssets)
        {
            if (sub == null)
            {
                Debug.LogError("[Diag]   NULL sub-asset");
                continue;
            }
            Debug.Log($"[Diag]   sub: {sub.GetType().Name} name='{sub.name}' instanceID={sub.GetInstanceID()} isMain={AssetDatabase.IsMainAsset(sub)} isSub={AssetDatabase.IsSubAsset(sub)}");
        }

        // TryGet tests
        Debug.Log($"[Diag] TryGet<DepthOfField>: {profile.TryGet<DepthOfField>(out var dof)} (obj={dof})");
        Debug.Log($"[Diag] TryGet<Bloom>: {profile.TryGet<Bloom>(out var bloom)} (obj={bloom})");
        Debug.Log($"[Diag] TryGet<Vignette>: {profile.TryGet<Vignette>(out var vig)} (obj={vig})");

        // Check the Global Volume reference
        var volumeGO = GameObject.Find("Global Volume");
        if (volumeGO != null)
        {
            var volume = volumeGO.GetComponent<Volume>();
            Debug.Log($"[Diag] Volume.sharedProfile: {volume.sharedProfile} instanceID={volume.sharedProfile?.GetInstanceID()}");
            Debug.Log($"[Diag] Same asset? {volume.sharedProfile == profile}");

            if (volume.sharedProfile != null)
            {
                Debug.Log($"[Diag] Volume profile component count: {volume.sharedProfile.components.Count}");
                for (int i = 0; i < volume.sharedProfile.components.Count; i++)
                {
                    var c = volume.sharedProfile.components[i];
                    Debug.Log($"[Diag]   Volume[{i}]: {(c == null ? "NULL" : c.GetType().Name)} isNull={c == null}");
                }
            }
        }
    }
}

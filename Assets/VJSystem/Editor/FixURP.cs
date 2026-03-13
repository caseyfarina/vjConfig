using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FixURP
{
    public static void Execute()
    {
        // Fix Renderer — assign PostProcessData
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VJ_Renderer.asset");
        if (renderer == null) { Debug.LogError("[FixURP] Renderer not found"); return; }

        var rso = new SerializedObject(renderer);
        rso.Update();
        var postProcessProp = rso.FindProperty("m_PostProcessData");

        if (postProcessProp != null && postProcessProp.objectReferenceValue == null)
        {
            // Find the default PostProcessData asset that ships with URP
            var guids = AssetDatabase.FindAssets("t:PostProcessData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Debug.Log($"[FixURP] Found PostProcessData at: {path}");
                var ppData = AssetDatabase.LoadAssetAtPath<PostProcessData>(path);
                if (ppData != null)
                {
                    postProcessProp.objectReferenceValue = ppData;
                    Debug.Log($"[FixURP] Assigned PostProcessData from {path}");
                    break;
                }
            }
        }
        else if (postProcessProp != null)
        {
            Debug.Log($"[FixURP] PostProcessData already set: {postProcessProp.objectReferenceValue}");
        }

        rso.ApplyModifiedProperties();
        EditorUtility.SetDirty(renderer);

        // Also rebuild the volume profile clean
        RebuildVolumeProfile();

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[FixURP] Done");
    }

    static void RebuildVolumeProfile()
    {
        var profilePath = "Assets/Settings/VJ_VolumeProfile.asset";

        // Delete and recreate to avoid ghost sub-assets
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);

        // Add all overrides using profile.Add<T>() which properly handles sub-assets
        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(10f);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.5f);
        bloom.threshold.Override(1f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.3f);

        // Add custom volume types via reflection
        var asm = System.Reflection.Assembly.Load("Assembly-CSharp");

        var psType = asm.GetType("VJSystem.PixelSortVolume");
        if (psType != null)
        {
            var ps = (VolumeComponent)ScriptableObject.CreateInstance(psType);
            ps.name = psType.Name;
            foreach (var param in ps.parameters) param.overrideState = true;
            profile.components.Add(ps);
            AssetDatabase.AddObjectToAsset(ps, profile);
            Debug.Log("[FixURP] Added PixelSortVolume");
        }

        var cdType = asm.GetType("VJSystem.ChromaticDisplacementVolume");
        if (cdType != null)
        {
            var cd = (VolumeComponent)ScriptableObject.CreateInstance(cdType);
            cd.name = cdType.Name;
            foreach (var param in cd.parameters) param.overrideState = true;
            profile.components.Add(cd);
            AssetDatabase.AddObjectToAsset(cd, profile);
            Debug.Log("[FixURP] Added ChromaticDisplacementVolume");
        }

        EditorUtility.SetDirty(profile);

        // Re-assign to Global Volume
        var volumeGO = GameObject.Find("Global Volume");
        if (volumeGO != null)
        {
            var volume = volumeGO.GetComponent<Volume>();
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
        }

        Debug.Log($"[FixURP] Rebuilt volume profile with {profile.components.Count} overrides");
        foreach (var c in profile.components)
            Debug.Log($"  {c.GetType().Name} active={c.active}");

        // Verify
        if (profile.TryGet<DepthOfField>(out _))
            Debug.Log("[FixURP] TryGet<DepthOfField> OK");
        else
            Debug.LogError("[FixURP] TryGet<DepthOfField> FAILED");
    }
}

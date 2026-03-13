using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RebuildPipeline
{
    public static void Execute()
    {
        const string rendererPath = "Assets/Settings/VJ_Renderer.asset";
        const string pipelinePath = "Assets/Settings/VJ_URPAsset.asset";
        const string profilePath  = "Assets/Settings/VJ_VolumeProfile.asset";

        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");

        // ── Step 1: Delete existing assets ──
        AssetDatabase.DeleteAsset(profilePath);
        AssetDatabase.DeleteAsset(pipelinePath);
        AssetDatabase.DeleteAsset(rendererPath);
        AssetDatabase.Refresh();
        Debug.Log("[Rebuild] Deleted old assets");

        // ── Step 2: Create renderer via proper path ──
        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(renderer, rendererPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Reload to get the persisted asset
        renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);

        // Check PostProcessData
        var rso = new SerializedObject(renderer);
        rso.Update();
        var ppProp = rso.FindProperty("m_PostProcessData");
        Debug.Log($"[Rebuild] PostProcessData after CreateInstance: {ppProp?.objectReferenceValue}");

        // If PostProcessData is still null, force-load it
        if (ppProp != null && ppProp.objectReferenceValue == null)
        {
            // Try direct path first
            var ppData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");

            if (ppData == null)
            {
                // Fallback: search
                var guids = AssetDatabase.FindAssets("t:PostProcessData");
                Debug.Log($"[Rebuild] FindAssets found {guids.Length} PostProcessData assets");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    Debug.Log($"[Rebuild]   Found: {path}");
                    ppData = AssetDatabase.LoadAssetAtPath<PostProcessData>(path);
                    if (ppData != null) break;
                }
            }

            if (ppData != null)
            {
                ppProp.objectReferenceValue = ppData;
                rso.ApplyModifiedProperties();
                EditorUtility.SetDirty(renderer);
                Debug.Log($"[Rebuild] Assigned PostProcessData: {ppData.name}");
            }
            else
            {
                Debug.LogWarning("[Rebuild] Could not find PostProcessData asset anywhere!");
            }
        }

        // ── Step 3: Create pipeline asset ──
        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        AssetDatabase.CreateAsset(pipeline, pipelinePath);
        EditorUtility.SetDirty(pipeline);
        Debug.Log("[Rebuild] Created pipeline asset");

        // Assign to graphics settings
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        Debug.Log("[Rebuild] Assigned pipeline to Graphics + Quality settings");

        // ── Step 4: Create fresh volume profile ──
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);

        // Built-in overrides
        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(10f);
        Debug.Log("[Rebuild] Added DepthOfField");

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.5f);
        bloom.threshold.Override(1f);
        Debug.Log("[Rebuild] Added Bloom");

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.3f);
        Debug.Log("[Rebuild] Added Vignette");

        // Custom volume types via reflection
        var asm = System.Reflection.Assembly.Load("Assembly-CSharp");

        var psType = asm?.GetType("VJSystem.PixelSortVolume");
        if (psType != null)
        {
            var ps = (VolumeComponent)ScriptableObject.CreateInstance(psType);
            ps.name = psType.Name;
            foreach (var param in ps.parameters) param.overrideState = true;
            profile.components.Add(ps);
            AssetDatabase.AddObjectToAsset(ps, profile);
            Debug.Log("[Rebuild] Added PixelSortVolume");
        }

        var cdType = asm?.GetType("VJSystem.ChromaticDisplacementVolume");
        if (cdType != null)
        {
            var cd = (VolumeComponent)ScriptableObject.CreateInstance(cdType);
            cd.name = cdType.Name;
            foreach (var param in cd.parameters) param.overrideState = true;
            profile.components.Add(cd);
            AssetDatabase.AddObjectToAsset(cd, profile);
            Debug.Log("[Rebuild] Added ChromaticDisplacementVolume");
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // ── Step 5: Verify profile ──
        Debug.Log($"[Rebuild] Profile has {profile.components.Count} components:");
        foreach (var c in profile.components)
            Debug.Log($"  {c.GetType().Name} active={c.active}");

        if (profile.TryGet<DepthOfField>(out var d))
            Debug.Log($"[Rebuild] TryGet<DepthOfField> OK — mode={d.mode.value}");
        else
            Debug.LogError("[Rebuild] TryGet<DepthOfField> FAILED");

        if (profile.TryGet<Bloom>(out var b))
            Debug.Log($"[Rebuild] TryGet<Bloom> OK — intensity={b.intensity.value}");
        else
            Debug.LogError("[Rebuild] TryGet<Bloom> FAILED");

        if (profile.TryGet<Vignette>(out _))
            Debug.Log("[Rebuild] TryGet<Vignette> OK");
        else
            Debug.LogError("[Rebuild] TryGet<Vignette> FAILED");

        // ── Step 6: Assign to scene volume + enable post-processing on camera ──
        var volumeGO = GameObject.Find("Global Volume");
        if (volumeGO != null)
        {
            var volume = volumeGO.GetComponent<Volume>();
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
            Debug.Log("[Rebuild] Assigned profile to Global Volume");
        }
        else
        {
            Debug.LogWarning("[Rebuild] Global Volume not found in scene!");
        }

        // Enable post-processing on main camera
        var cam = Camera.main;
        if (cam != null)
        {
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            EditorUtility.SetDirty(cam);
            Debug.Log("[Rebuild] Enabled renderPostProcessing on Main Camera");
        }
        else
        {
            Debug.LogWarning("[Rebuild] Main Camera not found!");
        }

        // ── Step 7: Save everything ──
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Rebuild] All saved. Pipeline rebuild complete.");
    }
}

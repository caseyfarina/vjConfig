using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

public static class SetVisibleDefaults
{
    public static void Execute()
    {
        // ── Step 1: Check renderer features ──
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VJ_Renderer.asset");
        if (renderer == null) { Debug.LogError("[Defaults] Renderer not found"); return; }

        var so = new SerializedObject(renderer);
        so.Update();
        var featuresProp = so.FindProperty("m_RendererFeatures");
        Debug.Log($"[Defaults] Renderer has {featuresProp.arraySize} features");
        for (int i = 0; i < featuresProp.arraySize; i++)
        {
            var f = featuresProp.GetArrayElementAtIndex(i).objectReferenceValue;
            Debug.Log($"[Defaults]   Feature[{i}]: {(f != null ? f.GetType().Name : "NULL")}");
        }

        // Add missing renderer features
        var asm = Assembly.Load("Assembly-CSharp");
        bool changed = false;

        var psFeatureType = asm.GetType("VJSystem.PixelSortFeature");
        var cdFeatureType = asm.GetType("VJSystem.ChromaticDisplacementFeature");

        bool hasPixelSort = false;
        bool hasChromatic = false;
        for (int i = 0; i < featuresProp.arraySize; i++)
        {
            var f = featuresProp.GetArrayElementAtIndex(i).objectReferenceValue;
            if (f != null)
            {
                if (f.GetType() == psFeatureType) hasPixelSort = true;
                if (f.GetType() == cdFeatureType) hasChromatic = true;
            }
        }

        if (!hasPixelSort && psFeatureType != null)
        {
            var feature = ScriptableObject.CreateInstance(psFeatureType) as ScriptableRendererFeature;
            feature.name = "PixelSortFeature";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;

            var mapProp = so.FindProperty("m_RendererFeatureMap");
            mapProp.arraySize++;
            mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = feature.GetInstanceID();

            changed = true;
            Debug.Log("[Defaults] Added PixelSortFeature to renderer");
        }
        else if (hasPixelSort)
        {
            Debug.Log("[Defaults] PixelSortFeature already on renderer");
        }

        if (!hasChromatic && cdFeatureType != null)
        {
            var feature = ScriptableObject.CreateInstance(cdFeatureType) as ScriptableRendererFeature;
            feature.name = "ChromaticDisplacementFeature";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;

            var mapProp = so.FindProperty("m_RendererFeatureMap");
            mapProp.arraySize++;
            mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = feature.GetInstanceID();

            changed = true;
            Debug.Log("[Defaults] Added ChromaticDisplacementFeature to renderer");
        }
        else if (hasChromatic)
        {
            Debug.Log("[Defaults] ChromaticDisplacementFeature already on renderer");
        }

        if (changed)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        // ── Step 2: Set visible volume defaults ──
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/VJ_VolumeProfile.asset");
        if (profile == null) { Debug.LogError("[Defaults] Profile not found"); return; }

        // PixelSort — set strength to visible value
        foreach (var comp in profile.components)
        {
            if (comp.GetType().Name == "PixelSortVolume")
            {
                var strengthField = comp.GetType().GetField("strength");
                if (strengthField != null)
                {
                    var param = strengthField.GetValue(comp) as ClampedFloatParameter;
                    param.Override(0.6f);
                    Debug.Log($"[Defaults] PixelSort strength = {param.value}");
                }

                var threshLowField = comp.GetType().GetField("thresholdLow");
                if (threshLowField != null)
                {
                    var param = threshLowField.GetValue(comp) as ClampedFloatParameter;
                    param.Override(0.25f);
                    Debug.Log($"[Defaults] PixelSort thresholdLow = {param.value}");
                }

                var threshHighField = comp.GetType().GetField("thresholdHigh");
                if (threshHighField != null)
                {
                    var param = threshHighField.GetValue(comp) as ClampedFloatParameter;
                    param.Override(0.75f);
                    Debug.Log($"[Defaults] PixelSort thresholdHigh = {param.value}");
                }

                EditorUtility.SetDirty(comp);
            }

            if (comp.GetType().Name == "ChromaticDisplacementVolume")
            {
                var amountField = comp.GetType().GetField("displacementAmount");
                if (amountField != null)
                {
                    var param = amountField.GetValue(comp) as ClampedFloatParameter;
                    param.Override(0.04f); // Visible but not extreme
                    Debug.Log($"[Defaults] ChromaticDisplacement amount = {param.value}");
                }

                var scaleField = comp.GetType().GetField("displacementScale");
                if (scaleField != null)
                {
                    var param = scaleField.GetValue(comp) as ClampedFloatParameter;
                    param.Override(3f);
                    Debug.Log($"[Defaults] ChromaticDisplacement scale = {param.value}");
                }

                var blurField = comp.GetType().GetField("blurRadius");
                if (blurField != null)
                {
                    var param = blurField.GetValue(comp) as ClampedFloatParameter;
                    param.Override(4f);
                    Debug.Log($"[Defaults] ChromaticDisplacement blurRadius = {param.value}");
                }

                // Channel A (red) pushes right, Channel C (blue) pushes left
                var chAField = comp.GetType().GetField("channelAAmount");
                if (chAField != null)
                {
                    var param = chAField.GetValue(comp) as ClampedFloatParameter;
                    param.Override(1.5f);
                }
                var chCField = comp.GetType().GetField("channelCAmount");
                if (chCField != null)
                {
                    var param = chCField.GetValue(comp) as ClampedFloatParameter;
                    param.Override(-1.5f);
                }

                EditorUtility.SetDirty(comp);
            }
        }

        // Also bump up Bloom for visual pop
        if (profile.TryGet<Bloom>(out var bloom))
        {
            bloom.intensity.Override(1.5f);
            bloom.threshold.Override(0.8f);
            bloom.scatter.Override(0.7f);
            Debug.Log("[Defaults] Bloom intensity=1.5, threshold=0.8, scatter=0.7");
        }

        // DepthOfField — make it clearly visible
        if (profile.TryGet<DepthOfField>(out var dof))
        {
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(8f);
            dof.focalLength.Override(50f);
            dof.aperture.Override(2.8f);
            Debug.Log("[Defaults] DoF focusDist=8, focalLen=50, aperture=2.8");
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Defaults] Done. Enter play mode to see effects.");
    }
}

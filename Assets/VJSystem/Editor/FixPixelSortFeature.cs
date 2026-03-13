using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using System.Reflection;

public static class FixPixelSortFeature
{
    public static void Execute()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VJ_Renderer.asset");
        if (renderer == null) { Debug.LogError("[FixPS] Renderer not found"); return; }

        // Load the compute shader
        var computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/VJSystem/Shaders/PixelSort.compute");
        Debug.Log($"[FixPS] ComputeShader: {computeShader}");
        if (computeShader == null) { Debug.LogError("[FixPS] PixelSort.compute not found!"); return; }

        // Find the PixelSortFeature in the renderer's sub-assets
        var allAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/VJ_Renderer.asset");
        Debug.Log($"[FixPS] Renderer has {allAssets.Length} total objects");

        ScriptableRendererFeature pixelSortFeature = null;
        foreach (var asset in allAssets)
        {
            Debug.Log($"[FixPS]   {asset.GetType().Name}: {asset.name}");
            if (asset.GetType().Name == "PixelSortFeature")
            {
                pixelSortFeature = asset as ScriptableRendererFeature;
            }
        }

        if (pixelSortFeature == null)
        {
            Debug.LogError("[FixPS] PixelSortFeature not found on renderer!");
            return;
        }

        // Use SerializedObject to set the compute shader field
        var so = new SerializedObject(pixelSortFeature);
        so.Update();

        // List all properties to find the right field name
        var iter = so.GetIterator();
        iter.Next(true);
        do
        {
            Debug.Log($"[FixPS]   prop: {iter.name} ({iter.propertyType})");
        } while (iter.Next(false));

        var csProp = so.FindProperty("m_ComputeShader");
        if (csProp != null)
        {
            Debug.Log($"[FixPS] Current m_ComputeShader: {csProp.objectReferenceValue}");
            csProp.objectReferenceValue = computeShader;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(pixelSortFeature);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FixPS] Assigned compute shader: {computeShader.name}");
        }
        else
        {
            Debug.LogError("[FixPS] m_ComputeShader property not found!");
        }

        // Verify
        so.Update();
        var verify = so.FindProperty("m_ComputeShader");
        Debug.Log($"[FixPS] Verify m_ComputeShader: {verify?.objectReferenceValue}");
        Debug.Log("[FixPS] Done");
    }
}

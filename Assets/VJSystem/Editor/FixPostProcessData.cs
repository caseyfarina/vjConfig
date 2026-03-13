using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

public static class FixPostProcessData
{
    public static void Execute()
    {
        var rendererPath = "Assets/Settings/VJ_Renderer.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null) { Debug.LogError("[FixPPD] Renderer not found"); return; }

        // Load PostProcessData from URP package
        var ppData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        Debug.Log($"[FixPPD] PostProcessData loaded: {ppData}");

        if (ppData == null) { Debug.LogError("[FixPPD] PostProcessData not found!"); return; }

        // Assign via SerializedObject
        var so = new SerializedObject(renderer);
        so.Update();

        // Log all properties to find the right field name
        var iter = so.GetIterator();
        iter.Next(true);
        do
        {
            if (iter.name.Contains("ostProcess") || iter.name.Contains("postProcess") || iter.name.Contains("PostProcess"))
                Debug.Log($"[FixPPD] Property: {iter.name} = {iter.propertyType} val={iter.objectReferenceValue}");
        } while (iter.Next(iter.hasChildren));

        // Try known field names
        string[] fieldNames = { "m_PostProcessData", "postProcessData", "m_postProcessData" };
        foreach (var fieldName in fieldNames)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                Debug.Log($"[FixPPD] Found property '{fieldName}', current value: {prop.objectReferenceValue}");
                prop.objectReferenceValue = ppData;
                Debug.Log($"[FixPPD] Set '{fieldName}' to: {ppData}");
            }
            else
            {
                Debug.Log($"[FixPPD] Property '{fieldName}' not found on renderer");
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();

        // Verify
        var verify = new SerializedObject(renderer);
        verify.Update();
        var verifyProp = verify.FindProperty("m_PostProcessData");
        Debug.Log($"[FixPPD] After save, m_PostProcessData = {verifyProp?.objectReferenceValue}");
        Debug.Log($"[FixPPD] renderer.postProcessData = {renderer.postProcessData}");
        Debug.Log("[FixPPD] Done");
    }
}

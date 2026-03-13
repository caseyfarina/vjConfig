using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

public static class FixPostProcess
{
    public static void Execute()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VJ_Renderer.asset");
        if (renderer == null) { Debug.LogError("[Fix] Renderer not found"); return; }

        var rso = new SerializedObject(renderer);
        rso.Update();
        var ppProp = rso.FindProperty("m_PostProcessData");

        if (ppProp != null && ppProp.objectReferenceValue == null)
        {
            // Load from URP package path
            var ppData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (ppData != null)
            {
                ppProp.objectReferenceValue = ppData;
                rso.ApplyModifiedProperties();
                EditorUtility.SetDirty(renderer);
                AssetDatabase.SaveAssets();
                Debug.Log("[Fix] Assigned PostProcessData to renderer");
            }
            else
            {
                Debug.LogError("[Fix] PostProcessData.asset not found in URP package");
            }
        }
        else
        {
            Debug.Log($"[Fix] PostProcessData already assigned: {ppProp?.objectReferenceValue}");
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class IncludeShadersInBuild
{
    public static void Execute()
    {
        string[] shaderPaths = {
            "Assets/com.projectionmapper/Runtime/Shaders/HomographyWarp.shader",
            "Assets/com.projectionmapper/Runtime/Shaders/DebugGrid.shader"
        };

        var graphicsSettings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        var so = new SerializedObject(graphicsSettings);
        var arrayProp = so.FindProperty("m_AlwaysIncludedShaders");

        foreach (string path in shaderPaths)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                Debug.LogWarning($"[IncludeShaders] Shader not found at {path}");
                continue;
            }

            // Check if already included
            bool found = false;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                int idx = arrayProp.arraySize;
                arrayProp.InsertArrayElementAtIndex(idx);
                arrayProp.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
                Debug.Log($"[IncludeShaders] Added {shader.name} to Always Included Shaders");
            }
            else
            {
                Debug.Log($"[IncludeShaders] {shader.name} already included");
            }
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[IncludeShaders] Done.");
    }
}

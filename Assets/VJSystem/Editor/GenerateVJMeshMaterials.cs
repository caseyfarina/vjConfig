using UnityEngine;
using UnityEditor;
using System.IO;

public static class GenerateVJMeshMaterials
{
    [MenuItem("VJSystem/Generate VJ Mesh Materials")]
    public static void Execute()
    {
        const string folder = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        // 10 materials: indices 0-4 non-emissive, 5-9 emissive
        // Greyscale values spread across the range for variety
        float[] greys      = { 0.08f, 0.22f, 0.45f, 0.68f, 0.88f,
                                0.15f, 0.35f, 0.55f, 0.75f, 0.95f };
        float[] metallics  = { 0.9f,  0.1f,  0.6f,  0.0f,  0.8f,
                                0.4f,  0.95f, 0.2f,  0.7f,  0.05f };
        float[] smoothness = { 0.3f,  0.8f,  0.5f,  0.15f, 0.95f,
                                0.6f,  0.2f,  0.85f, 0.4f,  0.75f };
        float[] emissionIntensity = { 0f, 0f, 0f, 0f, 0f,
                                       3f, 5f, 2f, 8f, 4f };

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[GenerateVJMeshMaterials] URP Lit shader not found.");
            return;
        }

        for (int i = 0; i < 10; i++)
        {
            string name = $"VJMesh_Mat_{i:D2}";
            string path = $"{folder}/{name}.mat";

            // Overwrite if exists
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var mat = existing != null ? existing : new Material(shader);

            float g = greys[i];
            mat.shader = shader;
            mat.SetColor("_BaseColor", new Color(g, g, g, 1f));
            mat.SetFloat("_Metallic",   metallics[i]);
            mat.SetFloat("_Smoothness", smoothness[i]);

            bool emissive = emissionIntensity[i] > 0f;
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(g, g, g, 1f) * emissionIntensity[i]);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            if (existing == null)
                AssetDatabase.CreateAsset(mat, path);
            else
                EditorUtility.SetDirty(mat);

            Debug.Log($"[GenerateVJMeshMaterials] {name} — grey={g:F2}  metal={metallics[i]:F2}  smooth={smoothness[i]:F2}  emissive={emissive}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GenerateVJMeshMaterials] Done — 10 materials written to Assets/Materials/");
    }
}

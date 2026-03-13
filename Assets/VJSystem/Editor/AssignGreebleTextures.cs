using UnityEngine;
using UnityEditor;

/// <summary>
/// Randomly assigns one of the three Greeble texture sets to each VJMesh material,
/// mapping each texture to its correct URP Lit channel.
/// Run after GenerateVJMeshMaterials.
/// </summary>
public static class AssignGreebleTextures
{
    const string TexturePath = "Assets/AssetPacks/Greeble_Materials/Textures";

    struct TextureSet
    {
        public string name;
        public string diffuse;
        public string normal;
        public string metallic;   // null if not in set
        public string occlusion;
        public string emissive;   // null if not in set
        public string height;
    }

    static readonly TextureSet[] Sets =
    {
        new TextureSet
        {
            name      = "Material_1",
            diffuse   = "Greeble_Material_1_Diffuse",
            normal    = "Greeble_Material_1_Normal",
            metallic  = "Greeble_Material_1_Metalilc",
            occlusion = "Greeble_Material_1_Occlusion",
            emissive  = null,
            height    = "Greeble_Material_1_Height",
        },
        new TextureSet
        {
            name      = "material_2",
            diffuse   = "Greeble_material_2_Diffuse",
            normal    = "Greeble_material_2_Normal",
            metallic  = "Greeble_material_2_Metal",
            occlusion = "Greeble_material_2_Occlusion",
            emissive  = "Greeble_material_2_Emissive",
            height    = "Greeble_material_2_Height",
        },
        new TextureSet
        {
            name      = "Windows",
            diffuse   = "Greeble_Materials_Windows_Diffuse",
            normal    = "Greeble_Materials_Windows_Normal",
            metallic  = null,
            occlusion = "Greeble_Materials_Windows_Occlusion",
            emissive  = "Greeble_Materials_Windows_Emissive",
            height    = "Greeble_Materials_Windows_Height",
        },
    };

    [MenuItem("VJSystem/Assign Greeble Textures")]
    public static void Execute()
    {
        var rng = new System.Random(42); // fixed seed for reproducibility

        for (int i = 0; i < 10; i++)
        {
            string matPath = $"Assets/Materials/VJMesh_Mat_{i:D2}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Debug.LogWarning($"[AssignGreebleTextures] Material not found: {matPath}");
                continue;
            }

            var set = Sets[rng.Next(Sets.Length)];
            ApplySet(mat, set);
            EditorUtility.SetDirty(mat);
            Debug.Log($"[AssignGreebleTextures] VJMesh_Mat_{i:D2} → {set.name}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AssignGreebleTextures] Done.");
    }

    static void ApplySet(Material mat, TextureSet set)
    {
        // Albedo
        SetTex(mat, "_BaseMap", set.diffuse);

        // Normal map
        if (set.normal != null)
        {
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1f);
            SetTex(mat, "_BumpMap", set.normal);
        }

        // Metallic / smoothness map
        if (set.metallic != null)
            SetTex(mat, "_MetallicGlossMap", set.metallic);

        // Occlusion
        if (set.occlusion != null)
        {
            mat.SetFloat("_OcclusionStrength", 1f);
            SetTex(mat, "_OcclusionMap", set.occlusion);
        }

        // Emission texture (only if the material already has emission enabled)
        if (set.emissive != null && mat.IsKeywordEnabled("_EMISSION"))
            SetTex(mat, "_EmissionMap", set.emissive);

        // Height / parallax
        if (set.height != null)
        {
            mat.EnableKeyword("_PARALLAXMAP");
            mat.SetFloat("_Parallax", 0.02f);
            SetTex(mat, "_ParallaxMap", set.height);
        }
    }

    static void SetTex(Material mat, string prop, string textureName)
    {
        if (textureName == null) return;
        string path = $"{TexturePath}/{textureName}.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
            Debug.LogWarning($"[AssignGreebleTextures] Texture not found: {path}");
        else
            mat.SetTexture(prop, tex);
    }
}

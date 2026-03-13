using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System.Linq;

public static class SetupStageLayersAndVolumes
{
    const int LAYER_STAGE_A = 8;
    const int LAYER_STAGE_B = 9;

    public static void Execute()
    {
        AddLayers();
        AssignObjectLayers();
        FixDirectionalLightCulling();
        CreateLocalVolumes();
        EditorUtility.SetDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()[0]);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SetupStageLayersAndVolumes] Done.");
    }

    // -------------------------------------------------------------------------

    static void AddLayers()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");

        SetLayer(layers, LAYER_STAGE_A, "StageA");
        SetLayer(layers, LAYER_STAGE_B, "StageB");

        tagManager.ApplyModifiedProperties();
        Debug.Log("[SetupStageLayersAndVolumes] Layers added: StageA=8, StageB=9");
    }

    static void SetLayer(SerializedProperty layers, int index, string name)
    {
        var element = layers.GetArrayElementAtIndex(index);
        if (string.IsNullOrEmpty(element.stringValue))
        {
            element.stringValue = name;
            Debug.Log($"  Layer {index} set to '{name}'");
        }
        else if (element.stringValue != name)
        {
            Debug.LogWarning($"  Layer {index} already used by '{element.stringValue}', skipping '{name}'");
        }
    }

    // -------------------------------------------------------------------------

    static void AssignObjectLayers()
    {
        var root = GameObject.Find("--- Stage A ---");
        if (root != null)
            SetLayerRecursive(root, LAYER_STAGE_A);

        root = GameObject.Find("--- Stage B ---");
        if (root != null)
            SetLayerRecursive(root, LAYER_STAGE_B);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // -------------------------------------------------------------------------

    static void FixDirectionalLightCulling()
    {
        // Only illuminate own stage layer. Keep Default (0) included so any
        // non-stage objects (shared content, etc.) still receive some light.
        int maskA = (1 << LAYER_STAGE_A) | (1 << 0);
        int maskB = (1 << LAYER_STAGE_B) | (1 << 0);

        var lightA = GameObject.Find("--- Stage A ---/DirectionalLight_A")
                              ?.GetComponent<Light>();
        var lightB = GameObject.Find("--- Stage B ---/DirectionalLight_B")
                              ?.GetComponent<Light>();

        if (lightA != null) { lightA.cullingMask = maskA; EditorUtility.SetDirty(lightA); }
        if (lightB != null) { lightB.cullingMask = maskB; EditorUtility.SetDirty(lightB); }

        Debug.Log("[SetupStageLayersAndVolumes] Directional light culling masks set.");
    }

    // -------------------------------------------------------------------------

    static void CreateLocalVolumes()
    {
        CreateDeckVolume("LocalVolume_A", "--- Stage A ---", new Vector3(0, 0, 0),
                         "Assets/VJSystem/PresetLibraries/DeckA_VolumeProfile.asset");
        CreateDeckVolume("LocalVolume_B", "--- Stage B ---", new Vector3(5000, 0, 0),
                         "Assets/VJSystem/PresetLibraries/DeckB_VolumeProfile.asset");
    }

    static void CreateDeckVolume(string name, string parentPath, Vector3 worldPos, string profileAssetPath)
    {
        // Skip if already exists
        var existing = GameObject.Find($"{parentPath}/{name}");
        if (existing != null)
        {
            Debug.Log($"[SetupStageLayersAndVolumes] {name} already exists, skipping.");
            return;
        }

        // Create profile asset
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        System.IO.Directory.CreateDirectory(
            System.IO.Path.GetDirectoryName(Application.dataPath + "/../" + profileAssetPath));
        AssetDatabase.CreateAsset(profile, profileAssetPath);

        // Create GameObject
        var go = new GameObject(name);
        var parent = GameObject.Find(parentPath);
        if (parent != null) go.transform.SetParent(parent.transform);
        go.transform.position = worldPos;

        // Volume — local (non-global), higher priority than global volume
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = false;
        vol.priority = 10f;
        vol.profile  = profile;

        // BoxCollider — big enough to contain stage cameras at max orbit radius (~15 units)
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size      = new Vector3(80, 40, 80);
        col.center    = Vector3.zero;

        EditorUtility.SetDirty(go);
        Debug.Log($"[SetupStageLayersAndVolumes] Created {name} at {worldPos} with profile {profileAssetPath}");
    }
}

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Wires up two MeshSpawnSystem MonoBehaviours (one per stage) and assigns
/// all meshes extracted from Assets/meshes/ FBX files plus the 10 generated
/// materials from Assets/Materials/.
///
/// Prerequisites:
///   1. Run "VJSystem/Generate VJ Mesh Materials" to create VJMesh_Mat_00-09.
///   2. Scene must contain a DualDeckPostFXRouter.
///   3. Play mode must be stopped.
/// </summary>
public static class WireMeshSpawnSystems
{
    static readonly string[] FbxPaths =
    {
        "Assets/meshes/Collection.fbx",
        "Assets/meshes/discombobulated_mesh.002.fbx",
        "Assets/meshes/discombobulated_mesh.005.fbx",
        "Assets/meshes/discombobulated_mesh.006.fbx",
        "Assets/meshes/discombobulated_mesh.008.fbx",
        "Assets/meshes/discombobulated_mesh.011.fbx",
        "Assets/meshes/discombobulated_mesh.014.fbx",
        "Assets/meshes/discombobulated_mesh.016.fbx",
        "Assets/meshes/dissss7.fbx"
    };

    [MenuItem("VJSystem/Wire Mesh Spawn Systems")]
    public static void Execute()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[WireMeshSpawnSystems] Stop Play Mode before running this script.");
            return;
        }

        var router = Object.FindFirstObjectByType<VJSystem.DualDeckPostFXRouter>();
        if (router == null)
        {
            Debug.LogError("[WireMeshSpawnSystems] DualDeckPostFXRouter not found in scene.");
            return;
        }

        // --- Collect meshes ---
        var meshes = new List<Mesh>();
        foreach (var path in FbxPaths)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in allAssets)
                if (asset is Mesh m) meshes.Add(m);
        }

        if (meshes.Count == 0)
            Debug.LogWarning("[WireMeshSpawnSystems] No meshes found in Assets/meshes/. Check that FBX files exist.");

        // --- Collect materials ---
        var materials = new List<Material>();
        for (int i = 0; i < 10; i++)
        {
            string matPath = $"Assets/Materials/VJMesh_Mat_{i:D2}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null) materials.Add(mat);
        }

        if (materials.Count == 0)
            Debug.LogWarning("[WireMeshSpawnSystems] No materials found in Assets/Materials/. " +
                             "Run 'VJSystem/Generate VJ Mesh Materials' first.");

        // --- Create / configure MeshSpawnSystem A ---
        var sysA = CreateOrFindSystem("MeshSpawn_A", Vector3.zero, meshes, materials, router.transform);

        // --- Create / configure MeshSpawnSystem B ---
        var sysB = CreateOrFindSystem("MeshSpawn_B", new Vector3(5000f, 0f, 0f), meshes, materials, router.transform);

        // --- Wire to router ---
        var routerSO = new SerializedObject(router);
        routerSO.FindProperty("meshSpawnA").objectReferenceValue = sysA;
        routerSO.FindProperty("meshSpawnB").objectReferenceValue = sysB;
        routerSO.ApplyModifiedProperties();

        EditorUtility.SetDirty(router);

        // Save the scene so references persist across play mode
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[WireMeshSpawnSystems] Done. " +
                  $"{meshes.Count} meshes, {materials.Count} materials assigned to MeshSpawn_A and MeshSpawn_B. Scene saved.");
    }

    static VJSystem.MeshSpawnSystem CreateOrFindSystem(
        string goName, Vector3 origin,
        List<Mesh> meshes, List<Material> materials,
        Transform parent)
    {
        var existing = GameObject.Find(goName);
        VJSystem.MeshSpawnSystem sys;

        if (existing != null)
        {
            sys = existing.GetComponent<VJSystem.MeshSpawnSystem>();
            if (sys == null) sys = existing.AddComponent<VJSystem.MeshSpawnSystem>();
        }
        else
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent);
            sys = go.AddComponent<VJSystem.MeshSpawnSystem>();
        }

        var so = new SerializedObject(sys);
        so.FindProperty("stageOrigin").vector3Value = origin;

        var meshesProp = so.FindProperty("meshes");
        meshesProp.arraySize = meshes.Count;
        for (int i = 0; i < meshes.Count; i++)
            meshesProp.GetArrayElementAtIndex(i).objectReferenceValue = meshes[i];

        var matsProp = so.FindProperty("materials");
        matsProp.arraySize = materials.Count;
        for (int i = 0; i < materials.Count; i++)
            matsProp.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(sys);
        return sys;
    }
}

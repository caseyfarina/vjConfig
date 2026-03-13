using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using VJSystem;
using MidiFighter64;
using KinoGlitch;

public static class FinishPostFXSetup
{
    public static void Execute()
    {
        AddSceneComponents();
        FixCameraCullingMasks();
        AddRendererFeatures();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[FinishPostFXSetup] Done.");
    }

    // -------------------------------------------------------------------------

    static void AddSceneComponents()
    {
        // MidiMixRouter — add to MidiEventManager GameObject
        var midiGO = GameObject.Find("--- Dual Deck Systems ---/MidiEventManager");
        if (midiGO != null && midiGO.GetComponent<MidiMixRouter>() == null)
        {
            midiGO.AddComponent<MidiMixRouter>();
            EditorUtility.SetDirty(midiGO);
            Debug.Log("[FinishPostFXSetup] MidiMixRouter added to MidiEventManager.");
        }

        // DualDeckPostFXRouter — create dedicated GameObject under Dual Deck Systems
        var dualDeckParent = GameObject.Find("--- Dual Deck Systems ---");
        if (dualDeckParent == null) { Debug.LogError("--- Dual Deck Systems --- not found."); return; }

        var existing = GameObject.Find("--- Dual Deck Systems ---/PostFXRouter");
        if (existing != null)
        {
            Debug.Log("[FinishPostFXSetup] PostFXRouter already exists, re-wiring references.");
            WireRouter(existing.GetComponent<DualDeckPostFXRouter>());
            return;
        }

        var routerGO = new GameObject("PostFXRouter");
        routerGO.transform.SetParent(dualDeckParent.transform);
        var router = routerGO.AddComponent<DualDeckPostFXRouter>();
        WireRouter(router);
        EditorUtility.SetDirty(routerGO);
        Debug.Log("[FinishPostFXSetup] PostFXRouter created and wired.");
    }

    static void WireRouter(DualDeckPostFXRouter router)
    {
        if (router == null) return;

        router.rigA    = GameObject.Find("--- Stage A ---/CameraRig_A")
                                   ?.GetComponent<DeckCameraRig>();
        router.rigB    = GameObject.Find("--- Stage B ---/CameraRig_B")
                                   ?.GetComponent<DeckCameraRig>();
        router.volumeA = GameObject.Find("--- Stage A ---/LocalVolume_A")
                                   ?.GetComponent<Volume>();
        router.volumeB = GameObject.Find("--- Stage B ---/LocalVolume_B")
                                   ?.GetComponent<Volume>();

        Debug.Log($"[FinishPostFXSetup] Router wired — rigA:{router.rigA != null} rigB:{router.rigB != null} " +
                  $"volA:{router.volumeA != null} volB:{router.volumeB != null}");
    }

    // -------------------------------------------------------------------------

    static void FixCameraCullingMasks()
    {
        // Each deck sees its own stage layer + Default (shared/unassigned objects)
        int maskA = (1 << 8) | (1 << 0);   // StageA + Default
        int maskB = (1 << 9) | (1 << 0);   // StageB + Default

        SetCameraMask("--- Stage A ---/CameraRig_A/Cam1_A", maskA);
        SetCameraMask("--- Stage A ---/CameraRig_A/Cam2_A", maskA);
        SetCameraMask("--- Stage B ---/CameraRig_B/Cam1_B", maskB);
        SetCameraMask("--- Stage B ---/CameraRig_B/Cam2_B", maskB);

        Debug.Log("[FinishPostFXSetup] Camera culling masks set.");
    }

    static void SetCameraMask(string path, int mask)
    {
        var go = GameObject.Find(path);
        if (go == null) { Debug.LogWarning($"[FinishPostFXSetup] Not found: {path}"); return; }
        var cam = go.GetComponent<Camera>();
        if (cam != null) { cam.cullingMask = mask; EditorUtility.SetDirty(go); }
    }

    // -------------------------------------------------------------------------

    static void AddRendererFeatures()
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
            "Assets/Settings/VJ_Renderer.asset");
        if (rendererData == null) { Debug.LogError("[FinishPostFXSetup] VJ_Renderer.asset not found."); return; }

        bool dirty = false;
        dirty |= EnsureFeature(rendererData, "KinoGlitch.AnalogGlitchRendererFeature");
        dirty |= EnsureFeature(rendererData, "KinoGlitch.DigitalGlitchRendererFeature");

        if (dirty)
        {
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            Debug.Log("[FinishPostFXSetup] Renderer features saved.");
        }
    }

    static bool EnsureFeature(UniversalRendererData data, string typeName)
    {
        if (data.rendererFeatures.Exists(f => f.GetType().FullName == typeName))
        {
            Debug.Log($"[FinishPostFXSetup] {typeName} already present.");
            return false;
        }

        // Renderer feature classes are internal — use reflection to instantiate
        var type = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.FullName == typeName);

        if (type == null) { Debug.LogError($"[FinishPostFXSetup] Type not found: {typeName}"); return false; }

        var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
        feature.name = type.Name;
        AssetDatabase.AddObjectToAsset(feature, data);
        data.rendererFeatures.Add(feature);
        Debug.Log($"[FinishPostFXSetup] {type.Name} added.");
        return true;
    }
}

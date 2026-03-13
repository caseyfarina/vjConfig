using UnityEngine;
using UnityEditor;
using VJSystem;

public static class WireDirectionalLights
{
    public static void Execute()
    {
        var routerGO = GameObject.Find("--- Dual Deck Systems ---/PostFXRouter");
        if (routerGO == null) { Debug.LogError("[WireDirectionalLights] PostFXRouter not found."); return; }

        var router = routerGO.GetComponent<DualDeckPostFXRouter>();
        if (router == null) { Debug.LogError("[WireDirectionalLights] DualDeckPostFXRouter component not found."); return; }

        var lightAGO = GameObject.Find("--- Stage A ---/DirectionalLight_A");
        var lightBGO = GameObject.Find("--- Stage B ---/DirectionalLight_B");

        if (lightAGO == null) { Debug.LogError("[WireDirectionalLights] DirectionalLight_A not found."); return; }
        if (lightBGO == null) { Debug.LogError("[WireDirectionalLights] DirectionalLight_B not found."); return; }

        router.directionalLightA = lightAGO.GetComponent<Light>();
        router.directionalLightB = lightBGO.GetComponent<Light>();

        if (router.directionalLightA == null) { Debug.LogError("[WireDirectionalLights] No Light on DirectionalLight_A."); return; }
        if (router.directionalLightB == null) { Debug.LogError("[WireDirectionalLights] No Light on DirectionalLight_B."); return; }

        EditorUtility.SetDirty(routerGO);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[WireDirectionalLights] A={router.directionalLightA.name}, B={router.directionalLightB.name}");
    }
}

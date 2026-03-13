using UnityEngine;
using UnityEditor;
using VJSystem;

public static class WireLightRigs
{
    public static void Execute()
    {
        var routerGO = GameObject.Find("--- Dual Deck Systems ---/PostFXRouter");
        if (routerGO == null) { Debug.LogError("[WireLightRigs] PostFXRouter not found."); return; }

        var router = routerGO.GetComponent<DualDeckPostFXRouter>();
        if (router == null) { Debug.LogError("[WireLightRigs] DualDeckPostFXRouter not found."); return; }

        var rigAGO = GameObject.Find("--- Stage A ---/LightRig_A");
        var rigBGO = GameObject.Find("--- Stage B ---/LightRig_B");

        if (rigAGO == null) { Debug.LogError("[WireLightRigs] LightRig_A not found."); return; }
        if (rigBGO == null) { Debug.LogError("[WireLightRigs] LightRig_B not found."); return; }

        router.lightRigA = rigAGO.GetComponent<DeckLightRig>();
        router.lightRigB = rigBGO.GetComponent<DeckLightRig>();

        if (router.lightRigA == null) { Debug.LogError("[WireLightRigs] No DeckLightRig on LightRig_A."); return; }
        if (router.lightRigB == null) { Debug.LogError("[WireLightRigs] No DeckLightRig on LightRig_B."); return; }

        EditorUtility.SetDirty(routerGO);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[WireLightRigs] lightRigA={router.lightRigA.name}, lightRigB={router.lightRigB.name}");
    }
}

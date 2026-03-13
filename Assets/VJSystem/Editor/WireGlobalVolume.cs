using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using VJSystem;

public static class WireGlobalVolume
{
    public static void Execute()
    {
        var routerGO = GameObject.Find("--- Dual Deck Systems ---/PostFXRouter");
        if (routerGO == null) { Debug.LogError("[WireGlobalVolume] PostFXRouter not found."); return; }

        var router = routerGO.GetComponent<DualDeckPostFXRouter>();
        if (router == null) { Debug.LogError("[WireGlobalVolume] DualDeckPostFXRouter not found."); return; }

        var globalVolumeGO = GameObject.Find("Global Volume");
        if (globalVolumeGO == null) { Debug.LogError("[WireGlobalVolume] 'Global Volume' GameObject not found."); return; }

        var vol = globalVolumeGO.GetComponent<Volume>();
        if (vol == null) { Debug.LogError("[WireGlobalVolume] No Volume component on Global Volume."); return; }

        router.globalVolume = vol;
        EditorUtility.SetDirty(routerGO);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[WireGlobalVolume] globalVolume assigned: {vol.name}");
    }
}

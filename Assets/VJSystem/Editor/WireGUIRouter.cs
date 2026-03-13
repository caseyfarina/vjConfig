using UnityEngine;
using UnityEditor;
using VJSystem;

public static class WireGUIRouter
{
    public static void Execute()
    {
        var guiGO = GameObject.Find("--- Dual Deck Systems ---/DualDeckGUI");
        if (guiGO == null) { Debug.LogError("[WireGUIRouter] DualDeckGUI not found."); return; }

        var gui = guiGO.GetComponent<DualDeckGUI>();
        if (gui == null) { Debug.LogError("[WireGUIRouter] DualDeckGUI component not found."); return; }

        var routerGO = GameObject.Find("--- Dual Deck Systems ---/PostFXRouter");
        if (routerGO == null) { Debug.LogError("[WireGUIRouter] PostFXRouter not found."); return; }

        gui.postFXRouter = routerGO.GetComponent<DualDeckPostFXRouter>();
        EditorUtility.SetDirty(guiGO);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[WireGUIRouter] postFXRouter wired: {gui.postFXRouter != null}");
    }
}

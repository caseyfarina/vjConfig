using UnityEngine;
using UnityEditor;
using MidiFighter64;

public static class AddMidiGridRouter
{
    public static void Execute()
    {
        var go = GameObject.Find("--- Dual Deck Systems ---/MidiDebugMonitor");
        if (go == null) { Debug.LogError("[AddMidiGridRouter] MidiDebugMonitor not found."); return; }

        if (go.GetComponent<MidiGridRouter>() != null)
        {
            Debug.Log("[AddMidiGridRouter] MidiGridRouter already present.");
            return;
        }

        go.AddComponent<MidiGridRouter>();
        EditorUtility.SetDirty(go);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[AddMidiGridRouter] MidiGridRouter added to MidiDebugMonitor.");
    }
}

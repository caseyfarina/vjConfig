using UnityEngine;
using UnityEditor;

public static class AddMidiToScene
{
    public static void Execute()
    {
        var systemsRoot = GameObject.Find("--- Dual Deck Systems ---");
        if (systemsRoot == null)
        {
            Debug.LogError("[AddMidiToScene] Could not find '--- Dual Deck Systems ---' in scene.");
            return;
        }

        // Remove any existing MIDI GameObjects to avoid duplicates
        foreach (string name in new[] { "MidiEventManager", "MidiDebugMonitor" })
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        // ===== MIDI core =====
        var midiGO = new GameObject("MidiEventManager");
        midiGO.transform.SetParent(systemsRoot.transform);
        midiGO.AddComponent<MidiFighter64.MidiEventManager>();
        midiGO.AddComponent<MidiFighter64.MidiGridRouter>();
        midiGO.AddComponent<MidiFighter64.MidiMixRouter>();
        midiGO.AddComponent<MidiFighter64.MidiFighterOutput>();
        midiGO.AddComponent<MidiFighter64.UnityMainThreadDispatcher>();

        // ===== MIDI debug monitor =====
        var monitorGO = new GameObject("MidiDebugMonitor");
        monitorGO.transform.SetParent(systemsRoot.transform);
        var monitor = monitorGO.AddComponent<VJSystem.MidiDebugMonitor>();

        // ===== Wire monitor into DualDeckGUI =====
        var guiGO = GameObject.Find("DualDeckGUI");
        if (guiGO != null)
        {
            var gui = guiGO.GetComponent<VJSystem.DualDeckGUI>();
            if (gui != null)
            {
                gui.midiMonitor = monitor;
                Debug.Log("[AddMidiToScene] Wired MidiDebugMonitor into DualDeckGUI.");
            }
        }

        EditorUtility.SetDirty(systemsRoot);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[AddMidiToScene] Done. Added MidiEventManager + MidiDebugMonitor under '--- Dual Deck Systems ---'.");
    }
}

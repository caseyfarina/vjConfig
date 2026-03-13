using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Collections;

/// <summary>
/// Runs in Play mode to simulate MIDI Fighter 64 input and test the full signal chain.
/// Invokes MidiEventManager.OnNoteOn/Off static events directly.
/// </summary>
public static class PlayModeTest
{
    static Assembly runtimeAsm;
    static Assembly midiAsm;

    static Type GetType(string name)
    {
        runtimeAsm ??= Assembly.Load("Assembly-CSharp");
        return runtimeAsm.GetType("VJSystem." + name) ?? runtimeAsm.GetType(name);
    }

    static Type GetMidiType(string name)
    {
        midiAsm ??= Assembly.Load("MidiFighter64.Runtime");
        return midiAsm.GetType("MidiFighter64." + name);
    }

    // MF64 note mapping: row 1 = top, row 8 = bottom, col 1-8
    static int NoteForGrid(int row, int col)
    {
        return 36 + (8 - row) * 8 + (col - 1);
    }

    static void SimulateNoteOn(int noteNumber, float velocity = 1f)
    {
        var eventType = GetMidiType("MidiEventManager");
        // C# event backing field is private static with same name
        var field = eventType.GetField("OnNoteOn", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        if (field == null) { Debug.LogError("[Test] OnNoteOn field not found"); return; }
        var handler = field.GetValue(null) as Action<int, float>;
        if (handler == null) { Debug.LogWarning($"[Test] OnNoteOn has no subscribers (note {noteNumber})"); return; }
        handler.Invoke(noteNumber, velocity);
    }

    static void SimulateNoteOff(int noteNumber)
    {
        var eventType = GetMidiType("MidiEventManager");
        var field = eventType.GetField("OnNoteOff", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        if (field == null) { Debug.LogError("[Test] OnNoteOff field not found"); return; }
        var handler = field.GetValue(null) as Action<int>;
        if (handler == null) return;
        handler.Invoke(noteNumber);
    }

    public static void Execute()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[Test] Must be in Play mode to run tests!");
            return;
        }

        Debug.Log("=== PLAY MODE TEST START ===");
        int passed = 0;
        int failed = 0;

        // --- Test 1: Verify systems initialized ---
        Debug.Log("[Test 1] Checking system initialization...");
        var systemNames = new[] {
            "PixelSortSystem", "ChromaticDisplacementSystem", "DepthOfFieldSystem",
            "PostFXRouter", "PresetSaveSystem", "RandomizationSystem", "VJCameraSystem"
        };
        foreach (var name in systemNames)
        {
            var type = GetType(name);
            if (type == null) { Debug.LogError($"  FAIL: Type {name} not found"); failed++; continue; }
            var obj = UnityEngine.Object.FindFirstObjectByType(type);
            if (obj != null) { Debug.Log($"  OK: {name} found"); passed++; }
            else { Debug.LogError($"  FAIL: {name} not found in scene"); failed++; }
        }

        // --- Test 2: Volume overrides accessible ---
        Debug.Log("[Test 2] Checking Volume overrides...");
        var volumeGO = GameObject.Find("Global Volume");
        var volume = volumeGO?.GetComponent<UnityEngine.Rendering.Volume>();
        if (volume != null && volume.sharedProfile != null)
        {
            Debug.Log($"  OK: Volume profile has {volume.sharedProfile.components.Count} overrides");
            passed++;

            // Check each override type
            foreach (var comp in volume.sharedProfile.components)
            {
                Debug.Log($"  OK: {comp.GetType().Name} active={comp.active}");
                passed++;
            }
        }
        else { Debug.LogError("  FAIL: Volume or profile is null"); failed++; }

        // --- Test 3: Camera system ---
        Debug.Log("[Test 3] Testing camera selection via simulated MIDI...");
        var camSystemType = GetType("VJCameraSystem");
        var camSystem = UnityEngine.Object.FindFirstObjectByType(camSystemType);
        if (camSystem != null)
        {
            var activeIdxProp = camSystemType.GetProperty("ActiveCameraIndex");
            var activeNameProp = camSystemType.GetProperty("ActiveCameraName");

            // Select camera 1 (row 1, col 1) -> note 92
            SimulateNoteOn(NoteForGrid(1, 1));
            int idx = (int)activeIdxProp.GetValue(camSystem);
            string name = (string)activeNameProp.GetValue(camSystem);
            if (idx == 0) { Debug.Log($"  OK: Camera 1 selected ({name})"); passed++; }
            else { Debug.LogError($"  FAIL: Expected camera index 0, got {idx}"); failed++; }

            // Select camera 3 (row 1, col 3) -> note 94
            SimulateNoteOn(NoteForGrid(1, 3));
            idx = (int)activeIdxProp.GetValue(camSystem);
            name = (string)activeNameProp.GetValue(camSystem);
            if (idx == 2) { Debug.Log($"  OK: Camera 3 selected ({name})"); passed++; }
            else { Debug.LogError($"  FAIL: Expected camera index 2, got {idx}"); failed++; }

            // Select camera 8 (row 1, col 8) -> note 99
            SimulateNoteOn(NoteForGrid(1, 8));
            idx = (int)activeIdxProp.GetValue(camSystem);
            name = (string)activeNameProp.GetValue(camSystem);
            if (idx == 7) { Debug.Log($"  OK: Camera 8 selected ({name})"); passed++; }
            else { Debug.LogError($"  FAIL: Expected camera index 7, got {idx}"); failed++; }
        }
        else { Debug.LogError("  FAIL: VJCameraSystem not found"); failed++; }

        // --- Test 4: PostFX preset selection ---
        Debug.Log("[Test 4] Testing PostFX preset selection via simulated MIDI...");
        var routerType = GetType("PostFXRouter");
        var router = UnityEngine.Object.FindFirstObjectByType(routerType);
        if (router != null)
        {
            var activeRowProp = routerType.GetProperty("ActiveEffectRow");

            // Row 2 (DoF), col 1 -> preset 1
            SimulateNoteOn(NoteForGrid(2, 1));
            int row = (int)activeRowProp.GetValue(router);
            if (row == 2) { Debug.Log("  OK: DoF row (2) activated"); passed++; }
            else { Debug.LogError($"  FAIL: Expected row 2, got {row}"); failed++; }

            // Row 3 (PixelSort), col 3 -> preset 3
            SimulateNoteOn(NoteForGrid(3, 3));
            row = (int)activeRowProp.GetValue(router);
            if (row == 3) { Debug.Log("  OK: PixelSort row (3) activated"); passed++; }
            else { Debug.LogError($"  FAIL: Expected row 3, got {row}"); failed++; }

            // Row 4 (Chromatic), col 5 -> preset 5
            SimulateNoteOn(NoteForGrid(4, 5));
            row = (int)activeRowProp.GetValue(router);
            if (row == 4) { Debug.Log("  OK: Chromatic row (4) activated"); passed++; }
            else { Debug.LogError($"  FAIL: Expected row 4, got {row}"); failed++; }
        }
        else { Debug.LogError("  FAIL: PostFXRouter not found"); failed++; }

        // --- Test 5: Randomization (col 8) ---
        Debug.Log("[Test 5] Testing randomization (col 8)...");
        // Row 2, col 8 -> DoF randomize
        try
        {
            SimulateNoteOn(NoteForGrid(2, 8));
            Debug.Log("  OK: DoF randomize triggered (no exception)");
            passed++;
        }
        catch (Exception e)
        {
            Debug.LogError($"  FAIL: DoF randomize threw: {e.Message}");
            failed++;
        }

        // Row 3, col 8 -> PixelSort randomize
        try
        {
            SimulateNoteOn(NoteForGrid(3, 8));
            Debug.Log("  OK: PixelSort randomize triggered (no exception)");
            passed++;
        }
        catch (Exception e)
        {
            Debug.LogError($"  FAIL: PixelSort randomize threw: {e.Message}");
            failed++;
        }

        // Row 4, col 8 -> Chromatic randomize
        try
        {
            SimulateNoteOn(NoteForGrid(4, 8));
            Debug.Log("  OK: Chromatic randomize triggered (no exception)");
            passed++;
        }
        catch (Exception e)
        {
            Debug.LogError($"  FAIL: Chromatic randomize threw: {e.Message}");
            failed++;
        }

        // --- Test 6: Light toggles ---
        Debug.Log("[Test 6] Testing light toggles (row 5)...");
        for (int col = 1; col <= 8; col++)
        {
            try
            {
                SimulateNoteOn(NoteForGrid(5, col));
                Debug.Log($"  OK: Light toggle col {col} (no exception)");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError($"  FAIL: Light toggle col {col} threw: {e.Message}");
                failed++;
            }
        }

        // --- Test 7: Scene slots (rows 6-8, note on + off) ---
        Debug.Log("[Test 7] Testing scene slots (rows 6-8)...");
        for (int row = 6; row <= 8; row++)
        {
            try
            {
                SimulateNoteOn(NoteForGrid(row, 1));
                SimulateNoteOff(NoteForGrid(row, 1));
                Debug.Log($"  OK: Scene slot row {row} col 1 on/off (no exception)");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError($"  FAIL: Scene slot row {row} threw: {e.Message}");
                failed++;
            }
        }

        // --- Test 8: All 7 presets for each effect ---
        Debug.Log("[Test 8] Testing all 7 preset slots for each effect row...");
        for (int row = 2; row <= 4; row++)
        {
            string effectName = row switch { 2 => "DoF", 3 => "PixelSort", 4 => "Chromatic", _ => "?" };
            for (int col = 1; col <= 7; col++)
            {
                try
                {
                    SimulateNoteOn(NoteForGrid(row, col));
                    Debug.Log($"  OK: {effectName} preset {col}");
                    passed++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"  FAIL: {effectName} preset {col}: {e.Message}");
                    failed++;
                }
            }
        }

        // --- Test 9: Sweep all 8 cameras ---
        Debug.Log("[Test 9] Sweeping all 8 cameras...");
        for (int col = 1; col <= 8; col++)
        {
            try
            {
                SimulateNoteOn(NoteForGrid(1, col));
                var activeNameProp = camSystemType.GetProperty("ActiveCameraName");
                string camName = (string)activeNameProp.GetValue(camSystem);
                Debug.Log($"  OK: Camera {col} -> {camName}");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError($"  FAIL: Camera {col}: {e.Message}");
                failed++;
            }
        }

        // Reset to camera 1
        SimulateNoteOn(NoteForGrid(1, 1));

        Debug.Log($"=== TEST RESULTS: {passed} passed, {failed} failed ===");
    }
}

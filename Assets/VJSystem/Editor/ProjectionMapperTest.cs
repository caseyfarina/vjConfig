using UnityEngine;
using UnityEditor;
using ProjectionMapper;
using VJSystem;

/// <summary>
/// Editor script that creates a runtime test MonoBehaviour on the ProjectionMapperManager.
/// The test component configures 3 cameras + 3 surfaces in Start(), after OnEnable
/// has finished loading profiles from JSON persistence.
/// </summary>
public static class ProjectionMapperTest
{
    public static string Execute()
    {
        var mgr = Object.FindFirstObjectByType<ProjectionMapperManager>();
        if (mgr == null)
            return "ERROR: No ProjectionMapperManager found in scene.";

        // Clean up previous test objects
        foreach (var old in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (old.name.StartsWith("TestCam_") || old.name == "TestOverlayCamera")
                Object.DestroyImmediate(old.gameObject);
        }

        // Remove any existing test component
        var existing = mgr.GetComponent<ProjectionMapperTestRunner>();
        if (existing != null) Object.DestroyImmediate(existing);

        // Disable VJProjectionBridge so it doesn't interfere
        var bridge = Object.FindFirstObjectByType<VJProjectionBridge>();
        if (bridge != null) bridge.enabled = false;

        // Add the runtime test component — it will do setup in Start()
        mgr.gameObject.AddComponent<ProjectionMapperTestRunner>();

        EditorUtility.SetDirty(mgr);
        return "SUCCESS: Added ProjectionMapperTestRunner. Enter Play mode to run the test.";
    }
}

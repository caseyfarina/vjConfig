using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

// Standalone bootstrap script for MCP execution (no dialog)
public static class DualDeckBootstrap
{
    public static void Execute()
    {
        DisableLegacySystems();
        CreateDualDeckHierarchy();
        Debug.Log("[DualDeckBootstrap] Scene setup complete. Enter Play mode to test.");
    }

    static void DisableLegacySystems()
    {
        // Disable the legacy VJ Systems parent and all children
        var legacyParent = GameObject.Find("--- VJ Systems ---");
        if (legacyParent != null)
        {
            legacyParent.SetActive(false);
            Debug.Log("[DualDeckBootstrap] Disabled legacy VJ Systems parent");
        }

        // Disable individual legacy objects that might be at root level
        string[] legacyNames = {
            "PixelSortSystem", "ChromaticDisplacementSystem", "DepthOfFieldSystem",
            "PostFXRouter", "RandomizationSystem", "PresetSaveSystem",
            "VJCameraSystem", "VJLightSystem", "VJSceneSlotSystem",
            "VJProjectionBridge", "SpoutOutputManager", "MidiGridRouter",
            "VJDebugHUD", "CameraRig"
        };
        foreach (string name in legacyNames)
        {
            var obj = GameObject.Find(name);
            if (obj != null)
            {
                obj.SetActive(false);
                Debug.Log($"[DualDeckBootstrap] Disabled: {name}");
            }
        }

        // Disable PostFX volume overrides
        var volumes = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
        foreach (var vol in volumes)
        {
            if (vol.profile == null) continue;
            foreach (var comp in vol.profile.components)
            {
                string typeName = comp.GetType().Name;
                if (typeName == "PixelSortVolume" || typeName == "ChromaticDisplacementVolume")
                {
                    comp.active = false;
                    Debug.Log($"[DualDeckBootstrap] Disabled volume override: {typeName}");
                }
            }
        }
    }

    static void CreateDualDeckHierarchy()
    {
        // Clean up previous dual deck objects
        DestroyIfExists("--- Dual Deck Systems ---");
        DestroyIfExists("--- Stage A ---");
        DestroyIfExists("--- Stage B ---");

        // ===== SYSTEMS ROOT =====
        var systemsRoot = new GameObject("--- Dual Deck Systems ---");

        // ===== STAGE A =====
        var stageARoot = new GameObject("--- Stage A ---");
        stageARoot.transform.position = Vector3.zero;
        var controllerA = SetupStage(stageARoot, VJSystem.DeckIdentity.A, Vector3.zero);

        // ===== STAGE B =====
        var stageBRoot = new GameObject("--- Stage B ---");
        stageBRoot.transform.position = new Vector3(5000, 0, 0);
        var controllerB = SetupStage(stageBRoot, VJSystem.DeckIdentity.B, new Vector3(5000, 0, 0));

        // ===== DUAL DECK MANAGER =====
        var managerGO = new GameObject("DualDeckManager");
        managerGO.transform.SetParent(systemsRoot.transform);
        var manager = managerGO.AddComponent<VJSystem.DualDeckManager>();
        manager.stageA = controllerA;
        manager.stageB = controllerB;

        // ===== OUTPUT MANAGER =====
        var outputGO = new GameObject("OutputManager");
        outputGO.transform.SetParent(systemsRoot.transform);
        var output = outputGO.AddComponent<VJSystem.OutputManager>();
        output.deckManager = manager;
        output.activateExtraDisplays = true;
        output.warpShader = Shader.Find("ProjectionMapper/HomographyWarp");

        // ===== MIDI =====
        var midiGO = new GameObject("MidiEventManager");
        midiGO.transform.SetParent(systemsRoot.transform);
        midiGO.AddComponent<MidiFighter64.MidiEventManager>();
        midiGO.AddComponent<MidiFighter64.MidiGridRouter>();
        midiGO.AddComponent<MidiFighter64.MidiMixRouter>();
        midiGO.AddComponent<MidiFighter64.MidiFighterOutput>();
        midiGO.AddComponent<MidiFighter64.UnityMainThreadDispatcher>();

        var midiMonitorGO = new GameObject("MidiDebugMonitor");
        midiMonitorGO.transform.SetParent(systemsRoot.transform);
        var midiMonitor = midiMonitorGO.AddComponent<VJSystem.MidiDebugMonitor>();

        // ===== GUI =====
        var guiGO = new GameObject("DualDeckGUI");
        guiGO.transform.SetParent(systemsRoot.transform);
        var gui = guiGO.AddComponent<VJSystem.DualDeckGUI>();
        gui.deckManager = manager;
        gui.outputManager = output;
        gui.midiMonitor = midiMonitor;

        // ===== SCREEN CAMERA =====
        var screenCamGO = new GameObject("ScreenCamera");
        screenCamGO.transform.SetParent(systemsRoot.transform);
        var screenCam = screenCamGO.AddComponent<Camera>();
        screenCam.clearFlags = CameraClearFlags.SolidColor;
        screenCam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
        screenCam.cullingMask = 0;
        screenCam.depth = -10;
        var urpData = screenCamGO.AddComponent<UniversalAdditionalCameraData>();
        urpData.renderType = CameraRenderType.Base;

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[DualDeckBootstrap] Created hierarchy: 2 stages, 4 cameras, 2 light rigs, GUI, screen camera");
    }

    static VJSystem.StageController SetupStage(GameObject root, VJSystem.DeckIdentity deck, Vector3 origin)
    {
        // Stage Controller
        var controllerGO = new GameObject($"StageController_{deck}");
        controllerGO.transform.SetParent(root.transform);
        controllerGO.transform.position = origin;
        var controller = controllerGO.AddComponent<VJSystem.StageController>();
        controller.deck = deck;
        controller.stageOrigin = origin;
        controller.autoSpawn = true;

        // Content root
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(controllerGO.transform);
        contentGO.transform.position = origin;
        controller.contentRoot = contentGO.transform;

        // Camera Rig
        var cameraRigGO = new GameObject($"CameraRig_{deck}");
        cameraRigGO.transform.SetParent(root.transform);
        cameraRigGO.transform.position = origin;
        var cameraRig = cameraRigGO.AddComponent<VJSystem.DeckCameraRig>();
        cameraRig.stageOrigin = origin;
        controller.cameraRig = cameraRig;

        // Create 2 cameras per stage
        for (int i = 0; i < 2; i++)
        {
            var camGO = new GameObject($"Cam{i + 1}_{deck}");
            camGO.transform.SetParent(cameraRigGO.transform);
            float angle = (i == 0 ? 45f : 225f) * Mathf.Deg2Rad;
            camGO.transform.position = origin + new Vector3(
                Mathf.Cos(angle) * 10f, 3f, Mathf.Sin(angle) * 10f
            );
            camGO.transform.LookAt(origin + Vector3.up);

            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.depth = 0;

            var urpCam = camGO.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderType = CameraRenderType.Base;
            urpCam.renderPostProcessing = true;

            if (i == 0) cameraRig.cam1 = cam;
            else cameraRig.cam2 = cam;
        }

        // Light Rig
        var lightRigGO = new GameObject($"LightRig_{deck}");
        lightRigGO.transform.SetParent(root.transform);
        lightRigGO.transform.position = origin;
        var lightRig = lightRigGO.AddComponent<VJSystem.DeckLightRig>();
        lightRig.stageOrigin = origin;
        lightRig.activeLightCount = deck == VJSystem.DeckIdentity.A ? 6 : 4;
        lightRig.hue = deck == VJSystem.DeckIdentity.A ? 0f : 200f;
        lightRig.hueSpread = 60f;
        controller.lightRig = lightRig;

        // Directional light
        var dirLightGO = new GameObject($"DirectionalLight_{deck}");
        dirLightGO.transform.SetParent(root.transform);
        dirLightGO.transform.position = origin + new Vector3(0, 10, 0);
        dirLightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
        var dirLight = dirLightGO.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.intensity = 0.5f;
        dirLight.shadows = LightShadows.Soft;

        return controller;
    }

    static void DestroyIfExists(string name)
    {
        var obj = GameObject.Find(name);
        if (obj != null) Object.DestroyImmediate(obj);
    }
}

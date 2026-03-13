using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace VJSystem.Editor
{
    public static class DualDeckSceneSetup
    {
        [MenuItem("VJSystem/Setup Dual Deck Scene")]
        public static void SetupScene()
        {
            if (!EditorUtility.DisplayDialog("Setup Dual Deck Scene",
                "This will disable legacy VJ systems and create the dual-deck hierarchy.\n\nContinue?",
                "Yes", "Cancel"))
                return;

            DisableLegacySystems();
            CreateDualDeckHierarchy();

            Debug.Log("[DualDeckSetup] Scene setup complete. Enter Play mode to test.");
        }

        static void DisableLegacySystems()
        {
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
                    Debug.Log($"[DualDeckSetup] Disabled: {name}");
                }
            }

            // Also disable legacy parent
            var legacyParent = GameObject.Find("--- VJ Systems ---");
            if (legacyParent != null)
            {
                legacyParent.SetActive(false);
                Debug.Log("[DualDeckSetup] Disabled legacy VJ Systems parent");
            }

            // Disable PostFX volume overrides (PixelSort, ChromaticDisplacement) if volume exists
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
                        Debug.Log($"[DualDeckSetup] Disabled volume override: {typeName}");
                    }
                }
            }
        }

        static void CreateDualDeckHierarchy()
        {
            // Clean up any previous dual deck setup
            var existing = GameObject.Find("--- Dual Deck Systems ---");
            if (existing != null) Object.DestroyImmediate(existing);
            var existingA = GameObject.Find("--- Stage A ---");
            if (existingA != null) Object.DestroyImmediate(existingA);
            var existingB = GameObject.Find("--- Stage B ---");
            if (existingB != null) Object.DestroyImmediate(existingB);

            // ===== ROOT =====
            var systemsRoot = new GameObject("--- Dual Deck Systems ---");

            // ===== STAGE A (origin 0,0,0) =====
            var stageARoot = new GameObject("--- Stage A ---");
            stageARoot.transform.position = Vector3.zero;
            SetupStage(stageARoot, DeckIdentity.A, Vector3.zero);

            // ===== STAGE B (origin 5000,0,0) =====
            var stageBRoot = new GameObject("--- Stage B ---");
            stageBRoot.transform.position = new Vector3(5000, 0, 0);
            SetupStage(stageBRoot, DeckIdentity.B, new Vector3(5000, 0, 0));

            // ===== DUAL DECK MANAGER =====
            var managerGO = new GameObject("DualDeckManager");
            managerGO.transform.SetParent(systemsRoot.transform);
            var manager = managerGO.AddComponent<DualDeckManager>();
            manager.stageA = stageARoot.GetComponentInChildren<StageController>();
            manager.stageB = stageBRoot.GetComponentInChildren<StageController>();

            // ===== DISPLAY MANAGER =====
            var displayGO = new GameObject("DisplayManager");
            displayGO.transform.SetParent(systemsRoot.transform);
            displayGO.AddComponent<DisplayManager>();

            // ===== GUI =====
            var guiGO = new GameObject("DualDeckGUI");
            guiGO.transform.SetParent(systemsRoot.transform);
            var gui = guiGO.AddComponent<DualDeckGUI>();
            gui.deckManager = manager;

            // ===== SCREEN CAMERA (renders nothing, just provides a clear color for IMGUI) =====
            var screenCamGO = new GameObject("ScreenCamera");
            screenCamGO.transform.SetParent(systemsRoot.transform);
            var screenCam = screenCamGO.AddComponent<Camera>();
            screenCam.clearFlags = CameraClearFlags.SolidColor;
            screenCam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            screenCam.cullingMask = 0; // render nothing
            screenCam.depth = -10; // render first (background for IMGUI)
            // Add URP camera data
            var urpData = screenCamGO.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderType = CameraRenderType.Base;

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        static void SetupStage(GameObject root, DeckIdentity deck, Vector3 origin)
        {
            // Stage Controller
            var controllerGO = new GameObject($"StageController_{deck}");
            controllerGO.transform.SetParent(root.transform);
            controllerGO.transform.position = origin;
            var controller = controllerGO.AddComponent<StageController>();
            controller.deck = deck;
            controller.stageOrigin = origin;

            // Content root
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(controllerGO.transform);
            contentGO.transform.position = origin;
            controller.contentRoot = contentGO.transform;

            // Camera Rig
            var cameraRigGO = new GameObject($"CameraRig_{deck}");
            cameraRigGO.transform.SetParent(root.transform);
            cameraRigGO.transform.position = origin;
            var cameraRig = cameraRigGO.AddComponent<DeckCameraRig>();
            cameraRig.stageOrigin = origin;
            controller.cameraRig = cameraRig;

            // Create 2 cameras
            for (int i = 0; i < 2; i++)
            {
                var camGO = new GameObject($"Cam{i + 1}_{deck}");
                camGO.transform.SetParent(cameraRigGO.transform);
                float angle = (i == 0 ? 0f : 180f) * Mathf.Deg2Rad;
                camGO.transform.position = origin + new Vector3(
                    Mathf.Cos(angle) * 10f, 2f, Mathf.Sin(angle) * 10f
                );
                camGO.transform.LookAt(origin + Vector3.up);

                var cam = camGO.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 200f;
                cam.depth = i; // doesn't matter since they render to RT

                // Add URP camera data
                var urpCamData = camGO.AddComponent<UniversalAdditionalCameraData>();
                urpCamData.renderType = CameraRenderType.Base;
                urpCamData.renderPostProcessing = true;

                if (i == 0) cameraRig.cam1 = cam;
                else cameraRig.cam2 = cam;
            }

            // Light Rig
            var lightRigGO = new GameObject($"LightRig_{deck}");
            lightRigGO.transform.SetParent(root.transform);
            lightRigGO.transform.position = origin;
            var lightRig = lightRigGO.AddComponent<DeckLightRig>();
            lightRig.stageOrigin = origin;
            lightRig.activeLightCount = deck == DeckIdentity.A ? 6 : 4;
            lightRig.hue = deck == DeckIdentity.A ? 0f : 200f;
            lightRig.hueSpread = 60f;
            controller.lightRig = lightRig;

            // Directional light per stage
            var dirLightGO = new GameObject($"DirectionalLight_{deck}");
            dirLightGO.transform.SetParent(root.transform);
            dirLightGO.transform.position = origin + new Vector3(0, 10, 0);
            dirLightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
            var dirLight = dirLightGO.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 0.5f;
            dirLight.shadows = LightShadows.Soft;
        }
    }
}

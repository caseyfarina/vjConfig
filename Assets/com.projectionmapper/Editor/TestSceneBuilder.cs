using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace ProjectionMapper.Editor
{
    /// <summary>
    /// Creates a complete test scene for Projection Mapper.
    /// Menu: ProjectionMapper > Create Test Scene
    ///
    /// Builds:
    ///   - 3 cameras (A, B, C) each viewing different content
    ///   - Animated content: spinning cubes, orbiting spheres, color-cycling plane
    ///   - ProjectionMapperManager with 3 pre-configured surfaces
    ///   - Surfaces arranged as: left third, center third, right third of screen
    ///   - A helper runtime script that animates the scene content
    /// </summary>
    public static class TestSceneBuilder
    {
        [MenuItem("ProjectionMapper/Create Test Scene", false, 100)]
        public static void CreateTestScene()
        {
            // Confirm if current scene is dirty
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorUtility.DisplayDialog(
                    "Projection Mapper Test Scene",
                    "Current scene has unsaved changes. Create test scene anyway?\n" +
                    "(A new scene will be created.)",
                    "Create", "Cancel"))
                    return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "ProjectionMapper_TestScene";

            // === LIGHTING ===
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.15f, 0.2f);

            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.9f);
            sun.intensity = 1.2f;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // === CONTENT GROUP A: Spinning cubes ===
            var groupA = new GameObject("ContentGroup_A");
            groupA.transform.position = new Vector3(0f, 0f, 0f);

            CreateCubeCluster(groupA.transform, Vector3.zero, 5,
                new Color(0.2f, 0.6f, 1f), new Color(1f, 0.3f, 0.5f));

            var floorA = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorA.name = "Floor_A";
            floorA.transform.SetParent(groupA.transform);
            floorA.transform.localPosition = new Vector3(0f, -1.5f, 0f);
            floorA.transform.localScale = new Vector3(1f, 1f, 1f);
            SetMaterialColor(floorA, new Color(0.1f, 0.1f, 0.15f));

            // === CONTENT GROUP B: Orbiting spheres ===
            var groupB = new GameObject("ContentGroup_B");
            groupB.transform.position = new Vector3(30f, 0f, 0f);

            CreateSphereOrbit(groupB.transform, Vector3.zero, 8,
                new Color(1f, 0.8f, 0.2f), new Color(0.2f, 1f, 0.4f));

            var coreB = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreB.name = "Core_B";
            coreB.transform.SetParent(groupB.transform);
            coreB.transform.localPosition = Vector3.zero;
            coreB.transform.localScale = Vector3.one * 1.5f;
            SetMaterialColor(coreB, new Color(0.9f, 0.1f, 0.3f));
            SetEmission(coreB, new Color(0.9f, 0.1f, 0.3f) * 0.5f);

            // === CONTENT GROUP C: Vertical bars / abstract ===
            var groupC = new GameObject("ContentGroup_C");
            groupC.transform.position = new Vector3(60f, 0f, 0f);

            CreateBarField(groupC.transform, Vector3.zero, 12);

            // === CAMERAS ===
            var camA = CreateCamera("Camera_A", groupA.transform.position + new Vector3(0f, 3f, -8f),
                Quaternion.Euler(15f, 0f, 0f), new Color(0.02f, 0.02f, 0.05f));

            var camB = CreateCamera("Camera_B", groupB.transform.position + new Vector3(0f, 2f, -10f),
                Quaternion.Euler(10f, 0f, 0f), new Color(0.05f, 0.02f, 0.02f));

            var camC = CreateCamera("Camera_C", groupC.transform.position + new Vector3(0f, 5f, -12f),
                Quaternion.Euler(20f, 0f, 0f), new Color(0.02f, 0.05f, 0.02f));

            // Disable all camera rendering to screen — the manager will
            // capture via RT and render warped surfaces via GL overlay.
            // The cameras still render, but to their target textures.
            camA.enabled = true;
            camB.enabled = true;
            camC.enabled = true;

            // === OVERLAY CAMERA (renders nothing, but triggers OnPostRender) ===
            var overlayCamGo = new GameObject("OverlayCamera");
            var overlayCam = overlayCamGo.AddComponent<Camera>();
            overlayCam.clearFlags = CameraClearFlags.SolidColor;
            overlayCam.backgroundColor = Color.black;
            overlayCam.cullingMask = 0; // Render nothing
            overlayCam.depth = 100;     // Render last
            overlayCam.targetDisplay = 0;

            // === PROJECTION MAPPER ===
            var mgrGo = new GameObject("ProjectionMapper");
            var mgr = mgrGo.AddComponent<ProjectionMapperManager>();

            // Surface A: Left third of screen
            var surfA = new ProjectionSurface
            {
                name = "Left - Cubes",
                targetDisplay = 0,
                sourceMode = SurfaceSourceMode.Camera,
                sourceCameraPath = "Camera_A",
                sourceCamera = camA,
                renderResolution = new Vector2Int(1920, 1080),
                aaQuality = AAQuality.Low,
                corners = new Vector2[]
                {
                    new Vector2(0.02f, 0.95f), // TL
                    new Vector2(0.32f, 0.95f), // TR
                    new Vector2(0.32f, 0.05f), // BR
                    new Vector2(0.02f, 0.05f), // BL
                },
                enabled = true,
                dirty = true
            };

            // Surface B: Center third
            var surfB = new ProjectionSurface
            {
                name = "Center - Spheres",
                targetDisplay = 0,
                sourceMode = SurfaceSourceMode.Camera,
                sourceCameraPath = "Camera_B",
                sourceCamera = camB,
                renderResolution = new Vector2Int(1920, 1080),
                aaQuality = AAQuality.Low,
                corners = new Vector2[]
                {
                    new Vector2(0.34f, 0.95f),
                    new Vector2(0.66f, 0.95f),
                    new Vector2(0.66f, 0.05f),
                    new Vector2(0.34f, 0.05f),
                },
                enabled = true,
                dirty = true
            };

            // Surface C: Right third — slightly skewed to demo warping
            var surfC = new ProjectionSurface
            {
                name = "Right - Bars (skewed)",
                targetDisplay = 0,
                sourceMode = SurfaceSourceMode.Camera,
                sourceCameraPath = "Camera_C",
                sourceCamera = camC,
                renderResolution = new Vector2Int(1920, 1080),
                aaQuality = AAQuality.High,
                corners = new Vector2[]
                {
                    new Vector2(0.70f, 0.92f), // TL nudged in
                    new Vector2(0.98f, 0.98f), // TR
                    new Vector2(0.96f, 0.08f), // BR nudged in
                    new Vector2(0.68f, 0.05f), // BL
                },
                enabled = true,
                dirty = true
            };

            mgr.surfaces.Add(surfA);
            mgr.surfaces.Add(surfB);
            mgr.surfaces.Add(surfC);
            mgr.editMode = true;
            mgr.showGUI = false; // Start with GUI hidden, F12 to open

            // === ANIMATOR ===
            var animGo = new GameObject("TestSceneAnimator");
            animGo.AddComponent<TestSceneAnimator>();

            // === MARK DIRTY ===
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                "[ProjectionMapper] Test scene created!\n" +
                "  3 cameras -> 3 surfaces (left / center / right)\n" +
                "  Right surface is pre-skewed to demo homography warping\n" +
                "  Press Play, then F12 for config panel\n" +
                "  Hold 1-4 + Arrows to adjust corners in Edit Mode");
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static Camera CreateCamera(string name, Vector3 pos, Quaternion rot, Color bg)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.rotation = rot;
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.depth = -1; // Below overlay camera
            return cam;
        }

        private static void CreateCubeCluster(Transform parent, Vector3 center, int count,
            Color colorA, Color colorB)
        {
            for (int i = 0; i < count; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Cube_{i}";
                cube.transform.SetParent(parent);

                float angle = (i / (float)count) * Mathf.PI * 2f;
                float radius = 2f + (i % 3) * 0.8f;
                float y = (i % 2 == 0) ? 0f : 1.2f;
                cube.transform.localPosition = center + new Vector3(
                    Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);

                float scale = 0.5f + (i * 0.15f);
                cube.transform.localScale = Vector3.one * scale;
                cube.transform.rotation = Quaternion.Euler(i * 15f, i * 30f, i * 10f);

                float t = i / (float)Mathf.Max(1, count - 1);
                SetMaterialColor(cube, Color.Lerp(colorA, colorB, t));

                // Tag for animation
                cube.AddComponent<SpinTag>();
            }
        }

        private static void CreateSphereOrbit(Transform parent, Vector3 center, int count,
            Color colorA, Color colorB)
        {
            for (int i = 0; i < count; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"OrbitSphere_{i}";
                sphere.transform.SetParent(parent);

                float angle = (i / (float)count) * Mathf.PI * 2f;
                float radius = 3f + (i % 3);
                float y = Mathf.Sin(angle * 2f) * 1.5f;
                sphere.transform.localPosition = center + new Vector3(
                    Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);

                float scale = 0.3f + (i * 0.08f);
                sphere.transform.localScale = Vector3.one * scale;

                float t = i / (float)Mathf.Max(1, count - 1);
                Color c = Color.Lerp(colorA, colorB, t);
                SetMaterialColor(sphere, c);
                SetEmission(sphere, c * 0.3f);

                sphere.AddComponent<OrbitTag>();
            }
        }

        private static void CreateBarField(Transform parent, Vector3 center, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"Bar_{i}";
                bar.transform.SetParent(parent);

                float x = (i - count / 2f) * 1.2f;
                float baseHeight = 1f + Mathf.Abs(Mathf.Sin(i * 0.8f)) * 4f;
                bar.transform.localPosition = center + new Vector3(x, baseHeight / 2f, 0f);
                bar.transform.localScale = new Vector3(0.6f, baseHeight, 0.6f);

                float hue = (i / (float)count);
                Color c = Color.HSVToRGB(hue, 0.7f, 0.9f);
                SetMaterialColor(bar, c);
                SetEmission(bar, c * 0.15f);

                bar.AddComponent<PulseTag>();
            }
        }

        private static void SetMaterialColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            // Create unique material (URP Lit if available, fallback Standard)
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            var mat = new Material(shader);
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.6f);
            renderer.sharedMaterial = mat;
        }

        private static void SetEmission(GameObject go, Color emission)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            var mat = renderer.sharedMaterial;

            // URP Lit emission
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
            }
        }
    }

    // =========================================================================
    // TAG COMPONENTS (for the animator to find objects)
    // =========================================================================

    /// <summary>Tag: object should spin.</summary>
    public class SpinTag : MonoBehaviour { }

    /// <summary>Tag: object should orbit its parent.</summary>
    public class OrbitTag : MonoBehaviour { }

    /// <summary>Tag: object should pulse height.</summary>
    public class PulseTag : MonoBehaviour { }

    // =========================================================================
    // RUNTIME ANIMATOR
    // =========================================================================

    /// <summary>
    /// Animates test scene objects at runtime.
    /// Finds tagged objects and applies continuous motion.
    /// </summary>
    [ExecuteInEditMode]
    public class TestSceneAnimator : MonoBehaviour
    {
        private SpinTag[] spinners;
        private OrbitTag[] orbiters;
        private PulseTag[] pulsers;

        private void OnEnable()
        {
            CacheObjects();
        }

        private void CacheObjects()
        {
            spinners = FindObjectsByType<SpinTag>(FindObjectsSortMode.None);
            orbiters = FindObjectsByType<OrbitTag>(FindObjectsSortMode.None);
            pulsers = FindObjectsByType<PulseTag>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            float t = Time.time;

            // Spin cubes
            if (spinners != null)
            {
                for (int i = 0; i < spinners.Length; i++)
                {
                    if (spinners[i] == null) continue;
                    float speed = 30f + i * 10f;
                    spinners[i].transform.Rotate(
                        Vector3.up * speed * Time.deltaTime, Space.World);
                    // Bob vertically
                    var pos = spinners[i].transform.localPosition;
                    pos.y += Mathf.Sin(t * (1f + i * 0.3f)) * 0.003f;
                    spinners[i].transform.localPosition = pos;
                }
            }

            // Orbit spheres around parent
            if (orbiters != null)
            {
                for (int i = 0; i < orbiters.Length; i++)
                {
                    if (orbiters[i] == null) continue;
                    float speed = 20f + i * 5f;
                    orbiters[i].transform.RotateAround(
                        orbiters[i].transform.parent != null
                            ? orbiters[i].transform.parent.position
                            : Vector3.zero,
                        Vector3.up,
                        speed * Time.deltaTime);

                    // Vertical oscillation
                    var pos = orbiters[i].transform.localPosition;
                    pos.y = Mathf.Sin(t * (0.5f + i * 0.2f)) * 1.5f;
                    orbiters[i].transform.localPosition = pos;
                }
            }

            // Pulse bars
            if (pulsers != null)
            {
                for (int i = 0; i < pulsers.Length; i++)
                {
                    if (pulsers[i] == null) continue;
                    float baseH = 1f + Mathf.Abs(Mathf.Sin(i * 0.8f)) * 4f;
                    float pulse = baseH + Mathf.Sin(t * 2f + i * 0.5f) * 1.5f;
                    pulse = Mathf.Max(0.3f, pulse);

                    var tr = pulsers[i].transform;
                    tr.localScale = new Vector3(0.6f, pulse, 0.6f);
                    var pos = tr.localPosition;
                    pos.y = pulse / 2f;
                    tr.localPosition = pos;

                    // Cycle hue over time
                    var rend = pulsers[i].GetComponent<Renderer>();
                    if (rend != null && rend.material != null)
                    {
                        float hue = Mathf.Repeat((i / 12f) + t * 0.05f, 1f);
                        Color c = Color.HSVToRGB(hue, 0.7f, 0.9f);
                        rend.material.color = c;
                        if (rend.material.HasProperty("_EmissionColor"))
                            rend.material.SetColor("_EmissionColor", c * 0.15f);
                    }
                }
            }
        }
    }
}

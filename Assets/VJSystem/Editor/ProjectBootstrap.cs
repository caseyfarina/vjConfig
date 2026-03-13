using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

public static class ProjectBootstrap
{
    public static void Execute()
    {
        Debug.Log("[Bootstrap] Starting VJ project bootstrap...");

        // --- 1. Create URP assets if missing ---
        string settingsDir = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(settingsDir))
            AssetDatabase.CreateFolder("Assets", "Settings");

        // Create URP Renderer
        string rendererPath = settingsDir + "/VJ_Renderer.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
            Debug.Log("[Bootstrap] Created URP Renderer asset");
        }

        // Create URP Pipeline Asset
        string pipelinePath = settingsDir + "/VJ_URPAsset.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
            Debug.Log("[Bootstrap] Created URP Pipeline asset");
        }

        // Assign pipeline to graphics settings
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        Debug.Log("[Bootstrap] Assigned URP pipeline to Graphics/Quality settings");

        // --- 2. Create Volume Profile ---
        string volumeProfilePath = settingsDir + "/VJ_VolumeProfile.asset";
        var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumeProfilePath);
        if (volumeProfile == null)
        {
            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(volumeProfile, volumeProfilePath);
            Debug.Log("[Bootstrap] Created Volume Profile");
        }

        // Add DepthOfField override if missing
        if (!volumeProfile.Has<DepthOfField>())
        {
            var dof = volumeProfile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(10f);
            Debug.Log("[Bootstrap] Added DepthOfField to Volume Profile");
        }

        // Add Bloom override
        if (!volumeProfile.Has<Bloom>())
        {
            var bloom = volumeProfile.Add<Bloom>(true);
            bloom.intensity.Override(0.5f);
            bloom.threshold.Override(1f);
            Debug.Log("[Bootstrap] Added Bloom to Volume Profile");
        }

        // Add Vignette
        if (!volumeProfile.Has<Vignette>())
        {
            var vignette = volumeProfile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            Debug.Log("[Bootstrap] Added Vignette to Volume Profile");
        }

        EditorUtility.SetDirty(volumeProfile);

        // --- 3. Create Preset Library folder ---
        string presetsDir = "Assets/VJSystem/PresetLibraries";
        if (!AssetDatabase.IsValidFolder("Assets/VJSystem"))
            AssetDatabase.CreateFolder("Assets", "VJSystem");
        if (!AssetDatabase.IsValidFolder(presetsDir))
            AssetDatabase.CreateFolder("Assets/VJSystem", "PresetLibraries");

        // --- 4. Create Materials ---
        string matDir = "Assets/VJSystem/Materials";
        if (!AssetDatabase.IsValidFolder(matDir))
            AssetDatabase.CreateFolder("Assets/VJSystem", "Materials");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Bootstrap] Asset creation complete. Now building scene objects...");

        // --- 5. Build scene objects ---
        BuildSceneObjects(volumeProfile);

        // Save scene
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Bootstrap] DONE - Project bootstrap complete!");
    }

    static void BuildSceneObjects(VolumeProfile volumeProfile)
    {
        // === Ground Plane ===
        if (GameObject.Find("Ground") == null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5, 1, 5);
            // Dark ground material
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = new Color(0.08f, 0.08f, 0.12f);
            ground.GetComponent<Renderer>().material = groundMat;
            Debug.Log("[Bootstrap] Created Ground");
        }

        // === Spinning Cubes Parent ===
        var cubesParent = GameObject.Find("SpinningCubes");
        if (cubesParent == null)
        {
            cubesParent = new GameObject("SpinningCubes");
            cubesParent.transform.position = Vector3.zero;
        }

        // Create spinning cubes in a circle
        Color[] cubeColors = new Color[]
        {
            new Color(1f, 0.2f, 0.3f),    // Red
            new Color(0.2f, 0.8f, 1f),    // Cyan
            new Color(1f, 0.9f, 0.1f),    // Yellow
            new Color(0.4f, 1f, 0.3f),    // Green
            new Color(1f, 0.4f, 0.9f),    // Pink
            new Color(0.3f, 0.4f, 1f),    // Blue
            new Color(1f, 0.6f, 0.1f),    // Orange
            new Color(0.7f, 0.3f, 1f),    // Purple
        };

        for (int i = 0; i < 8; i++)
        {
            string cubeName = $"Cube_{i + 1}";
            if (GameObject.Find(cubeName) != null) continue;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = cubeName;
            cube.transform.SetParent(cubesParent.transform);

            float angle = i * Mathf.PI * 2f / 8f;
            float radius = 6f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = 1f + (i % 3) * 1.5f; // Varying heights
            cube.transform.position = new Vector3(x, y, z);

            float scale = 0.8f + (i % 3) * 0.4f;
            cube.transform.localScale = Vector3.one * scale;

            // Random initial rotation
            cube.transform.rotation = Quaternion.Euler(i * 30f, i * 45f, i * 15f);

            // Emissive material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = cubeColors[i];
            mat.SetColor("_EmissionColor", cubeColors[i] * 2f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            cube.GetComponent<Renderer>().material = mat;

            // Add spinner component
            var spinner = cube.AddComponent<SpinCube>();
            spinner.rotationSpeed = new Vector3(
                30f + i * 10f,
                50f + i * 8f,
                20f + i * 5f
            );
        }
        Debug.Log("[Bootstrap] Created 8 spinning cubes");

        // === Center Sphere ===
        if (GameObject.Find("CenterSphere") == null)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "CenterSphere";
            sphere.transform.position = new Vector3(0, 2.5f, 0);
            sphere.transform.localScale = Vector3.one * 2f;
            var sphereMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            sphereMat.color = Color.white;
            sphereMat.SetColor("_EmissionColor", Color.white * 3f);
            sphereMat.EnableKeyword("_EMISSION");
            sphere.GetComponent<Renderer>().material = sphereMat;

            var spinner = sphere.AddComponent<SpinCube>();
            spinner.rotationSpeed = new Vector3(10f, 25f, 5f);
            Debug.Log("[Bootstrap] Created CenterSphere");
        }

        // === Directional Light ===
        var existingLight = Object.FindFirstObjectByType<Light>();
        if (existingLight == null)
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.9f);
            light.intensity = 1.2f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Debug.Log("[Bootstrap] Created Directional Light");
        }

        // === Point Lights for VJ feel ===
        Color[] lightColors = { Color.red, Color.blue, Color.green, Color.magenta };
        for (int i = 0; i < 4; i++)
        {
            string lightName = $"PointLight_{i + 1}";
            if (GameObject.Find(lightName) != null) continue;

            var lightGO = new GameObject(lightName);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColors[i];
            light.intensity = 5f;
            light.range = 15f;

            float angle = i * Mathf.PI * 2f / 4f;
            lightGO.transform.position = new Vector3(
                Mathf.Cos(angle) * 4f, 4f, Mathf.Sin(angle) * 4f
            );
            Debug.Log($"[Bootstrap] Created {lightName}");
        }

        // === Global Volume ===
        if (GameObject.Find("Global Volume") == null)
        {
            var volumeGO = new GameObject("Global Volume");
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = volumeProfile;
            volume.priority = 1f;
            Debug.Log("[Bootstrap] Created Global Volume");
        }
    }
}

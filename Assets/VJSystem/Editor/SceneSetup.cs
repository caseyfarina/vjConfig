using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using System;
using System.Reflection;

public static class SceneSetup
{
    static Assembly runtimeAssembly;

    static Type GetRuntimeType(string typeName)
    {
        if (runtimeAssembly == null)
            runtimeAssembly = Assembly.Load("Assembly-CSharp");
        // All VJ types are in VJSystem namespace
        var type = runtimeAssembly.GetType("VJSystem." + typeName);
        if (type == null)
            type = runtimeAssembly.GetType(typeName); // fallback for non-namespaced
        return type;
    }

    public static void Execute()
    {
        Debug.Log("[Setup] Starting full VJ scene setup...");

        SetupRendererFeatures();
        SetupVolumeOverrides();
        CreatePresetLibraries();
        CreateSystemGameObjects();
        SetupCameraSystem();

        EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VJ_Renderer.asset"));
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Setup] DONE - Full VJ scene setup complete!");
    }

    static void SetupRendererFeatures()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VJ_Renderer.asset");
        if (renderer == null) { Debug.LogError("[Setup] VJ_Renderer.asset not found!"); return; }

        bool hasPixelSort = false;
        bool hasChromatic = false;
        foreach (var feature in renderer.rendererFeatures)
        {
            if (feature != null && feature.GetType().Name == "PixelSortFeature") hasPixelSort = true;
            if (feature != null && feature.GetType().Name == "ChromaticDisplacementFeature") hasChromatic = true;
        }

        if (!hasPixelSort)
        {
            var featureType = GetRuntimeType("PixelSortFeature");
            if (featureType != null)
            {
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(featureType);
                feature.name = "PixelSort";
                var computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/VJSystem/Shaders/PixelSort.compute");
                if (computeShader != null)
                {
                    var field = featureType.GetField("m_ComputeShader", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (field != null) field.SetValue(feature, computeShader);
                }
                AddRendererFeature(renderer, feature);
                Debug.Log("[Setup] Added PixelSortFeature to renderer");
            }
            else Debug.LogWarning("[Setup] PixelSortFeature type not found");
        }

        if (!hasChromatic)
        {
            var featureType = GetRuntimeType("ChromaticDisplacementFeature");
            if (featureType != null)
            {
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(featureType);
                feature.name = "ChromaticDisplacement";
                AddRendererFeature(renderer, feature);
                Debug.Log("[Setup] Added ChromaticDisplacementFeature to renderer");
            }
            else Debug.LogWarning("[Setup] ChromaticDisplacementFeature type not found");
        }

        EditorUtility.SetDirty(renderer);
    }

    static void AddRendererFeature(UniversalRendererData renderer, ScriptableRendererFeature feature)
    {
        AssetDatabase.AddObjectToAsset(feature, renderer);
        var listField = typeof(UniversalRendererData).GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
        if (listField != null)
        {
            var list = (System.Collections.Generic.List<ScriptableRendererFeature>)listField.GetValue(renderer);
            list.Add(feature);
        }
        var mapField = typeof(UniversalRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mapField != null)
        {
            var map = (System.Collections.Generic.List<long>)mapField.GetValue(renderer);
            map.Add(feature.GetInstanceID());
        }
        EditorUtility.SetDirty(renderer);
    }

    static void SetupVolumeOverrides()
    {
        var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/VJ_VolumeProfile.asset");
        if (volumeProfile == null) { Debug.LogError("[Setup] VJ_VolumeProfile.asset not found!"); return; }

        AddVolumeOverrideIfMissing(volumeProfile, "PixelSortVolume");
        AddVolumeOverrideIfMissing(volumeProfile, "ChromaticDisplacementVolume");
        EditorUtility.SetDirty(volumeProfile);
    }

    static void AddVolumeOverrideIfMissing(VolumeProfile profile, string typeName)
    {
        var type = GetRuntimeType(typeName);
        if (type == null) { Debug.LogWarning($"[Setup] {typeName} type not found"); return; }

        foreach (var c in profile.components)
            if (c.GetType() == type) return;

        var comp = (VolumeComponent)ScriptableObject.CreateInstance(type);
        comp.name = typeName;
        foreach (var param in comp.parameters)
            param.overrideState = true;
        profile.components.Add(comp);
        AssetDatabase.AddObjectToAsset(comp, profile);
        Debug.Log($"[Setup] Added {typeName} to volume profile");
    }

    static void CreatePresetLibraries()
    {
        string dir = "Assets/VJSystem/PresetLibraries";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/VJSystem", "PresetLibraries");

        CreatePresetLibraryAsset("PixelSortPresetLibrary", dir, "PixelSortPresets");
        CreatePresetLibraryAsset("ChromaticPresetLibrary", dir, "ChromaticPresets");
        CreatePresetLibraryAsset("DoFPresetLibrary", dir, "DoFPresets");
    }

    static void CreatePresetLibraryAsset(string typeName, string dir, string assetName)
    {
        string path = $"{dir}/{assetName}.asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null)
        {
            Debug.Log($"[Setup] {assetName} already exists, skipping");
            return;
        }
        var type = GetRuntimeType(typeName);
        if (type == null) { Debug.LogWarning($"[Setup] {typeName} type not found"); return; }

        var asset = ScriptableObject.CreateInstance(type);
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[Setup] Created {assetName}.asset");
    }

    static void CreateSystemGameObjects()
    {
        var globalVolume = GameObject.Find("Global Volume");
        Volume volumeComp = globalVolume != null ? globalVolume.GetComponent<Volume>() : null;

        var pixelSortLib = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/VJSystem/PresetLibraries/PixelSortPresets.asset");
        var chromaticLib = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/VJSystem/PresetLibraries/ChromaticPresets.asset");
        var dofLib = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/VJSystem/PresetLibraries/DoFPresets.asset");

        // Systems parent
        var systemsParent = GameObject.Find("--- VJ Systems ---");
        if (systemsParent == null)
        {
            systemsParent = new GameObject("--- VJ Systems ---");
            systemsParent.transform.position = Vector3.zero;
        }

        // Effect systems
        var pixelSortSystem = FindOrCreateComponent(systemsParent, "PixelSortSystem", "PixelSortSystem");
        SetField(pixelSortSystem, "globalVolume", volumeComp);
        SetField(pixelSortSystem, "presetLibrary", pixelSortLib);

        var chromaticSystem = FindOrCreateComponent(systemsParent, "ChromaticDisplacementSystem", "ChromaticDisplacementSystem");
        SetField(chromaticSystem, "globalVolume", volumeComp);
        SetField(chromaticSystem, "presetLibrary", chromaticLib);

        var dofSystem = FindOrCreateComponent(systemsParent, "DepthOfFieldSystem", "DepthOfFieldSystem");
        SetField(dofSystem, "globalVolume", volumeComp);
        SetField(dofSystem, "presetLibrary", dofLib);

        // MIDI
        FindOrCreateComponent(systemsParent, "MidiEventManager", "MidiEventManager");
        FindOrCreateComponent(systemsParent, "MidiGridRouter", "MidiGridRouter");
        FindOrCreateComponent(systemsParent, "UnityMainThreadDispatcher", "UnityMainThreadDispatcher");

        // PostFXRouter
        var postFXRouter = FindOrCreateComponent(systemsParent, "PostFXRouter", "PostFXRouter");
        SetField(postFXRouter, "dofSystem", dofSystem);
        SetField(postFXRouter, "pixelSortSystem", pixelSortSystem);
        SetField(postFXRouter, "chromaticSystem", chromaticSystem);

        // Preset & Randomization
        var presetSaveSystem = FindOrCreateComponent(systemsParent, "PresetSaveSystem", "PresetSaveSystem");
        SetField(presetSaveSystem, "postFXRouter", postFXRouter);

        var randomizationSystem = FindOrCreateComponent(systemsParent, "RandomizationSystem", "RandomizationSystem");
        SetField(randomizationSystem, "postFXRouter", postFXRouter);

        // Light, Scene, Spout
        FindOrCreateComponent(systemsParent, "VJLightSystem", "VJLightSystem");
        FindOrCreateComponent(systemsParent, "VJSceneSlotSystem", "VJSceneSlotSystem");

        var spoutManager = FindOrCreateComponent(systemsParent, "SpoutOutputManager", "SpoutOutputManager");
        var mainCam = Camera.main;
        if (mainCam != null) SetField(spoutManager, "mainCamera", mainCam);

        Debug.Log("[Setup] All system GameObjects created and wired");
    }

    static Component FindOrCreateComponent(GameObject parent, string childName, string typeName)
    {
        var type = GetRuntimeType(typeName);
        if (type == null) { Debug.LogWarning($"[Setup] Type {typeName} not found"); return null; }

        // Check existing children
        var existing = parent.transform.Find(childName);
        if (existing != null)
        {
            var comp = existing.GetComponent(type);
            if (comp != null) return comp;
        }

        var go = new GameObject(childName);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = Vector3.zero;
        var component = go.AddComponent(type);
        Debug.Log($"[Setup] Created {childName}");
        return component;
    }

    static void SetField(object target, string fieldName, object value)
    {
        if (target == null || value == null) return;
        var type = target.GetType();
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
            if (target is Component comp) EditorUtility.SetDirty(comp);
        }
        else
            Debug.LogWarning($"[Setup] Field '{fieldName}' not found on {type.Name}");
    }

    static void SetupCameraSystem()
    {
        var mainCam = Camera.main;
        if (mainCam == null) { Debug.LogError("[Setup] No Main Camera found!"); return; }

        // CinemachineBrain
        var brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = mainCam.gameObject.AddComponent<CinemachineBrain>();
            Debug.Log("[Setup] Added CinemachineBrain to Main Camera");
        }

        // Camera rig
        var cameraRig = GameObject.Find("CameraRig");
        if (cameraRig == null)
        {
            cameraRig = new GameObject("CameraRig");
            cameraRig.transform.position = Vector3.zero;
        }

        string[] cameraNames = { "CM_Wide", "CM_Closeup", "CM_LowAngle", "CM_Overhead", "CM_Orbital", "CM_Handheld", "CM_Figure8", "CM_ZoomPulse" };
        Vector3[] positions = {
            new Vector3(0, 5, -12),
            new Vector3(3, 2, -4),
            new Vector3(-5, 0.5f, -3),
            new Vector3(0, 12, -1),
            new Vector3(8, 4, -8),
            new Vector3(-2, 3, -6),
            new Vector3(0, 3, -8),
            new Vector3(0, 4, -10),
        };
        Vector3[] lookAts = {
            Vector3.zero,
            new Vector3(0, 2, 0),
            new Vector3(0, 2, 0),
            Vector3.zero,
            Vector3.zero,
            new Vector3(0, 2, 0),
            Vector3.zero,
            Vector3.zero,
        };

        var cinemachineCameras = new CinemachineCamera[8];

        for (int i = 0; i < 8; i++)
        {
            var existing = cameraRig.transform.Find(cameraNames[i]);
            CinemachineCamera cmCam;

            if (existing != null)
            {
                cmCam = existing.GetComponent<CinemachineCamera>();
                if (cmCam == null) cmCam = existing.gameObject.AddComponent<CinemachineCamera>();
            }
            else
            {
                var go = new GameObject(cameraNames[i]);
                go.transform.SetParent(cameraRig.transform);
                cmCam = go.AddComponent<CinemachineCamera>();
                go.transform.position = positions[i];
                go.transform.LookAt(lookAts[i]);
                Debug.Log($"[Setup] Created {cameraNames[i]}");
            }

            cmCam.Priority.Value = (i == 0) ? 100 : 0;
            cinemachineCameras[i] = cmCam;
        }

        // Extensions on specific cameras
        AddExtIfMissing(cinemachineCameras[4].gameObject, "OrbitalDriftExtension");
        AddExtIfMissing(cinemachineCameras[5].gameObject, "HandheldNoiseExtension");
        AddExtIfMissing(cinemachineCameras[6].gameObject, "Figure8PathExtension");
        AddExtIfMissing(cinemachineCameras[7].gameObject, "ZoomPulseExtension");

        // Wire VJCameraSystem
        var systemsParent = GameObject.Find("--- VJ Systems ---");
        if (systemsParent != null)
        {
            var vjCamSystemType = GetRuntimeType("VJCameraSystem");
            Component vjCamSystem = null;
            var existingCamSys = systemsParent.transform.Find("VJCameraSystem");
            if (existingCamSys != null)
                vjCamSystem = existingCamSys.GetComponent(vjCamSystemType);

            if (vjCamSystem == null)
            {
                var go = new GameObject("VJCameraSystem");
                go.transform.SetParent(systemsParent.transform);
                go.transform.localPosition = Vector3.zero;
                vjCamSystem = go.AddComponent(vjCamSystemType);
                Debug.Log("[Setup] Created VJCameraSystem");
            }

            SetField(vjCamSystem, "cameras", cinemachineCameras);
            SetField(vjCamSystem, "brain", brain);
            Debug.Log("[Setup] Wired VJCameraSystem with 8 cameras + brain");
        }
    }

    static void AddExtIfMissing(GameObject go, string typeName)
    {
        var type = GetRuntimeType(typeName);
        if (type == null) { Debug.LogWarning($"[Setup] Extension type {typeName} not found"); return; }
        if (go.GetComponent(type) == null)
        {
            go.AddComponent(type);
            Debug.Log($"[Setup] Added {typeName} to {go.name}");
        }
    }
}

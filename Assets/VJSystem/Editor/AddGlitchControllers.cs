using UnityEngine;
using UnityEditor;
using KinoGlitch;

public static class AddGlitchControllers
{
    public static void Execute()
    {
        var analogShader  = Shader.Find("Hidden/KinoGlitch/Analog");
        var digitalShader = Shader.Find("Hidden/KinoGlitch/Digital");

        if (analogShader == null || digitalShader == null)
        {
            Debug.LogError("[AddGlitchControllers] Could not find KinoGlitch shaders via Shader.Find.");
            return;
        }

        string[] cameraPaths =
        {
            "--- Stage A ---/CameraRig_A/Cam1_A",
            "--- Stage A ---/CameraRig_A/Cam2_A",
            "--- Stage B ---/CameraRig_B/Cam1_B",
            "--- Stage B ---/CameraRig_B/Cam2_B",
        };

        foreach (var path in cameraPaths)
        {
            var go = GameObject.Find(path);
            if (go == null) { Debug.LogWarning($"[AddGlitchControllers] Not found: {path}"); continue; }

            SetupAnalog(go, analogShader);
            SetupDigital(go, digitalShader);
            EditorUtility.SetDirty(go);
        }

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[AddGlitchControllers] Done — glitch controllers added and shaders assigned.");
    }

    static void SetupAnalog(GameObject go, Shader shader)
    {
        var ctrl = go.GetComponent<AnalogGlitchController>()
                ?? go.AddComponent<AnalogGlitchController>();

        // Assign shader via SerializedObject so it's properly serialised
        var so   = new SerializedObject(ctrl);
        var prop = so.FindProperty("_shader");
        if (prop != null)
        {
            prop.objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Start values at zero
        ctrl.ScanLineJitter  = 0f;
        ctrl.VerticalJump    = 0f;
        ctrl.HorizontalShake = 0f;
        ctrl.ColorDrift      = 0f;
    }

    static void SetupDigital(GameObject go, Shader shader)
    {
        var ctrl = go.GetComponent<DigitalGlitchController>()
                ?? go.AddComponent<DigitalGlitchController>();

        var so   = new SerializedObject(ctrl);
        var prop = so.FindProperty("_shader");
        if (prop != null)
        {
            prop.objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        ctrl.Intensity = 0f;
    }
}

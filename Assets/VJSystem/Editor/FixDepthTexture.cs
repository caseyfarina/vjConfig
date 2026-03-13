using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;

public static class FixDepthTexture
{
    public static void Execute()
    {
        string[] paths =
        {
            "--- Stage A ---/CameraRig_A/Cam1_A",
            "--- Stage A ---/CameraRig_A/Cam2_A",
            "--- Stage B ---/CameraRig_B/Cam1_B",
            "--- Stage B ---/CameraRig_B/Cam2_B",
        };

        foreach (var path in paths)
        {
            var go = GameObject.Find(path);
            if (go == null) { Debug.LogWarning($"[FixDepthTexture] Not found: {path}"); continue; }

            var camData = go.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) { Debug.LogWarning($"[FixDepthTexture] No URP camera data on {path}"); continue; }

            camData.requiresDepthOption = CameraOverrideOption.On;
            EditorUtility.SetDirty(go);
            Debug.Log($"[FixDepthTexture] Depth texture enabled on {go.name}");
        }

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[FixDepthTexture] Done.");
    }
}

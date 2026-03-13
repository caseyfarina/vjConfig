using UnityEngine;
using UnityEditor;
using VJSystem;

public static class SetDarkDefaults
{
    public static void Execute()
    {
        // Zero directional lights
        SetLightIntensity("--- Stage A ---/DirectionalLight_A", 0f);
        SetLightIntensity("--- Stage B ---/DirectionalLight_B", 0f);

        // Zero light rigs
        SetLightRig("--- Stage A ---/LightRig_A", 0f);
        SetLightRig("--- Stage B ---/LightRig_B", 0f);

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SetDarkDefaults] All lights set to zero intensity.");
    }

    static void SetLightIntensity(string path, float intensity)
    {
        var go = GameObject.Find(path);
        if (go == null) { Debug.LogError($"[SetDarkDefaults] Not found: {path}"); return; }
        var light = go.GetComponent<Light>();
        if (light == null) { Debug.LogError($"[SetDarkDefaults] No Light on {path}"); return; }
        light.intensity = intensity;
        EditorUtility.SetDirty(go);
    }

    static void SetLightRig(string path, float intensity)
    {
        var go = GameObject.Find(path);
        if (go == null) { Debug.LogError($"[SetDarkDefaults] Not found: {path}"); return; }
        var rig = go.GetComponent<DeckLightRig>();
        if (rig == null) { Debug.LogError($"[SetDarkDefaults] No DeckLightRig on {path}"); return; }
        rig.lightIntensity = intensity;
        rig.activeLightCount = 0;
        EditorUtility.SetDirty(go);
    }
}

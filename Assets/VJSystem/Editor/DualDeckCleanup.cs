using UnityEngine;
using UnityEditor;

public static class DualDeckCleanup
{
    public static void Execute()
    {
        // Disable old scene objects that conflict with Stage A at origin
        string[] toDisable = {
            "Main Camera",
            "SpinningCubes",
            "CenterSphere",
            "Ground",
            "Directional Light",
            "PointLight_1", "PointLight_2", "PointLight_3", "PointLight_4",
            "CameraRig",
            "UI"
        };

        foreach (string name in toDisable)
        {
            var obj = GameObject.Find(name);
            if (obj != null)
            {
                obj.SetActive(false);
                Debug.Log($"[Cleanup] Disabled: {name}");
            }
        }

        // Keep Global Volume active (needed for URP rendering)

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Cleanup] Old scene objects disabled. Scene ready for dual-deck.");
    }
}

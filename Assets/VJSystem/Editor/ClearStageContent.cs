using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ClearStageContent
{
    [MenuItem("VJSystem/Clear Stage Content")]
    public static void Execute()
    {
        var stages = Object.FindObjectsByType<VJSystem.StageController>(FindObjectsSortMode.None);
        foreach (var s in stages)
        {
            s.autoSpawn = false;
            s.ClearContent();
            EditorUtility.SetDirty(s);
            Debug.Log($"[ClearStageContent] Cleared {s.name}");
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
}

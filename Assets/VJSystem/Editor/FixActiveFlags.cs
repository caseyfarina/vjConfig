using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class FixActiveFlags
{
    public static void Execute()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/VJ_VolumeProfile.asset");
        if (profile == null) { Debug.LogError("[FixActive] Profile not found"); return; }

        foreach (var comp in profile.components)
        {
            Debug.Log($"[FixActive] {comp.GetType().Name}: active={comp.active}");
            if (!comp.active)
            {
                comp.active = true;
                EditorUtility.SetDirty(comp);
                Debug.Log($"[FixActive]   -> Set active=true");
            }
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[FixActive] Done");
    }
}

using UnityEngine;
using UnityEngine.VFX;
using UnityEditor;
using VJSystem;

public static class WireCapacityVFX
{
    public static void Execute()
    {
        var routerGO = GameObject.Find("--- Dual Deck Systems ---/PostFXRouter");
        if (routerGO == null) { Debug.LogError("[WireCapacityVFX] DualDeckPostFXRouter not found."); return; }

        var router = routerGO.GetComponent<DualDeckPostFXRouter>();
        if (router == null) { Debug.LogError("[WireCapacityVFX] DualDeckPostFXRouter component not found."); return; }

        var vfxAGO = GameObject.Find("--- Stage A ---/Capacity (1)");
        var vfxBGO = GameObject.Find("--- Stage B ---/Capacity");

        if (vfxAGO == null) { Debug.LogError("[WireCapacityVFX] 'Capacity (1)' not found under Stage A."); return; }
        if (vfxBGO == null) { Debug.LogError("[WireCapacityVFX] 'Capacity' not found under Stage B."); return; }

        router.capacityVfxA = vfxAGO.GetComponent<VisualEffect>();
        router.capacityVfxB = vfxBGO.GetComponent<VisualEffect>();

        if (router.capacityVfxA == null) { Debug.LogError("[WireCapacityVFX] No VisualEffect on Stage A Capacity."); return; }
        if (router.capacityVfxB == null) { Debug.LogError("[WireCapacityVFX] No VisualEffect on Stage B Capacity."); return; }

        EditorUtility.SetDirty(routerGO);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[WireCapacityVFX] capacityVfxA={router.capacityVfxA.name}, capacityVfxB={router.capacityVfxB.name}");
    }
}

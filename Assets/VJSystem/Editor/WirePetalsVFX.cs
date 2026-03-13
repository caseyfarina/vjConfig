using UnityEngine;
using UnityEngine.VFX;
using UnityEditor;
using VJSystem;

public static class WirePetalsVFX
{
    public static void Execute()
    {
        var routerGO = GameObject.Find("--- Dual Deck Systems ---/PostFXRouter");
        if (routerGO == null) { Debug.LogError("[WirePetalsVFX] PostFXRouter not found."); return; }

        var router = routerGO.GetComponent<DualDeckPostFXRouter>();
        if (router == null) { Debug.LogError("[WirePetalsVFX] DualDeckPostFXRouter component not found."); return; }

        var vfxAGO = GameObject.Find("--- Stage A ---/petals");
        var vfxBGO = GameObject.Find("--- Stage B ---/petals (1)");

        if (vfxAGO == null) { Debug.LogError("[WirePetalsVFX] 'petals' not found under Stage A."); return; }
        if (vfxBGO == null) { Debug.LogError("[WirePetalsVFX] 'petals (1)' not found under Stage B."); return; }

        router.petalsVfxA = vfxAGO.GetComponent<VisualEffect>();
        router.petalsVfxB = vfxBGO.GetComponent<VisualEffect>();

        if (router.petalsVfxA == null) { Debug.LogError("[WirePetalsVFX] No VisualEffect on Stage A petals."); return; }
        if (router.petalsVfxB == null) { Debug.LogError("[WirePetalsVFX] No VisualEffect on Stage B petals."); return; }

        EditorUtility.SetDirty(routerGO);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[WirePetalsVFX] petalsVfxA={router.petalsVfxA.name}, petalsVfxB={router.petalsVfxB.name}");
    }
}

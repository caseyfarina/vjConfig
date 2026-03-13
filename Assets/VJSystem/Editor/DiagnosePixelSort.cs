using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class DiagnosePixelSort
{
    public static void Execute()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/VJ_VolumeProfile.asset");
        if (profile == null) { Debug.LogError("[DiagPS] Profile not found"); return; }

        foreach (var comp in profile.components)
        {
            if (comp.GetType().Name == "PixelSortVolume")
            {
                Debug.Log($"[DiagPS] Found PixelSortVolume: active={comp.active}");

                foreach (var param in comp.parameters)
                {
                    Debug.Log($"[DiagPS]   {param.GetType().Name} overrideState={param.overrideState} value={GetParamValue(param)}");
                }

                // Check specific fields
                var strengthField = comp.GetType().GetField("strength");
                var strength = strengthField.GetValue(comp) as ClampedFloatParameter;
                Debug.Log($"[DiagPS] strength.value={strength.value} strength.overrideState={strength.overrideState}");

                var threshLow = comp.GetType().GetField("thresholdLow").GetValue(comp) as ClampedFloatParameter;
                Debug.Log($"[DiagPS] thresholdLow.value={threshLow.value} overrideState={threshLow.overrideState}");

                var threshHigh = comp.GetType().GetField("thresholdHigh").GetValue(comp) as ClampedFloatParameter;
                Debug.Log($"[DiagPS] thresholdHigh.value={threshHigh.value} overrideState={threshHigh.overrideState}");
            }
        }
    }

    static string GetParamValue(VolumeParameter param)
    {
        try
        {
            var prop = param.GetType().GetProperty("value");
            return prop?.GetValue(param)?.ToString() ?? "null";
        }
        catch { return "?"; }
    }
}

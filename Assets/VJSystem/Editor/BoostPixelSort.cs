using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class BoostPixelSort
{
    public static void Execute()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/VJ_VolumeProfile.asset");
        if (profile == null) { Debug.LogError("[Boost] Profile not found"); return; }

        foreach (var comp in profile.components)
        {
            if (comp.GetType().Name == "PixelSortVolume")
            {
                // Max out strength
                var strengthField = comp.GetType().GetField("strength");
                var strength = strengthField.GetValue(comp) as UnityEngine.Rendering.ClampedFloatParameter;
                strength.Override(1.0f);
                Debug.Log($"[Boost] strength = {strength.value}");

                // Very wide thresholds to include almost all pixels
                var threshLow = comp.GetType().GetField("thresholdLow").GetValue(comp) as ClampedFloatParameter;
                threshLow.Override(0.02f);
                Debug.Log($"[Boost] thresholdLow = {threshLow.value}");

                var threshHigh = comp.GetType().GetField("thresholdHigh").GetValue(comp) as ClampedFloatParameter;
                threshHigh.Override(0.98f);
                Debug.Log($"[Boost] thresholdHigh = {threshHigh.value}");

                // Sort by luminance, ascending — creates visible streaking
                var sortAxis = comp.GetType().GetField("sortAxis");
                var sortAxisParam = sortAxis.GetValue(comp);
                // SortAxis.Horizontal = 0
                var overrideMethod = sortAxisParam.GetType().GetMethod("Override");
                // Use reflection to set the enum
                var sortAxisType = comp.GetType().Assembly.GetType("VJSystem.SortAxis");
                overrideMethod.Invoke(sortAxisParam, new object[] { System.Enum.ToObject(sortAxisType, 0) }); // Horizontal
                Debug.Log("[Boost] sortAxis = Horizontal");

                EditorUtility.SetDirty(comp);
            }
        }

        // Also temporarily reduce chromatic displacement so pixel sort is more visible
        foreach (var comp in profile.components)
        {
            if (comp.GetType().Name == "ChromaticDisplacementVolume")
            {
                var amount = comp.GetType().GetField("displacementAmount").GetValue(comp) as ClampedFloatParameter;
                amount.Override(0.01f); // Subtle
                EditorUtility.SetDirty(comp);
                Debug.Log("[Boost] ChromaticDisplacement amount = 0.01 (reduced for visibility)");
            }
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[Boost] Done — enter play mode to see pixel sort effect");
    }
}

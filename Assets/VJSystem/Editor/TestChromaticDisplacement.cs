using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

/// <summary>
/// Systematic visual test for the ChromaticDisplacement effect.
/// Disables all other effects, then modulates each parameter one at a time,
/// capturing screenshots for visual comparison.
///
/// Run via Coplay MCP execute_script.
/// </summary>
public static class TestChromaticDisplacement
{
    private const string ProfilePath = "Assets/Settings/VJ_VolumeProfile.asset";
    private const string ScreenshotDir = "Assets/VJSystem/Editor/ChromDispTest";

    public static void Execute()
    {
        Debug.Log("[ChromDispTest] === Starting Chromatic Displacement Parameter Test ===");

        // Ensure screenshot directory exists
        if (!Directory.Exists(ScreenshotDir))
            Directory.CreateDirectory(ScreenshotDir);

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogError("[ChromDispTest] Profile not found at " + ProfilePath);
            return;
        }

        // --- Step 1: Disable all non-chromatic effects ---
        DisableOtherEffects(profile);

        // --- Step 2: Get ChromaticDisplacementVolume ---
        var asm = Assembly.Load("Assembly-CSharp");
        var cdType = asm.GetType("VJSystem.ChromaticDisplacementVolume");

        VolumeComponent cdComp = null;
        foreach (var comp in profile.components)
        {
            if (comp.GetType() == cdType)
            {
                cdComp = comp;
                break;
            }
        }

        if (cdComp == null)
        {
            Debug.LogError("[ChromDispTest] ChromaticDisplacementVolume not found in profile");
            return;
        }

        // Ensure it's active
        cdComp.active = true;
        foreach (var param in cdComp.parameters)
            param.overrideState = true;

        // --- Step 3: Set baseline visible values ---
        SetBaseline(cdComp);
        EditorUtility.SetDirty(cdComp);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        Debug.Log("[ChromDispTest] Baseline set. All other effects disabled.");
        Debug.Log("[ChromDispTest] Ready for parameter modulation tests.");
        Debug.Log("[ChromDispTest] Current ChromaticDisplacement state:");
        LogAllParams(cdComp);
    }

    /// <summary>
    /// Called to test a specific parameter configuration.
    /// Args JSON: { "test": "testName", "param": "fieldName", "value": "floatValue" }
    /// </summary>
    public static void SetParam(string argsJson)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) { Debug.LogError("[ChromDispTest] Profile not found"); return; }

        var asm = Assembly.Load("Assembly-CSharp");
        var cdType = asm.GetType("VJSystem.ChromaticDisplacementVolume");

        VolumeComponent cdComp = null;
        foreach (var comp in profile.components)
        {
            if (comp.GetType() == cdType) { cdComp = comp; break; }
        }
        if (cdComp == null) { Debug.LogError("[ChromDispTest] ChromDisp not found"); return; }

        // Parse simple key=value from args
        // Expected format: "fieldName=value"
        var parts = argsJson.Split('=');
        if (parts.Length != 2)
        {
            Debug.LogError($"[ChromDispTest] Invalid args format: {argsJson}. Expected: fieldName=value");
            return;
        }

        string fieldName = parts[0].Trim();
        string valueStr = parts[1].Trim();

        var field = cdType.GetField(fieldName);
        if (field == null)
        {
            Debug.LogError($"[ChromDispTest] Field '{fieldName}' not found on ChromaticDisplacementVolume");
            return;
        }

        var paramObj = field.GetValue(cdComp);

        // Handle different parameter types
        if (paramObj is ClampedFloatParameter cfp)
        {
            if (float.TryParse(valueStr, out float fv))
            {
                cfp.Override(fv);
                Debug.Log($"[ChromDispTest] Set {fieldName} = {fv}");
            }
        }
        else if (paramObj is BoolParameter bp)
        {
            if (bool.TryParse(valueStr, out bool bv))
            {
                bp.Override(bv);
                Debug.Log($"[ChromDispTest] Set {fieldName} = {bv}");
            }
        }
        else if (paramObj is Vector2Parameter v2p)
        {
            var coords = valueStr.Split(',');
            if (coords.Length == 2 && float.TryParse(coords[0], out float x) && float.TryParse(coords[1], out float y))
            {
                v2p.Override(new Vector2(x, y));
                Debug.Log($"[ChromDispTest] Set {fieldName} = ({x},{y})");
            }
        }
        else
        {
            // Try enum parameters via reflection
            var valueProp = paramObj.GetType().GetProperty("value");
            if (valueProp != null)
            {
                var valueType = valueProp.PropertyType;
                if (valueType.IsEnum)
                {
                    try
                    {
                        var enumVal = System.Enum.Parse(valueType, valueStr);
                        // Call Override method
                        var overrideMethod = paramObj.GetType().GetMethod("Override");
                        if (overrideMethod != null)
                        {
                            overrideMethod.Invoke(paramObj, new object[] { enumVal });
                            Debug.Log($"[ChromDispTest] Set {fieldName} = {valueStr}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[ChromDispTest] Failed to set enum {fieldName}: {e.Message}");
                    }
                }
            }
        }

        EditorUtility.SetDirty(cdComp);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Reset to baseline after each test
    /// </summary>
    public static void ResetBaseline()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) return;

        var asm = Assembly.Load("Assembly-CSharp");
        var cdType = asm.GetType("VJSystem.ChromaticDisplacementVolume");

        VolumeComponent cdComp = null;
        foreach (var comp in profile.components)
        {
            if (comp.GetType() == cdType) { cdComp = comp; break; }
        }
        if (cdComp == null) return;

        SetBaseline(cdComp);
        EditorUtility.SetDirty(cdComp);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[ChromDispTest] Reset to baseline");
    }

    // =========================================================================

    private static void DisableOtherEffects(VolumeProfile profile)
    {
        foreach (var comp in profile.components)
        {
            string typeName = comp.GetType().Name;

            if (typeName == "ChromaticDisplacementVolume")
                continue; // Keep this one

            // Disable all others
            if (typeName == "DepthOfField" || typeName == "Bloom" ||
                typeName == "Vignette" || typeName == "PixelSortVolume")
            {
                comp.active = false;
                EditorUtility.SetDirty(comp);
                Debug.Log($"[ChromDispTest] Disabled: {typeName}");
            }
        }
    }

    private static void SetBaseline(VolumeComponent cdComp)
    {
        var t = cdComp.GetType();

        // Core displacement - visible but moderate
        SetFloat(t, cdComp, "displacementAmount", 0.04f);
        SetFloat(t, cdComp, "displacementScale", 3f);
        SetFloat(t, cdComp, "blurRadius", 4f);
        SetFloat(t, cdComp, "depthInfluence", 1f);

        // Channel amounts - spread out for visible RGB separation
        SetFloat(t, cdComp, "channelAAmount", 1f);
        SetFloat(t, cdComp, "channelBAmount", 0f);
        SetFloat(t, cdComp, "channelCAmount", -1f);

        // Channel angles - classic 120-degree separation
        SetFloat(t, cdComp, "channelAAngle", 0f);
        SetFloat(t, cdComp, "channelBAngle", 120f);
        SetFloat(t, cdComp, "channelCAngle", 240f);

        // Disable optional features for baseline
        SetBool(t, cdComp, "useObjectMask", false);
        SetBool(t, cdComp, "useRadialFalloff", false);

        // Ensure displacement source is Luminance
        SetEnum(t, cdComp, "displacementSource", "Luminance");
        SetEnum(t, cdComp, "colorMode", "RGB");
        SetEnum(t, cdComp, "channelBlendMode", "Additive");
    }

    private static void SetFloat(System.Type t, VolumeComponent comp, string fieldName, float value)
    {
        var field = t.GetField(fieldName);
        if (field?.GetValue(comp) is ClampedFloatParameter param)
            param.Override(value);
    }

    private static void SetBool(System.Type t, VolumeComponent comp, string fieldName, bool value)
    {
        var field = t.GetField(fieldName);
        if (field?.GetValue(comp) is BoolParameter param)
            param.Override(value);
    }

    private static void SetEnum(System.Type t, VolumeComponent comp, string fieldName, string value)
    {
        var field = t.GetField(fieldName);
        if (field == null) return;
        var paramObj = field.GetValue(comp);
        var valueProp = paramObj.GetType().GetProperty("value");
        if (valueProp == null) return;
        var enumVal = System.Enum.Parse(valueProp.PropertyType, value);
        var overrideMethod = paramObj.GetType().GetMethod("Override");
        overrideMethod?.Invoke(paramObj, new object[] { enumVal });
    }

    private static void LogAllParams(VolumeComponent comp)
    {
        foreach (var param in comp.parameters)
        {
            try
            {
                var prop = param.GetType().GetProperty("value");
                var val = prop?.GetValue(param);
                Debug.Log($"[ChromDispTest]   {param.GetType().Name}: override={param.overrideState} value={val}");
            }
            catch { }
        }
    }
}

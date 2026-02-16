using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VJSystem
{
    [System.Serializable]
    public class DoFPresetData
    {
        public string           presetName;
        public DepthOfFieldMode mode;
        public float            focusDistance;
        public float            focalLength;
        public float            aperture;
        public float            gaussianStart;
        public float            gaussianEnd;
    }

    [System.Serializable]
    public class DoFRandomBounds
    {
        public Vector2 focusDistance = new Vector2(0.5f,  20f);
        public Vector2 focalLength  = new Vector2(10f,  100f);
        public Vector2 aperture     = new Vector2(1f,    16f);
        public Vector2 gaussianStart = new Vector2(0f,    5f);
        public Vector2 gaussianEnd  = new Vector2(1f,    20f);
    }

    [CreateAssetMenu(menuName = "VJSystem/DoF Preset Library")]
    public class DoFPresetLibrary : ScriptableObject
    {
        public DoFPresetData[] presets     = new DoFPresetData[7];
        public DoFRandomBounds randomBounds = new();
    }
}

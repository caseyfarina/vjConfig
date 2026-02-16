using UnityEngine;

namespace VJSystem
{
    [System.Serializable]
    public class PixelSortPresetData
    {
        public string        presetName;
        public bool          enabled;
        public float         strength;       // 0-1
        public SortAxis      sortAxis;
        public PixelProperty thresholdMode;
        public float         thresholdLow;   // 0-1
        public float         thresholdHigh;  // 0-1
        public PixelProperty sortMode;
        public SortOrder     sortOrder;
        public int           maxSpanLength;  // 0-1920
    }

    [System.Serializable]
    public class PixelSortRandomBounds
    {
        public Vector2 strength     = new Vector2(0.3f, 1.0f);
        public Vector2 thresholdLow = new Vector2(0.0f, 0.4f);
        public Vector2 thresholdHigh = new Vector2(0.5f, 1.0f);
        public Vector2Int maxSpanLength = new Vector2Int(0, 960);
    }

    [CreateAssetMenu(menuName = "VJSystem/Pixel Sort Preset Library")]
    public class PixelSortPresetLibrary : ScriptableObject
    {
        public PixelSortPresetData[]  presets      = new PixelSortPresetData[7];
        public PixelSortRandomBounds  randomBounds = new();
    }
}

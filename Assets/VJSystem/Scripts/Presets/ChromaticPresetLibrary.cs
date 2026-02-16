using UnityEngine;

namespace VJSystem
{
    [System.Serializable]
    public class ChromaticDisplacementPresetData
    {
        public string presetName;
        public bool   enabled;

        // Displacement
        public float              displacementAmount;
        public DisplacementSource displacementSource;
        public float              displacementScale;
        public float              depthInfluence;

        // Pre-Blur
        public float blurRadius;

        // Channel controls
        public float channelAAmount;
        public float channelAAngle;
        public float channelBAmount;
        public float channelBAngle;
        public float channelCAmount;
        public float channelCAngle;

        // Color palette
        public ColorMode        colorMode;
        public Color            colorA = Color.red;
        public Color            colorB = Color.green;
        public Color            colorC = Color.blue;
        public ChannelBlendMode channelBlendMode;

        // Object mask
        public bool      useObjectMask;
        public LayerMask maskLayer;
        public float     maskDilation;
        public float     maskFeather;

        // Radial falloff
        public bool    useRadialFalloff;
        public Vector2 center = new Vector2(0.5f, 0.5f);
        public float   falloffStart;
        public float   falloffEnd;
        public float   falloffPower = 1f;
    }

    [System.Serializable]
    public class ChromaticRandomBounds
    {
        public Vector2 displacementAmount = new Vector2(0.01f, 0.08f);
        public Vector2 displacementScale  = new Vector2(0.5f,  5.0f);
        public Vector2 channelAmount      = new Vector2(-2.0f, 2.0f);
        public Vector2 channelAngle       = new Vector2(-180f, 180f);
        public Vector2 depthInfluence     = new Vector2(0f, 2f);
        public Vector2 blurRadius         = new Vector2(0f, 10f);
        public Vector2 radialFalloffStart = new Vector2(0.0f,  0.5f);
        public Vector2 radialFalloffEnd   = new Vector2(0.5f,  1.5f);
        public Vector2 maskDilation       = new Vector2(2f, 16f);
        public Vector2 maskFeather        = new Vector2(1f, 8f);
    }

    [CreateAssetMenu(menuName = "VJSystem/Chromatic Preset Library")]
    public class ChromaticPresetLibrary : ScriptableObject
    {
        public ChromaticDisplacementPresetData[] presets      = new ChromaticDisplacementPresetData[7];
        public ChromaticRandomBounds             randomBounds = new();
    }
}

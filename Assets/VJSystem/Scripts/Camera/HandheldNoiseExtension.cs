using UnityEngine;
using Unity.Cinemachine;

namespace VJSystem
{
    /// <summary>
    /// Perlin noise on position XY and rotation Z for handheld camera feel.
    /// </summary>
    public class HandheldNoiseExtension : CinemachineExtension
    {
        [Header("Position Noise")]
        public float positionFrequency  = 0.3f;
        public float positionAmplitude  = 0.05f;

        [Header("Rotation Noise")]
        public float rotationFrequency  = 0.2f;
        public float rotationAmplitude  = 0.5f;

        float _seed;

        void Awake()
        {
            _seed = Random.Range(0f, 1000f);
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;

            float t = Time.time;

            float px = (Mathf.PerlinNoise(_seed,       t * positionFrequency) - 0.5f) * 2f * positionAmplitude;
            float py = (Mathf.PerlinNoise(_seed + 100, t * positionFrequency) - 0.5f) * 2f * positionAmplitude;

            state.PositionCorrection += new Vector3(px, py, 0f);

            float rz = (Mathf.PerlinNoise(_seed + 200, t * rotationFrequency) - 0.5f) * 2f * rotationAmplitude;
            state.OrientationCorrection *= Quaternion.Euler(0f, 0f, rz);
        }
    }
}

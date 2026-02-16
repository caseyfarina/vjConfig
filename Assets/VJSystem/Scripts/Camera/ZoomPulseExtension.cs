using UnityEngine;
using Unity.Cinemachine;

namespace VJSystem
{
    /// <summary>
    /// Oscillates the camera FOV with a sine wave for a zoom-pulse effect.
    /// </summary>
    public class ZoomPulseExtension : CinemachineExtension
    {
        [Header("Zoom Pulse Settings")]
        public float baseFOV         = 60f;
        public float pulseAmplitude  = 10f;
        public float pulseSpeed      = 2f;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;

            float fov = baseFOV + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
            state.Lens = new LensSettings
            {
                FieldOfView      = fov,
                NearClipPlane    = state.Lens.NearClipPlane,
                FarClipPlane     = state.Lens.FarClipPlane,
                OrthographicSize = state.Lens.OrthographicSize,
            };
        }
    }
}

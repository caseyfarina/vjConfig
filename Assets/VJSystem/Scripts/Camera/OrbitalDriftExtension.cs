using UnityEngine;
using Unity.Cinemachine;

namespace VJSystem
{
    /// <summary>
    /// Rotates the camera around a target pivot using Sin/Cos orbital motion.
    /// </summary>
    public class OrbitalDriftExtension : CinemachineExtension
    {
        [Header("Orbital Settings")]
        public float orbitSpeed     = 0.5f;
        public float orbitRadius    = 5f;
        public float verticalOffset = 1f;

        float _time;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;

            _time += deltaTime * orbitSpeed;

            var offset = new Vector3(
                Mathf.Cos(_time) * orbitRadius,
                verticalOffset,
                Mathf.Sin(_time) * orbitRadius
            );

            state.PositionCorrection += offset;
        }
    }
}

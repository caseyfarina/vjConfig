using UnityEngine;
using Unity.Cinemachine;

namespace VJSystem
{
    /// <summary>
    /// Moves the camera along a Lemniscate of Bernoulli (figure-8) path.
    /// x = a*cos(t) / (1 + sin²(t)), z = a*cos(t)*sin(t) / (1 + sin²(t))
    /// </summary>
    public class Figure8PathExtension : CinemachineExtension
    {
        [Header("Figure-8 Settings")]
        public float pathScale = 3f;
        public float pathSpeed = 0.5f;

        float _time;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;

            _time += deltaTime * pathSpeed;

            float cosT    = Mathf.Cos(_time);
            float sinT    = Mathf.Sin(_time);
            float denom   = 1f + sinT * sinT;

            float x = pathScale * cosT / denom;
            float z = pathScale * cosT * sinT / denom;

            state.PositionCorrection += new Vector3(x, 0f, z);
        }
    }
}

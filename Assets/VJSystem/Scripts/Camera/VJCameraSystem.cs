using UnityEngine;
using Unity.Cinemachine;

namespace VJSystem
{
    /// <summary>
    /// Manages 8 Cinemachine virtual cameras. Switches on MidiGridRouter.OnCameraSelect.
    /// Priority-based: active = 100, all others = 0. Brain blend = Cut.
    /// </summary>
    public class VJCameraSystem : MonoBehaviour
    {
        [Header("Cameras (1-8, matching grid columns)")]
        [Tooltip("Assign in order: Wide, Closeup, LowAngle, Overhead, Orbital, Handheld, Figure8, ZoomPulse")]
        [SerializeField] CinemachineCamera[] cameras = new CinemachineCamera[8];

        [Header("Brain")]
        [SerializeField] CinemachineBrain brain;

        int _activeIndex = -1;

        public int ActiveCameraIndex => _activeIndex;
        public string ActiveCameraName => _activeIndex >= 0 && _activeIndex < cameras.Length && cameras[_activeIndex] != null
            ? cameras[_activeIndex].gameObject.name
            : "None";

        void OnEnable()
        {
            MidiGridRouter.OnCameraSelect += HandleCameraSelect;
        }

        void OnDisable()
        {
            MidiGridRouter.OnCameraSelect -= HandleCameraSelect;
        }

        void Start()
        {
            if (brain != null)
            {
                brain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.Cut, 0f);
            }

            // Default to camera 1
            HandleCameraSelect(1);
        }

        void HandleCameraSelect(int col)
        {
            int index = col - 1; // col is 1-based
            if (index < 0 || index >= cameras.Length) return;
            if (cameras[index] == null) return;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                    cameras[i].Priority = (i == index) ? 100 : 0;
            }

            _activeIndex = index;
        }
    }
}

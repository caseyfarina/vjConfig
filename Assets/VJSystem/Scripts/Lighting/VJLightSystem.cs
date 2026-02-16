using UnityEngine;
using DG.Tweening;

namespace VJSystem
{
    [System.Serializable]
    public class LightGroup
    {
        public string  groupName;
        public Light[] lights;
        public float   targetIntensity = 1f;
        public Color   lightColor      = Color.white;
        public float   tweenDuration   = 0.2f;
    }

    /// <summary>
    /// Manages 8 light groups toggled via Row 5 of the Midi Fighter 64.
    /// Each toggle fades intensity with DOTween.
    /// </summary>
    public class VJLightSystem : MonoBehaviour
    {
        [SerializeField] LightGroup[] lightGroups = new LightGroup[8];

        readonly bool[] _states = new bool[8];

        public bool[] States => _states;

        void OnEnable()
        {
            MidiGridRouter.OnLightToggle += HandleLightToggle;
        }

        void OnDisable()
        {
            MidiGridRouter.OnLightToggle -= HandleLightToggle;
        }

        void HandleLightToggle(int col)
        {
            int index = col - 1;
            if (index < 0 || index >= lightGroups.Length) return;

            var group = lightGroups[index];
            if (group == null || group.lights == null) return;

            _states[index] = !_states[index];
            float target = _states[index] ? group.targetIntensity : 0f;

            foreach (var light in group.lights)
            {
                if (light == null) continue;

                DOTween.Kill(light);
                light.DOIntensity(target, group.tweenDuration);
                light.color = group.lightColor;
            }
        }
    }
}

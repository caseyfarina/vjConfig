using UnityEngine;
using DG.Tweening;

namespace VJSystem
{
    [System.Serializable]
    public class SceneSlot
    {
        public string         slotName;
        public GameObject[]   objects;
        public bool           toggleMode    = true;   // false = momentary
        public float          tweenDuration = 0.3f;
        public AnimationCurve fadeCurve     = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    /// <summary>
    /// Manages 24 scene slots (Rows 6-8 of the Midi Fighter 64).
    /// Toggle mode: press on/off. Momentary mode: press = on, release = off.
    /// Enable/disable via DOTween scale animation.
    /// </summary>
    public class VJSceneSlotSystem : MonoBehaviour
    {
        [SerializeField] SceneSlot[] slots = new SceneSlot[24];

        readonly bool[] _states = new bool[24];

        public bool[] States => _states;

        void OnEnable()
        {
            MidiGridRouter.OnSceneSlotToggle += HandleSlotToggle;
        }

        void OnDisable()
        {
            MidiGridRouter.OnSceneSlotToggle -= HandleSlotToggle;
        }

        void Start()
        {
            // Initialize all slots to off
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i]?.objects == null) continue;
                foreach (var obj in slots[i].objects)
                {
                    if (obj != null)
                        obj.transform.localScale = Vector3.zero;
                }
            }
        }

        void HandleSlotToggle(int slot, bool isNoteOn)
        {
            int index = slot - 1; // slot is 1-based
            if (index < 0 || index >= slots.Length) return;

            var slotData = slots[index];
            if (slotData == null || slotData.objects == null) return;

            if (slotData.toggleMode)
            {
                // Toggle mode: only respond to note on
                if (!isNoteOn) return;
                _states[index] = !_states[index];
            }
            else
            {
                // Momentary mode: note on = active, note off = inactive
                _states[index] = isNoteOn;
            }

            SetSlotActive(index, _states[index]);
        }

        void SetSlotActive(int index, bool active)
        {
            var slotData = slots[index];
            Vector3 targetScale = active ? Vector3.one : Vector3.zero;

            foreach (var obj in slotData.objects)
            {
                if (obj == null) continue;

                DOTween.Kill(obj.transform);

                if (active)
                    obj.SetActive(true);

                obj.transform.DOScale(targetScale, slotData.tweenDuration)
                    .SetEase(slotData.fadeCurve != null ? Ease.Unset : Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        if (!active)
                            obj.SetActive(false);
                    });
            }
        }
    }
}

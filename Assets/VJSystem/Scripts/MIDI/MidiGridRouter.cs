using System;
using UnityEngine;

namespace VJSystem
{
    /// <summary>
    /// Subscribes to MidiEventManager and converts raw notes to typed VJ events.
    /// All subsystems subscribe here — none touch MidiEventManager directly.
    /// </summary>
    public class MidiGridRouter : MonoBehaviour
    {
        public static event Action<int>       OnCameraSelect;       // col 1-8
        public static event Action<int, int>  OnPostFXPresetSelect; // row 2-4, col 1-7
        public static event Action<int>       OnPostFXRandomize;    // row 2-4 (col 8)
        public static event Action<int>       OnLightToggle;        // col 1-8
        public static event Action<int, bool> OnSceneSlotToggle;    // slot 1-24, isNoteOn

        void OnEnable()
        {
            MidiEventManager.OnNoteOn  += HandleNoteOn;
            MidiEventManager.OnNoteOff += HandleNoteOff;
        }

        void OnDisable()
        {
            MidiEventManager.OnNoteOn  -= HandleNoteOn;
            MidiEventManager.OnNoteOff -= HandleNoteOff;
        }

        void HandleNoteOn(int noteNumber, float velocity)
        {
            if (!MidiFighter64InputMap.IsInRange(noteNumber)) return;

            var btn = MidiFighter64InputMap.FromNote(noteNumber);
            RouteButton(btn, isNoteOn: true);
        }

        void HandleNoteOff(int noteNumber)
        {
            if (!MidiFighter64InputMap.IsInRange(noteNumber)) return;

            var btn = MidiFighter64InputMap.FromNote(noteNumber);
            RouteButton(btn, isNoteOn: false);
        }

        void RouteButton(GridButton btn, bool isNoteOn)
        {
            switch (btn.row)
            {
                // Row 1: Camera select (note on only)
                case 1:
                    if (isNoteOn) OnCameraSelect?.Invoke(btn.col);
                    break;

                // Rows 2-4: Post-FX presets or randomize (note on only)
                case 2:
                case 3:
                case 4:
                    if (!isNoteOn) break;
                    if (btn.col == 8)
                        OnPostFXRandomize?.Invoke(btn.row);
                    else
                        OnPostFXPresetSelect?.Invoke(btn.row, btn.col);
                    break;

                // Row 5: Light toggle (note on only)
                case 5:
                    if (isNoteOn) OnLightToggle?.Invoke(btn.col);
                    break;

                // Rows 6-8: Scene slots (note on and off for momentary support)
                case 6:
                case 7:
                case 8:
                    int slot = ((btn.row - 6) * 8) + btn.col; // 1-24
                    OnSceneSlotToggle?.Invoke(slot, isNoteOn);
                    break;
            }
        }
    }
}

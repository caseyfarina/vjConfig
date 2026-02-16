using UnityEngine;

namespace VJSystem
{
    /// <summary>
    /// Routes MidiGridRouter PostFX events to the correct system based on row.
    /// Row 2 = DoF, Row 3 = PixelSort, Row 4 = ChromaticDisplacement.
    /// Tracks activeEffectRow so PresetSaveSystem knows what to capture.
    /// </summary>
    public class PostFXRouter : MonoBehaviour
    {
        [SerializeField] DepthOfFieldSystem dofSystem;
        [SerializeField] PixelSortSystem pixelSortSystem;
        [SerializeField] ChromaticDisplacementSystem chromaticSystem;

        /// <summary>Last row that received a preset or randomize event (2, 3, or 4).</summary>
        public int ActiveEffectRow { get; private set; } = 2;

        public IPostFXSystem ActiveSystem => ActiveEffectRow switch
        {
            2 => dofSystem,
            3 => pixelSortSystem,
            4 => chromaticSystem,
            _ => null
        };

        public IPostFXSystem GetSystemForRow(int row) => row switch
        {
            2 => dofSystem,
            3 => pixelSortSystem,
            4 => chromaticSystem,
            _ => null
        };

        void OnEnable()
        {
            MidiGridRouter.OnPostFXPresetSelect += HandlePresetSelect;
            MidiGridRouter.OnPostFXRandomize    += HandleRandomize;
        }

        void OnDisable()
        {
            MidiGridRouter.OnPostFXPresetSelect -= HandlePresetSelect;
            MidiGridRouter.OnPostFXRandomize    -= HandleRandomize;
        }

        void HandlePresetSelect(int row, int col)
        {
            ActiveEffectRow = row;
            int slotIndex = col - 1; // col is 1-based, slot is 0-based

            var system = GetSystemForRow(row);
            system?.ApplyPreset(slotIndex);
        }

        void HandleRandomize(int row)
        {
            ActiveEffectRow = row;

            var system = GetSystemForRow(row);
            system?.Randomize();
        }
    }
}

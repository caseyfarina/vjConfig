using UnityEngine;

namespace VJSystem
{
    /// <summary>
    /// Listens to MidiGridRouter.OnPostFXRandomize and delegates to the
    /// appropriate IPostFXSystem. Kept as a separate MonoBehaviour for
    /// scene hierarchy clarity, though PostFXRouter also handles routing.
    /// This component can be extended with global randomization logic
    /// (e.g., randomize all effects at once, weighted randomization, etc.)
    /// </summary>
    public class RandomizationSystem : MonoBehaviour
    {
        [SerializeField] PostFXRouter postFXRouter;

        /// <summary>
        /// Randomize a specific effect row (2=DoF, 3=PixelSort, 4=Chromatic).
        /// Called by PostFXRouter or directly for programmatic randomization.
        /// </summary>
        public void RandomizeRow(int row)
        {
            var system = postFXRouter.GetSystemForRow(row);
            system?.Randomize();
        }

        /// <summary>
        /// Randomize all three effect systems at once.
        /// </summary>
        public void RandomizeAll()
        {
            postFXRouter.GetSystemForRow(2)?.Randomize();
            postFXRouter.GetSystemForRow(3)?.Randomize();
            postFXRouter.GetSystemForRow(4)?.Randomize();
        }
    }
}

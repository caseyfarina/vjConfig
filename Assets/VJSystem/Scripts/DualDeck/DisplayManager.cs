using UnityEngine;

namespace VJSystem
{
    public class DisplayManager : MonoBehaviour
    {
        [Tooltip("Enable to activate secondary displays for projector output")]
        public bool activateMultiDisplay = false;

        void Start()
        {
            if (!activateMultiDisplay) return;

            for (int i = 1; i < Mathf.Min(Display.displays.Length, 3); i++)
            {
                Display.displays[i].Activate();
                Debug.Log($"[DisplayManager] Activated Display {i} ({Display.displays[i].systemWidth}x{Display.displays[i].systemHeight})");
            }
        }
    }
}

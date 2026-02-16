using UnityEngine;
using Klak.Spout;

namespace VJSystem
{
    /// <summary>
    /// Manages Spout output to MadMapper. Creates a RenderTexture, assigns it
    /// to the main camera, and sends via KlakSpout SpoutSender.
    /// </summary>
    public class SpoutOutputManager : MonoBehaviour
    {
        const string SENDER_NAME   = "VJOutput";
        const string PREVIEW_NAME  = "VJPreview";

        [Header("Main Output")]
        [SerializeField] Camera mainCamera;
        [SerializeField] int outputWidth  = 1920;
        [SerializeField] int outputHeight = 1080;

        [Header("Preview (Optional)")]
        [SerializeField] bool enablePreview = false;
        [SerializeField] Camera previewCamera;
        [SerializeField] int previewWidth  = 960;
        [SerializeField] int previewHeight = 540;

        [Header("References (Auto-created if null)")]
        [SerializeField] SpoutSender mainSender;
        [SerializeField] SpoutSender previewSender;

        RenderTexture _mainRT;
        RenderTexture _previewRT;

        public RenderTexture MainRenderTexture => _mainRT;

        void Awake()
        {
            SetupMainOutput();

            if (enablePreview)
                SetupPreviewOutput();
        }

        void OnDestroy()
        {
            if (_mainRT != null)
            {
                mainCamera.targetTexture = null;
                _mainRT.Release();
                Destroy(_mainRT);
            }

            if (_previewRT != null)
            {
                if (previewCamera != null)
                    previewCamera.targetTexture = null;
                _previewRT.Release();
                Destroy(_previewRT);
            }
        }

        void SetupMainOutput()
        {
            _mainRT = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "VJOutput_RT"
            };
            _mainRT.Create();

            if (mainCamera != null)
                mainCamera.targetTexture = _mainRT;

            if (mainSender == null)
                mainSender = gameObject.AddComponent<SpoutSender>();

            mainSender.spoutName = SENDER_NAME;
            mainSender.sourceTexture = _mainRT;
        }

        void SetupPreviewOutput()
        {
            if (previewCamera == null) return;

            _previewRT = new RenderTexture(previewWidth, previewHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "VJPreview_RT"
            };
            _previewRT.Create();

            previewCamera.targetTexture = _previewRT;

            if (previewSender == null)
            {
                var previewGO = new GameObject("PreviewSender");
                previewGO.transform.SetParent(transform);
                previewSender = previewGO.AddComponent<SpoutSender>();
            }

            previewSender.spoutName = PREVIEW_NAME;
            previewSender.sourceTexture = _previewRT;
        }

        public void SetResolution(int w, int h)
        {
            if (_mainRT != null)
            {
                mainCamera.targetTexture = null;
                _mainRT.Release();
                Destroy(_mainRT);
            }

            outputWidth  = w;
            outputHeight = h;
            SetupMainOutput();
        }
    }
}

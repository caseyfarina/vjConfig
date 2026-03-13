using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectionMapper;
using System.Collections;

namespace VJSystem
{
    public class OutputManager : MonoBehaviour
    {
        public DualDeckManager deckManager;

        [Header("Display")]
        public bool activateExtraDisplays = true;

        [Header("Shader (must be assigned for builds)")]
        public Shader warpShader;

        [Header("Output 1 (Projector 1)")]
        public ProjectionSurface output1 = new ProjectionSurface();

        [Header("Output 2 (Projector 2)")]
        public ProjectionSurface output2 = new ProjectionSurface();

        Camera _outCam1, _outCam2;

        // Which output is selected for keyboard corner editing (0 or 1)
        int _selectedOutput = 0;
        int _heldCorner = -1;

        public int SelectedOutput { get => _selectedOutput; set => _selectedOutput = Mathf.Clamp(value, 0, 1); }
        public int HeldCorner => _heldCorner;
        public ProjectionSurface GetOutput(int index) => index == 0 ? output1 : output2;

        void Awake()
        {
            // Set Display 0 to native resolution before Unity applies PlayerSettings defaults.
            // Screen.currentResolution returns the desktop resolution of the primary monitor.
            var native = Screen.currentResolution;
            Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
            Debug.Log($"[OutputManager] Display 0 set to {native.width}x{native.height} fullscreen");
        }

        void Start()
        {
            // Use serialized reference first, fall back to Shader.Find for editor
            if (warpShader == null)
                warpShader = Shader.Find("ProjectionMapper/HomographyWarp");
            if (warpShader == null)
            {
                Debug.LogError("[OutputManager] HomographyWarp shader not found! Assign it in the inspector.");
                enabled = false;
                return;
            }

            int displayCount = Display.displays.Length;
            Debug.Log($"[OutputManager] Detected {displayCount} display(s)");

            if (activateExtraDisplays)
            {
                for (int i = 1; i < Mathf.Min(displayCount, 3); i++)
                {
                    Display.displays[i].Activate(
                        Display.displays[i].systemWidth,
                        Display.displays[i].systemHeight,
                        new RefreshRate { numerator = 60, denominator = 1 }
                    );
                    Debug.Log($"[OutputManager] Activated Display {i}: {Display.displays[i].systemWidth}x{Display.displays[i].systemHeight}");
                }

                if (displayCount < 3)
                    Debug.LogWarning($"[OutputManager] Only {displayCount} display(s) found. Need 3 for full output (GUI + 2 projectors).");
            }

            InitSurface(output1, "Projector 1", 1);
            InitSurface(output2, "Projector 2", 2);

            _outCam1 = CreateOutputCamera("OutputCam_Proj1", 1);
            _outCam2 = CreateOutputCamera("OutputCam_Proj2", 2);

            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

            if (deckManager != null)
                deckManager.OnTakeCompleted += RefreshSources;

            StartCoroutine(DelayedInit());
        }

        void InitSurface(ProjectionSurface s, string name, int display)
        {
            s.name = name;
            s.sourceMode = SurfaceSourceMode.RenderTexture;
            s.targetDisplay = display;
            // Fullscreen corners (TL, TR, BR, BL)
            s.corners = new Vector2[]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f)
            };
            s.sourceCropUV = new Rect(0, 0, 1, 1);
            s.brightness = 1f;
            s.gamma = 1f;
            s.dirty = true;
        }

        IEnumerator DelayedInit()
        {
            yield return null; // wait for DeckCameraRig to create RTs
            RefreshSources();
        }

        public void RefreshSources()
        {
            if (deckManager == null) return;
            var live = deckManager.LiveStage;
            if (live == null || live.cameraRig == null) return;

            output1.sourceTexture = live.cameraRig.GetRT(0);
            output2.sourceTexture = live.cameraRig.GetRT(1);
            Debug.Log($"[OutputManager] Sources updated to {deckManager.liveDeck} deck");
        }

        Camera CreateOutputCamera(string camName, int targetDisplay)
        {
            var go = new GameObject(camName);
            go.transform.SetParent(transform);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            cam.depth = 10 + targetDisplay;
            cam.targetDisplay = targetDisplay;
            var urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderType = CameraRenderType.Base;
            return cam;
        }

        void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == _outCam1) RenderSurface(output1);
            else if (cam == _outCam2) RenderSurface(output2);
        }

        void RenderSurface(ProjectionSurface surface)
        {
            if (surface == null || !surface.enabled) return;
            var tex = surface.GetActiveTexture();
            if (tex == null) return;
            if (surface.dirty) surface.RecomputeHomography();

            if (surface.warpMaterial == null)
                surface.warpMaterial = new Material(warpShader) { hideFlags = HideFlags.HideAndDontSave };
            surface.UpdateMaterial(surface.warpMaterial);

            GL.PushMatrix();
            GL.LoadOrtho();
            surface.warpMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.TexCoord2(surface.corners[3].x, surface.corners[3].y);
            GL.Vertex3(surface.corners[3].x, surface.corners[3].y, 0f);
            GL.TexCoord2(surface.corners[2].x, surface.corners[2].y);
            GL.Vertex3(surface.corners[2].x, surface.corners[2].y, 0f);
            GL.TexCoord2(surface.corners[1].x, surface.corners[1].y);
            GL.Vertex3(surface.corners[1].x, surface.corners[1].y, 0f);
            GL.TexCoord2(surface.corners[0].x, surface.corners[0].y);
            GL.Vertex3(surface.corners[0].x, surface.corners[0].y, 0f);
            GL.End();
            GL.PopMatrix();
        }

        void Update()
        {
            // Recompute homography if dirty
            if (output1.dirty) output1.RecomputeHomography();
            if (output2.dirty) output2.RecomputeHomography();

            HandleMappingInput();
        }

        void HandleMappingInput()
        {
            // Tab to switch selected output
            if (Input.GetKeyDown(KeyCode.Tab))
                _selectedOutput = 1 - _selectedOutput;

            // Hold 1-4 for corner selection
            _heldCorner = -1;
            if (Input.GetKey(KeyCode.Keypad1) || Input.GetKey(KeyCode.Alpha1)) _heldCorner = 0;
            if (Input.GetKey(KeyCode.Keypad2) || Input.GetKey(KeyCode.Alpha2)) _heldCorner = 1;
            if (Input.GetKey(KeyCode.Keypad3) || Input.GetKey(KeyCode.Alpha3)) _heldCorner = 2;
            if (Input.GetKey(KeyCode.Keypad4) || Input.GetKey(KeyCode.Alpha4)) _heldCorner = 3;

            if (_heldCorner >= 0)
            {
                float step = 0.001f;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step = 0.0001f;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) step = 0.01f;

                Vector2 d = Vector2.zero;
                if (Input.GetKeyDown(KeyCode.LeftArrow))  d.x = -step;
                if (Input.GetKeyDown(KeyCode.RightArrow)) d.x =  step;
                if (Input.GetKeyDown(KeyCode.UpArrow))    d.y =  step;
                if (Input.GetKeyDown(KeyCode.DownArrow))  d.y = -step;

                float hr = step * 0.3f * Time.deltaTime / 0.016f;
                if (Input.GetKey(KeyCode.LeftArrow)  && !Input.GetKeyDown(KeyCode.LeftArrow))  d.x -= hr;
                if (Input.GetKey(KeyCode.RightArrow) && !Input.GetKeyDown(KeyCode.RightArrow)) d.x += hr;
                if (Input.GetKey(KeyCode.UpArrow)    && !Input.GetKeyDown(KeyCode.UpArrow))    d.y += hr;
                if (Input.GetKey(KeyCode.DownArrow)  && !Input.GetKeyDown(KeyCode.DownArrow))  d.y -= hr;

                if (d.sqrMagnitude > 0f)
                    GetOutput(_selectedOutput).MoveCorner(_heldCorner, d);
            }
        }

        void OnDestroy()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            if (deckManager != null)
                deckManager.OnTakeCompleted -= RefreshSources;
            output1?.Cleanup();
            output2?.Cleanup();
        }
    }
}

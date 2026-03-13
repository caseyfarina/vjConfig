using UnityEngine;

namespace ProjectionMapper
{
    public enum SurfaceSourceMode
    {
        Camera,
        RenderTexture
    }

    public enum AAQuality
    {
        None,
        Low,  // 2x RGSS
        High  // 4x RGSS
    }

    /// <summary>
    /// Represents a single projection-mapped quad surface.
    /// Stores corner positions, source configuration, and computed homography.
    /// </summary>
    [System.Serializable]
    public class ProjectionSurface
    {
        public string name = "Surface";
        public int targetDisplay = 0;

        public SurfaceSourceMode sourceMode = SurfaceSourceMode.Camera;

        /// <summary>Camera to capture from when sourceMode == Camera.</summary>
        [System.NonSerialized] public Camera sourceCamera;

        /// <summary>Serializable camera path for persistence.</summary>
        public string sourceCameraPath = "";

        /// <summary>External RenderTexture when sourceMode == RenderTexture.</summary>
        [System.NonSerialized] public RenderTexture sourceTexture;

        /// <summary>Resolution for auto-created RenderTextures in Camera mode.</summary>
        public Vector2Int renderResolution = new Vector2Int(1920, 1080);

        /// <summary>
        /// Four corners in normalized screen space (0-1).
        /// Order: TL, TR, BR, BL (clockwise from top-left).
        /// Default places a centered quad at 25%-75% of screen.
        /// </summary>
        public Vector2[] corners = new Vector2[]
        {
            new Vector2(0.25f, 0.75f), // TL
            new Vector2(0.75f, 0.75f), // TR
            new Vector2(0.75f, 0.25f), // BR
            new Vector2(0.25f, 0.25f), // BL
        };

        /// <summary>
        /// Source UV crop rectangle. Defines which sub-region of the source
        /// texture feeds into this surface BEFORE corner-pin warping.
        /// Default (0,0,1,1) = full frame. Adjust to isolate content regions
        /// for non-rectangular surfaces or multi-surface content slicing.
        /// (x, y) = bottom-left origin, (width, height) = extent.
        /// </summary>
        public Rect sourceCropUV = new Rect(0f, 0f, 1f, 1f);

        /// <summary>
        /// Soft edge feathering in UV space. Each component is the feather
        /// width (0-0.5) for Left, Right, Bottom, Top edges respectively.
        /// 0 = hard edge, 0.5 = feather reaches center.
        /// </summary>
        public Vector4 edgeFeather = Vector4.zero;

        public AAQuality aaQuality = AAQuality.Low;

        [Range(0.5f, 2f)]
        public float brightness = 1f;

        [Range(0.2f, 3f)]
        public float gamma = 1f;

        public bool enabled = true;

        // --- Runtime state (not serialized to JSON) ---

        /// <summary>Auto-created RT when using Camera mode.</summary>
        [System.NonSerialized] public RenderTexture managedRT;

        /// <summary>Inverse homography matrix for the shader.</summary>
        [System.NonSerialized] public Matrix4x4 inverseHomography = Matrix4x4.identity;

        /// <summary>Forward homography (src->dst) for editor visualization.</summary>
        [System.NonSerialized] public Matrix4x4 forwardHomography = Matrix4x4.identity;

        /// <summary>Material instance for this surface's warp rendering.</summary>
        [System.NonSerialized] public Material warpMaterial;

        /// <summary>Whether the homography needs recomputing.</summary>
        [System.NonSerialized] public bool dirty = true;

        /// <summary>
        /// Recompute the homography matrix from current corner positions.
        /// Should be called whenever corners change.
        /// </summary>
        public void RecomputeHomography()
        {
            inverseHomography = HomographyMath.ComputeInverseHomography(corners);

            Vector2[] src = new Vector2[]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f),
            };
            forwardHomography = HomographyMath.ComputeHomography(src, corners);

            dirty = false;
        }

        /// <summary>
        /// Get the active render texture for this surface (managed or external).
        /// </summary>
        public RenderTexture GetActiveTexture()
        {
            if (sourceMode == SurfaceSourceMode.RenderTexture)
                return sourceTexture;
            return managedRT;
        }

        /// <summary>
        /// Ensure the managed RT exists and is the right size (Camera mode).
        /// </summary>
        public void EnsureManagedRT()
        {
            if (sourceMode != SurfaceSourceMode.Camera) return;

            if (managedRT == null ||
                managedRT.width != renderResolution.x ||
                managedRT.height != renderResolution.y)
            {
                if (managedRT != null)
                {
                    managedRT.Release();
                    Object.Destroy(managedRT);
                }

                managedRT = new RenderTexture(
                    renderResolution.x,
                    renderResolution.y,
                    24,
                    RenderTextureFormat.ARGB32
                );
                managedRT.antiAliasing = 1; // AA handled in warp shader
                managedRT.filterMode = FilterMode.Bilinear;
                managedRT.wrapMode = TextureWrapMode.Clamp;
                managedRT.Create();
            }

            if (sourceCamera != null)
            {
                sourceCamera.targetTexture = managedRT;
            }
        }

        /// <summary>
        /// Apply current settings to the warp material.
        /// </summary>
        public void UpdateMaterial(Material mat)
        {
            if (mat == null) return;

            RenderTexture tex = GetActiveTexture();
            if (tex != null)
                mat.SetTexture("_MainTex", tex);

            mat.SetMatrix("_InvHomography", inverseHomography);
            mat.SetVector("_CropRect", new Vector4(
                sourceCropUV.x, sourceCropUV.y,
                sourceCropUV.width, sourceCropUV.height));
            mat.SetVector("_EdgeFeather", edgeFeather);
            mat.SetFloat("_Brightness", brightness);
            mat.SetFloat("_Gamma", gamma);
            mat.SetFloat("_AAQuality", (float)aaQuality);

            warpMaterial = mat;
        }

        /// <summary>
        /// Move a corner by a delta in normalized screen space.
        /// </summary>
        public void MoveCorner(int cornerIndex, Vector2 delta)
        {
            if (cornerIndex < 0 || cornerIndex > 3) return;
            corners[cornerIndex] += delta;
            corners[cornerIndex].x = Mathf.Clamp(corners[cornerIndex].x, 0f, 1f);
            corners[cornerIndex].y = Mathf.Clamp(corners[cornerIndex].y, 0f, 1f);
            dirty = true;
        }

        /// <summary>
        /// Reset corners to default centered position.
        /// </summary>
        public void ResetCorners()
        {
            corners[0] = new Vector2(0.25f, 0.75f);
            corners[1] = new Vector2(0.75f, 0.75f);
            corners[2] = new Vector2(0.75f, 0.25f);
            corners[3] = new Vector2(0.25f, 0.25f);
            dirty = true;
        }

        /// <summary>
        /// Clean up resources.
        /// </summary>
        public void Cleanup()
        {
            if (managedRT != null)
            {
                managedRT.Release();
                if (Application.isPlaying)
                    Object.Destroy(managedRT);
                else
                    Object.DestroyImmediate(managedRT);
                managedRT = null;
            }
            if (warpMaterial != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(warpMaterial);
                else
                    Object.DestroyImmediate(warpMaterial);
                warpMaterial = null;
            }
        }
    }
}

# Projection Mapper — Technical Architecture

This document provides full context for AI-assisted development (Claude Code) on the `com.projectionmapper` Unity UPM package. Read this before modifying any file.

## Purpose

Runtime projection mapping inside Unity. Captures scene cameras into RenderTextures, warps them via per-fragment inverse homography, and composites them as overlay quads onto one or more physical displays. Replaces external tools like MadMapper or TouchDesigner for projects where content is already rendered in Unity.

## Package Identity

- **Name:** `com.projectionmapper`
- **Unity:** 2021.3+ (tested on Unity 6.3)
- **Dependency:** URP (`com.unity.render-pipelines.universal` >= 12.0.0)
- **No other dependencies.** Zero third-party packages.

## File Map

```
Runtime/
  ProjectionMapperManager.cs  (184 lines) — MonoBehaviour entry point
  ProjectionSurface.cs        (228 lines) — Per-surface data + RT management
  ProjectionRenderer.cs       (139 lines) — GL-based overlay rendering
  ProjectionGUI.cs            (278 lines) — IMGUI config panel
  HomographyMath.cs           (244 lines) — DLT solver, matrix math
  ProjectionPersistence.cs    (216 lines) — JSON profile save/load
  Shaders/
    HomographyWarp.shader     (230 lines) — Perspective warp + crop + AA + feather
    DebugGrid.shader          (93 lines)  — Flat tiled passthrough for debugging
Editor/
  ProjectionMapperManagerEditor.cs (49 lines) — Custom inspector
  TestSceneBuilder.cs         (437 lines) — Menu item: generates test scene
```

## Data Flow

```
Scene Camera → RenderTexture (managed) → HomographyWarp shader → GL overlay quad → Display
                                            ↑                        ↑
                                     Inverse homography        Corner positions
                                     + crop rect              (screen-space 0-1)
                                     + edge feather
                                     + brightness/gamma
```

### Per-Frame Pipeline

1. `ProjectionMapperManager.Update()` ensures each surface's managed RenderTexture exists and is assigned to its source camera. Recomputes homography if `surface.dirty` is true.
2. Scene cameras render normally to their target RenderTextures.
3. An overlay camera (culling mask 0, highest depth) renders nothing visible but triggers `Camera.onPostRender`.
4. `ProjectionMapperManager.OnPostRenderCallback()` fires. For each display, calls `ProjectionRenderer` to draw warped quads via `GL.LoadOrtho()` + `GL.Begin(QUADS)`.
5. The HomographyWarp shader runs per-fragment: applies inverse homography to get unit-square UV, remaps through crop rect to get source texture UV, applies RGSS anti-aliasing, edge feather, brightness, and gamma.
6. If edit mode is active, `ProjectionRenderer.RenderEditOverlays()` draws corner handles and outline.
7. IMGUI panel (`ProjectionGUI`) draws on top if toggled.

### Homography Math

- Uses Direct Linear Transform (DLT) to compute a 3×3 homography from 4 source→destination point pairs.
- Source points are the unit square corners: (0,1), (1,1), (1,0), (0,0).
- Destination points are the surface's screen-space corner positions.
- The *inverse* homography (destination→source) is passed to the shader so each fragment can compute its source UV.
- Computed CPU-side only when corners change, not per-frame.
- `HomographyMath.cs` contains: `ComputeHomography()`, `ComputeInverseHomography()`, `InvertMatrix3x3()`, `TransformPoint()`, and a Gaussian elimination solver with partial pivoting.

## Key Data Structures

### ProjectionSurface (Serializable class, not MonoBehaviour)

```csharp
string name;
int targetDisplay;                    // 0-7, which Unity Display to render on
SurfaceSourceMode sourceMode;         // Camera or RenderTexture
Camera sourceCamera;                  // [NonSerialized] runtime reference
string sourceCameraPath;              // Serialized path for persistence
RenderTexture sourceTexture;          // [NonSerialized] external RT for RenderTexture mode
Vector2Int renderResolution;          // Size of managed RT (Camera mode)
Rect sourceCropUV;                    // Sub-region of source (x,y,w,h in 0-1 UV space)
Vector4 edgeFeather;                  // Soft edge widths (L,R,B,T) in 0-0.5
Vector2[] corners;                    // 4 corners in screen-space (0-1): TL, TR, BR, BL
AAQuality aaQuality;                  // None, Low (2x RGSS), High (4x RGSS)
float brightness;                     // 0.5-2.0
float gamma;                          // 0.2-3.0
bool enabled;
// Runtime state (not serialized):
RenderTexture managedRT;
Matrix4x4 inverseHomography;
Matrix4x4 forwardHomography;
Material warpMaterial;
bool dirty;
```

### ProfileCollection → ProfileData → SurfaceData

JSON persistence chain. `SurfaceData` is a flat serializable mirror of `ProjectionSurface` (Vector2[] becomes float[], Rect becomes 4 floats, etc.). Saved to `Application.persistentDataPath/ProjectionMapper_Profiles.json`.

Backward-compatible: loading old profiles where `cropW`/`cropH` are 0 defaults to full-frame (0,0,1,1).

## Shader Details

### HomographyWarp.shader

- **Uniforms:** `_MainTex`, `_InvHomography` (4x4, 3x3 packed in upper-left), `_CropRect` (float4: xy=origin, zw=size), `_EdgeFeather` (float4: L,R,B,T), `_Brightness`, `_Gamma`, `_AAQuality`.
- **UV computation:** Inverse homography gives unit-square UV → crop remap (`cropOrigin + uv * cropSize`) → final texture UV.
- **Gradient computation:** `tex2Dgrad` with analytical derivatives from the homography for correct anisotropic filtering under nonlinear warp.
- **RGSS AA:** 2x or 4x Rotated Grid Super Sampling in fragment shader. Offsets rotated 26.6° to break axis-aligned patterns.
- **Edge feather:** Computed in unit-square space (pre-crop) so feathering is relative to the output quad geometry, not the source texture.
- **Bounds rejection:** Fragments mapping outside the crop region return alpha 0.

### DebugGrid.shader

Flat passthrough. Tiles all enabled surfaces for a display into a grid with colored borders. Used for ground-truth verification of source inputs before warping.

## Input System

All input is in `ProjectionMapperManager.HandleInput()`:

- **F12:** Toggle IMGUI config panel (configurable via `guiToggleKey`)
- **1/2/3/4 (held) + Arrows:** Move corner TL/TR/BR/BL of selected surface
- **Shift+Arrow:** Fine step (0.0001), **Ctrl+Arrow:** Coarse step (0.01), **Normal:** 0.001
- **Continuous hold:** Arrows repeat at scaled rate while held
- **[ / ]:** Cycle selected surface index

Input uses legacy `Input.GetKey`/`Input.GetKeyDown`. If migrating to Input System package, replace these in `HandleInput()`.

## GUI Architecture

IMGUI-based (no Canvas, no scene dependencies). Two static classes:

- `ProjectionGUI.DrawConfigWindow()` — Scrollable panel with profile management, surface list, per-surface detail (display, source, crop, feather, AA, brightness, gamma, corners).
- `ProjectionGUI.DrawEditLabels()` — Floating corner labels and HUD readout during edit mode.

Both called from `ProjectionMapperManager.OnGUI()`. The manager passes itself to the GUI so it can call public API methods (AddSurface, RemoveSurface, SwitchProfile, etc.).

## Rendering Architecture

`ProjectionRenderer` is a static class with three public methods:

- `RenderWarpedSurfaces()` — For each enabled surface on the target display, sets up material and draws a GL quad with the warp shader.
- `RenderDebugView()` — Tiles all surfaces into a grid using the debug shader.
- `RenderEditOverlays()` — Draws corner handles (filled circles) and quad outline using `Hidden/Internal-Colored` shader.

All rendering uses `GL.PushMatrix() / GL.LoadOrtho() / GL.Begin(QUADS)`. This works in URP compatibility mode. If RenderGraph mode is required, this needs to be replaced with a `ScriptableRenderPass`.

## Extension Points

### Adding Spout/NDI Input
Set surface `sourceMode = RenderTexture` and assign the external receiver's RenderTexture to `surface.sourceTexture`. No package changes needed.

### Adding OSC Control
Create a new component that references `ProjectionMapperManager`, receives OSC messages (via extOSC or similar), and calls the public API: `surfaces[i].MoveCorner()`, `surfaces[i].corners[n] = ...`, `SwitchProfile()`, etc.

### ScriptableRenderPass Migration
Replace `Camera.onPostRender` callback with a custom `ScriptableRenderPass` injected via `ScriptableRendererFeature`. The pass would call the same `ProjectionRenderer` methods but within the URP render graph. Required if URP compatibility mode is disabled.

### Multi-Display
Call `Display.Activate()` for displays 1-7 in your project startup code. Assign surfaces to displays via `targetDisplay`. The overlay camera must exist on display 0; for other displays, create additional overlay cameras with the appropriate `targetDisplay`.

## Common Modifications

| Task | File(s) to modify |
|---|---|
| Change keyboard shortcuts | `ProjectionMapperManager.HandleInput()` |
| Add new per-surface parameter | `ProjectionSurface` (field) → `UpdateMaterial()` → shader (uniform) → `ProjectionGUI.DrawDetail()` → `SurfaceData` + `FromSurface()`/`ToSurface()` |
| Change warp algorithm | `HomographyWarp.shader` fragment function |
| Add mesh subdivision for large warps | `ProjectionRenderer.RenderWarpedSurfaces()` — replace GL quad with subdivided mesh |
| Add save/load UI for importing configs | `ProjectionPersistence` — add Import/Export methods that read/write arbitrary paths |
| Switch to Input System package | `ProjectionMapperManager.HandleInput()` — replace `Input.GetKey` calls |

## Test Scene

`ProjectionMapper > Create Test Scene` menu item (in `TestSceneBuilder.cs`) generates a complete scene with 3 content groups, 3 cameras, 3 pre-configured surfaces (left/center/right thirds, right one pre-skewed), an overlay camera, and a `TestSceneAnimator` that drives continuous motion via tag components (`SpinTag`, `OrbitTag`, `PulseTag`).

# VJConfig Project

## Overview
Dual-deck projection mapping VJ system built in Unity 6 (URP). Two independent 3D stages with cameras, lighting, and content — a "live" deck outputs to projectors while a "standby" deck is prepared, then a hard-cut "Take" swaps them. Output via HomographyWarp corner-pin warping to up to 2 projector displays. MIDI control surface support via Midi Fighter 64.

## Unity Project
- **Unity Version**: 6000.3.5f2 (Unity 6)
- **Render Pipeline**: URP 17.x (Forward+)
- **Platform**: Windows
- **Resolution**: 1920x1080 per camera RT
- **Main Scene**: `Assets/Test.unity`

## Architecture: Dual-Deck System

### Signal Flow
```
GUI / MIDI Input → DualDeckManager → StageController (A or B)
                                   → DeckCameraRig (Still/Orbit/Push behaviors)
                                   → DeckLightRig (0-50 point lights, hue/spread)
                                   → Content (calibration grid / primitives)

Take (hard cut) → DualDeckManager.Take()
               → Swap live/standby
               → OutputManager.RefreshSources() → swap RTs on projection surfaces
```

### Output Flow
```
Stage Camera (x4) → RenderTexture (1920x1080)
                  → OutputManager (2 ProjectionSurfaces in RT mode)
                  → HomographyWarp shader (corner-pin, crop, brightness, gamma)
                  → GL.DrawQuad via endCameraRendering → Display 1 & 2 (projectors)

Display 0 (GUI) → ScreenCamera (clears to dark, cullingMask=0)
               → IMGUI overlay (DualDeckGUI: Master/DeckA/DeckB/Mapping tabs)
```

### Scene Spatial Layout
- **Stage A**: Centered at `(0, 0, 0)`
- **Stage B**: Centered at `(5000, 0, 0)`
- Stages are 5000 units apart to eliminate light/physics bleed

### Display Configuration
- **Display 0**: Control GUI (IMGUI with camera previews and controls)
- **Display 1**: Projector 1 — live deck Cam 1 with homography warp
- **Display 2**: Projector 2 — live deck Cam 2 with homography warp
- Multi-display activated via `Display.displays[i].Activate()` in builds (editor only shows Display 0)

## Project Structure
```
Assets/VJSystem/
  Scripts/
    DualDeck/                              # ACTIVE SYSTEM
      DualDeckManager.cs                   # Singleton orchestrator, Take() hard cut, live/standby tracking
      StageController.cs                   # Per-stage content management, calibration grid, test content
      DeckCameraRig.cs                     # 2 cameras/stage, 3 behaviors (Still/Orbit/Push), runtime RTs
      DeckLightRig.cs                      # 0-50 dynamic point lights, hue/spread/intensity control
      DualDeckGUI.cs                       # IMGUI: Master/DeckA/DeckB/Mapping tabs, Esc quit, F1 toggle
      OutputManager.cs                     # Multi-display output, ProjectionSurface warp, Take RT swaps
      DisplayManager.cs                    # Optional multi-display activation (superseded by OutputManager)
    PostFX/                                # DISABLED — legacy single-stage PostFX
      IPostFXSystem.cs, PixelSortSystem.cs, ChromaticDisplacementSystem.cs, DepthOfFieldSystem.cs
      PixelSortVolume.cs, ChromaticDisplacementVolume.cs
      PixelSortFeature.cs, PixelSortPass.cs
      ChromaticDisplacementFeature.cs, ChromaticDisplacementPass.cs
      PostFXRouter.cs
    Presets/                               # DISABLED — legacy preset system
      PixelSortPresetLibrary.cs, ChromaticPresetLibrary.cs, DoFPresetLibrary.cs
      PresetSaveSystem.cs, RandomizationSystem.cs
    MIDI/                                  # Available for future dual-deck MIDI routing
      MidiEventManager.cs                  # Singleton bridge to Minis MIDI input
      MidiFighter64InputMap.cs             # Note numbers (36-99) → GridButton (row/col)
      MidiGridRouter.cs                    # Legacy routing (needs replacement for dual-deck)
      UnityMainThreadDispatcher.cs         # MIDI thread → main thread dispatch
    Camera/                                # DISABLED — legacy 8-camera Cinemachine system
      VJCameraSystem.cs, OrbitalDriftExtension.cs, Figure8PathExtension.cs
      HandheldNoiseExtension.cs, ZoomPulseExtension.cs
    Lighting/                              # DISABLED — legacy 8-group light system
      VJLightSystem.cs
    SceneSlots/                            # DISABLED — legacy scene slot system
      VJSceneSlotSystem.cs
    Output/                                # DISABLED — legacy output
      SpoutOutputManager.cs, VJProjectionBridge.cs
    UI/
      VJDebugHUD.cs                        # DISABLED
    Utility/
      SpinCube.cs                          # Simple rotation component (global namespace)
  Shaders/
    PixelSort.compute                      # GPU bitonic sort (disabled, available for future use)
    ChromaticDisplacement.shader           # 8-pass displacement pipeline (disabled)
    ChromaticDisplacementMask.shader       # Object mask writer (disabled)
  Editor/
    DualDeckBootstrap.cs                   # Creates full dual-deck scene hierarchy (MCP-callable)
    DualDeckCleanup.cs                     # Disables old scene objects
    DualDeckSceneSetup.cs                  # MenuItem version with confirmation dialog
    IncludeShadersInBuild.cs               # Adds ProjectionMapper shaders to Always Included Shaders

Assets/com.projectionmapper/              # Projection mapping package (reused by OutputManager)
  Runtime/
    ProjectionMapperManager.cs            # Original manager (disabled, OutputManager replaces it)
    ProjectionSurface.cs                  # Surface config: corners, source RT, crop, feather, brightness, gamma
    ProjectionRenderer.cs                 # Static GL rendering: warped quads, debug grid, edit overlays
    ProjectionPersistence.cs              # JSON profile save/load
    ProjectionGUI.cs                      # IMGUI config window (F12)
    HomographyMath.cs                     # 3x3 homography from 4 corner correspondences
    Shaders/
      HomographyWarp.shader               # Perspective-correct warp (Always Included in build)
      DebugGrid.shader                    # Debug view grid overlay (Always Included in build)
```

## Scene Hierarchy (Active)
```
Global Volume                              # URP volume (PixelSort/Chromatic overrides disabled)

--- Dual Deck Systems ---
  DualDeckManager                          # Orchestrator + Take
  OutputManager                            # Display output + mapping surfaces (warpShader serialized)
    OutputCam_Proj1                        # Camera targeting Display 1 (created at runtime)
    OutputCam_Proj2                        # Camera targeting Display 2 (created at runtime)
  DualDeckGUI                              # IMGUI control interface
  ScreenCamera                             # Display 0 background (cullingMask=0, depth=-10)

--- Stage A --- (origin 0,0,0)
  StageController_A                        # Content management
    Content/                               # Runtime-spawned primitives + ground plane
  CameraRig_A                              # DeckCameraRig
    Cam1_A, Cam2_A                         # Cameras rendering to 1920x1080 RTs
  LightRig_A                               # DeckLightRig (dynamic point lights)
  DirectionalLight_A

--- Stage B --- (origin 5000,0,0)
  (mirror of Stage A)

--- [DISABLED] Legacy Systems ---
  VJ Systems parent, CameraRig, Main Camera, SpinningCubes, etc.
```

## Camera Behaviors
- **Still**: Random position on configurable-radius perimeter circle, looking at stage center. Re-triggering reshuffles position.
- **Orbit**: Continuous Y-axis rotation at configurable speed (1-45 deg/s), radius, and height.
- **Push**: Linear dolly from start to end distance using SmoothStep easing, auto-loops with configurable pause.

## Keyboard Controls
| Key | Action |
|-----|--------|
| Escape | Quit application (build) / Stop play mode (editor) |
| F1 | Toggle GUI visibility |
| Space | Take (hard cut swap live/standby) |
| Tab | Switch selected mapping output (Projector 1/2) |
| Hold 1-4 + Arrows | Move corner pin (1=TL, 2=TR, 3=BR, 4=BL) |
| Shift + Arrows | Fine corner adjustment |
| Ctrl + Arrows | Coarse corner adjustment |

## Dependencies
- **DOTween Pro** — parameter tweening (`Assets/Plugins/Demigiant/`)
- **ProjectionMapper** — in-project package (`Assets/com.projectionmapper/`), HomographyWarp shader, ProjectionSurface used by OutputManager
- **KlakSpout** (`jp.keijiro.klak.spout` 2.0.6) — Spout output (available, currently disabled)
- **Cinemachine 3** (`com.unity.cinemachine` 3.1.5) — available for future camera enhancement
- **Minis** (`jp.keijiro.minis`) — MIDI input via InputSystem
- **RtMidi** (`jp.keijiro.rtmidi`) — native MIDI runtime

## Build Notes
- **Shaders**: HomographyWarp and DebugGrid are in Always Included Shaders AND serialized on OutputManager. Never use `Shader.Find()` alone for shaders needed in builds.
- **Materials**: Runtime primitives must use `Shader.Find("Universal Render Pipeline/Lit")` explicitly — `CreatePrimitive()` default material uses built-in Standard shader which URP strips from builds.
- **Multi-Display**: `Display.displays[i].Activate()` only works in standalone builds, not in editor. Editor always shows Display 0 only.
- **Design Document**: `Design Document Dual-Deck Projectio.txt` at repo root describes the full target architecture.

## MCP Servers
- **Coplay MCP** is configured at user scope for communicating with the running Unity Editor
- Uses full absolute path to `uvx.exe` to avoid PATH issues
- Added via: `claude mcp add --scope user --transport stdio coplay-mcp --env MCP_TOOL_TIMEOUT=720000 -- "C:\Users\casey\AppData\Roaming\Python\Python313\Scripts\uvx.exe" --python ">=3.11" coplay-mcp-server@latest`
- If Coplay tools aren't showing up, restart Claude Code to pick up the MCP server connection
- Custom Coplay package located at `Packages/Coplay/`

## Conventions
- All active runtime types are in `VJSystem` namespace (except `SpinCube` in global)
- Event-driven architecture: static events on routers, subscribe in `OnEnable`, unsubscribe in `OnDisable`
- `RenderPipelineManager.endCameraRendering` for per-camera GL rendering (not OnPostRender)
- DOTween available for parameter transitions
- Runtime content uses explicit URP Lit materials (never default Standard shader)
- Editor setup via `DualDeckBootstrap.cs` (callable from MCP or menu)

## Current State (2026-03-07)

### Active & Working
- Dual-deck architecture: two stages at (0,0,0) and (5000,0,0)
- 4 cameras (2/stage) rendering to 1920x1080 RTs
- 3 camera behaviors: Still, Orbit, Push
- 0-50 dynamic point lights per stage with hue/spread/intensity
- Take system (hard cut) swaps live/standby decks
- OutputManager with HomographyWarp GL rendering to Display 1+2
- IMGUI with 4 tabs: Master (previews), Deck A, Deck B, Mapping (corner pin/crop/brightness/gamma)
- Calibration grid (5x3 checkerboard cubes) as default content
- Escape to quit, F1 toggle GUI, keyboard corner pin editing
- ProjectionMapper shaders in Always Included Shaders for builds

### Disabled (Legacy Single-Stage System)
- PostFX stack (PixelSort, ChromaticDisplacement, DepthOfField — volume overrides disabled)
- 8-camera Cinemachine rig, 8-group light system, 24 scene slots
- PostFXRouter, PresetSaveSystem, RandomizationSystem
- VJProjectionBridge, SpoutOutputManager, VJDebugHUD
- Old scene objects (Main Camera, SpinningCubes, CenterSphere, Ground, PointLights)

### Next Steps
1. **Add MIDI routing for dual-deck** — new DualDeckMidiRouter replacing MidiGridRouter
2. **Test multi-display output** — build and test with 3 monitors/projectors
3. **Prefab library system** — VJPrefabManifest, dropdown selection per stage
4. **Quality tiers** — dual URP pipeline assets (live=full, edit=reduced)
5. **Mapping persistence** — save/load corner pin + crop to JSON
6. **Re-enable PostFX** — integrate PixelSort/ChromaticDisplacement per-deck when basic system is stable
7. **VFX Graph integration** — per-stage VFX toggle
8. **Error recovery** — display disconnect detection, frame budget monitoring

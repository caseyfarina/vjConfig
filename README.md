# VJ Config — Unity 6 VJ System

A MIDI-driven VJ system built in Unity 6 (URP) for live visual performance. Controls post-processing effects, camera motion, lighting, and scene management via a Midi Fighter 64 grid controller, with Spout output to MadMapper.

## Features

- **MIDI Control**: Full 8x8 grid mapping via Midi Fighter 64 — presets, randomization, and live parameter control through `MidiGridRouter` and `PostFXRouter`
- **GPU Pixel Sort**: Real compute shader pixel sorting with configurable threshold modes (luminance, hue, saturation, brightness), sort axes, span length control, and ascending/descending order
- **Chromatic Displacement**: Multi-pass Sobel-gradient chromatic displacement with per-channel angle/amount control, custom color palettes, object masking with dilation/feather, radial falloff, and pre-blur
- **Depth of Field**: URP DepthOfField volume control with Bokeh and Gaussian modes
- **Camera System**: Cinemachine-based with orbital drift, handheld noise, figure-8 paths, and zoom pulse extensions
- **Preset System**: 7 named presets per effect + randomization within configurable bounds, JSON save/recall via `PresetSaveSystem`
- **Spout Output**: KlakSpout integration for sending frames to MadMapper or other Spout receivers
- **Debug HUD**: On-screen display of active preset names and effect states

## Architecture

Effects use the **Volume bridge pattern** — each System (`PixelSortSystem`, `ChromaticDisplacementSystem`, `DepthOfFieldSystem`) implements `IPostFXSystem` and programmatically writes to URP `VolumeParameter` values with DOTween transitions. The Volume overrides drive `ScriptableRendererFeature` passes built on the Unity 6 RenderGraph API.

```
MIDI Input → MidiGridRouter → PostFXRouter → IPostFXSystem.ApplyPreset()
                                                    ↓
                                            Volume bridge (DOTween)
                                                    ↓
                                          VolumeComponent parameters
                                                    ↓
                                    ScriptableRendererFeature + RenderGraph
```

## System Requirements

- Unity 6 (6000.3+)
- Universal Render Pipeline (URP 17.x)
- Windows (Spout output is Windows-only)

## Dependencies

- [DOTween Pro](http://dotween.demigiant.com/) — parameter tweening
- [KlakSpout](https://github.com/keijiro/KlakSpout) — Spout output to MadMapper
- [Cinemachine 3](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/) — camera system

## Setup

1. Open in Unity 6 with URP
2. **URP Renderer Asset**: Add "Pixel Sort" feature (assign `PixelSort.compute`) and "Chromatic Displacement" feature
3. **Global Volume**: Add "Pixel Sort" and "Chromatic Displacement" overrides, enable all parameter overrides, set strength/amount to 0
4. **System GameObjects**: Wire the `globalVolume` reference on `PixelSortSystem`, `ChromaticDisplacementSystem`, and `DepthOfFieldSystem`
5. **Preset Libraries**: Create ScriptableObject assets via `Create > VJSystem > Pixel Sort Preset Library` and `Create > VJSystem > Chromatic Preset Library`
6. Connect Midi Fighter 64 and enter Play mode

## Project Structure

```
Assets/VJSystem/
  Scripts/
    PostFX/          # Effect systems, features, passes, volumes
    Presets/          # Preset libraries, save system, randomization
    MIDI/             # MIDI input routing
    Camera/           # Cinemachine extensions
    Lighting/         # Light control
    Output/           # Spout output
    UI/               # Debug HUD
  Shaders/
    PixelSort.compute                 # GPU bitonic sort
    ChromaticDisplacement.shader      # 8-pass displacement pipeline
    ChromaticDisplacementMask.shader  # Object mask writer
```

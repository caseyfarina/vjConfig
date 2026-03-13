# Projection Mapper

Lightweight Unity URP package for real-time projection mapping with multi-surface corner-pin warping.

## Features

- Multi-surface corner pinning with perspective-correct homography
- Per-display output (up to 8 displays)
- Camera or RenderTexture sources (Spout-compatible via KlakSpout)
- Runtime IMGUI config panel
- Profile system with JSON persistence
- Debug view for ground-truth input verification
- Shader-based RGSS anti-aliasing with analytical gradients
- Per-surface brightness and gamma

## Installation

Copy `com.projectionmapper` into your project's `Packages/` directory.

## Quick Start

1. Add empty GameObject with `ProjectionMapperManager` component
2. Enter Play mode, press F12 to open config
3. Add surfaces, set camera paths, enable Edit Mode
4. Hold 1/2/3/4 + Arrows to pin corners
5. Config auto-saves on exit

## Controls

- **F12**: Toggle config GUI (configurable)
- **Hold 1/2/3/4 + Arrows**: Move corner (TL/TR/BR/BL)
- **Shift+Arrow**: Fine | **Ctrl+Arrow**: Coarse
- **[ / ]**: Cycle selected surface

## Requirements

- Unity 2021.3+ with URP 12.0+

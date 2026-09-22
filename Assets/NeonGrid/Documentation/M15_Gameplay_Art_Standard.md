# Neon Grid M15 Gameplay Art Standard

## Technical Neon Infrastructure

Gameplay tile art is authored against a normalized 256 × 256 canvas. Connector centers are fixed at:

- Top: `(128, 256)`
- Right: `(256, 128)`
- Bottom: `(128, 0)`
- Left: `(0, 128)`

The prototype conduit width is 30 px, the powered hot core is 11 px, and the local soft-halo metric is 28 px. One base orientation is authored per tile; circuit content rotates clockwise around `(128, 128)` while the housing remains stable.

Presentation layers are, back to front: housing, circuit base, powered halo/energy, hot core, functional symbol, lock treatment, and hint/tutorial rim. Powered readability must remain sufficient with post-processing and bloom disabled.

## Raster Import Standard

Future raster gameplay assets should use:

- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single where applicable
- Pivot: Center
- Mip Maps: Off
- sRGB: On
- Alpha Is Transparency: On
- Filter Mode: Bilinear unless a specific asset requires another mode

Android texture-compression optimization is intentionally deferred beyond M15-B1.

## Production Isolation

`TechnicalNeonPrototype` is opt-in. A `BoardView` without an assigned `CircuitVisualThemeDefinition` continues to use the accepted programmer-art fallback. The isolated `M15_GameplayVisualPrototype` scene binds PS01 and CG10 for visual QA and is not part of production campaign flow.

The prototype enables a presentation-only `Hold Completion` control by default. It keeps the logically completed board and powered objective visible while suppressing only the prototype completion panel. Disabling the hold reveals the normal completion presentation; production gameplay never enables this option.

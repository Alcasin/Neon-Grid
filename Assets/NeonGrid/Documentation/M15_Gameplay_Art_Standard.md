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

## B2 Production Skin Prototype

`TechnicalNeonProductionPrototype` is a second opt-in theme. It shares B1's authoritative 256 × 256 geometry, connector centers, 30 px conduit, 11 px hot core, 28 px halo, semantic palette, and clockwise content rotation. B1 and the programmer-art fallback remain separate selectable paths.

### Housing and board

Production tiles sit in low-contrast recessed sockets. Each module uses a graphite/navy surface, narrow bevel keyline, inset center, and four compact fasteners. The board adds a dark technical plate, shallow recess, restrained cyan edge, corner fasteners, and a calm background plate. These layers never contribute gameplay-like lines.

### Conductors and ports

Straight, Corner, T, and Cross use the same conductive channel width. A subtle material edge sits behind the inactive conductor. Powered state adds the established cyan energy, hot inner core, and local halo. Compact collars remain centered on the exact four connector coordinates and do not change conduit endpoints.

### Functional modules

- Source: reinforced chamber, magenta core, inner emission, lightning motif, and cyan network output.
- LED Objective: framed receiver backplate, diffuser, four text-free emitters, dim warm OFF state, bright warm ON state, and local amber halo.
- Diode: orange chevron and blocking bar seated in a dark mechanism plate.
- Switch: enlarged open/closed contact geometry on a mechanical bed with terminal collars.
- AND/OR: label-free D-shaped and curved/pointed silhouettes seated in framed logic-module beds. Accepted two-input/one-output rotations remain authoritative.
- Locked: compact retaining brackets and fasteners strengthen the housing without covering the circuit.
- Hint: the accepted gold outer-rim pulse remains unchanged and never recolors the circuit.

### HUD

The B2 HUD keeps the accepted layout and readable font sizes. Buttons and modal panels use graphite/navy housings, restrained cyan outlines, bold control labels, existing active/disabled behavior, and thin top/bottom keylines. Prototype selector and completion-hold controls remain development UI above the gameplay HUD.

### Mobile and performance rules

Topology and mechanic symbols remain the first two visual priorities at 720 × 1280. Decorative contrast stays below circuit contrast. Runtime construction uses shared sprites and default materials; each hierarchy is built once, with refresh limited to transforms, colors, and active states. No per-frame textures, geometry rebuilding, custom shaders, lighting, global bloom, or new font dependency is required.

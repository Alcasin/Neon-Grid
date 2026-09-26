# M15-E1A — City building art pipeline (prototype only)

## Direction and acceptance boundary

Stylized 2D / 2.5D isometric infrastructure diorama: dark navy/graphite architecture,
warm amber service/interior lights, controlled cyan/teal network energy. Buildings
are environmental architecture, not enlarged puzzle tiles or UI panels.
The supplied flat geometry swatches demonstrate registration, massing and lighting;
they are **not final concept artwork**. No final roads, city background or other
three building illustrations are included.

Production `neon_grid_main` continues to create its accepted programmer silhouettes.
No existing production script, scene, campaign, theme or build setting is changed.
Only `M15_CityBuildingArtPrototype.unity` binds the new definitions. Its art lives
outside Resources, so an excluded prototype scene does not pull the textures into
a production player. Runtime types are reusable, but no production binding exists.

## Data and layer contract

`CityBuildingArtDefinition` contains a stable chapter association, four registered
sprite slots, four tints, five opacity vectors, footprint fraction, local offset and
local scale. It contains no progress, narrative, save data or authored map positions.

Order (back to front):

1. Base architecture — required, visible even while locked.
2. Warm facility lights — optional service/interior lighting.
3. Electrical energy — optional cyan infrastructure routing.
4. Restored/core accent — optional restrained focal highlight.

All layers use identical transparent source canvas dimensions/pivot and registration.
Never bake chapter labels, locks, stars or UI text into the images. No global Bloom
or custom shader/material is required. A separate glow layer is intentionally omitted
in E1A: the fourth slot provides enough demonstration without another transparent pass.

State opacities (base, warm, energy, core):

| M13 state | Opacities |
| --- | --- |
| Locked | .65, 0, 0, 0 |
| Stage 1, 0–3 | .75, .35, 0, 0 |
| Stage 2, 4–6 | .85, .65, .30, 0 |
| Stage 3, 7–9 | .95, .85, .65, 0 |
| Restored, 10 | 1, 1, 1, .85 |

Warm tint is (1, .69, .32), energy (.12, .85, .88), core (.55, 1, .9).
The geometry never swaps between five full images. Lighting becomes cumulative:
warm areas appear first, routing appears next and the core is added only at restored.
Stage 2 to 3 increases existing lighting intensity. State does not rely on a single
whole-building tint. Validation requires five finite, bounded, monotonic vectors
and a visible base. Optional overlays may be absent.

`CityBuildingArtView.Present(chapterState, completed, total)` calls the existing
`CityMapPresentationModel.GetChapterVisualState`; the overload accepting an already
resolved state allows a future M13 presentation adapter. Neither API records progress.

## Geometry, safe areas and restoration

M13 node positions, hit targets and visual regions are authoritative. The view is
parented under the existing `Building Silhouette`, inheriting D2's already-applied
themed transform. Runtime dimensions never come from source pixel counts.

Power Station remains at (-310, -510), hitbox 280×280, visual region 210×150,
scale 1, center Y 38. Its art rectangle is .9×.7 of that region: **189×105**.
Central Grid remains at (40, -300), hitbox 350×330, visual region 260×205,
effective scale **1.075**, center Y **76**. Its rectangle is .88×.7 of the region:
**228.8×143.5** before inherited scaling, **245.96×154.2625** afterward.
Local offsets are zero and art-local scale is 1. The display-width ratio is about
1.30; Central Grid remains the dominant taller hub.

These explicitly inset rectangles reserve the lower label area, rather than relying
on transparent pixel luck. At maximum art emphasis (1.045), full rectangular bounds
remain inside the hit region and over six reference units above the label rectangle.
No clipping mask trims the source art. Source padding provides additional clearance.
If future authoring changes offset/scale, re-run bounds tests before accepting it.

The view captures its exact local position/rotation/scale. `ApplyEmphasis(0..1)`
computes a multiplier from that base (never from last frame); `ResetEmphasis()` and
state changes restore the base exactly, including on disable. Alpha is assigned from
the selected state's data, never accumulated. Existing node focus/power-up transforms
can also be inherited through the parent. A future adapter must choose one emphasis
owner to avoid multiplying two simultaneous effects. Production restoration timing,
energy travel and controller code are untouched; E1A only demonstrates compatibility.

## Fallback and performance

The caller passes the existing placeholder Images. Missing/invalid definitions,
wrong chapter associations or missing base sprites leave placeholders intact.
Valid art disables (does not destroy) those Images. Missing optional layers are
disabled individually. Disabling/removing the view restores the original Image enable
states. If base art becomes invalid during a subsequent state update, the view returns
to fallback; reinitialization/rebinding after runtime asset replacement is intentionally
not implemented. Author-time replacements are picked up on the next Play run.

Four Images are created once at initialization. State changes only assign sprite,
color and enabled state. No per-frame texture/sprite generation, hierarchy rebuilding,
material cloning or decorative Update loop. The preview emphasis uses a short, user-
triggered QA coroutine only. Texture creation is an editor-only build step.

## Source artwork and import standard

Ordinary buildings: transparent source canvas roughly 768–1024 px wide. Central Grid:
roughly 1024–1536 px. Included swatches are 1008×560 and 1232×770, respectively,
matching their intended rectangular aspect ratios. Keep 6–10% transparent edge padding
for local accent edges and emphasis. All layers for one building must share dimensions.

Import as Sprite (2D and UI), Single, full rectangle mesh, centered pivot, alpha
transparency enabled, sRGB enabled, mipmaps off, bilinear filter, clamp wrapping.
The prototype uses uncompressed RGBA and max size 2048 for inspectable swatches.
Final Android texture compression/atlas optimization is explicitly deferred.

Power Station brief: compact, grounded rectangular industrial hall, wide base and
two recognizable vertical stacks. Warm service strips precede cyan operational accents.
Central Grid brief: larger radial/multi-arm hub and central tower, clearly distinct
from an ordinary factory. Warm satellite modules precede cyan routes and core.
Keep both silhouettes clean at phone-scale map size, not only at source-art zoom.

## Replacement workflow

Import new transparent layers using the standard above. Select `PowerStation.asset`
or `CentralGrid.asset` in `Art/CityBuildingPrototype`; replace sprite references and,
if needed, layer tints/opacities in the Inspector. Keep association IDs unchanged.
Do not run the builder over artist replacements: it deliberately refuses to overwrite
an existing definition. No presentation code edit is required to replace art.
Re-run `CityBuildingArtTests`, inspect all states at map scale, then the regression suite.
Production rollout needs a separately authorized task; do not assign art to production.

## Manual visual acceptance

1. Open `Assets/NeonGrid/Scenes/M15_CityBuildingArtPrototype.unity`, enter Play.
2. Set Game view to 1080×1920, then 1080×2340, then 720×1280.
3. Use gray DEV controls to select Power Station / Central Grid. Both stay at their
   real map positions among unchanged placeholder neighbors and accepted paths.
4. For each, click Locked, Stage 1, Stage 2, Stage 3, Restored. Confirm dark recognizable
   massing, warm-first recovery, cumulative cyan activation, restrained restored core.
5. Check labels/progress/stars remain clear, Central Grid remains dominant, no cropping
   or offscreen art, and QA controls remain readable. Sample progress labels are not saves.
6. Click Emphasis repeatedly; switch state/building during emphasis. Confirm exact
   settle, no growth/drift, no label collision. This is not a production sequence replay.
7. Exit Play, open the normal production campaign scene: accepted D2 placeholders
   must remain, with no DEV controls or authored building art.

Automated tests verify layout/data/state contracts, not subjective final-art quality.
Final illustrations, production adapter wiring and mobile device performance/compression
are intentionally deferred.

## Implementation verification — Unity 6000.3.8f1

Initial working tree was clean. Final implementation adds only E1A files: no pre-existing
tracked file changed. SHA-256 verification matched all 216 original files under
Resources, Scenes and ProjectSettings, including production levels/campaigns/themes
and build settings. The two new scene files are the only additions in those directories.

- New E1A tests: 35/35 passed.
- Combined focused/regression run: 433/433 passed.
- D2: 24; D1: 33; C: 39; B2: 41; B1: 50 — all passed.
- M14: 85; M13: 58; campaign/progression: 51; persistence: 17 — all passed.
- Complete EditMode suite: 896/896 passed (861 baseline + 35 new).
- Final runs: 0 failed, 0 skipped, 0 inconclusive.
- Build, focused regression and full-suite logs: 0 C# compiler errors/warnings.

Local test artifacts: `Logs/m15e1a-regression.xml`, `Logs/m15e1a-full.xml` and
corresponding `.log` files (ignored by git). Initial focused iterations exposed an
EditMode lifecycle-harness issue: non-ExecuteAlways MonoBehaviour callbacks cannot
be tested by toggling enabled or using SendMessage. The final test invokes those
callbacks directly through reflection, without suppressing any log or weakening
the fallback assertions.

Portrait coverage is structural at 1080×1920, 1080×2340 and 720×1280, including
full art rectangles under maximum emphasis and QA controls versus map label bounds.
Manual Game-view visual acceptance remains required; no subjective art-quality or
on-device performance signoff is claimed.

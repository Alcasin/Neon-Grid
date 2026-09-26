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

## M15-E1B — final-art binding contract (prepared, art not supplied)

E1B adds production-ready definitions without connecting them to the production map:

- `Art/CityBuildings/PowerStation/PowerStation_Final.asset` → `power_station`
- `Art/CityBuildings/CentralGrid/CentralGrid_Final.asset` → `central_grid`

Stable `ChapterId` is the sole association. Chapter index, display text, level ID and
scene names are never used. Both definitions are separate from E1A prototype assets.

Expected authored files are exactly:

```text
Assets/NeonGrid/Art/CityBuildings/PowerStation/PowerStation_Base.png
Assets/NeonGrid/Art/CityBuildings/PowerStation/PowerStation_WarmLights.png
Assets/NeonGrid/Art/CityBuildings/PowerStation/PowerStation_Energy.png
Assets/NeonGrid/Art/CityBuildings/PowerStation/PowerStation_Core.png
Assets/NeonGrid/Art/CityBuildings/CentralGrid/CentralGrid_Base.png
Assets/NeonGrid/Art/CityBuildings/CentralGrid/CentralGrid_WarmLights.png
Assets/NeonGrid/Art/CityBuildings/CentralGrid/CentralGrid_Energy.png
Assets/NeonGrid/Art/CityBuildings/CentralGrid/CentralGrid_Core.png
```

Power Station authoring target is a 1024×1024 transparent canvas: grounded industrial
hall, strong roof massing, twin stacks, dark graphite/navy structure, restrained warm
service lights and progressively introduced cyan infrastructure. Central Grid target
is 1536×1536: symmetric radial support hub, central technical tower/core, readable
dark structure and controlled cyan routing. Source dimensions are recommendations;
all four files for one building must share their actual dimensions and registration.

`CityBuildingArtAssetValidator` reports rather than repairs: missing mandatory base,
wrong chapter ID, non-Sprite texture, non-Single mode, disabled alpha/sRGB, enabled
mipmaps, non-Bilinear filtering, non-centered/mismatched pivots, canvas mismatch and
invalid definition data. Optional overlays may be absent; the base remains mandatory.
The preparation command deliberately does not modify importers to conceal a failure.

E1B authored opacity profiles differ slightly by building:

| State | Power Station base/warm/energy/core | Central Grid base/warm/energy/core |
| --- | --- | --- |
| Locked | 1 / 0 / 0 / 0 | 1 / 0 / 0 / 0 |
| Stage 1 | 1 / .28 / 0 / 0 | 1 / .32 / 0 / 0 |
| Stage 2 | 1 / .62 / .18 / 0 | 1 / .70 / .22 / 0 |
| Stage 3 | 1 / .88 / .63 / .12 | 1 / .90 / .72 / .30 |
| Restored | 1 / 1 / 1 / .65 | 1 / 1 / 1 / 1 |

The architecture remains readable at full alpha; authored Base art supplies its dark
material values. Warm facility activity precedes electrical energy. Central Grid gains
more core emphasis at Stage 3/Restored while retaining dark structural regions.

Run **Neon Grid > Prepare M15 Final City Building Art Bindings** after importing or
updating authored PNGs. The command creates missing definition/folder assets, binds
only Sprites Unity can actually load at the exact contract paths, preserves import
errors for validation, and updates only the isolated comparison scene references.
It never generates images and never assigns final art to a production campaign/node.

The isolated scene provides `PROTOTYPE / FINAL` switching. `FINAL` is disabled and
marked missing until both final definitions have valid base sprites. State and emphasis
controls operate on whichever valid source is selected; switching reuses the existing
four Images and assigns registered sprites/tints without rebuilding on state changes.
Production `CampaignRuntimeView` continues to create only programmer silhouettes.

Final-art QA after files are supplied:

1. Import every supplied layer using Sprite 2D/UI, Single, alpha + sRGB enabled,
   mipmaps off, Bilinear, centered pivot. Do not trim layers independently.
2. Run the preparation command. Resolve every validation issue; do not use prototype
   images or importer auto-fixes as substitutes.
3. Open `M15_CityBuildingArtPrototype.unity`, enter Play and compare PROTOTYPE/FINAL.
4. For each building, inspect all five states and repeated EMPHASIS at 1080×1920,
   1080×2340 and 720×1280. Check pixel registration, label clearance, readable dark
   architecture, warm-first activation, restrained cyan and zero transform drift.
5. Confirm Central Grid retains center Y 76, effective scale 1.075 and approximately
   20–30% greater map prominence; confirm Power Station remains inside 210×150.
6. Open production `M12_NeonGrid_Main.unity` and confirm it still uses placeholders.
   Do not authorize rollout based only on automated tests.

At E1B preparation time all eight authored PNGs listed above are absent. The final
definitions intentionally contain null sprite slots, fail mandatory-base validation,
and leave FINAL comparison disabled. E1A prototype art remains available and no
substitute final artwork was generated. Final visual acceptance is therefore pending.

E1B automated verification in Unity 6000.3.8f1:

- E1B focused: 32/32 passed; E1A focused: 35/35 passed.
- Requested combined regressions: 465/465 passed.
- Complete EditMode suite: 928/928 passed.
- 0 failed, 0 skipped, 0 inconclusive, 0 C# compiler errors/warnings.
- Production Resources and ProjectSettings have no working-tree modifications.
- The comparison scene remains excluded from Editor build settings.

## M15-E1B.1 — final Power Station overlays

The manually supplied `PowerStation_Base.png` is authoritative and locked. It is a
1254×1254 RGBA PNG with real alpha and SHA-256
`C713B39D3E579A13DDC3F4672896D7F49E32D203C918FDCA5D7F7698E05CC343`.
It imports as Sprite 2D/UI, Single, Full Rect, center pivot, Input Texture Alpha,
alpha-is-transparency and sRGB on, mipmaps/read-write/physics shape off, Clamp,
Bilinear, max size 2048 and no compression. E1B.1 does not rewrite this file.

`Tools/Art/GeneratePowerStationOverlays.py` uses Pillow and hand-authored masks in
the locked image's exact coordinate space. It asserts the Base hash before and after
generation, never samples or redraws architecture, never crops/scales/rotates the
canvas, and writes only three transparent registered 1254×1254 RGBA overlays:

- `PowerStation_WarmLights.png`: three sparse, segmented amber service-light clusters,
  subordinate machinery indicators and warm-white hot points. The clusters are sized
  to remain readable at the 189×105 map footprint; they do not reproduce the yellow
  rail network or outline the architecture.
- `PowerStation_Energy.png`: a limited set of narrow cyan major-pipe/conduit routes
  plus two restrained stack-base arcs. It does not outline the silhouette.
- `PowerStation_Core.png`: six small final-state junctions, with three cyan-white hot
  centers. There is no reactor sphere or large roof fill.

Solid effects are intersected with Base alpha. Compact Gaussian halos are constrained
to a 15-pixel dilation of authored building alpha, preventing rectangular matte/glow
artifacts. At meaningful alpha above 8/255, Warm covers under .8%, Energy under 2%,
and Core under .3% of the source canvas. The accepted Power Station opacity profile
is `(1,0,0,0)`, `(1,.42,0,0)`, `(1,.72,.24,0)`,
`(1,.88,.63,.12)`, `(1,1,1,.65)`.
Final definitions use neutral white layer tints because these authored PNG overlays
already contain their intended amber/cyan color; this prevents double-tint darkening.

`M15PowerStationOverlayImportConfigurator` explicitly configures generated overlay
importers to the locked Base contract after first rejecting any canvas mismatch. The
general E1B preparation step then binds all four Sprites to `PowerStation_Final.asset`
and validates them; it still reports Central Grid missing. Final mode is now available
per selected building: Power Station can compare Prototype/Final while Central Grid
remains prototype-only. Switching source reuses the same four Images.

`Documentation/Previews/PowerStation_StatePreview.png` is a development-only strip
showing Locked through Restored at the actual 189×105 map display size. It is not a
scene/build dependency. This preview supports composition review but does not replace
manual Game-view acceptance at 1080×1920, 1080×2340 and 720×1280.

Manual acceptance for E1B.1:

1. Open `M15_CityBuildingArtPrototype.unity`, enter Play and select Power Station.
2. Alternate PROTOTYPE and FINAL; confirm the FINAL architecture is the supplied Base,
   with no crop, shift, scale, perspective or registration change between layers.
3. Cycle all five states. Confirm warm lights appear first, cyan remains controlled,
   and Restored retains graphite/navy architecture instead of becoming a glow blob.
4. Run EMPHASIS at least ten times and switch states/source between cycles. Confirm
   all four layers remain registered and settle to the exact authored transform.
5. Repeat at 1080×1920, 1080×2340 and 720×1280; inspect label/progress/star clearance.
6. Select Central Grid and confirm FINAL is unavailable. Open production M12 and
   confirm the City Map still uses the accepted programmer silhouettes.

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

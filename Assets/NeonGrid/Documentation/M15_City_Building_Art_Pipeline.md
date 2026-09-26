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
and validates them. At E1B.1 acceptance, Central Grid was still missing and therefore
remained prototype-only. Switching source reuses the same four Images.

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
6. Open production M12 and confirm the City Map still uses the accepted programmer
   silhouettes. E1B.2 supersedes the earlier Central Grid FINAL-missing check.

## M15-E1B.2 — final Central Grid overlays

The manually supplied `CentralGrid_Base.png` is authoritative and locked. It is a
1278×1278 RGBA PNG with real 0–255 alpha and SHA-256
`C42FA4773C5A0430D7E6DF2A559B895F065B66F6181AF52B51BAAAE1858B3ECB`.
The Base is an isolated radial infrastructure hub with a dominant central tower. It
imports as Sprite 2D/UI, Single, Full Rect, center pivot `(639,639)`, Input Texture
Alpha, alpha-is-transparency and sRGB on, mipmaps/read-write/physics shape off, Clamp,
Bilinear, max size 2048 and no compression. E1B.2 never rewrites this file.

`Tools/Art/GenerateCentralGridOverlays.py` uses Pillow and hand-authored masks in the
locked Base's exact coordinate system. It asserts the Base hash before and after every
run and writes only three registered transparent RGBA overlays:

- `CentralGrid_WarmLights.png`: four readable amber service banks, selected tower and
  arm indicators, restrained inner-ring activity and warm-white hot points. It does
  not illuminate every module or trace the complete silhouette.
- `CentralGrid_Energy.png`: selected cyan central-ring sectors, five primary radial
  distribution routes, paired tower conduits and limited interface nodes. Graphite
  architecture remains visible between routes.
- `CentralGrid_Core.png`: a narrow central-tower focal column, compact cyan-white hot
  center, restrained ring reinforcement and five final-state distribution nodes.
  It does not introduce a full-building bloom or obscure the tower geometry.

All final layers retain neutral white tints because their intended amber/cyan colors
are authored into the PNGs. The accepted Central Grid profile remains the prepared E1B
profile: `(1,0,0,0)`, `(1,.32,0,0)`, `(1,.70,.22,0)`,
`(1,.90,.72,.30)`, `(1,1,1,1)`. This preserves warm-only Stage 1, subtle first cyan
at Stage 2, a clear radial network at Stage 3 and the strongest central core only when
Restored.

The isolated prototype binds all four final Central Grid layers to
`CentralGrid_Final.asset` under stable association `central_grid`. Both buildings now
support Prototype/Final comparison without creating additional Images. Production
campaign scenes remain placeholder-bound and have no dependency on final-art assets.

`Documentation/Previews/CentralGrid_StatePreview.png` shows the five Central Grid
states at the inherited real map footprint of approximately 246×154. The separate
`PowerStation_CentralGrid_Comparison.png` shows final Power Station at 189×105 beside
final Central Grid at 246×154. This preserves the accepted 1.30 width hierarchy:
Central Grid is approximately 20–30% more prominent due to silhouette, tower and
existing map scale rather than indiscriminate brightness. Both previews are
development-only and excluded from build dependencies.

Central Grid remains at authored node `(40,-300)`, outer node `350×330`, themed visual
scale `1.075` and center Y `76`. Its `.88×.7` art rectangle reserves the D2 lower
label/progress/star safe area. E1B.2 does not change M13/D2 geometry.

Manual acceptance for E1B.2:

1. Open `M15_CityBuildingArtPrototype.unity`, enter Play and select Central Grid.
2. Alternate PROTOTYPE and FINAL. Confirm FINAL uses the supplied radial Base and all
   four layers remain perfectly registered with no crop, shift or geometry change.
3. Cycle Locked, Stage 1, Stage 2, Stage 3 and Restored. Confirm warm-first activation,
   subtle Stage 2 cyan, clear Stage 3 routing and a strong but compact restored core.
4. Compare final Central Grid with final Power Station. Confirm Central Grid is roughly
   20–30% more prominent while Power Station remains visually intact.
5. Run EMPHASIS at least ten times while switching state/source. Confirm exact transform
   return, no layer separation, alpha drift, hierarchy growth or label collision.
6. Repeat at 1080×1920, 1080×2340 and 720×1280. Inspect the tower, radial arms,
   label/progress/star clearance and restored-glow restraint.
7. Exit Play and open production M12. Confirm both buildings still use the accepted
   programmer silhouettes and no final-art prototype controls or bindings are present.

## M15-E1C — production rollout for Power Station and Central Grid

Production campaign data now owns an optional list of `CampaignCityBuildingArtBinding`
records. Each record associates a stable authored chapter ID with one
`CityBuildingArtDefinition`; display names, chapter indexes, level IDs and hierarchy
names are never used for lookup. `neon_grid_main` contains exactly:

- `power_station` → `PowerStation_Final.asset`
- `central_grid` → `CentralGrid_Final.asset`

Substation, Control Center and Automation Plant have no binding and therefore retain
their accepted programmer-art silhouettes. Final art remains environmental map content
on the campaign, separate from `CampaignUiThemeDefinition`.

`MainCampaignBuilder` loads and validates both accepted definitions, authors the two
stable-ID bindings after cloning the five source chapters, and fails clearly if either
accepted production definition is missing or invalid. Rebuilding repeatedly produces
the same campaign asset. Vertical-slice campaigns remain unbound and use fallback UI.

During production node construction, `CampaignRuntimeView` resolves optional art from
the campaign and passes it to `CityChapterNodeView`. A valid definition creates one
`CityBuildingArtView` with four Images beneath the existing Building Silhouette root.
No binding creates no art component. Missing, mismatched or invalid optional art emits
validation diagnostics but remains runtime-safe: the existing silhouette stays visible.
Null binding records, duplicate IDs and unknown chapter IDs remain validation errors.

The chapter node continues to own its hit target, label, M13 visual state and resolved
themed silhouette transform. Final art only mirrors the shared
`CityMapPresentationModel` state. The existing restoration focus, power-up, reveal and
network-pulse animations scale the common silhouette root, so all registered layers
move together and settle to the same authored/themed base transform. No art state is
written to saves: load and replay derive it again from campaign progress.

The shared M13 progression semantics remain authoritative: Locked chapters use Locked;
an available chapter with 0–3 completed levels uses Stage 1, 4–6 Stage 2, 7–9 Stage 3,
and 10 Restored. Therefore a fresh campaign intentionally displays available Power
Station at Stage 1 and locked Central Grid at Locked. Stars affect labels only, never
building restoration state.

The existing editor-only **Neon Grid > Narrative QA** window now includes deterministic
Power Station Stage 1/2/3/Restored and Central Grid Stage 1/2/3 presets. It retains the
existing automatic production-save backup and explicit restore flow; no debug controls
are added to production runtime UI.

Production manual QA:

1. Back up the current production save in **Neon Grid > Narrative QA**.
2. Prepare Fresh Map, Power Station Stage 1/2/3/Restored and First Restoration Pending.
3. Run `M12_NeonGrid_Main.unity`; inspect the map and first restoration sequence.
4. Prepare Central Grid Stage 1/2/3, Final Restoration Pending, Ending Pending and
   Post-Ending Complete; inspect each production lifecycle state.
5. Repeat map inspection at 1080×1920, 1080×2340 and 720×1280. Confirm final Power
   Station and Central Grid remain label-safe while the other three silhouettes remain
   unchanged and every original node hit target remains authoritative.
6. Replay an already completed Power Station and Central Grid level, return to the map
   and confirm their Restored art state remains derived from progress.
7. Restore the Narrative QA backup when finished.

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

## Substation isolated final-art preparation

`Substation_Source.png` is the immutable geometry source. The editor-only
`M15SubstationFinalArtPreparation` tool centers its `1484×1060` RGBA pixels on a
transparent `1484×1484` canvas, preserving the silhouette and perspective. It
neutralizes only the authored emissive control-building windows and warning beacon in
the derived Base, then draws sparse deterministic amber facility, cyan distribution,
and compact restored-instrument overlays in the same registered coordinate system.
The source file is never overwritten.

The four derived textures use the normal final-art import contract: Sprite (2D and
UI), Single, Full Rect, centered pivot, input alpha, sRGB, no mipmaps, no Read/Write,
Bilinear, Clamp, max size 2048, and no compression. `Substation_Final.asset` uses the
stable `substation` ID and the shared cumulative five-state profile philosophy.
The deterministic documentation-only `Substation_StatePreview.png` shows all five
registered states and is not referenced by production scenes or build settings.

This preparation remains isolated. The E1 prototype uses the existing programmer
Substation silhouette in Prototype mode and reuses the same four final Image objects
in Final mode. Invalid or missing Substation final art leaves the programmer silhouette
visible. `neon_grid_main` is intentionally unchanged and continues to bind only final
Power Station and Central Grid art; Substation is not a production binding in this pass.

## Control Center isolated final-art preparation

`ControlCenter_Source.png` remains the immutable `1484×1060` geometry source. The
editor-only `M15ControlCenterFinalArtPreparation` tool centers it without cropping or
scaling on a transparent `1484×1484` registered canvas. The derived Base neutralizes
only command-room, monitor, service-indicator and beacon emission while retaining the
tower, dishes, platforms, equipment, rails, supports and non-emissive painted detail.

The registered overlays provide restrained warm operational rooms and beacons, sparse
cyan communications/routing activation, and compact restored command-core highlights.
All four runtime textures use the shared final-art importer contract.
`ControlCenter_Final.asset` uses stable ID `control_center` and the authored cumulative
five-state opacity profile. `ControlCenter_StatePreview.png` is deterministic,
documentation-only, and excluded from production dependencies.

The existing prototype uses the accepted programmer Control Center silhouette in
Prototype mode and one reused four-Image art view in Final mode. Invalid or missing
Control Center final art preserves fallback. Production remains the E1C rollout:
Power Station and Central Grid final, with Substation, Control Center and Automation
Plant still unbound programmer-art chapters.

## Automation Plant isolated final-art preparation

`AutomationPlant_Source.png` remains the immutable `1536×1024` geometry source. The
editor-only `M15AutomationPlantFinalArtPreparation` tool centers its pixels without
cropping, scaling or rotation on a transparent `1536×1536` registered canvas. The
derived Base neutralizes only localized facility windows, service lights, machine-bay
emission and safety beacons while preserving the modular factory silhouette,
perspective, machinery, conveyors, platforms, rails, pipes and painted detail.

The registered overlays provide restrained amber facility activation, sparse cyan
automation/conduit routing, and compact restored machine-bay instrumentation. All four
runtime textures use the established final-art import contract.
`AutomationPlant_Final.asset` uses stable ID `automation_plant` and its exact cumulative
five-state opacity profile. `AutomationPlant_StatePreview.png` is deterministic,
documentation-only, and excluded from production dependencies.

The isolated prototype retains the programmer Automation Plant silhouette in Prototype
mode and reuses one four-Image art view in Final mode. Missing, invalid or mismatched art
safely retains fallback. Production remains the E1C rollout: only Power Station and
Central Grid use final art; Substation, Control Center and Automation Plant remain
programmer-art chapters in the production campaign.

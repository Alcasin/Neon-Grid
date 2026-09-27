# M15 City Environment Art Pipeline

## Direction and scope

The city environment is a restrained technical-infrastructure diorama behind the accepted City
Map presentation. Dark navy and graphite city blocks, roads, rooftops and utility corridors provide
readable structure. Sparse warm service lights and controlled cyan utility accents communicate
restoration without competing with chapter buildings, M13 energy routes, labels or campaign UI.

The E3A/E3B isolated prototype remains available for deterministic art and state QA. M15-E3C binds
the accepted final definition to the production campaign through authored campaign data; the
prototype scene remains excluded from build settings.

## Authored layer contract

`CityEnvironmentArtDefinition` owns one registered sprite set:

1. Base City
2. `power_station` district overlay
3. `substation` district overlay
4. `control_center` district overlay
5. `automation_plant` district overlay
6. `central_grid` district overlay
7. optional final all-city accent

All sprites share identical canvas dimensions, center pivot and registration. Stable chapter IDs,
never display names, object names or chapter indices, associate overlays with campaign state. The
definition stores presentation assets and transform data only; it contains no progress, narrative
or save state and does not duplicate chapter positions.

The Base City represents the mostly-offline city and can contain subdued blocks, roads, rooftops,
utility corridors and inactive infrastructure. It must not contain chapter labels, final buildings,
M13 energy paths, restoration text or broad bright lighting. Each district overlay contains only
localized restored infrastructure such as selected windows, service lamps and restrained utility
accents. It does not redraw the full city.

## State and rendering

`CityEnvironmentArtView` creates the fixed seven-image hierarchy once. Images are non-raycast and
share normal UI rendering; there are no unique materials, masks, runtime textures, `Update` polling
or per-state hierarchy rebuilds.

The campaign presentation supplies `CampaignProgressService`. A district is visible only when the
authoritative `CampaignChapterState` is `Restored`. Partial progress and stars do not affect it. When
all five districts are restored, the optional final accent is shown. Save/load simply reconstructs
chapter state through the existing campaign schema and then refreshes presentation; environment
state is never serialized.

The required conceptual order is:

1. campaign backdrop / City Ground
2. Base City and district overlays
3. existing ambient/background map details
4. existing M13 energy paths
5. accepted final chapter buildings
6. chapter labels, progress and stars
7. campaign UI chrome

Both prototype and production insert art immediately after City Ground in the existing 1020 x 1500
City Composition. Chapter positions, node geometry and the composition offset remain authoritative.

## Safe areas

Keep bright details away from chapter name, LOCKED/RESTORED, progress and star anchors. Preserve the
Central Grid lower label-safe region, the Control Center header-safe region and left/right containment
at Substation and Automation Plant. Environment artwork must adapt to the accepted map rather than
moving buildings or nodes.

## Source and import recommendation

Final authored layers should use a shared portrait source canvas around 1536 x 2048 (or another
consistent resolution with sufficient detail), transparent where appropriate, and identical pivot
and registration. Recommended Unity import settings are Sprite (2D and UI), Single, Full Rect,
center pivot, alpha enabled, sRGB enabled, mipmaps off, Clamp and Bilinear. Android compression is
intentionally deferred to mobile optimization.

## Prototype and replacement workflow

Use **Neon Grid > M15 > Build City Environment Art Prototype** to deterministically regenerate the
simple validation sprites, definition and isolated `M15_CityEnvironmentArtPrototype` scene. The
scene offers environment OFF/ON and 0-through-5 restored-state controls without writing progress.

## E3B final-art preparation

`CityEnvironment_Source.png` is the byte-preserved approved source. Use **Neon Grid > M15 > Prepare
Final City Environment Art** to deterministically derive the inactive Base, five localized warm-light
district contributions, the restrained final accent, and `CityEnvironment_Final.asset`. Preparation
keeps the source dimensions, alpha silhouette, center pivot, and layer registration. It cools only
detected baked warm emphasis in the Base; overlays restore source pixels through stable-ID elliptical
district masks, with dedicated Control Center upper and Central Grid lower safe-area offsets.

The preparation tool refreshes the isolated prototype binding. It does not author production data.

## E3C production binding

`CampaignDefinition.CityEnvironmentArt` is the single optional production association. The main
campaign builder loads and validates `CityEnvironment_Final.asset`, rejects a missing or invalid
definition before authoring, and writes that reference alongside the existing five building-art
bindings. Repeated builder runs are byte-idempotent. Vertical-slice campaigns keep a null reference.

`CampaignRuntimeView` creates exactly one `CityEnvironmentArtView` when the authored definition is
configured and every district stable ID belongs to the campaign. When the association is missing,
invalid or mismatched, no environment view is created and the legacy programmer-art backdrop stays
available. This optional presentation failure is non-fatal.

With valid final art, only the redundant direct children `City Block N`, `Road Horizontal`,
`Road Vertical`, and `Road Diagonal` are disabled at runtime. They are not deleted or reordered.
`Energy Path N`, chapter nodes, hit targets, final building art and labels remain enabled above the
environment. The isolated prototype uses the same exact suppression rule and explicitly opts out of
the production association to avoid duplicate layers.

Production state is derived whenever the authoritative map presentation refreshes:

- partial chapter progress never enables a district;
- `CampaignChapterState.Restored` enables the matching stable-ID overlay;
- stars do not influence environment state;
- all five restored chapters enable `FinalAccent`;
- replaying completed levels cannot duplicate restoration events or change the derived state.

No environment data is added to the save schema. Existing save/load progress reconstructs chapter
state, which reconstructs the visible overlays. The M13 restoration sequence retains timing
ownership; E3C adds no fades, pulses, coroutines or per-frame polling.

Runtime cost is capped at seven reused non-raycast UI Images under the existing campaign Canvas.
There is no runtime texture generation, unique material, extra Canvas, decorative `Update`, or
per-map hierarchy creation.

## Production QA

Use the existing Narrative QA presets with backup/restore protection to inspect Fresh Map, each
chapter Restored state, every pending restoration, Ending Pending and Post-Ending Complete. At each
state verify district count, M13 paths, building/label priority and the absence of legacy block/road
occlusion. Repeat Map -> Selector -> Map at least ten times and check 1080x1920, 1080x2340 and
720x1280. Subjective Game View acceptance remains manual.

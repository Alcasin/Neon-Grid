# M15 City Environment Art Pipeline

## Direction and scope

The city environment is a restrained technical-infrastructure diorama behind the accepted City
Map presentation. Dark navy and graphite city blocks, roads, rooftops and utility corridors provide
readable structure. Sparse warm service lights and controlled cyan utility accents communicate
restoration without competing with chapter buildings, M13 energy routes, labels or campaign UI.

M15-E3A is an isolated pipeline and composition prototype. It is not connected to the production
campaign, production runtime map or build settings.

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

The prototype inserts its art immediately after City Ground in the existing 1020 x 1500 City
Composition. Chapter positions, node geometry and the composition offset remain authoritative.

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

For final-art replacement, author every layer on the same registered canvas, import with the contract
above, replace sprite references in a reviewed environment definition, validate all restoration states
and portrait resolutions in the isolated scene, and obtain manual acceptance before any separate
production rollout. Production campaign binding and restoration animation remain future work.

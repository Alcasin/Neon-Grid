# Neon Grid — Milestones 0–4

Open any scene under `Assets/NeonGrid/Scenes` and press Play. Each scene references its own data-driven `LevelDefinition`; no level layout is selected by gameplay code.

To solve the included circuit, rotate the straight wire once and the corner wire three times.

The core simulation lives under `Assets/NeonGrid/Scripts/Simulation` and has no dependency on presentation or input code. EditMode tests cover rotation, mutual connection rules, power removal, and completion.

## Milestone 0 simulation boundary

Milestone 0 power propagation used breadth-first traversal with one `IsPowered` flag per tile. That model remains appropriate for passive components, but Milestone 2 extends transient state with energized input sides and active output sides for stateful and multi-input evaluation.

## Milestone 1 circuit vocabulary

Physical connector sides and permitted power-flow sides are separate simulation concepts. Passive components permit power to enter and leave through every physical connection. A diode has physical Left/Right connections at rotation zero, but accepts power only on Left and emits it only on Right; rotation transforms all three direction sets together.

Programmer-art test scenes:

- `M1_Test_01`: rotate the T-junction three times to power both lamps; also displays a fixed cross junction.
- `M1_Test_02`: rotate the lower corner three times and follow the fixed wire/corner path.
- `M1_Test_03`: the source initially faces the diode output; rotate the diode twice to permit input-to-output flow.

## Milestone 2 stateful logic

Each recalculation clears transient electrical state, seeds every source, and runs a monotonic work queue until no new output side can become active. A tile records energized input sides separately from active and already-propagated output sides. AND activates only after both designated inputs are recorded; OR activates after either; gate output sides reject incoming power. Switch ON/OFF state is persistent runtime state initialized by `TileDefinition.startingSwitchOn`, while all electrical flags are rebuilt from scratch after every interaction.

Base gate orientation uses Left and Right as inputs and Up as output. Rotation transforms all port roles together.

Programmer-art test scenes:

- `M2_Test_01`: tap the initially OFF switch to power the lamp; tap again to depower it.
- `M2_Test_02`: either input switch alone leaves AND output OFF; turn both ON to power the lamp.
- `M2_Test_03`: either input switch activates OR output; both inputs also remain valid.

## Milestone 3 solver and validation

`PuzzleAction` is the shared simulation-domain representation for a clockwise rotation or switch toggle. Runtime interaction and the solver ask `BoardState` for the same valid actions and apply them through the same tile rules.

`PuzzleSearchState` owns an independent board copy. Its canonical key contains only player-changeable rotations and switch states in stable grid order; powered flags and gate input/output bookkeeping are recalculated by the existing propagation service and do not define state identity. `PuzzleSolver` performs deterministic breadth-first search, returning the first (therefore minimum-move) solution or a distinct `Unsolvable` / `SearchLimitReached` status.

Select any `LevelDefinition` asset and click **Validate Level** in its Inspector to run structural checks followed by solver validation. The M3 assets under `Resources/Levels` cover one-move rotation, rotation plus switch, two-input AND, unsolvable, and constrained-search cases.

The current propagation model is a monotonic work queue tailored to the accepted M0–M2 component behavior. Passive tiles are handled by reachability, while AND/OR already rely on explicit energized input sides. Future stateful or multi-input components must define their own input/output evaluation and must not assume a single `powered` or `visited` boolean is sufficient.

## Milestone 4 level authoring

Open **Neon Grid > Level Editor** to create or load a `LevelDefinition`, edit its visual grid, rotate and configure selected cells, resize safely, and validate or solve the current unsaved working copy. Edits remain isolated until **Save Level** is used. Saving records one Unity Undo operation and is blocked while structural validation errors remain.

The editor preview is generated directly from `TileType`, `TileConnections`, and `TilePowerFlow`; it does not use scene GameObjects or production art. Palette entries are enumerated from the domain `TileType` values.

`TestLevelAssetBuilder` remains responsible only for deterministic milestone fixtures and their regression scenes. Future manually authored production levels should use the Level Editor instead of adding level-specific builder code.

## Milestone 7 tutorial boundary

Tutorial sequences are optional serialized metadata on campaign level entries. Their runtime state is presentation-local to one attempt and observes successful authoritative `PuzzleAction` values; it never changes circuit state or move accounting. Campaign completion history decides whether a new attempt receives its tutorial, so completed replays skip it and resetting campaign progress restores it without a separate tutorial save format.

Hint and tutorial targets share one reason-based tile highlight layer. They retain independent state when active together; tutorial green has visual priority when both reasons target the same tile, while hint magenta remains visible on a different tile.

# Neon Grid — Milestones 0–2

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

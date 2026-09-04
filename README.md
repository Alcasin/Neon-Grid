# Neon Grid — Milestones 0–1

Open any scene under `Assets/NeonGrid/Scenes` and press Play. Each scene references its own data-driven `LevelDefinition`; no level layout is selected by gameplay code.

To solve the included circuit, rotate the straight wire once and the corner wire three times.

The core simulation lives under `Assets/NeonGrid/Scripts/Simulation` and has no dependency on presentation or input code. EditMode tests cover rotation, mutual connection rules, power removal, and completion.

## Milestone 0 simulation boundary

Power propagation uses breadth-first traversal with one `IsPowered` flag per tile. This model is intentionally limited to the passive circuit components supported in Milestone 0. Future stateful or multi-input components such as AND/OR gates must evaluate their complete input state and must not assume that a simple powered/visited boolean is sufficient. No gate behavior is implemented in this milestone.

## Milestone 1 circuit vocabulary

Physical connector sides and permitted power-flow sides are separate simulation concepts. Passive components permit power to enter and leave through every physical connection. A diode has physical Left/Right connections at rotation zero, but accepts power only on Left and emits it only on Right; rotation transforms all three direction sets together.

Programmer-art test scenes:

- `M1_Test_01`: rotate the T-junction three times to power both lamps; also displays a fixed cross junction.
- `M1_Test_02`: rotate the lower corner three times and follow the fixed wire/corner path.
- `M1_Test_03`: the source initially faces the diode output; rotate the diode twice to permit input-to-output flow.

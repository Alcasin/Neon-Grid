# Neon Grid — Milestone 0

Open `Assets/NeonGrid/Scenes/Prototype.unity` and press Play. The prototype loads the data-driven 4x4 level from `Assets/NeonGrid/Resources/Levels/TestLevel4x4.asset`.

To solve the included circuit, rotate the straight wire once and the corner wire three times.

The core simulation lives under `Assets/NeonGrid/Scripts/Simulation` and has no dependency on presentation or input code. EditMode tests cover rotation, mutual connection rules, power removal, and completion.

## Milestone 0 simulation boundary

Power propagation uses breadth-first traversal with one `IsPowered` flag per tile. This model is intentionally limited to the passive circuit components supported in Milestone 0. Future stateful or multi-input components such as AND/OR gates must evaluate their complete input state and must not assume that a simple powered/visited boolean is sufficient. No gate behavior is implemented in this milestone.

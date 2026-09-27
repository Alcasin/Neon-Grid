# M16-A1 Interaction and Circuit Juice Foundation

## Scope

M16-A1 adds an opt-in presentation layer to the existing Technical Neon gameplay view.
`GameplaySession`, `BoardState`, simulation, hint solving, completion, move accounting and
persistence remain authoritative and have no dependency on animation completion.

The accepted production campaign does not reference a juice definition. The current binding is
limited to `M15_GameplayVisualPrototype.asset`, using
`M16_CircuitJuicePrototype.asset` for manual validation with PS01 and CG10.

## Event-driven presentation

`BoardController` labels an authoritative refresh as player action, undo, restart or synchronize.
`CircuitTileView` compares its previous presentation snapshot with the new authoritative tile state
and emits only meaningful visual transitions: accepted rotation, power activation/deactivation,
switch change, gate-output change and objective activation. Press, rejection, hint and completion
are explicit calls. No gameplay state is inferred from an animated transform.

`CircuitJuiceCoordinator` owns the board's single animation update. It advances only active
`CircuitTileJuiceView` instances and performs no board-state polling. Tile views do not implement
`Update`, allocate per frame, create materials, spawn particles or rebuild hierarchy per event.

## Transform ownership

The tile GameObject remains the canonical layout/input root. Opted-in tiles add one stable child:

```
Tile layout + collider + input
└── Juice Visual Root (temporary scale/offset only)
    └── accepted B2 visual hierarchy
        └── Rotating Circuit (temporary rotation interpolation)
```

The B2 renderer owns canonical circuit orientation. Juice owns only temporary scale, offset and the
interpolated path to the newest authoritative rotation. Settling and cancellation write exact
`Vector3.one`, `Vector3.zero` and the canonical quarter-turn quaternion.

## Definition and reduced-motion preparation

`CircuitJuiceDefinition` centralizes feature toggles, timings, scales, rejection distance and a
global `motionScale`. A value of zero keeps the opt-in hierarchy valid but resolves all events
immediately to stable canonical presentation. This is preparation for future accessibility work;
M16-A1 adds no user-facing settings.

## Rapid input, undo and restart

- Repeated rotations retarget from the current visual angle to the newest authoritative quarter
  turn and snap exactly when settled.
- Multiple tiles animate independently under the one board coordinator.
- Undo retargets from the current presentation to the restored authoritative orientation/state.
- Restart cancels all active channels immediately, clears offsets/scales and snaps rotations.
- Hint emphasis uses the existing gold rim and never replaces powered cyan semantics.
- Completion remains immediate; active final power/rotation presentation may settle afterward.

## Power and component transitions

Power activation/deactivation is detected during board refresh and represented by restrained scale
impulses over the already accepted B2 layers. Source, LED objective, switch and AND/OR output
changes use distinct centralized amplitudes/durations but introduce no new semantic colors or art.
Locked interaction uses a small static-axis nudge and never invokes a gameplay action.

## Prototype QA

Open `M15_GameplayVisualPrototype.unity`. The prototype now opts into the M16 definition while
production remains unbound. PS01 covers basic rotation/source/lamp behavior; CG10 contains the full
tile vocabulary, locked tiles and a 36-tile density case. `HINT READY` advances only the isolated
prototype attempt to the existing 180-second hint threshold; press the normal HUD Hint button to
exercise the unchanged solver path. Use the existing Undo, Restart and Hold Completion controls.

Verify rapid repeated taps, multi-tile taps, undo/restart during motion, locked rejection, hint over
powered circuits, power loss/recovery, switch and gate output changes, final completion, and exact
settling. No audio, haptics, particles, screen shake or completion celebration is part of A1.

## Production isolation

Production initialization continues to call `BoardController` without a `CircuitJuiceDefinition`,
so no coordinator, juice root or animation state is created across the 50 production levels. A
later explicitly accepted rollout can author a production binding without changing simulation or
save data.

## A2 power-propagation presentation

`PropagationPresentationPlan` derives visual arrival depths only after the simulation has produced
its final authoritative board. It starts from every currently powered source, traverses only
authoritatively powered tiles, and uses the existing directed electrical rules: current active
outputs, mutual connector geometry and `TilePowerFlow.CanReceiveFrom`. The plan cannot mark a tile
powered, cannot change active gate outputs and is never consulted by gameplay, completion or saves.

Positions at the same breadth-first depth share one scheduled step, so T and Cross branches spread
together instead of becoming a serialized path. Diodes are traversed only from an active output
toward an accepted input; reverse visual travel is therefore impossible. Multiple sources are
supported by the same multi-root breadth-first search and a tile receives its nearest valid depth.

The prototype definition uses a 35 ms nominal depth delay, dynamically compressed to a maximum
420 ms arrival window. Local energy activation settles over 140 ms and deactivation uses an 85 ms
restrained response. The coordinator owns one bounded step list and its existing single `Update`;
there are no tile updates, per-segment coroutines, spawned wave objects, material instances or
hierarchy rebuilds.

## A2 stale-wave and lifecycle policy

Every accepted player action or Undo cancels the previous scheduled plan, restores its affected
renderer channels to the newest authoritative state and builds a replacement plan. A tile waiting
for its depth keeps that explicit waiting state even if its A1 rotation or press channel advances.
Restart cancels scheduled steps, source pulses and completion work, then performs the existing exact
canonical snap. Large deterministic `Advance` calls are subdivided at scheduled boundaries, which
makes tests independent of wall-clock timing while preserving correct settle behavior.

Deactivation never waits for a reverse wave. The renderer receives the unpowered authoritative
model immediately and a short local settle response follows. Hint target state and the gold hint
rim are separate from power intensity and are never cleared by the propagation coordinator.

## A2 completion presentation and channel ownership

`GameplaySession` still raises completion immediately. `BoardView` forwards that fact as a pending
presentation request; after the following authoritative refresh, the coordinator schedules a short
success response after the final planned arrival and local activation settle. All currently powered
tiles receive a synchronized 180 ms pulse using `#22DFA5` sparingly in halo/energy layers plus a
1.018 local scale impulse. Stable presentation returns exactly to accepted B2 colors and transforms.

Channel ownership remains explicit:

- layout owns the tile root;
- B2 rotating content owns authoritative orientation;
- A1 press/rejection owns temporary local scale/offset;
- A2 power owns energy visibility and halo emphasis;
- A2 completion owns temporary success emphasis and composes its scale with A1 mathematically.

No channel incrementally multiplies its previous frame, so repeated waves cannot accumulate drift.
With `motionScale = 0`, travel delay, energy ramps and success motion collapse to the immediate
authoritative B2 presentation while semantic powered/unpowered state remains visible.

## A2 prototype QA checklist

In `M15_GameplayVisualPrototype.unity`, keep B2 active and use PS01 for a short source-to-objective
path and CG10 for dense branches, locked tiles, diodes, switches and gates. Verify source-first
activation, equal-depth branch arrival, fast power loss, rapid rotations during an active wave,
Undo and Restart before settle, hint gold over cyan energy, final completion during rotation, and
repeated solve/restart cycles. `HOLD COMPLETION` remains isolated developer tooling for observing
the final powered board; it does not change production completion. Check the three accepted portrait
resolutions and confirm CG10's wave remains below the configured 560 ms travel-plus-local-settle
ceiling. Audio, haptics, particles, camera shake and production rollout remain outside A2.

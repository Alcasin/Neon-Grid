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

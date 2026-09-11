# Exact BFS profiling and implementation optimization

## Continuation and scope

Recovered the existing working tree with status, complete tracked diffs, diff statistics, and inspection of all modified solver/simulation/test files and new profiling files. All existing optimization changes were preserved. The continuation changed only tests and this report; no further production optimization or semantic fix was necessary. No reset, revert, stash, discard, asset regeneration, or process termination was performed.

Across the entire optimization task, modified files (relative to the project root):

- `Assets/NeonGrid/Scripts/Simulation/BoardState.cs`: direct independent copy.
- `Assets/NeonGrid/Scripts/Simulation/CircuitTileState.cs`: persistent-field copy constructor.
- `Assets/NeonGrid/Scripts/Simulation/PuzzleSearchState.cs`: lazy electrical evaluation and optional per-call profiling.
- `Assets/NeonGrid/Scripts/Simulation/PuzzleSolver.cs`: local key reuse and internal profiled entry point.
- `Assets/NeonGrid/Scripts/AssemblyInfo.cs`: Editor friend assembly for the developer benchmark.
- `Assets/NeonGrid/Tests/EditMode/PuzzleSolverTests.cs`: exact graph, differential, copy-isolation, and concurrency regressions.

Created `Assets/NeonGrid/Editor/SolverBenchmark.cs`, `Assets/NeonGrid/Scripts/Simulation/SolverProfile.cs`, their two Unity `.meta` files, and this `SolverProfiling.md`. No scenes, runtime session/hint/presentation files, or production assets were modified.

## Measurement method

Unity 6000.3.8f1, Windows, the same desktop and project, batch mode with the null graphics device. Each fixture received one complete warm-up solve, then three measured solves with an explicit GC collection before each. All three runs are reported; the summary uses the arithmetic mean. Fixture boards and RuntimeHint options were identical before and after. The scramble is the existing first legal action on S10: RotateClockwise (3, 0), applied once to an independent board.

The unprofiled production entry point supplies wall timings. A separate fourth solve uses optional, per-call Stopwatch counters to measure board-copy, power, key, and action-generation costs. Counter timing is exclusive across those four categories; the instrumented run is not used for speedup claims. There is no global mutable instrumentation or instrumentation logging on gameplay workers.

`GC.GetAllocatedBytesForCurrentThread()` returned zero in this Unity environment, so total allocated bytes are **unavailable**, not zero. A separate warmed copy benchmark compares the original definition-based copying operation and the optimized operation in the same process: three batches of 50,000 S10 copies, collecting before each batch, and recording GC.CollectionCount(0). GC counts show collection pressure, not precise allocated bytes or peak live memory.

Run the retained developer harness using Unity `-executeMethod NeonGrid.Editor.SolverBenchmark.Run -batchmode -nographics -projectPath <project> -logFile <log> -quit`. `NeonGrid.Editor.SolverBenchmark.CompareCopies` runs the copy allocation comparison. These benchmarks are separate from correctness tests and contain no performance pass/fail threshold. Full Unity logs/results from this task are under the ignored `Temp` directory and may be removed by Unity.

## Before and after

All fixtures returned Solved. Minimum moves and deepest depth were respectively 7, 6, and 9 on every run. Explored states and the full deterministic solution sequences were identical before and after.

| Fixture | States | Before mean | After mean | Speedup | States/sec before | States/sec after |
|---|---:|---:|---:|---:|---:|---:|
| S09 initial | 88,579 | 7.298 s | 2.186 s | 3.34x | 12,137 | 40,513 |
| S10 initial | 15,895 | 1.480 s | 0.421 s | 3.52x | 10,743 | 37,764 |
| S10 scrambled | 131,295 | 17.047 s | 4.917 s | 3.47x | 7,702 | 26,703 |

All measured elapsed milliseconds:

| Fixture | Before runs 1 / 2 / 3 | Earlier final-version runs | Continuation confirmation runs |
|---|---|---|---|
| S09 | 7221.25 / 7147.18 / 7526.12 | 2202.74 / 2253.78 / 2243.48 | 2085.35 / 2200.35 / 2273.58 |
| S10 | 1411.34 / 1501.29 / 1525.92 | 455.32 / 413.30 / 397.41 | 427.45 / 426.64 / 408.61 |
| S10 scrambled | 16909.10 / 17166.87 / 17065.76 | 4806.45 / 4698.42 / 4717.53 | 4779.57 / 5113.11 / 4857.84 |

The headline table uses every measured run from the latest confirmation, not selected fastest runs. Earlier final-version means were 2.233 / 0.422 / 4.741 seconds; the solver implementation was identical between these two sets. The original implementation was not restored to repeat baseline measurements.

An intermediate version cached the key inside each search state and measured 3.4–3.5x gains. The final version instead reuses the key locally inside BFS, preserving the existing dynamic key getter and reducing cache state. Only final-version timings are used above.

## Ranked hot paths and exact changes

S10 scrambled, separate instrumented runs:

| Category | Before ms | After ms | Before calls | After calls |
|---|---:|---:|---:|---:|
| Board copying | 12189.43 | 3315.40 | 731,778 | 731,778 |
| Power recalculation | 3670.02 | 367.05 | 1,463,555 | 131,295 |
| Canonical-key construction | 680.00 | 638.66 | 863,072 | 731,778 |
| Action-list generation | 105.69 | 105.59 | 56,291 | 56,291 |

For S09: copy 5502.82 → 1564.54 ms; power 1289.94 → 146.66 ms; key 376.09 → 330.93 ms; actions 54.19 → 54.67 ms. Copies remain 425,391; power calls 850,781 → 88,579; key calls 513,969 → 425,391; action lists remain 30,385.

For S10 initial: copy 1048.50 → 311.52 ms; power 328.56 → 40.52 ms; key 63.64 → 46.56 ms; actions 10.76 → 5.13 ms. Copies remain 64,670; power calls 129,339 → 15,895; key calls 80,564 → 64,670; action lists remain 4,975.

The continuation's separate instrumented confirmation measured copy/power/key/action milliseconds of 1577.81/142.02/335.27/57.08 (S09), 298.07/48.96/54.22/8.87 (S10), and 3345.02/344.95/647.74/112.93 (scramble). All call counts matched the earlier final-version profile above.

1. **Validated board reconstruction dominated.** Every copy created definitions for all tiles, invoked the general board constructor, allocated a full board of empty tiles, revalidated positions using a HashSet, then replaced every empty tile. A private copy constructor now copies already-validated persistent tile fields directly into a new array and independent tile objects. Rotation, switch state, position, type, and rotatability are preserved. Transient electrical fields still reset, matching the old copy contract. The public definition-based constructor retains all its validation.
2. **Each successor recalculated electricity twice before duplicate rejection.** Copying PuzzleSearchState recalculated its unmodified state; ApplyAction immediately recalculated it again, even for already-visited successors. Search states now evaluate power lazily when IsSolved or the internal Board accessor needs it. Applying an action invalidates electrical state. The existing fixed-point propagation code is called unchanged. Duplicate successors and depth-limit probes need only persistent state and keys; their electricity is never observed. Accepted new states receive the same complete recalculation before completion is checked. No legal action or canonical state is pruned.
3. **New states built identical keys twice.** BFS now computes a local key once and reuses it for Contains and Add. The canonical string format, tile ordering, switch/rotation encoding, and full string equality remain unchanged; hashes do not determine equality alone.

## Allocation observations

The isolated copy comparison produced:

| Operation, 50,000 S10 copies | Times, ms | GC collections |
|---|---|---|
| Original copying | 724.05 / 689.82 / 688.76 | 203 / 202 / 202 |
| Direct copying | 175.75 / 194.97 / 185.88 | 48 / 48 / 48 |

That is approximately 76% fewer collections in this controlled copy comparison. This is not a claim about full-solve GC counts or exact bytes.

At source level, the original 25-cell S10 copy allocated 50 TileDefinition and 50 CircuitTileState objects. The replacement allocates 25 CircuitTileState objects, with no TileDefinition objects. Across the measured 731,778 copies, this removes 54,883,350 such object allocations (75%), plus the definition lists, validation HashSets and associated arrays/enumerators. The source-list/frontier allocations inside power propagation are unchanged per call, but 1,332,260 calls are avoided on scrambled S10. The total number of copied candidate boards is unchanged.

## Deliberately retained implementation

- Exact FIFO BFS, HashSet visited membership, action ordering and all search-budget boundaries are unchanged.
- Full string state keys remain. A packed state representation was considered but rejected for this pass: key construction was a much smaller baseline cost than copying/power, and the measured target was reached without another representation.
- Action generation still scans tiles in the accepted order and returns the same legal actions. It was only about 0.1 s of the 17 s scrambled solve; precomputation is deferred.
- Nodes still store complete action arrays, and the existing solution path construction is unchanged. No evidence from the dominant categories required a parent-index rewrite to meet the target.
- PowerPropagationService and TilePowerFlow retain their queues/lists, per-input tracking, active-output tracking, cycle handling, AND/OR/Diode/Switch rules, and fixed-point algorithm. No shared reusable buffer was introduced.
- Completion still checks every required lamp and requires at least one lamp. No cached completion result is reused across state changes.
- No changes to budgets, production metadata, level startup, UI, campaign behavior, saves, stars, navigation, or the async Hint architecture.

## Thread and lifetime safety

All new mutable data is owned by a board/search state or the optional per-solve profile. There are no static scratch buffers. RuntimeHint still receives its independent main-thread snapshot, executes on Task.Run, returns via ConcurrentQueue, and applies results through the main-thread controller. No Unity API is used by the solver.

Inspection confirms invalidation discards the result but **does not cancel the worker**. An abandoned solve continues to completion or its existing budget limit. Repeated invalidation/re-request can therefore overlap independent workers. Cancellation is not implemented here. A future isolated change could check a CancellationToken at BFS expansion boundaries, with separately tested terminal cleanup; it is not needed for the throughput change.

Specifically: valid actions and Undo clear/invalidate the hint; Restart directly invalidates it; leaving to LEVELS or Map detaches/disposes the session; Retry and NEXT replace/dispose the previous session; completion and BoardController destruction also invalidate/dispose. All reject stale application using request identity, board version, and disposal checks. None signals cancellation to the underlying Task. Merely opening a leave confirmation is not the same as actually leaving.

The new concurrent regression runs independent S09 and S10 exact solves on worker tasks and compares each complete result against its sequential result. Each task owns a separate board, search frontier, visited set, and power service. The optional profile is per solve, not static. No worker touches Unity objects; assets are loaded before task creation. This establishes independence of separate solves, not support for concurrently mutating the same BoardState.

## Regression scope and production safety

New tests pin all three measured fixtures to their original status, exact minimum, depth, explored count, and complete action sequence. Differential tests use the existing eager PowerPropagationService and the original definition-based copying operation as the reference, across up to 80 deterministic action steps per resource level, plus an explicitly powered cycle with T/Cross junctions, locked tiles and two lamps. They compare legal actions, rotations, switches, powered tiles, energized inputs, active outputs, propagated outputs, completion and canonical strings. Production and component fixtures include AND, OR, Switch and Diode behavior. Independent-copy tests check persistent field equality and reset transient fields, rotation/switch/power isolation in both directions, and dirty lazy clones whose parent previously had a solved electrical cache. Key identity remains unchanged by evaluating electrical caches and differs for distinct rotation/switch states.

The existing component, cycle, multiple-output, fixed-tile, optimal BFS, state/depth limit, unsolvable, first-hint-action, async lifecycle, and production metadata tests remain required. Production startup tests for S09/S10 continue to verify authored baselines without solver invocation.

Production AuthoringExact minima verified by the existing tests, including equality with campaign AuthoredOptimalMoves metadata:

| Chapter | 01 | 02 | 03 | 04 | 05 | 06 | 07 | 08 | 09 | 10 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Power Station | 1 | 1 | 4 | 5 | 4 | 3 | 4 | 4 | 2 | 5 |
| Substation | 3 | 7 | 3 | 3 | 3 | 3 | 5 | 6 | 7 | 6 |

Budgets remain AuthoringExact 500,000 states / depth 64 and RuntimeHint 250,000 states / depth 64. Existing tests continue to cover Solved, Unsolvable, SearchLimitReached, exact minima, deterministic actions, and depth/state-limit behavior. The solver remains exact BFS.

Exact measured solution sequences before and after (R = RotateClockwise; T = ToggleSwitch):

- S09: T(1,1), R(2,1), R(2,1), R(2,1), R(3,3), R(3,3), R(3,3).
- S10: T(1,1), R(2,1), R(2,1), R(3,1), R(3,1), T(2,2).
- S10 after the accepted R(3,0) scramble: R(3,0), R(3,0), R(3,0), T(1,1), R(2,1), R(2,1), R(3,1), R(3,1), T(2,2).

## Final verification result

Unity 6000.3.8f1 complete EditMode suite: **307/307 passed**, 0 failed, 0 skipped, 0 inconclusive; duration 41.622 seconds. There are eight new test cases relative to the accepted 299-test baseline (three parameterized exact-graph cases, one differential test, two direct-copy tests, one dirty-search-copy test, one concurrent-solve test). The earlier complete run before the continuation's additional three tests was 304/304 passed.

Compiler results: **0 errors, 0 warnings**. `git diff --check` passes; Git's LF-to-CRLF notices are line-ending notices, not Unity compiler diagnostics. Final test evidence is `Temp/solver-tests-final.xml` and `Temp/solver-tests-final.log`; confirmation timing evidence is `Temp/solver-confirmation-benchmark.log`.

Existing async regressions passed: current snapshot, nonblocking/duplicate prevention, queue pumping, stale result rejection after actions/Undo/Restart/completion/disposal, failure/limit handling, first action without auto-execution, and authored-baseline startup. The real background runner test measured S10 initial 480.6 ms worker / 0.2 ms delivery and the deterministic scramble 5091.5 ms worker / 0.2 ms delivery; both reached HintAvailable. These are regression-run observations, not the warmed benchmark averages. S09 and S10 production session-start tests passed without invoking either baseline or hint search. Manual visual runtime acceptance has not been performed in this batch-mode pass.

Final SHA-256 comparison against the pre-optimization snapshot: **23/23 unchanged** (PS01–PS10, S01–S10, and all three campaign assets); 0 mismatches. Existing production hash/minimum/metadata tests also passed. No production content or campaign baseline serialization changed.

## Remaining cost and manual acceptance

Copying candidate boards is still the largest measured cost, followed by canonical string construction and power evaluation. Scrambled S10 still takes roughly 4–5 s on this desktop; mobile and larger-board throughput have not been measured. This is sufficient to recommend provisional M8 solver-maintenance closure after manual acceptance of the responsive waiting experience, not to claim mobile performance acceptance. Profile a development build on representative low/mid-range target phones before mobile sign-off, measuring worker elapsed time, main-thread frame time, allocation/GC pressure, peak memory, and overlapping stale searches. No Chapter 3 work is included and this benchmark does not establish performance for larger future levels.

1. Complete S08 and press NEXT: S09 entry should remain smooth, with its authored optimal baseline of 7 and no startup search.
2. Start S10, wait for the existing three-minute Hint unlock, and request Hint. Check Finding hint..., a continuing timer/responsive UI, one eventual highlighted action, no auto-execution, and duplicate-request prevention. On a fresh S10 attempt, rotate tile (3,0) clockwise exactly once to reproduce the benchmark scramble, wait for Hint unlock, and repeat. Expected desktop worker time is roughly 4–5 seconds for that exact state, not a promise for arbitrary scrambles.
3. During separate hint searches, perform a valid action, Undo, Restart, and confirmed leave to LEVELS/Map. Verify obsolete suggestions never appear. Exercise Retry/NEXT and a new session afterward; old results must not affect the replacement session. Inspect Console for new errors/warnings. These invalidations discard results but currently do not cancel the old CPU work.

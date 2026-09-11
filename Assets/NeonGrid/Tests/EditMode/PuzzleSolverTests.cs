using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using NeonGrid.Data;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NeonGrid.Tests
{
    public sealed class PuzzleSolverTests
    {
        [Test]
        public void NamedSearchProfiles_ExposeIntentionalPolicyValues()
        {
            PuzzleSolverOptions authoring = PuzzleSolverProfiles.AuthoringExact;
            PuzzleSolverOptions runtimeHint = PuzzleSolverProfiles.RuntimeHint;

            Assert.That(authoring.MaximumExploredStates, Is.EqualTo(500000));
            Assert.That(authoring.MaximumDepth, Is.EqualTo(64));
            Assert.That(runtimeHint.MaximumExploredStates, Is.EqualTo(250000));
            Assert.That(runtimeHint.MaximumDepth, Is.EqualTo(64));
        }

        [Test]
        public void ActionModel_ExposesOnlySupportedPlayerInteractions()
        {
            var board = new BoardState(3, 1, new[]
            {
                Tile(0, 0, TileType.CornerWire, 0, true),
                Tile(1, 0, TileType.Switch, 0, true, false),
                Tile(2, 0, TileType.StraightWire, 1, false)
            });

            Assert.That(board.GetValidActions(), Is.EqualTo(new[]
            {
                new PuzzleAction(new GridPosition(0, 0), PuzzleActionType.RotateClockwise),
                new PuzzleAction(new GridPosition(1, 0), PuzzleActionType.ToggleSwitch)
            }));
            Assert.That(board.IsActionValid(new PuzzleAction(new GridPosition(2, 0),
                PuzzleActionType.RotateClockwise)), Is.False);
        }

        [Test]
        public void RuntimeAndSearchState_UseTheSameActionValidity()
        {
            var board = new BoardState(3, 1, new[]
            {
                Tile(0, 0, TileType.AndGate, 0, true),
                Tile(1, 0, TileType.Switch, 0, false, false),
                Tile(2, 0, TileType.OutputLamp, 0, false)
            });
            IReadOnlyList<PuzzleAction> runtimeActions = board.GetValidActions();
            IReadOnlyList<PuzzleAction> searchActions = PuzzleSearchState.FromBoard(board).GetValidActions();

            Assert.That(searchActions, Is.EqualTo(runtimeActions));
            foreach (PuzzleAction action in runtimeActions)
            {
                var simulation = new CircuitSimulation(board.CreateIndependentCopy());
                PuzzleSearchState searchState = PuzzleSearchState.FromBoard(board);
                Assert.That(simulation.ApplyAction(action), Is.True);
                Assert.That(searchState.ApplyAction(action), Is.True);
            }
        }

        [Test]
        public void IdenticalLogicalStates_ProduceEqualKeys()
        {
            BoardState board = CreateSwitchAndRotationBoard();
            Assert.That(PuzzleSearchState.FromBoard(board).Key,
                Is.EqualTo(PuzzleSearchState.FromBoard(board).Key));
        }

        [Test]
        public void RotationAndSwitchChanges_ProduceDifferentKeys()
        {
            PuzzleSearchState initial = PuzzleSearchState.FromBoard(CreateSwitchAndRotationBoard());
            PuzzleSearchState rotated = initial.CreateIndependentCopy();
            PuzzleSearchState toggled = initial.CreateIndependentCopy();

            rotated.ApplyAction(new PuzzleAction(new GridPosition(1, 0), PuzzleActionType.RotateClockwise));
            toggled.ApplyAction(new PuzzleAction(new GridPosition(2, 0), PuzzleActionType.ToggleSwitch));

            Assert.That(rotated.Key, Is.Not.EqualTo(initial.Key));
            Assert.That(toggled.Key, Is.Not.EqualTo(initial.Key));
            Assert.That(rotated.Key, Is.Not.EqualTo(toggled.Key));
        }

        [Test]
        public void TransientElectricalState_DoesNotChangeLogicalStateIdentity()
        {
            PuzzleSearchState state = PuzzleSearchState.FromBoard(CreateSwitchAndRotationBoard());
            PuzzleStateKey originalKey = state.Key;
            CircuitTileState tile = state.Board.GetTile(new GridPosition(1, 0));

            tile.IsPowered = !tile.IsPowered;
            tile.EnergizedInputSides = CardinalDirection.Up | CardinalDirection.Left;
            tile.ActiveOutputSides = CardinalDirection.Down;
            tile.PropagatedOutputSides = CardinalDirection.Right;

            Assert.That(state.Key, Is.EqualTo(originalKey));
        }

        [Test]
        public void Solver_DoesNotMutateSuppliedBoard()
        {
            BoardState board = CreateSwitchAndRotationBoard();
            CircuitTileState wire = board.GetTile(new GridPosition(1, 0));
            CircuitTileState switchTile = board.GetTile(new GridPosition(2, 0));

            new PuzzleSolver().Solve(board);

            Assert.That(wire.Rotation, Is.Zero);
            Assert.That(wire.IsPowered, Is.False);
            Assert.That(switchTile.IsSwitchOn, Is.False);
            Assert.That(switchTile.IsPowered, Is.False);
        }

        [Test]
        public void Solver_DoesNotMutateLevelDefinitionAsset()
        {
            LevelDefinition level = LoadLevel("Levels/M3_Test_02");
            int[] rotations = level.Tiles.Select(tile => tile.startingRotation).ToArray();
            bool[] switches = level.Tiles.Select(tile => tile.startingSwitchOn).ToArray();

            PuzzleSolverResult result = new PuzzleSolver().Solve(level.CreateBoardState());

            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(level.Tiles.Select(tile => tile.startingRotation), Is.EqualTo(rotations));
            Assert.That(level.Tiles.Select(tile => tile.startingSwitchOn), Is.EqualTo(switches));
        }

        [Test]
        public void RepeatedSolving_IsDeterministic()
        {
            BoardState board = LoadLevel("Levels/M3_Test_03").CreateBoardState();
            PuzzleSolverResult first = new PuzzleSolver().Solve(board);
            PuzzleSolverResult second = new PuzzleSolver().Solve(board);

            Assert.That(second.Status, Is.EqualTo(first.Status));
            Assert.That(second.MinimumMoveCount, Is.EqualTo(first.MinimumMoveCount));
            Assert.That(second.ExploredStateCount, Is.EqualTo(first.ExploredStateCount));
            Assert.That(second.DeepestSearchDepth, Is.EqualTo(first.DeepestSearchDepth));
            Assert.That(second.Solution, Is.EqualTo(first.Solution));
        }

        [Test]
        public void AlreadySolvedLevel_HasZeroMoveSolution()
        {
            var board = new BoardState(2, 1, new[]
            {
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.OutputLamp, 0, false)
            });

            PuzzleSolverResult result = new PuzzleSolver().Solve(board);

            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(result.MinimumMoveCount, Is.Zero);
            Assert.That(result.Solution, Is.Empty);
        }

        [TestCase("Levels/M3_Test_01", 1)]
        [TestCase("Levels/M3_Test_02", 2)]
        [TestCase("Levels/M3_Test_03", 2)]
        public void KnownPuzzle_ReturnsExactMinimumAndReplaySolves(string resourcePath, int expectedMoves)
        {
            LevelDefinition level = LoadLevel(resourcePath);
            PuzzleSolverResult result = new PuzzleSolver().Solve(level.CreateBoardState());

            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(result.MinimumMoveCount, Is.EqualTo(expectedMoves));
            Assert.That(result.Solution.Count, Is.EqualTo(expectedMoves));
            TestContext.WriteLine($"{resourcePath}: minimum={result.MinimumMoveCount}, " +
                                  $"explored={result.ExploredStateCount}, depth={result.DeepestSearchDepth}");

            var replay = new CircuitSimulation(level.CreateBoardState());
            foreach (PuzzleAction action in result.Solution)
                Assert.That(replay.ApplyAction(action), Is.True);
            Assert.That(replay.IsLevelCompleted, Is.True);
        }

        [Test]
        public void DefinitionEnumerationOrder_DoesNotChangeBfsResult()
        {
            var definitions = new List<TileDefinition>
            {
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.StraightWire, 0, true),
                Tile(2, 0, TileType.Switch, 0, false, false),
                Tile(3, 0, TileType.OutputLamp, 0, false)
            };
            PuzzleSolverResult forward = new PuzzleSolver().Solve(new BoardState(4, 1, definitions));
            definitions.Reverse();
            PuzzleSolverResult reversed = new PuzzleSolver().Solve(new BoardState(4, 1, definitions));

            Assert.That(reversed.MinimumMoveCount, Is.EqualTo(forward.MinimumMoveCount));
            Assert.That(reversed.ExploredStateCount, Is.EqualTo(forward.ExploredStateCount));
            Assert.That(reversed.Solution, Is.EqualTo(forward.Solution));
        }

        [Test]
        public void CustomStateBudgets_PreserveSolvedUnsolvableAndLimitStatuses()
        {
            PuzzleSolverResult solved = new PuzzleSolver().Solve(
                LoadLevel("Levels/M3_Test_02").CreateBoardState(),
                new PuzzleSolverOptions { MaximumExploredStates = 100, MaximumDepth = 64 });
            PuzzleSolverResult unsolvable = new PuzzleSolver().Solve(
                LoadLevel("Levels/M3_Test_04").CreateBoardState(),
                new PuzzleSolverOptions { MaximumExploredStates = 100, MaximumDepth = 64 });
            PuzzleSolverResult limited = new PuzzleSolver().Solve(
                LoadLevel("Levels/M3_Test_05").CreateBoardState(),
                new PuzzleSolverOptions { MaximumExploredStates = 1, MaximumDepth = 64 });

            Assert.That(solved.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(unsolvable.Status, Is.EqualTo(PuzzleSolverStatus.Unsolvable));
            Assert.That(limited.Status, Is.EqualTo(PuzzleSolverStatus.SearchLimitReached));
            Assert.That(unsolvable.Status, Is.Not.EqualTo(limited.Status));
        }

        [Test]
        public void MaximumDepth_StopsSearchWithLimitStatus()
        {
            PuzzleSolverResult result = new PuzzleSolver().Solve(
                LoadLevel("Levels/M3_Test_02").CreateBoardState(),
                new PuzzleSolverOptions { MaximumExploredStates = 100, MaximumDepth = 1 });

            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.SearchLimitReached));
        }

        [TestCase("Levels/M1_Test_01")]
        [TestCase("Levels/M1_Test_02")]
        [TestCase("Levels/M1_Test_03")]
        [TestCase("Levels/M2_Test_01")]
        [TestCase("Levels/M2_Test_02")]
        [TestCase("Levels/M2_Test_03")]
        public void Solver_HandlesAcceptedComponentLevelAssets(string resourcePath)
        {
            PuzzleSolverResult result = new PuzzleSolver().Solve(LoadLevel(resourcePath).CreateBoardState());
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
        }

        private static BoardState CreateSwitchAndRotationBoard()
        {
            return new BoardState(4, 1, new[]
            {
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.StraightWire, 0, true),
                Tile(2, 0, TileType.Switch, 0, false, false),
                Tile(3, 0, TileType.OutputLamp, 0, false)
            });
        }

        [TestCase("S_09", false, 88579, 7,
            "ToggleSwitch (1, 1)|RotateClockwise (2, 1)|RotateClockwise (2, 1)|RotateClockwise (2, 1)|RotateClockwise (3, 3)|RotateClockwise (3, 3)|RotateClockwise (3, 3)")]
        [TestCase("S_10", false, 15895, 6,
            "ToggleSwitch (1, 1)|RotateClockwise (2, 1)|RotateClockwise (2, 1)|RotateClockwise (3, 1)|RotateClockwise (3, 1)|ToggleSwitch (2, 2)")]
        [TestCase("S_10", true, 131295, 9,
            "RotateClockwise (3, 0)|RotateClockwise (3, 0)|RotateClockwise (3, 0)|ToggleSwitch (1, 1)|RotateClockwise (2, 1)|RotateClockwise (2, 1)|RotateClockwise (3, 1)|RotateClockwise (3, 1)|ToggleSwitch (2, 2)")]
        public void OptimizedSearch_PreservesMeasuredBaselineGraphAndSequence(string asset,
            bool scramble, int states, int depth, string sequence)
        {
            BoardState board = LoadLevel("Levels/Substation/" + asset).CreateBoardState();
            if (scramble) board.TryApplyAction(board.GetValidActions()[0]);
            PuzzleSolverResult result = new PuzzleSolver().Solve(board, PuzzleSolverProfiles.RuntimeHint);
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(result.MinimumMoveCount, Is.EqualTo(depth));
            Assert.That(result.DeepestSearchDepth, Is.EqualTo(depth));
            Assert.That(result.ExploredStateCount, Is.EqualTo(states));
            Assert.That(string.Join("|", result.Solution), Is.EqualTo(sequence));
        }

        [Test]
        public void LazySearchAndFastCopy_MatchEagerSimulationAcrossProductionAndComponentStates()
        {
            var fixtures = Resources.LoadAll<LevelDefinition>("Levels")
                .Select(level => (name: level.name, board: level.CreateBoardState())).ToList();
            fixtures.Add(("Powered cycle with junctions, locked tiles and two lamps", new BoardState(3, 3, new[]
            {
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.TJunction, 0, true),
                Tile(2, 0, TileType.CornerWire, 3, true),
                Tile(2, 1, TileType.CornerWire, 2, true),
                Tile(1, 1, TileType.CrossJunction, 0, false),
                Tile(0, 1, TileType.OutputLamp, 2, false),
                Tile(1, 2, TileType.OutputLamp, 3, false)
            })));
            foreach (var fixture in fixtures)
            {
                BoardState reference = fixture.board;
                var propagation = new PowerPropagationService();
                propagation.Recalculate(reference);
                PuzzleSearchState search = PuzzleSearchState.FromBoard(reference);
                var random = new System.Random(743);
                for (int step = 0; step < 80; step++)
                {
                    Assert.That(search.IsSolved, Is.EqualTo(CircuitCompletion.IsCompleted(reference)), fixture.name);
                    foreach (CircuitTileState expected in reference.AllTiles())
                    {
                        CircuitTileState actual = search.Board.GetTile(expected.Position);
                        Assert.That(actual.Rotation, Is.EqualTo(expected.Rotation));
                        Assert.That(actual.IsSwitchOn, Is.EqualTo(expected.IsSwitchOn));
                        Assert.That(actual.IsPowered, Is.EqualTo(expected.IsPowered));
                        Assert.That(actual.EnergizedInputSides, Is.EqualTo(expected.EnergizedInputSides));
                        Assert.That(actual.ActiveOutputSides, Is.EqualTo(expected.ActiveOutputSides));
                        Assert.That(actual.PropagatedOutputSides, Is.EqualTo(expected.PropagatedOutputSides));
                    }
                    string expectedKey = string.Concat(reference.AllTiles()
                        .Where(tile => tile.TileType == TileType.Switch || tile.IsRotatable)
                        .Select(tile => tile.TileType == TileType.Switch
                            ? (tile.IsSwitchOn ? "1" : "0") : tile.Rotation.ToString()));
                    Assert.That(search.Key.ToString(), Is.EqualTo(expectedKey));
                    IReadOnlyList<PuzzleAction> actions = reference.GetValidActions();
                    Assert.That(search.GetValidActions(), Is.EqualTo(actions));
                    if (actions.Count == 0) break;
                    PuzzleAction action = actions[random.Next(actions.Count)];
                    PuzzleStateKey previousKey = search.Key;
                    PuzzleSearchState next = search.CreateIndependentCopy();
                    Assert.That(next.ApplyAction(action), Is.True);
                    Assert.That(search.Key, Is.EqualTo(previousKey), "Copy must not mutate its parent.");
                    // Exercise the old definition-based copy as a reference oracle.
                    reference = new BoardState(reference.Width, reference.Height,
                        reference.AllTiles().Select(tile => new TileDefinition(tile.Position,
                            tile.TileType, tile.Rotation, tile.IsRotatable, tile.IsSwitchOn)));
                    reference.TryApplyAction(action);
                    propagation.Recalculate(reference);
                    search = next;
                }
            }
        }

        [Test]
        public void FastBoardCopy_PreservesPersistentFieldsAndResetsTransientFields()
        {
            BoardState source = CreateSwitchAndRotationBoard();
            source.TryApplyAction(source.GetValidActions()[0]);
            new PowerPropagationService().Recalculate(source);
            BoardState copy = source.CreateIndependentCopy();
            foreach (CircuitTileState tile in source.AllTiles())
            {
                CircuitTileState cloned = copy.GetTile(tile.Position);
                Assert.That(cloned, Is.Not.SameAs(tile));
                Assert.That(cloned.Position, Is.EqualTo(tile.Position));
                Assert.That(cloned.TileType, Is.EqualTo(tile.TileType));
                Assert.That(cloned.IsRotatable, Is.EqualTo(tile.IsRotatable));
                Assert.That(cloned.Rotation, Is.EqualTo(tile.Rotation));
                Assert.That(cloned.IsSwitchOn, Is.EqualTo(tile.IsSwitchOn));
                Assert.That(cloned.IsPowered, Is.False);
                Assert.That(cloned.EnergizedInputSides, Is.EqualTo(CardinalDirection.None));
                Assert.That(cloned.ActiveOutputSides, Is.EqualTo(CardinalDirection.None));
                Assert.That(cloned.PropagatedOutputSides, Is.EqualTo(CardinalDirection.None));
            }
        }

        [Test]
        public void FastBoardCopy_ActionsAndPowerRemainIndependentInBothDirections()
        {
            BoardState original = CreateSwitchAndRotationBoard();
            BoardState copy = original.CreateIndependentCopy();
            var rotate = new PuzzleAction(new GridPosition(1, 0), PuzzleActionType.RotateClockwise);
            var toggle = new PuzzleAction(new GridPosition(2, 0), PuzzleActionType.ToggleSwitch);
            var propagation = new PowerPropagationService();
            Assert.That(copy.TryApplyAction(rotate), Is.True);
            Assert.That(copy.TryApplyAction(toggle), Is.True);
            propagation.Recalculate(copy);
            Assert.That(CircuitCompletion.IsCompleted(copy), Is.True);
            Assert.That(original.GetTile(rotate.Position).Rotation, Is.Zero);
            Assert.That(original.GetTile(toggle.Position).IsSwitchOn, Is.False);
            Assert.That(original.AllTiles().All(tile => !tile.IsPowered), Is.True);

            propagation.Recalculate(original);
            Assert.That(CircuitCompletion.IsCompleted(original), Is.False);
            Assert.That(CircuitCompletion.IsCompleted(copy), Is.True);
            original.TryApplyAction(rotate);
            original.TryApplyAction(toggle);
            propagation.Recalculate(original);
            copy.TryApplyAction(toggle);
            propagation.Recalculate(copy);
            Assert.That(CircuitCompletion.IsCompleted(original), Is.True);
            Assert.That(CircuitCompletion.IsCompleted(copy), Is.False);
            Assert.That(original.GetTile(toggle.Position).IsSwitchOn, Is.True);
        }

        [Test]
        public void DirtySearchCopies_EvaluateIndependentlyWithoutChangingCanonicalIdentity()
        {
            PuzzleSearchState parent = PuzzleSearchState.FromBoard(CreateSwitchAndRotationBoard());
            var rotate = new PuzzleAction(new GridPosition(1, 0), PuzzleActionType.RotateClockwise);
            var toggle = new PuzzleAction(new GridPosition(2, 0), PuzzleActionType.ToggleSwitch);
            parent.ApplyAction(rotate);
            parent.ApplyAction(toggle);
            Assert.That(parent.IsSolved, Is.True);
            parent.ApplyAction(toggle); // Dirty, with the old solved electrical cache still present.
            PuzzleSearchState child = parent.CreateIndependentCopy();
            PuzzleStateKey dirtyKey = child.Key;
            Assert.That(child.Key, Is.EqualTo(parent.Key));
            child.ApplyAction(toggle);
            Assert.That(child.IsSolved, Is.True);
            Assert.That(parent.IsSolved, Is.False);
            Assert.That(parent.Key, Is.EqualTo(dirtyKey));
            parent.ApplyAction(rotate);
            PuzzleSearchState grandchild = parent.CreateIndependentCopy();
            Assert.That(grandchild.IsSolved, Is.False);
            Assert.That(grandchild.Key, Is.EqualTo(parent.Key));
            Assert.That(child.IsSolved, Is.True);
            Assert.That(child.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.True);
            Assert.That(parent.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.False);
        }

        [UnityTest]
        public IEnumerator IndependentConcurrentSolves_MatchSequentialResults()
        {
            // Load Unity assets before workers start; workers receive only independent C# boards.
            BoardState[] boards = { LoadLevel("Levels/Substation/S_09").CreateBoardState(),
                LoadLevel("Levels/Substation/S_10").CreateBoardState() };
            PuzzleSolverResult[] expected = boards.Select(board =>
                new PuzzleSolver().Solve(board, PuzzleSolverProfiles.RuntimeHint)).ToArray();
            var results = new ConcurrentQueue<(int index, PuzzleSolverResult result)>();
            var errors = new ConcurrentQueue<System.Exception>();
            Task[] tasks = boards.Select((board, index) => Task.Run(() =>
            {
                try { results.Enqueue((index, new PuzzleSolver().Solve(board, PuzzleSolverProfiles.RuntimeHint))); }
                catch (System.Exception error) { errors.Enqueue(error); }
            })).ToArray();
            while (tasks.Any(task => !task.IsCompleted)) yield return null;
            Assert.That(errors, Is.Empty);
            Assert.That(results.Count, Is.EqualTo(boards.Length));
            while (results.TryDequeue(out var completed))
            {
                PuzzleSolverResult reference = expected[completed.index];
                Assert.That(completed.result.Status, Is.EqualTo(reference.Status));
                Assert.That(completed.result.MinimumMoveCount, Is.EqualTo(reference.MinimumMoveCount));
                Assert.That(completed.result.ExploredStateCount, Is.EqualTo(reference.ExploredStateCount));
                Assert.That(completed.result.DeepestSearchDepth, Is.EqualTo(reference.DeepestSearchDepth));
                Assert.That(completed.result.Solution, Is.EqualTo(reference.Solution));
            }
        }

        private static LevelDefinition LoadLevel(string resourcePath)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(resourcePath);
            Assert.That(level, Is.Not.Null, $"Missing level data at {resourcePath}.");
            return level;
        }

        private static TileDefinition Tile(int x, int y, TileType type, int rotation, bool rotatable,
            bool startingSwitchOn = false)
        {
            return new TileDefinition(new GridPosition(x, y), type, rotation, rotatable, startingSwitchOn);
        }
    }
}

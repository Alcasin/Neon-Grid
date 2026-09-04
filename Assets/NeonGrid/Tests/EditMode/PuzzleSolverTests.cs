using System.Collections.Generic;
using System.Linq;
using NeonGrid.Data;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class PuzzleSolverTests
    {
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
        public void UnsolvableAndSearchLimitReached_AreDistinct()
        {
            PuzzleSolverResult unsolvable = new PuzzleSolver().Solve(
                LoadLevel("Levels/M3_Test_04").CreateBoardState());
            PuzzleSolverResult limited = new PuzzleSolver().Solve(
                LoadLevel("Levels/M3_Test_05").CreateBoardState(),
                new PuzzleSolverOptions { MaximumExploredStates = 1, MaximumDepth = 64 });

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

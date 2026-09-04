using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class CircuitSimulationTests
    {
        [Test]
        public void Rotation_IsExactlyOneClockwiseQuarterTurn()
        {
            CircuitSimulation simulation = CreateSimulation(1, 1,
                Tile(0, 0, TileType.CornerWire, 0, true));

            Assert.That(simulation.Board.GetTile(new GridPosition(0, 0)).Connections,
                Is.EqualTo(CardinalDirection.Up | CardinalDirection.Right));

            simulation.RotateTileClockwise(new GridPosition(0, 0));

            CircuitTileState tile = simulation.Board.GetTile(new GridPosition(0, 0));
            Assert.That(tile.Rotation, Is.EqualTo(1));
            Assert.That(tile.Connections, Is.EqualTo(CardinalDirection.Right | CardinalDirection.Down));
        }

        [Test]
        public void FourClockwiseRotations_ReturnToOriginalLogicalOrientation()
        {
            CircuitSimulation simulation = CreateSimulation(1, 1,
                Tile(0, 0, TileType.CornerWire, 0, true));
            CircuitTileState tile = simulation.Board.GetTile(new GridPosition(0, 0));
            CardinalDirection originalConnections = tile.Connections;

            for (int i = 0; i < 4; i++)
                simulation.RotateTileClockwise(tile.Position);

            Assert.That(tile.Rotation, Is.Zero);
            Assert.That(tile.Connections, Is.EqualTo(originalConnections));
        }

        [Test]
        public void TJunction_RotationsProduceExpectedConnections()
        {
            CircuitSimulation simulation = CreateSimulation(1, 1,
                Tile(0, 0, TileType.TJunction, 0, true));
            CircuitTileState tile = simulation.Board.GetTile(new GridPosition(0, 0));

            Assert.That(tile.Connections, Is.EqualTo(
                CardinalDirection.Up | CardinalDirection.Right | CardinalDirection.Left));
            simulation.RotateTileClockwise(tile.Position);
            Assert.That(tile.Connections, Is.EqualTo(
                CardinalDirection.Up | CardinalDirection.Right | CardinalDirection.Down));
            simulation.RotateTileClockwise(tile.Position);
            Assert.That(tile.Connections, Is.EqualTo(
                CardinalDirection.Right | CardinalDirection.Down | CardinalDirection.Left));
            simulation.RotateTileClockwise(tile.Position);
            Assert.That(tile.Connections, Is.EqualTo(
                CardinalDirection.Up | CardinalDirection.Down | CardinalDirection.Left));
        }

        [Test]
        public void FourTJunctionRotations_ReturnToOriginalLogicalOrientation()
        {
            CircuitSimulation simulation = CreateSimulation(1, 1,
                Tile(0, 0, TileType.TJunction, 0, true));
            CircuitTileState tile = simulation.Board.GetTile(new GridPosition(0, 0));
            CardinalDirection originalConnections = tile.Connections;

            for (int i = 0; i < 4; i++)
                simulation.RotateTileClockwise(tile.Position);

            Assert.That(tile.Rotation, Is.Zero);
            Assert.That(tile.Connections, Is.EqualTo(originalConnections));
        }

        [Test]
        public void CrossJunction_ExposesAllFourDirections()
        {
            CircuitSimulation simulation = CreateSimulation(1, 1,
                Tile(0, 0, TileType.CrossJunction, 0, false));

            Assert.That(simulation.Board.GetTile(new GridPosition(0, 0)).Connections, Is.EqualTo(
                CardinalDirection.Up | CardinalDirection.Right |
                CardinalDirection.Down | CardinalDirection.Left));
        }

        [Test]
        public void Power_RequiresMutualOppositeConnections()
        {
            CircuitSimulation simulation = CreateSimulation(2, 1,
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.StraightWire, 0, false));

            Assert.That(simulation.Board.GetTile(new GridPosition(0, 0)).IsPowered, Is.True);
            Assert.That(simulation.Board.GetTile(new GridPosition(1, 0)).IsPowered, Is.False,
                "The source points right, but the neighbour does not expose left.");
        }

        [Test]
        public void BreakingConnection_RemovesPreviouslyPropagatedPower()
        {
            CircuitSimulation simulation = CreateSimulation(4, 1,
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.StraightWire, 1, true),
                Tile(2, 0, TileType.StraightWire, 1, false),
                Tile(3, 0, TileType.OutputLamp, 0, false));

            Assert.That(simulation.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.True);
            Assert.That(simulation.IsLevelCompleted, Is.True);

            simulation.RotateTileClockwise(new GridPosition(1, 0));

            Assert.That(simulation.Board.GetTile(new GridPosition(1, 0)).IsPowered, Is.False);
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 0)).IsPowered, Is.False);
            Assert.That(simulation.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.False);
            Assert.That(simulation.IsLevelCompleted, Is.False);
        }

        [Test]
        public void PassiveCycle_PropagatesWithoutRevisitingForever()
        {
            var board = new BoardState(2, 2, new[]
            {
                Tile(0, 0, TileType.CornerWire, 0, false),
                Tile(1, 0, TileType.CornerWire, 3, false),
                Tile(1, 1, TileType.CornerWire, 2, false),
                Tile(0, 1, TileType.CornerWire, 1, false)
            });
            CircuitTileState seed = board.GetTile(new GridPosition(0, 0));
            new PowerPropagationService().RecalculateFromSeeds(board, new[] { seed });

            foreach (CircuitTileState tile in board.AllTiles())
                Assert.That(tile.IsPowered, Is.True, $"Expected cycle tile {tile.Position} to be reached.");
        }

        [Test]
        public void SolvedUnsolvedSolved_FiresOncePerTransitionIntoSolved()
        {
            CircuitSimulation simulation = CreateSimulation(3, 2,
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.StraightWire, 0, true),
                Tile(2, 0, TileType.OutputLamp, 0, false),
                Tile(0, 1, TileType.CornerWire, 0, true));
            int completedEvents = 0;
            simulation.LevelCompleted += () => completedEvents++;

            simulation.RotateTileClockwise(new GridPosition(1, 0));
            Assert.That(simulation.IsLevelCompleted, Is.True);
            Assert.That(completedEvents, Is.EqualTo(1));

            simulation.RotateTileClockwise(new GridPosition(0, 1));
            Assert.That(simulation.IsLevelCompleted, Is.True);
            Assert.That(completedEvents, Is.EqualTo(1), "Remaining solved must not emit another event.");

            simulation.RotateTileClockwise(new GridPosition(1, 0));
            Assert.That(simulation.IsLevelCompleted, Is.False);

            simulation.RotateTileClockwise(new GridPosition(1, 0));
            Assert.That(simulation.IsLevelCompleted, Is.True);
            Assert.That(completedEvents, Is.EqualTo(2));
        }

        [Test]
        public void TestCircuit_StartsUnsolved_AndCompletesAfterRequiredRotations()
        {
            CircuitSimulation simulation = CreateSimulation(4, 4,
                Tile(0, 1, TileType.PowerSource, 0, false),
                Tile(1, 1, TileType.StraightWire, 0, true),
                Tile(2, 1, TileType.CornerWire, 0, true),
                Tile(2, 2, TileType.OutputLamp, 3, false));
            int completedEvents = 0;
            simulation.LevelCompleted += () => completedEvents++;

            Assert.That(simulation.IsLevelCompleted, Is.False);
            simulation.RotateTileClockwise(new GridPosition(1, 1));
            simulation.RotateTileClockwise(new GridPosition(2, 1));
            simulation.RotateTileClockwise(new GridPosition(2, 1));
            simulation.RotateTileClockwise(new GridPosition(2, 1));

            Assert.That(simulation.IsLevelCompleted, Is.True);
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 2)).IsPowered, Is.True);
            Assert.That(completedEvents, Is.EqualTo(1));
        }

        [Test]
        public void NonRotatableTile_IgnoresRotationRequest()
        {
            CircuitSimulation simulation = CreateSimulation(1, 1,
                Tile(0, 0, TileType.PowerSource, 0, false));

            Assert.That(simulation.RotateTileClockwise(new GridPosition(0, 0)), Is.False);
            Assert.That(simulation.Board.GetTile(new GridPosition(0, 0)).Rotation, Is.Zero);
        }

        [Test]
        public void NonRotatableWire_StillConductsPowerNormally()
        {
            CircuitSimulation simulation = CreateSimulation(3, 1,
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.StraightWire, 1, false),
                Tile(2, 0, TileType.OutputLamp, 0, false));

            Assert.That(simulation.Board.GetTile(new GridPosition(1, 0)).IsPowered, Is.True);
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 0)).IsPowered, Is.True);
            Assert.That(simulation.IsLevelCompleted, Is.True);
            Assert.That(simulation.RotateTileClockwise(new GridPosition(1, 0)), Is.False);
        }

        [Test]
        public void Diode_PassesPowerFromInputToOutput()
        {
            CircuitSimulation simulation = CreateSimulation(3, 1,
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.Diode, 0, false),
                Tile(2, 0, TileType.OutputLamp, 0, false));

            Assert.That(simulation.Board.GetTile(new GridPosition(1, 0)).IsPowered, Is.True);
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 0)).IsPowered, Is.True);
            Assert.That(simulation.IsLevelCompleted, Is.True);
        }

        [Test]
        public void Diode_RejectsPowerFromOutputToInput()
        {
            CircuitSimulation simulation = CreateSimulation(3, 1,
                Tile(0, 0, TileType.OutputLamp, 2, false),
                Tile(1, 0, TileType.Diode, 0, false),
                Tile(2, 0, TileType.PowerSource, 2, false));

            Assert.That(simulation.Board.GetTile(new GridPosition(1, 0)).IsPowered, Is.False);
            Assert.That(simulation.Board.GetTile(new GridPosition(0, 0)).IsPowered, Is.False);
            Assert.That(simulation.IsLevelCompleted, Is.False);
        }

        [Test]
        public void RotatingDiode_ChangesItsInputAndOutputDirections()
        {
            CircuitSimulation simulation = CreateSimulation(3, 1,
                Tile(0, 0, TileType.OutputLamp, 2, false),
                Tile(1, 0, TileType.Diode, 0, true),
                Tile(2, 0, TileType.PowerSource, 2, false));
            CircuitTileState diode = simulation.Board.GetTile(new GridPosition(1, 0));

            simulation.RotateTileClockwise(diode.Position);
            simulation.RotateTileClockwise(diode.Position);

            Assert.That(TilePowerFlow.GetInputSides(diode.TileType, diode.Rotation),
                Is.EqualTo(CardinalDirection.Right));
            Assert.That(TilePowerFlow.GetOutputSides(diode.TileType, diode.Rotation),
                Is.EqualTo(CardinalDirection.Left));
            Assert.That(diode.IsPowered, Is.True);
            Assert.That(simulation.IsLevelCompleted, Is.True);
        }

        [Test]
        public void MultipleRequiredLamps_AllMustRemainPoweredForCompletion()
        {
            CircuitSimulation simulation = CreateSimulation(3, 2,
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.TJunction, 0, true),
                Tile(2, 0, TileType.OutputLamp, 0, false),
                Tile(1, 1, TileType.OutputLamp, 3, false));

            Assert.That(simulation.IsLevelCompleted, Is.True);
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 0)).IsPowered, Is.True);
            Assert.That(simulation.Board.GetTile(new GridPosition(1, 1)).IsPowered, Is.True);

            simulation.RotateTileClockwise(new GridPosition(1, 0));
            simulation.RotateTileClockwise(new GridPosition(1, 0));
            simulation.RotateTileClockwise(new GridPosition(1, 0));

            Assert.That(simulation.Board.GetTile(new GridPosition(1, 1)).IsPowered, Is.True);
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 0)).IsPowered, Is.False);
            Assert.That(simulation.IsLevelCompleted, Is.False);
        }

        [Test]
        public void MultipleSources_SeedPropagationIndependently()
        {
            CircuitSimulation simulation = CreateSimulation(4, 1,
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.OutputLamp, 0, false),
                Tile(2, 0, TileType.PowerSource, 0, false),
                Tile(3, 0, TileType.OutputLamp, 0, false));

            Assert.That(simulation.Board.GetTile(new GridPosition(1, 0)).IsPowered, Is.True);
            Assert.That(simulation.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.True);
            Assert.That(simulation.IsLevelCompleted, Is.True);
        }

        [Test]
        public void GeneratedLevelAsset_BuildsA4x4UnsolvedBoard()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/TestLevel4x4");

            Assert.That(level, Is.Not.Null);
            Assert.That(level.Width, Is.EqualTo(4));
            Assert.That(level.Height, Is.EqualTo(4));
            Assert.That(level.Tiles, Has.Count.EqualTo(16));
            Assert.That(new CircuitSimulation(level.CreateBoardState()).IsLevelCompleted, Is.False);
        }

        [Test]
        public void MilestoneOneLevelAssets_LoadUnsolvedAndAreManuallySolvable()
        {
            AssertLevelSolves("Levels/M1_Test_01", new GridPosition(2, 1), 3);
            AssertLevelSolves("Levels/M1_Test_02", new GridPosition(1, 0), 3);
            AssertLevelSolves("Levels/M1_Test_03", new GridPosition(1, 1), 2);
        }

        private static void AssertLevelSolves(string resourcePath, GridPosition position, int rotations)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(resourcePath);
            Assert.That(level, Is.Not.Null, $"Missing level data at {resourcePath}.");
            var simulation = new CircuitSimulation(level.CreateBoardState());
            Assert.That(simulation.IsLevelCompleted, Is.False, $"{resourcePath} must start unsolved.");

            for (int i = 0; i < rotations; i++)
                Assert.That(simulation.RotateTileClockwise(position), Is.True);

            Assert.That(simulation.IsLevelCompleted, Is.True, $"{resourcePath} should be manually solvable.");
        }

        private static CircuitSimulation CreateSimulation(int width, int height, params TileDefinition[] tiles)
        {
            return new CircuitSimulation(new BoardState(width, height, new List<TileDefinition>(tiles)));
        }

        private static TileDefinition Tile(int x, int y, TileType type, int rotation, bool rotatable)
        {
            return new TileDefinition(new GridPosition(x, y), type, rotation, rotatable);
        }
    }
}

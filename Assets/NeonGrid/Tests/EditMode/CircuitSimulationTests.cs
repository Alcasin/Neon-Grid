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
            seed.IsPowered = true;
            var frontier = new Queue<CircuitTileState>();
            frontier.Enqueue(seed);

            new PowerPropagationService().PropagateFromSeededTiles(board, frontier);

            foreach (CircuitTileState tile in board.AllTiles())
                Assert.That(tile.IsPowered, Is.True, $"Expected cycle tile {tile.Position} to be reached.");
            Assert.That(frontier, Is.Empty);
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

using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class StatefulComponentTests
    {
        [Test]
        public void OffSwitch_BlocksPower()
        {
            CircuitSimulation simulation = CreateSwitchLine(false);

            Assert.That(simulation.Board.GetTile(new GridPosition(1, 0)).IsSwitchOn, Is.False);
            Assert.That(simulation.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.False);
            Assert.That(simulation.IsLevelCompleted, Is.False);
        }

        [Test]
        public void OnSwitch_ConductsPower()
        {
            CircuitSimulation simulation = CreateSwitchLine(true);

            Assert.That(simulation.Board.GetTile(new GridPosition(1, 0)).IsSwitchOn, Is.True);
            Assert.That(simulation.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.True);
            Assert.That(simulation.IsLevelCompleted, Is.True);
        }

        [Test]
        public void TogglingPoweredSwitchOff_RemovesAllDownstreamPower()
        {
            CircuitSimulation simulation = CreateSwitchLine(true);

            Assert.That(simulation.InteractWithTile(new GridPosition(1, 0)), Is.True);

            Assert.That(simulation.Board.GetTile(new GridPosition(1, 0)).IsSwitchOn, Is.False);
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 0)).IsPowered, Is.False);
            Assert.That(simulation.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.False);
            Assert.That(simulation.IsLevelCompleted, Is.False);
        }

        [Test]
        public void SwitchInteraction_TogglesStateInsteadOfRotating()
        {
            CircuitSimulation simulation = CreateSimulation(1, 1,
                Tile(0, 0, TileType.Switch, 1, true, false));
            CircuitTileState switchTile = simulation.Board.GetTile(new GridPosition(0, 0));

            Assert.That(simulation.InteractWithTile(switchTile.Position), Is.True);

            Assert.That(switchTile.IsSwitchOn, Is.True);
            Assert.That(switchTile.Rotation, Is.EqualTo(1));
            Assert.That(switchTile.IsRotatable, Is.False);
            Assert.That(simulation.RotateTileClockwise(switchTile.Position), Is.False);
        }

        [Test]
        public void SwitchStartingState_LoadsFromTileDefinition()
        {
            CircuitTileState initiallyOn = CreateSimulation(1, 1,
                Tile(0, 0, TileType.Switch, 0, false, true)).Board.GetTile(new GridPosition(0, 0));
            CircuitTileState initiallyOff = CreateSimulation(1, 1,
                Tile(0, 0, TileType.Switch, 0, false, false)).Board.GetTile(new GridPosition(0, 0));

            Assert.That(initiallyOn.IsSwitchOn, Is.True);
            Assert.That(initiallyOff.IsSwitchOn, Is.False);
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        public void AndGate_ImplementsTruthTable(bool inputA, bool inputB, bool expectedOutput)
        {
            CircuitSimulation simulation = CreateGateTruthTable(TileType.AndGate, inputA, inputB);
            CircuitTileState gate = simulation.Board.GetTile(new GridPosition(1, 0));

            Assert.That(gate.IsPowered, Is.EqualTo(inputA || inputB));
            Assert.That(gate.ActiveOutputSides != CardinalDirection.None, Is.EqualTo(expectedOutput));
            Assert.That(simulation.Board.GetTile(new GridPosition(1, 1)).IsPowered, Is.EqualTo(expectedOutput));
            Assert.That(simulation.IsLevelCompleted, Is.EqualTo(expectedOutput));
        }

        [Test]
        public void AndGate_ResultIsIndependentOfSourceDiscoveryOrder()
        {
            BoardState board = CreateGateBoard(TileType.AndGate, true, true);
            CircuitTileState leftSource = board.GetTile(new GridPosition(0, 0));
            CircuitTileState rightSource = board.GetTile(new GridPosition(2, 0));
            CircuitTileState gate = board.GetTile(new GridPosition(1, 0));
            CircuitTileState lamp = board.GetTile(new GridPosition(1, 1));
            var propagation = new PowerPropagationService();

            propagation.RecalculateFromSeeds(board, new[] { leftSource, rightSource });
            CardinalDirection firstInputs = gate.EnergizedInputSides;
            CardinalDirection firstOutputs = gate.ActiveOutputSides;
            bool firstLampState = lamp.IsPowered;

            propagation.RecalculateFromSeeds(board, new[] { rightSource, leftSource });

            Assert.That(gate.EnergizedInputSides, Is.EqualTo(firstInputs));
            Assert.That(gate.ActiveOutputSides, Is.EqualTo(firstOutputs));
            Assert.That(lamp.IsPowered, Is.EqualTo(firstLampState));
            Assert.That(lamp.IsPowered, Is.True);
        }

        [TestCase(1)]
        [TestCase(3)]
        public void AndGate_LosingEitherInputDeactivatesOutput(int switchX)
        {
            CircuitSimulation simulation = CreateSwitchedGate(TileType.AndGate, true, true);
            Assert.That(simulation.IsLevelCompleted, Is.True);

            simulation.InteractWithTile(new GridPosition(switchX, 0));

            CircuitTileState gate = simulation.Board.GetTile(new GridPosition(2, 0));
            Assert.That(gate.ActiveOutputSides, Is.EqualTo(CardinalDirection.None));
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 1)).IsPowered, Is.False);
            Assert.That(simulation.IsLevelCompleted, Is.False);
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public void OrGate_ImplementsTruthTable(bool inputA, bool inputB, bool expectedOutput)
        {
            CircuitSimulation simulation = CreateGateTruthTable(TileType.OrGate, inputA, inputB);
            CircuitTileState gate = simulation.Board.GetTile(new GridPosition(1, 0));

            Assert.That(gate.IsPowered, Is.EqualTo(inputA || inputB));
            Assert.That(gate.ActiveOutputSides != CardinalDirection.None, Is.EqualTo(expectedOutput));
            Assert.That(simulation.Board.GetTile(new GridPosition(1, 1)).IsPowered, Is.EqualTo(expectedOutput));
            Assert.That(simulation.IsLevelCompleted, Is.EqualTo(expectedOutput));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void OrGate_LosingOneInputKeepsOutputActive(int switchX)
        {
            CircuitSimulation simulation = CreateSwitchedGate(TileType.OrGate, true, true);
            Assert.That(simulation.IsLevelCompleted, Is.True);

            simulation.InteractWithTile(new GridPosition(switchX, 0));

            CircuitTileState gate = simulation.Board.GetTile(new GridPosition(2, 0));
            Assert.That(gate.ActiveOutputSides, Is.EqualTo(CardinalDirection.Up));
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 1)).IsPowered, Is.True);
            Assert.That(simulation.IsLevelCompleted, Is.True);
        }

        [TestCase(TileType.AndGate)]
        [TestCase(TileType.OrGate)]
        public void GateOutputSide_RejectsBackfeedIntoInputs(TileType gateType)
        {
            CircuitSimulation simulation = CreateSimulation(3, 2,
                Tile(0, 0, TileType.OutputLamp, 2, false),
                Tile(1, 0, gateType, 0, false),
                Tile(2, 0, TileType.OutputLamp, 0, false),
                Tile(1, 1, TileType.PowerSource, 1, false));
            CircuitTileState gate = simulation.Board.GetTile(new GridPosition(1, 0));

            Assert.That(gate.EnergizedInputSides, Is.EqualTo(CardinalDirection.None));
            Assert.That(gate.ActiveOutputSides, Is.EqualTo(CardinalDirection.None));
            Assert.That(simulation.Board.GetTile(new GridPosition(0, 0)).IsPowered, Is.False);
            Assert.That(simulation.Board.GetTile(new GridPosition(2, 0)).IsPowered, Is.False);
        }

        [Test]
        public void GateRotation_RotatesInputAndOutputRolesTogether()
        {
            CircuitTileState gate = CreateSimulation(1, 1,
                Tile(0, 0, TileType.AndGate, 1, false)).Board.GetTile(new GridPosition(0, 0));

            Assert.That(TilePowerFlow.GetInputSides(gate.TileType, gate.Rotation),
                Is.EqualTo(CardinalDirection.Up | CardinalDirection.Down));
            Assert.That(TilePowerFlow.GetOutputSides(gate.TileType, gate.Rotation),
                Is.EqualTo(CardinalDirection.Right));
            Assert.That(gate.Connections,
                Is.EqualTo(CardinalDirection.Up | CardinalDirection.Right | CardinalDirection.Down));
        }

        [Test]
        public void MilestoneTwoLevelAssets_LoadAndBehaveAsDocumented()
        {
            VerifySwitchLevel();
            VerifyAndLevel();
            VerifyOrLevel();
        }

        private static void VerifySwitchLevel()
        {
            CircuitSimulation simulation = LoadLevel("Levels/M2_Test_01");
            GridPosition switchPosition = new GridPosition(1, 0);
            Assert.That(simulation.Board.GetTile(switchPosition).IsSwitchOn, Is.False);
            Assert.That(simulation.IsLevelCompleted, Is.False);
            simulation.InteractWithTile(switchPosition);
            Assert.That(simulation.IsLevelCompleted, Is.True);
            simulation.InteractWithTile(switchPosition);
            Assert.That(simulation.IsLevelCompleted, Is.False);
        }

        private static void VerifyAndLevel()
        {
            CircuitSimulation simulation = LoadLevel("Levels/M2_Test_02");
            Assert.That(simulation.IsLevelCompleted, Is.False);
            simulation.InteractWithTile(new GridPosition(1, 0));
            Assert.That(simulation.IsLevelCompleted, Is.False);
            simulation.InteractWithTile(new GridPosition(1, 0));
            simulation.InteractWithTile(new GridPosition(3, 0));
            Assert.That(simulation.IsLevelCompleted, Is.False);
            simulation.InteractWithTile(new GridPosition(1, 0));
            Assert.That(simulation.IsLevelCompleted, Is.True);
        }

        private static void VerifyOrLevel()
        {
            CircuitSimulation simulation = LoadLevel("Levels/M2_Test_03");
            Assert.That(simulation.IsLevelCompleted, Is.False);
            simulation.InteractWithTile(new GridPosition(1, 0));
            Assert.That(simulation.IsLevelCompleted, Is.True);
            simulation.InteractWithTile(new GridPosition(1, 0));
            Assert.That(simulation.IsLevelCompleted, Is.False);
            simulation.InteractWithTile(new GridPosition(3, 0));
            Assert.That(simulation.IsLevelCompleted, Is.True);
            simulation.InteractWithTile(new GridPosition(1, 0));
            simulation.InteractWithTile(new GridPosition(3, 0));
            Assert.That(simulation.IsLevelCompleted, Is.True);
        }

        private static CircuitSimulation LoadLevel(string resourcePath)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(resourcePath);
            Assert.That(level, Is.Not.Null, $"Missing level data at {resourcePath}.");
            return new CircuitSimulation(level.CreateBoardState());
        }

        private static CircuitSimulation CreateSwitchLine(bool startingOn)
        {
            return CreateSimulation(4, 1,
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.Switch, 0, false, startingOn),
                Tile(2, 0, TileType.StraightWire, 1, false),
                Tile(3, 0, TileType.OutputLamp, 0, false));
        }

        private static CircuitSimulation CreateGateTruthTable(TileType gateType, bool inputA, bool inputB)
        {
            return new CircuitSimulation(CreateGateBoard(gateType, inputA, inputB));
        }

        private static BoardState CreateGateBoard(TileType gateType, bool inputA, bool inputB)
        {
            var tiles = new List<TileDefinition>
            {
                Tile(1, 0, gateType, 0, false),
                Tile(1, 1, TileType.OutputLamp, 3, false)
            };
            if (inputA) tiles.Add(Tile(0, 0, TileType.PowerSource, 0, false));
            if (inputB) tiles.Add(Tile(2, 0, TileType.PowerSource, 2, false));
            return new BoardState(3, 2, tiles);
        }

        private static CircuitSimulation CreateSwitchedGate(TileType gateType, bool leftOn, bool rightOn)
        {
            return CreateSimulation(5, 2,
                Tile(0, 0, TileType.PowerSource, 0, false),
                Tile(1, 0, TileType.Switch, 0, false, leftOn),
                Tile(2, 0, gateType, 0, false),
                Tile(3, 0, TileType.Switch, 0, false, rightOn),
                Tile(4, 0, TileType.PowerSource, 2, false),
                Tile(2, 1, TileType.OutputLamp, 3, false));
        }

        private static CircuitSimulation CreateSimulation(int width, int height, params TileDefinition[] tiles)
        {
            return new CircuitSimulation(new BoardState(width, height, tiles));
        }

        private static TileDefinition Tile(int x, int y, TileType type, int rotation, bool rotatable,
            bool startingSwitchOn = false)
        {
            return new TileDefinition(new GridPosition(x, y), type, rotation, rotatable, startingSwitchOn);
        }
    }
}

using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using NeonGrid.Validation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class LevelValidatorTests
    {
        [TestCase(0, 1, LevelValidationCode.InvalidWidth)]
        [TestCase(1, 0, LevelValidationCode.InvalidHeight)]
        [TestCase(-1, -1, LevelValidationCode.InvalidWidth)]
        public void InvalidGridDimensions_AreRejected(int width, int height, LevelValidationCode expectedCode)
        {
            LevelValidationResult result = new LevelValidator().Validate(width, height,
                new List<TileDefinition>());

            Assert.That(result.IsValid, Is.False);
            AssertHasError(result, expectedCode);
        }

        [Test]
        public void OutOfBoundsTile_IsRejected()
        {
            LevelValidationResult result = new LevelValidator().Validate(2, 1, new[]
            {
                Tile(0, 0, TileType.PowerSource),
                Tile(2, 0, TileType.OutputLamp)
            });

            AssertHasError(result, LevelValidationCode.TileOutOfBounds);
        }

        [Test]
        public void DuplicatePosition_IsRejected()
        {
            LevelValidationResult result = new LevelValidator().Validate(2, 1, new[]
            {
                Tile(0, 0, TileType.PowerSource),
                Tile(0, 0, TileType.OutputLamp)
            });

            AssertHasError(result, LevelValidationCode.DuplicateTilePosition);
        }

        [Test]
        public void MissingSource_IsRejected()
        {
            LevelValidationResult result = new LevelValidator().Validate(1, 1, new[]
            {
                Tile(0, 0, TileType.OutputLamp)
            });

            AssertHasError(result, LevelValidationCode.MissingPowerSource);
        }

        [Test]
        public void MissingRequiredOutput_IsRejected()
        {
            LevelValidationResult result = new LevelValidator().Validate(1, 1, new[]
            {
                Tile(0, 0, TileType.PowerSource)
            });

            AssertHasError(result, LevelValidationCode.MissingOutputLamp);
        }

        [Test]
        public void InvalidSerializedConfiguration_IsReportedWithoutNormalization()
        {
            LevelValidationResult result = new LevelValidator().Validate(3, 1, new[]
            {
                Tile(0, 0, TileType.PowerSource),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 5, true, true),
                Tile(2, 0, TileType.OutputLamp)
            });

            AssertHasError(result, LevelValidationCode.InvalidRotation);
            AssertHasError(result, LevelValidationCode.SwitchStateOnNonSwitch);
        }

        [Test]
        public void UnknownTileType_IsRejected()
        {
            LevelValidationResult result = new LevelValidator().Validate(3, 1, new[]
            {
                Tile(0, 0, TileType.PowerSource),
                Tile(1, 0, (TileType)999),
                Tile(2, 0, TileType.OutputLamp)
            });

            AssertHasError(result, LevelValidationCode.InvalidTileType);
        }

        [Test]
        public void IncompleteTileRecordSet_IsRejected()
        {
            LevelValidationResult result = new LevelValidator().Validate(3, 1, new[]
            {
                Tile(0, 0, TileType.PowerSource),
                Tile(2, 0, TileType.OutputLamp)
            });

            AssertHasError(result, LevelValidationCode.UnexpectedTileRecordCount);
        }

        [Test]
        public void ValidLevel_PassesStructuralValidation()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_01");
            Assert.That(level, Is.Not.Null);

            LevelValidationResult result = new LevelValidator().Validate(level);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [TestCase("Levels/M3_Test_01", PuzzleSolverStatus.Solved)]
        [TestCase("Levels/M3_Test_04", PuzzleSolverStatus.Unsolvable)]
        public void SolverBackedValidation_PreservesConclusiveStatus(string resourcePath,
            PuzzleSolverStatus expectedStatus)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(resourcePath);
            LevelValidationReport report = new LevelValidator().ValidateWithSolver(level);

            Assert.That(report.StructuralValidation.IsValid, Is.True);
            Assert.That(report.SolverResult, Is.Not.Null);
            Assert.That(report.SolverResult.Status, Is.EqualTo(expectedStatus));
        }

        [Test]
        public void SolverBackedValidation_ReportsUnknownWhenBudgetIsExhausted()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_05");
            LevelValidationReport report = new LevelValidator().ValidateWithSolver(level,
                new PuzzleSolverOptions { MaximumExploredStates = 1, MaximumDepth = 64 });

            Assert.That(report.StructuralValidation.IsValid, Is.True);
            Assert.That(report.SolverResult.Status, Is.EqualTo(PuzzleSolverStatus.SearchLimitReached));
        }

        private static void AssertHasError(LevelValidationResult result, LevelValidationCode expectedCode)
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<LevelValidationIssue>(issue => issue.Code == expectedCode));
        }

        private static TileDefinition Tile(int x, int y, TileType type)
        {
            return new TileDefinition(new GridPosition(x, y), type, 0, false);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NeonGrid.Data;
using NeonGrid.Editor.Authoring;
using NeonGrid.Simulation;
using NeonGrid.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class LevelAuthoringTests
    {
        [Test]
        public void NewGrid_InitializesExactlyOneEmptyRecordPerCell()
        {
            var model = new LevelAuthoringModel(3, 2);

            IReadOnlyList<TileDefinition> snapshot = model.CreateSnapshot();

            Assert.That(snapshot.Count, Is.EqualTo(6));
            Assert.That(snapshot.All(tile => tile.tileType == TileType.Empty), Is.True);
            Assert.That(snapshot.Select(tile => tile.position).Distinct().Count(), Is.EqualTo(6));
        }

        [Test]
        public void LoadingAsset_ReproducesAllCellData()
        {
            LevelDefinition level = CreateLevel(4, 1,
                Tile(0, TileType.PowerSource, 2, true),
                Tile(1, TileType.CornerWire, 3, true),
                Tile(2, TileType.Switch, 0, false, true),
                Tile(3, TileType.OutputLamp, 1, false));
            try
            {
                var model = new LevelAuthoringModel();
                model.Load(level);

                Assert.That(model.Width, Is.EqualTo(4));
                Assert.That(model.Height, Is.EqualTo(1));
                AssertCell(model, 0, TileType.PowerSource, 2, true, false);
                AssertCell(model, 1, TileType.CornerWire, 3, true, false);
                AssertCell(model, 2, TileType.Switch, 0, false, true);
                AssertCell(model, 3, TileType.OutputLamp, 1, false, false);
                Assert.That(model.HasUnsavedChanges, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void Save_RoundTripsTypeRotationRotatableAndSwitchState()
        {
            LevelDefinition level = CreateLevel(4, 1,
                Tile(0, TileType.PowerSource),
                Tile(1, TileType.StraightWire, 0, true),
                Tile(2, TileType.Switch),
                Tile(3, TileType.OutputLamp));
            try
            {
                var model = new LevelAuthoringModel();
                model.Load(level);
                model.SetTileType(new GridPosition(1, 0), TileType.CornerWire);
                model.SetRotation(new GridPosition(1, 0), 2);
                model.SetRotatable(new GridPosition(1, 0), true);
                model.SetStartingSwitchOn(new GridPosition(2, 0), true);

                LevelValidationResult result = LevelAssetPersistence.Save(model, level);

                Assert.That(result.IsValid, Is.True);
                Assert.That(level.Tiles[1].tileType, Is.EqualTo(TileType.CornerWire));
                Assert.That(level.Tiles[1].startingRotation, Is.EqualTo(2));
                Assert.That(level.Tiles[1].isRotatable, Is.True);
                Assert.That(level.Tiles[2].startingSwitchOn, Is.True);
                Assert.That(model.HasUnsavedChanges, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void ResizeGrow_PreservesExistingCellsAndInitializesNewCells()
        {
            var model = new LevelAuthoringModel(2, 1);
            model.Place(new GridPosition(1, 0), TileType.Diode);
            model.SetRotation(new GridPosition(1, 0), 2);

            Assert.That(model.Resize(4, 2), Is.True);

            AssertCell(model, 1, TileType.Diode, 2, true, false);
            AssertCell(model, 2, TileType.Empty, 0, false, false);
            AssertCell(model, 3, TileType.Empty, 0, false, false);
            Assert.That(model.CreateSnapshot().Count, Is.EqualTo(8));
        }

        [Test]
        public void ResizeShrink_DetectsAndRequiresConfirmationForNonEmptyRemoval()
        {
            var model = new LevelAuthoringModel(3, 1);
            model.Place(new GridPosition(2, 0), TileType.OutputLamp);

            LevelResizeImpact impact = model.AnalyzeResize(2, 1);

            Assert.That(impact.RequiresDestructiveConfirmation, Is.True);
            Assert.That(impact.RemovedNonEmptyCells, Is.EqualTo(new[] { new GridPosition(2, 0) }));
            Assert.That(model.Resize(2, 1), Is.False);
            Assert.That(model.Width, Is.EqualTo(3));
            Assert.That(model.Resize(2, 1, true), Is.True);
            Assert.That(model.Width, Is.EqualTo(2));
        }

        [Test]
        public void Erase_ProducesCanonicalEmptyCell()
        {
            var model = new LevelAuthoringModel(1, 1);
            GridPosition position = new GridPosition(0, 0);
            model.Place(position, TileType.Switch);
            model.SetStartingSwitchOn(position, true);

            model.Erase(position);

            AssertCell(model, 0, TileType.Empty, 0, false, false);
        }

        [Test]
        public void Placement_ReplacesCellWithRelevantDefaults()
        {
            var model = new LevelAuthoringModel(1, 1);
            GridPosition position = new GridPosition(0, 0);
            model.Place(position, TileType.Switch);
            model.SetStartingSwitchOn(position, true);

            model.Place(position, TileType.CornerWire);

            AssertCell(model, 0, TileType.CornerWire, 0, true, false);
        }

        [Test]
        public void Validation_UsesCurrentUnsavedWorkingState()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_01");
            var model = new LevelAuthoringModel();
            model.Load(level);
            model.Erase(new GridPosition(0, 0));

            LevelValidationResult result = model.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<LevelValidationIssue>(
                issue => issue.Code == LevelValidationCode.MissingPowerSource));
            Assert.That(level.Tiles[0].tileType, Is.EqualTo(TileType.PowerSource));
        }

        [Test]
        public void SolverAnalysis_UsesUnsavedStateWithoutMutatingSourceAsset()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_01");
            var model = new LevelAuthoringModel();
            model.Load(level);
            GridPosition wirePosition = new GridPosition(1, 0);
            model.RotateClockwise(wirePosition);

            LevelValidationReport report = model.Analyze();

            Assert.That(report.StructuralValidation.IsValid, Is.True);
            Assert.That(report.SolverResult.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(report.SolverResult.MinimumMoveCount, Is.Zero);
            Assert.That(level.Tiles[1].startingRotation, Is.Zero);
            Assert.That(model.HasUnsavedChanges, Is.True);
        }

        [Test]
        public void Save_ProducesStructurallyValidExactGrid()
        {
            LevelDefinition level = CreateLevel(2, 1,
                Tile(0, TileType.PowerSource), Tile(1, TileType.OutputLamp));
            try
            {
                var model = new LevelAuthoringModel();
                model.Load(level);
                model.SetRotation(new GridPosition(0, 0), 1);

                LevelValidationResult saveResult = LevelAssetPersistence.Save(model, level);
                LevelValidationResult persistedResult = new LevelValidator().Validate(level);

                Assert.That(saveResult.IsValid, Is.True);
                Assert.That(persistedResult.IsValid, Is.True);
                Assert.That(level.Tiles.Count, Is.EqualTo(level.Width * level.Height));
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void CreateAsset_InitializesExactGridAtChosenPath()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/NeonGrid/Tests/M4_TemporaryLevel.asset");
            try
            {
                LevelDefinition level = LevelAssetPersistence.CreateAsset(path, 3, 2);

                Assert.That(level, Is.Not.Null);
                Assert.That(level.Width, Is.EqualTo(3));
                Assert.That(level.Height, Is.EqualTo(2));
                Assert.That(level.Tiles.Count, Is.EqualTo(6));
                Assert.That(level.Tiles.All(tile => tile.tileType == TileType.Empty), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void CancelNavigation_KeepsOriginalWorkingCopyDirtyAndDoesNotSave()
        {
            LevelDefinition level = CreateLevel(2, 1,
                Tile(0, TileType.PowerSource), Tile(1, TileType.OutputLamp));
            try
            {
                var model = new LevelAuthoringModel();
                model.Load(level);
                model.SetRotation(new GridPosition(0, 0), 1);
                bool saveAttempted = false;

                bool mayNavigate = UnsavedChangesNavigation.CanNavigate(UnsavedChangesChoice.Cancel, () =>
                {
                    saveAttempted = true;
                    return true;
                });

                Assert.That(mayNavigate, Is.False);
                Assert.That(saveAttempted, Is.False);
                Assert.That(model.SourceAsset, Is.SameAs(level));
                Assert.That(model.GetCell(new GridPosition(0, 0)).Rotation, Is.EqualTo(1));
                Assert.That(model.HasUnsavedChanges, Is.True);
                Assert.That(level.Tiles[0].startingRotation, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        private static LevelDefinition CreateLevel(int width, int height, params TileDefinition[] definitions)
        {
            var cells = new List<TileDefinition>();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                cells.Add(new TileDefinition(new GridPosition(x, y), TileType.Empty, 0, false));
            foreach (TileDefinition definition in definitions)
                cells[definition.position.y * width + definition.position.x] = definition;

            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.SetData(width, height, cells);
            return level;
        }

        private static TileDefinition Tile(int x, TileType type, int rotation = 0, bool rotatable = false,
            bool startingSwitchOn = false)
        {
            return new TileDefinition(new GridPosition(x, 0), type, rotation, rotatable, startingSwitchOn);
        }

        private static void AssertCell(LevelAuthoringModel model, int x, TileType type, int rotation,
            bool rotatable, bool switchOn)
        {
            LevelCellData cell = model.GetCell(new GridPosition(x, 0));
            Assert.That(cell.TileType, Is.EqualTo(type));
            Assert.That(cell.Rotation, Is.EqualTo(rotation));
            Assert.That(cell.IsRotatable, Is.EqualTo(rotatable));
            Assert.That(cell.StartingSwitchOn, Is.EqualTo(switchOn));
        }
    }
}

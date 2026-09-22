using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class TechnicalNeonVisualThemeTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CircuitVisualThemeDefinition theme;

        [SetUp]
        public void SetUp()
        {
            theme = CircuitVisualThemeCatalog.LoadTechnicalNeonPrototype();
            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.IsConfigured, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object item in cleanup)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            cleanup.Clear();
        }

        [Test]
        public void Theme_ContainsExactSemanticPaletteAndNormalizedMetrics()
        {
            AssertColor(theme.Background, "#050810");
            AssertColor(theme.Board, "#0B1220");
            AssertColor(theme.InactiveConductor, "#263349");
            AssertColor(theme.PoweredEnergy, "#18DCCA");
            AssertColor(theme.PoweredHotCore, "#BFFFFA");
            AssertColor(theme.SecondaryBlue, "#27A9FF");
            AssertColor(theme.PowerSource, "#FF3CB4");
            AssertColor(theme.LedObjective, "#FFD84A");
            AssertColor(theme.Success, "#22DFA5");
            AssertColor(theme.DirectionalAccent, "#FF8A3D");
            AssertColor(theme.Hint, "#FFCF4A");
            Assert.That(theme.ReferenceCanvasSize, Is.EqualTo(256f));
            Assert.That(theme.ConduitWidth, Is.EqualTo(30f));
            Assert.That(theme.PoweredCoreWidth, Is.InRange(10f, 12f));
            Assert.That(theme.HaloWidth, Is.InRange(24f, 30f));
        }

        [Test]
        public void Theme_HasProceduralEntryAndFutureAssetSlotForEveryTileFamily()
        {
            foreach (TileType tileType in Enum.GetValues(typeof(TileType)))
                Assert.That(theme.HasVisualEntry(tileType), Is.True, tileType.ToString());
            Assert.That(theme.ThemeId, Is.EqualTo("technical_neon_prototype"));
            Assert.That(theme.DisplayName, Is.EqualTo("Technical Neon Infrastructure"));
            Assert.That(theme.HousingSprite, Is.Null);
            Assert.That(theme.SourceSymbolSprite, Is.Null);
            Assert.That(theme.LedOffSprite, Is.Null);
            Assert.That(theme.LedOnSprite, Is.Null);
        }

        [Test]
        public void ConnectorReferenceCenters_AreExact()
        {
            Assert.That(CircuitVisualGeometry.GetConnectorCenter(CardinalDirection.Up),
                Is.EqualTo(new Vector2(128f, 256f)));
            Assert.That(CircuitVisualGeometry.GetConnectorCenter(CardinalDirection.Right),
                Is.EqualTo(new Vector2(256f, 128f)));
            Assert.That(CircuitVisualGeometry.GetConnectorCenter(CardinalDirection.Down),
                Is.EqualTo(new Vector2(128f, 0f)));
            Assert.That(CircuitVisualGeometry.GetConnectorCenter(CardinalDirection.Left),
                Is.EqualTo(new Vector2(0f, 128f)));
        }

        [Test]
        public void AdjacentCompatibleConnectorCenterlines_AlignExactly()
        {
            Vector2 leftTileCenter = Vector2.zero;
            Vector2 rightTileCenter = Vector2.right;
            Vector2 leftExit = leftTileCenter +
                               CircuitVisualGeometry.GetConnectorCenterNormalized(
                                   CardinalDirection.Right);
            Vector2 rightEntry = rightTileCenter +
                                 CircuitVisualGeometry.GetConnectorCenterNormalized(
                                     CardinalDirection.Left);
            Assert.That(leftExit, Is.EqualTo(rightEntry));

            Vector2 lowerTileCenter = Vector2.zero;
            Vector2 upperTileCenter = Vector2.up;
            Vector2 lowerExit = lowerTileCenter +
                                CircuitVisualGeometry.GetConnectorCenterNormalized(
                                    CardinalDirection.Up);
            Vector2 upperEntry = upperTileCenter +
                                 CircuitVisualGeometry.GetConnectorCenterNormalized(
                                     CardinalDirection.Down);
            Assert.That(lowerExit, Is.EqualTo(upperEntry));
        }

        [TestCase(CardinalDirection.Up, 0, 0f, 0.5f)]
        [TestCase(CardinalDirection.Up, 1, 0.5f, 0f)]
        [TestCase(CardinalDirection.Up, 2, 0f, -0.5f)]
        [TestCase(CardinalDirection.Up, 3, -0.5f, 0f)]
        public void ConnectorGeometry_RotatesDeterministically(CardinalDirection direction,
            int rotation, float expectedX, float expectedY)
        {
            Vector2 result = CircuitVisualGeometry.GetRotatedConnectorCenterNormalized(direction,
                rotation);
            Assert.That(result.x, Is.EqualTo(expectedX).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(expectedY).Within(0.0001f));
        }

        [TestCase(TileType.StraightWire, 2)]
        [TestCase(TileType.CornerWire, 2)]
        [TestCase(TileType.TJunction, 3)]
        [TestCase(TileType.CrossJunction, 4)]
        public void WireFamily_UsesSharedConduitAndCoreMetrics(TileType tileType,
            int expectedPorts)
        {
            CircuitTileView view = BuildTile(tileType, 0, true);
            SpriteRenderer[] bases = Parts(view, "Circuit Base");
            SpriteRenderer[] hotCores = Parts(view, "Powered Hot Core");
            float expectedConduit = theme.ToTileUnits(theme.ConduitWidth);
            float expectedCore = theme.ToTileUnits(theme.PoweredCoreWidth);

            Assert.That(bases, Has.Length.EqualTo(expectedPorts));
            Assert.That(hotCores, Has.Length.EqualTo(expectedPorts));
            foreach (SpriteRenderer part in bases)
                Assert.That(Mathf.Min(part.transform.localScale.x,
                    part.transform.localScale.y), Is.EqualTo(expectedConduit).Within(0.0001f));
            foreach (SpriteRenderer part in hotCores)
                Assert.That(Mathf.Min(part.transform.localScale.x,
                    part.transform.localScale.y), Is.EqualTo(expectedCore).Within(0.0001f));
        }

        [Test]
        public void CircuitContentRotatesWhileHousingRemainsStable()
        {
            CircuitTileView view = BuildTile(TileType.CornerWire, 1, true);
            Transform housing = view.transform.Find("Housing");
            Transform rotating = view.transform.Find("Rotating Circuit");

            Assert.That(housing.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(NormalizeDegrees(rotating.localEulerAngles.z), Is.EqualTo(270f)
                .Within(0.001f));
            Assert.That(view.RotatingContentDegrees, Is.EqualTo(270f).Within(0.001f));
        }

        [TestCase(0, CardinalDirection.Right)]
        [TestCase(1, CardinalDirection.Down)]
        [TestCase(2, CardinalDirection.Left)]
        [TestCase(3, CardinalDirection.Up)]
        public void Diode_StrongDirectionalGeometryFollowsLogicalOutput(int rotation,
            CardinalDirection expectedOutput)
        {
            CircuitTileView view = BuildTile(TileType.Diode, rotation, true);
            CircuitTileState state = State(TileType.Diode, rotation, true);

            Assert.That(view.CurrentFunctionalSymbol, Is.EqualTo(CircuitFunctionalSymbol.Diode));
            Assert.That(FindPart(view, "Diode Chevron Upper"), Is.Not.Null);
            Assert.That(FindPart(view, "Diode Blocking Bar"), Is.Not.Null);
            Assert.That(TilePowerFlow.GetOutputSides(TileType.Diode, rotation),
                Is.EqualTo(expectedOutput));
            Assert.That((state.Connections & expectedOutput) != 0, Is.True);
        }

        [Test]
        public void Switch_OpenAndClosedUseDistinctGeometryNotOnlyColor()
        {
            CircuitTileState state = State(TileType.Switch, 0, false, false);
            CircuitTileView view = BuildTile(state);
            GameObject open = FindPart(view, "Switch Open Contact").gameObject;
            GameObject closed = FindPart(view, "Switch Closed Contact").gameObject;

            Assert.That(view.CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.SwitchOpen));
            Assert.That(open.activeSelf, Is.True);
            Assert.That(closed.activeSelf, Is.False);
            Assert.That(open.transform.localScale.x, Is.GreaterThanOrEqualTo(0.42f));
            Assert.That(open.transform.localScale.y, Is.GreaterThanOrEqualTo(0.065f));
            Assert.That(FindPart(view, "Switch Left Contact").localScale.x,
                Is.GreaterThanOrEqualTo(0.10f));
            Assert.That(state.TryApplyAction(new PuzzleAction(state.Position,
                PuzzleActionType.ToggleSwitch)), Is.True);
            view.Refresh(state, SpriteFor(view));

            Assert.That(view.CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.SwitchClosed));
            Assert.That(open.activeSelf, Is.False);
            Assert.That(closed.activeSelf, Is.True);
            Assert.That(closed.transform.localScale.x, Is.GreaterThanOrEqualTo(0.42f));
        }

        [TestCase(TileType.AndGate, CircuitFunctionalSymbol.AndGate,
            "AND Gate Rounded Output")]
        [TestCase(TileType.OrGate, CircuitFunctionalSymbol.OrGate,
            "OR Gate Upper Wing")]
        public void Gates_HaveUniqueSilhouetteWithoutTextDependency(TileType type,
            CircuitFunctionalSymbol symbol, string uniquePart)
        {
            CircuitTileView view = BuildTile(type, 0, true);

            Assert.That(view.CurrentFunctionalSymbol, Is.EqualTo(symbol));
            Assert.That(view.CurrentLabel, Is.Empty);
            Assert.That(view.GetComponentInChildren<TextMesh>(true), Is.Null);
            Assert.That(FindPart(view, uniquePart), Is.Not.Null);
            Transform body = FindPart(view, type == TileType.AndGate
                ? "AND Gate Body"
                : "OR Gate Body");
            Assert.That(body, Is.Not.Null);
            Assert.That(body.localScale.x, Is.InRange(0.45f, 0.55f));
            Assert.That(body.localScale.y, Is.InRange(0.45f, 0.55f));
            Assert.That(TilePowerFlow.GetInputSides(type, 0),
                Is.EqualTo(CardinalDirection.Left | CardinalDirection.Right));
            Assert.That(TilePowerFlow.GetOutputSides(type, 0),
                Is.EqualTo(CardinalDirection.Up));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void GateSilhouettes_RemainGeometricallyDistinctAtEveryRotation(int rotation)
        {
            CircuitTileView andGate = BuildTile(TileType.AndGate, rotation, true);
            CircuitTileView orGate = BuildTile(TileType.OrGate, rotation, true);

            Assert.That(andGate.CurrentLabel, Is.Empty);
            Assert.That(orGate.CurrentLabel, Is.Empty);
            Assert.That(FindPart(andGate, "AND Gate Rounded Output"), Is.Not.Null);
            Assert.That(FindPart(andGate, "OR Gate Point"), Is.Null);
            Assert.That(FindPart(orGate, "OR Gate Point"), Is.Not.Null);
            Assert.That(FindPart(orGate, "AND Gate Rounded Output"), Is.Null);
            Assert.That(NormalizeDegrees(andGate.RotatingContentDegrees),
                Is.EqualTo(NormalizeDegrees(-90f * rotation)).Within(0.001f));
            Assert.That(NormalizeDegrees(orGate.RotatingContentDegrees),
                Is.EqualTo(NormalizeDegrees(-90f * rotation)).Within(0.001f));
        }

        [TestCase(TileType.AndGate, 0)]
        [TestCase(TileType.AndGate, 1)]
        [TestCase(TileType.AndGate, 2)]
        [TestCase(TileType.AndGate, 3)]
        [TestCase(TileType.OrGate, 0)]
        [TestCase(TileType.OrGate, 1)]
        [TestCase(TileType.OrGate, 2)]
        [TestCase(TileType.OrGate, 3)]
        public void GateVisualRotation_PreservesAcceptedInputAndOutputOrientation(
            TileType type, int rotation)
        {
            CircuitTileView view = BuildTile(type, rotation, true);
            CardinalDirection expectedInputs =
                (CardinalDirection.Left | CardinalDirection.Right).RotateClockwise(rotation);
            CardinalDirection expectedOutput = CardinalDirection.Up.RotateClockwise(rotation);

            Assert.That(TilePowerFlow.GetInputSides(type, rotation), Is.EqualTo(expectedInputs));
            Assert.That(TilePowerFlow.GetOutputSides(type, rotation), Is.EqualTo(expectedOutput));
            Assert.That(NormalizeDegrees(view.RotatingContentDegrees),
                Is.EqualTo(NormalizeDegrees(-90f * rotation)).Within(0.001f));
        }

        [Test]
        public void Source_UsesMagentaCoreAndCyanOutgoingEnergy()
        {
            BoardState board = new BoardState(2, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 1, false)
            });
            CircuitTileState source = new CircuitSimulation(board).Board.GetTile(
                new GridPosition(0, 0));
            CircuitTileView view = BuildTile(source);

            Assert.That(view.CurrentFunctionalSymbol, Is.EqualTo(CircuitFunctionalSymbol.Source));
            Assert.That(view.CurrentCircuitColor, Is.EqualTo(theme.PowerSource));
            Assert.That(FindPart(view, "Source Magenta Core"), Is.Not.Null);
            Assert.That(ActiveParts(view, "Powered Energy"), Has.Length.GreaterThan(0));
        }

        [Test]
        public void LedObjective_OffAndOnAreDistinctTechnicalStates()
        {
            CircuitTileState off = State(TileType.OutputLamp, 0, false);
            CircuitTileView offView = BuildTile(off);
            BoardState board = new BoardState(2, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.OutputLamp, 0, false)
            });
            CircuitTileState on = new CircuitSimulation(board).Board.GetTile(new GridPosition(1, 0));
            CircuitTileView onView = BuildTile(on);

            Assert.That(offView.CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.LedObjectiveOff));
            Assert.That(onView.CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.LedObjectiveOn));
            Assert.That(offView.CurrentCircuitColor, Is.Not.EqualTo(onView.CurrentCircuitColor));
            Assert.That(offView.CurrentLabel, Is.Empty);
            Assert.That(onView.CurrentLabel, Is.Empty);
            Assert.That(FindPart(onView, "LED Label"), Is.Null);
            Transform diffuser = FindPart(onView, "LED Diffuser");
            Assert.That(diffuser, Is.Not.Null);
            Assert.That(diffuser.localScale.x, Is.GreaterThanOrEqualTo(0.50f));
            Assert.That(diffuser.localScale.y, Is.GreaterThanOrEqualTo(0.40f));
            Assert.That(Parts(onView, "LED Emitter 1"), Has.Length.EqualTo(1));
            Assert.That(Parts(onView, "LED Emitter 4"), Has.Length.EqualTo(1));
            Assert.That(FindPart(offView, "LED Objective Halo").gameObject.activeSelf, Is.False);
            Assert.That(FindPart(onView, "LED Objective Halo").gameObject.activeSelf, Is.True);
            Assert.That(FindPart(offView, "LED Emitter 1").GetComponent<SpriteRenderer>().color,
                Is.Not.EqualTo(FindPart(onView, "LED Emitter 1")
                    .GetComponent<SpriteRenderer>().color));
            Assert.That(ActiveParts(onView, "Powered Energy"), Has.Length.GreaterThan(0));
        }

        [Test]
        public void ProductionCompletionPresentation_RemainsImmediateByDefault()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/PowerStation/PS_01");
            var root = NewObject("Production Completion Controller");
            var controller = root.AddComponent<BoardController>();
            controller.Initialize(level);

            Assert.That(controller.IsCompletionPresentationHeld, Is.False);
            Assert.That(controller.PerformPlayerAction(new GridPosition(1, 0)), Is.True);
            Assert.That(controller.Session.IsCompleted, Is.True);
            Assert.That(controller.IsCompletionPanelVisible, Is.True);
        }

        [Test]
        public void PrototypeCompletionHold_PreservesPoweredCompletionAndCanReleasePresentation()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/PowerStation/PS_01");
            var root = NewObject("Prototype Completion Hold Controller");
            var controller = root.AddComponent<BoardController>();
            controller.Initialize(level, theme);
            controller.SetCompletionPresentationHeld(true);

            Assert.That(controller.PerformPlayerAction(new GridPosition(1, 0)), Is.True);
            Assert.That(controller.Session.IsCompleted, Is.True);
            Assert.That(controller.Session.CompletionResult, Is.Not.Null);
            Assert.That(controller.Session.Board.GetTile(new GridPosition(2, 0)).IsPowered,
                Is.True);
            Assert.That(controller.BoardView.GetTileView(new GridPosition(2, 0))
                .CurrentFunctionalSymbol, Is.EqualTo(CircuitFunctionalSymbol.LedObjectiveOn));
            Assert.That(controller.IsCompletionPresentationHeld, Is.True);
            Assert.That(controller.IsCompletionPanelVisible, Is.False);

            controller.SetCompletionPresentationHeld(false);

            Assert.That(controller.Session.IsCompleted, Is.True);
            Assert.That(controller.IsCompletionPanelVisible, Is.True);
        }

        [Test]
        public void LockedTreatment_UsesCornerClampsAndLeavesCircuitVisible()
        {
            CircuitTileView view = BuildTile(TileType.Diode, 0, false);

            Assert.That(view.IsVisualLocked, Is.True);
            Assert.That(FindPart(view, "Lock Clamp Top Left"), Is.Not.Null);
            Assert.That(FindPart(view, "Lock Clamp Bottom Right"), Is.Not.Null);
            Assert.That(FindPart(view, "Diode Chevron Upper"), Is.Not.Null);
            Assert.That(FindPart(view, "Diode Blocking Bar"), Is.Not.Null);
            Assert.That(FindPart(view, "Lock Indicator"), Is.Null,
                "Technical Neon uses corner hardware instead of a central padlock.");
        }

        [Test]
        public void HintRim_DoesNotAlterUnderlyingPoweredPresentation()
        {
            BoardState board = new BoardState(2, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 1, true)
            });
            CircuitTileState powered = new CircuitSimulation(board).Board.GetTile(
                new GridPosition(1, 0));
            CircuitTileView view = BuildTile(powered);
            Color before = view.CurrentCircuitColor;
            bool poweredBefore = view.IsUnderlyingPowered;

            view.SetHintHighlighted(true);

            Assert.That(view.IsHintHighlighted, Is.True);
            Assert.That(view.CurrentCircuitColor, Is.EqualTo(before));
            Assert.That(view.IsUnderlyingPowered, Is.EqualTo(poweredBefore));
            Assert.That(FindPart(view, "Hint Rim Top").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void NoAssignedTheme_PreservesAcceptedProgrammerArtFallback()
        {
            var root = NewObject("Fallback Board");
            var view = root.AddComponent<BoardView>();
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/PowerStation/PS_01");

            view.Build(level.CreateBoardState(), _ => { });

            Assert.That(view.UsesVisualTheme, Is.False);
            foreach (CircuitTileView tile in root.GetComponentsInChildren<CircuitTileView>())
            {
                Assert.That(tile.IsUsingVisualTheme, Is.False);
                Assert.That(tile.transform.Find("Background"), Is.Not.Null);
            }
        }

        [Test]
        public void ProductionBoardController_DefaultOverloadDoesNotAdoptPrototypeTheme()
        {
            var root = NewObject("Production Fallback Controller");
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/PowerStation/PS_01");
            var controller = root.AddComponent<BoardController>();

            controller.Initialize(level);

            Assert.That(controller.BoardView.UsesVisualTheme, Is.False);
            Assert.That(controller.BoardView.VisualTheme, Is.Null);
        }

        [Test]
        public void PrototypeDefinition_BindsOnlyPS01CG10AndTechnicalTheme()
        {
            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();

            Assert.That(prototype, Is.Not.Null);
            Assert.That(prototype.IsConfigured, Is.True);
            Assert.That(prototype.VisualTheme, Is.SameAs(theme));
            Assert.That(prototype.SimpleLevel.name, Is.EqualTo("PS_01"));
            Assert.That(prototype.DenseLevel.name, Is.EqualTo("CG_10"));
            Assert.That(prototype.SimpleLevel.Width, Is.EqualTo(3));
            Assert.That(prototype.DenseLevel.Width, Is.EqualTo(6));
            Assert.That(prototype.DenseLevel.Height, Is.EqualTo(6));
        }

        [Test]
        public void PS01Prototype_UsesThemeForSourceWireAndObjective()
        {
            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            BoardView view = BuildBoard(prototype.SimpleLevel);

            Assert.That(view.UsesVisualTheme, Is.True);
            Assert.That(view.GetTileView(new GridPosition(0, 0)).CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.Source));
            Assert.That(view.GetTileView(new GridPosition(1, 0)).IsUsingVisualTheme, Is.True);
            Assert.That(view.GetTileView(new GridPosition(2, 0)).CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.LedObjectiveOff));
        }

        [Test]
        public void CG10Prototype_CoversCompleteMechanicVocabularyAndThemeBinding()
        {
            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            BoardView view = BuildBoard(prototype.DenseLevel);
            var types = new HashSet<TileType>(prototype.DenseLevel.Tiles.Select(tile =>
                tile.tileType));

            Assert.That(types, Does.Contain(TileType.StraightWire));
            Assert.That(types, Does.Contain(TileType.CornerWire));
            Assert.That(types, Does.Contain(TileType.TJunction));
            Assert.That(types, Does.Contain(TileType.CrossJunction));
            Assert.That(types, Does.Contain(TileType.Diode));
            Assert.That(types, Does.Contain(TileType.Switch));
            Assert.That(types, Does.Contain(TileType.AndGate));
            Assert.That(types, Does.Contain(TileType.OrGate));
            Assert.That(types, Does.Contain(TileType.PowerSource));
            Assert.That(types, Does.Contain(TileType.OutputLamp));
            Assert.That(view.GetComponentsInChildren<CircuitTileView>(),
                Has.All.Matches<CircuitTileView>(tile => tile.IsUsingVisualTheme));
        }

        [TestCase("Levels/PowerStation/PS_01", 1080, 1920)]
        [TestCase("Levels/PowerStation/PS_01", 1080, 2340)]
        [TestCase("Levels/PowerStation/PS_01", 720, 1280)]
        [TestCase("Levels/CentralGrid/CG_10", 1080, 1920)]
        [TestCase("Levels/CentralGrid/CG_10", 1080, 2340)]
        [TestCase("Levels/CentralGrid/CG_10", 720, 1280)]
        public void ThemedRepresentativeBoards_PreservePortraitBoardFitting(string resourcePath,
            int width, int height)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(resourcePath);
            BoardView view = BuildBoard(level);
            Physics2D.SyncTransforms();
            Bounds bounds = view.GetWorldBounds();
            float aspect = width / (float)height;
            BoardViewportFit fit = BoardViewportFitter.Calculate(bounds, aspect,
                GameplayLayoutMetrics.BoardViewport, GameplayLayoutMetrics.BoardPaddingWorld);

            Assert.That(bounds.size.x, Is.EqualTo(level.Width - 0.1f).Within(0.001f));
            Assert.That(bounds.size.y, Is.EqualTo(level.Height - 0.1f).Within(0.001f));
            Assert.That(fit.PlayableWorldRect.xMin,
                Is.LessThanOrEqualTo(bounds.min.x - GameplayLayoutMetrics.BoardPaddingWorld +
                                     0.0001f));
            Assert.That(fit.PlayableWorldRect.xMax,
                Is.GreaterThanOrEqualTo(bounds.max.x + GameplayLayoutMetrics.BoardPaddingWorld -
                                        0.0001f));
            Assert.That(fit.PlayableWorldRect.yMin,
                Is.LessThanOrEqualTo(bounds.min.y - GameplayLayoutMetrics.BoardPaddingWorld +
                                     0.0001f));
            Assert.That(fit.PlayableWorldRect.yMax,
                Is.GreaterThanOrEqualTo(bounds.max.y + GameplayLayoutMetrics.BoardPaddingWorld -
                                        0.0001f));
        }

        [Test]
        public void PrototypeScene_IsIsolatedAndProductionSceneHasNoThemeReference()
        {
            string prototypeScene = Path.Combine(Application.dataPath, "NeonGrid", "Scenes",
                "M15_GameplayVisualPrototype.unity");
            string productionScene = Path.Combine(Application.dataPath, "NeonGrid", "Scenes",
                "M12_NeonGrid_Main.unity");
            string themeGuid = UnityEditor.AssetDatabase.AssetPathToGUID(
                "Assets/NeonGrid/Resources/VisualThemes/TechnicalNeonPrototype.asset");

            Assert.That(File.Exists(prototypeScene), Is.True);
            Assert.That(File.ReadAllText(prototypeScene), Does.Contain(
                "GameplayVisualPrototypeController"));
            Assert.That(File.ReadAllText(prototypeScene), Does.Contain(
                "holdCompletionPresentation: 1"));
            Assert.That(File.ReadAllText(productionScene), Does.Not.Contain(themeGuid));
            Assert.That(File.ReadAllText(productionScene), Does.Not.Contain(
                "holdCompletionPresentation"));
        }

        private BoardView BuildBoard(LevelDefinition level)
        {
            var root = NewObject($"Themed Board {level.name}");
            var view = root.AddComponent<BoardView>();
            view.Build(level.CreateBoardState(), _ => { }, theme);
            return view;
        }

        private CircuitTileView BuildTile(TileType type, int rotation, bool rotatable)
        {
            return BuildTile(State(type, rotation, rotatable));
        }

        private CircuitTileView BuildTile(CircuitTileState state)
        {
            var root = NewObject($"Technical Tile {state.TileType}");
            Sprite sprite = CreateSprite();
            var view = root.AddComponent<CircuitTileView>();
            view.Build(sprite, theme);
            view.Refresh(state, sprite);
            return view;
        }

        private static CircuitTileState State(TileType type, int rotation, bool rotatable,
            bool switchOn = false)
        {
            return new BoardState(1, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), type, rotation, rotatable, switchOn)
            }).GetTile(new GridPosition(0, 0));
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            cleanup.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1),
                Vector2.one * 0.5f, 1f);
            cleanup.Add(sprite);
            return sprite;
        }

        private GameObject NewObject(string name)
        {
            var value = new GameObject(name);
            cleanup.Add(value);
            return value;
        }

        private static Sprite SpriteFor(CircuitTileView view)
        {
            return view.GetComponentInChildren<SpriteRenderer>(true).sprite;
        }

        private static SpriteRenderer[] Parts(CircuitTileView view, string name)
        {
            return view.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(part => part.gameObject.name == name).ToArray();
        }

        private static SpriteRenderer[] ActiveParts(CircuitTileView view, string name)
        {
            return Parts(view, name).Where(part => part.gameObject.activeSelf).ToArray();
        }

        private static Transform FindPart(CircuitTileView view, string name)
        {
            return view.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.gameObject.name == name);
        }

        private static float NormalizeDegrees(float value)
        {
            value %= 360f;
            return value < 0f ? value + 360f : value;
        }

        private static void AssertColor(Color actual, string html)
        {
            Assert.That(ColorUtility.TryParseHtmlString(html, out Color expected), Is.True);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(1f).Within(0.0001f));
        }
    }
}

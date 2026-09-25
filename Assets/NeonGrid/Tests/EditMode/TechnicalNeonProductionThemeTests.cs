using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class TechnicalNeonProductionThemeTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CircuitVisualThemeDefinition b1;
        private CircuitVisualThemeDefinition b2;

        [SetUp]
        public void SetUp()
        {
            b1 = CircuitVisualThemeCatalog.LoadTechnicalNeonPrototype();
            b2 = CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype();
            Assert.That(b1, Is.Not.Null);
            Assert.That(b2, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object item in cleanup)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            cleanup.Clear();
        }

        [Test]
        public void B2Theme_ExistsIsValidAndDoesNotReplaceB1()
        {
            Assert.That(b1.IsConfigured, Is.True);
            Assert.That(b1.VisualStyle, Is.EqualTo(CircuitVisualStyle.TechnicalPrototype));
            Assert.That(b1.ThemeId, Is.EqualTo("technical_neon_prototype"));
            Assert.That(b2.IsConfigured, Is.True);
            Assert.That(b2.VisualStyle, Is.EqualTo(CircuitVisualStyle.ProductionPrototype));
            Assert.That(b2.ThemeId, Is.EqualTo("technical_neon_production_prototype"));
            Assert.That(b2, Is.Not.SameAs(b1));
        }

        [Test]
        public void B2_PreservesSemanticPaletteAndNormalizedGeometryContract()
        {
            AssertColor(b2.Background, "#050810");
            AssertColor(b2.Board, "#0B1220");
            AssertColor(b2.InactiveConductor, "#263349");
            AssertColor(b2.PoweredEnergy, "#18DCCA");
            AssertColor(b2.PoweredHotCore, "#BFFFFA");
            AssertColor(b2.SecondaryBlue, "#27A9FF");
            AssertColor(b2.PowerSource, "#FF3CB4");
            AssertColor(b2.LedObjective, "#FFD84A");
            AssertColor(b2.DirectionalAccent, "#FF8A3D");
            AssertColor(b2.Hint, "#FFCF4A");
            Assert.That(b2.ReferenceCanvasSize, Is.EqualTo(256f));
            Assert.That(b2.ConduitWidth, Is.EqualTo(30f));
            Assert.That(b2.PoweredCoreWidth, Is.EqualTo(11f));
            Assert.That(b2.HaloWidth, Is.EqualTo(28f));
        }

        [Test]
        public void B2_UsesAuthoritativeConnectorCenters()
        {
            Assert.That(CircuitVisualGeometry.TopConnector, Is.EqualTo(new Vector2(128f, 256f)));
            Assert.That(CircuitVisualGeometry.RightConnector, Is.EqualTo(new Vector2(256f, 128f)));
            Assert.That(CircuitVisualGeometry.BottomConnector, Is.EqualTo(new Vector2(128f, 0f)));
            Assert.That(CircuitVisualGeometry.LeftConnector, Is.EqualTo(new Vector2(0f, 128f)));
        }

        [TestCase(TileType.StraightWire, 2)]
        [TestCase(TileType.CornerWire, 2)]
        [TestCase(TileType.TJunction, 3)]
        [TestCase(TileType.CrossJunction, 4)]
        public void B2WireFamily_SharesConduitCoreHaloAndPortTreatment(TileType type,
            int expectedPorts)
        {
            CircuitTileView view = BuildTile(State(type, 0, true));
            Assert.That(Parts(view, "Circuit Base"), Has.Length.EqualTo(expectedPorts));
            Assert.That(Parts(view, "Powered Hot Core"), Has.Length.EqualTo(expectedPorts));
            Assert.That(Parts(view, "Energy Halo"), Has.Length.EqualTo(expectedPorts));
            Assert.That(Parts(view, "Connector Port Collar"), Has.Length.EqualTo(expectedPorts));
            foreach (SpriteRenderer part in Parts(view, "Circuit Base"))
                Assert.That(MinDimension(part),
                    Is.EqualTo(b2.ToTileUnits(b2.ConduitWidth)).Within(0.0001f));
            foreach (SpriteRenderer part in Parts(view, "Powered Hot Core"))
                Assert.That(MinDimension(part),
                    Is.EqualTo(b2.ToTileUnits(b2.PoweredCoreWidth)).Within(0.0001f));
        }

        [Test]
        public void B2HousingAndBoardSurface_UseProductionLayersOnlyForB2()
        {
            LevelDefinition level = LoadPs01();
            BoardView b2Board = BuildBoard(level, b2);
            BoardView b1Board = BuildBoard(level, b1);

            Assert.That(b2Board.UsesProductionSkin, Is.True);
            Assert.That(b2Board.transform.Find("Production Board Surface"), Is.Not.Null);
            CircuitTileView b2Tile = b2Board.GetTileView(new GridPosition(1, 0));
            Assert.That(FindPart(b2Tile, "Cell Socket"), Is.Not.Null);
            Assert.That(FindPart(b2Tile, "Recessed Module Surface"), Is.Not.Null);
            Assert.That(FindPart(b2Tile, "Housing Fastener Bolt 1"), Is.Not.Null);

            Assert.That(b1Board.UsesProductionSkin, Is.False);
            Assert.That(b1Board.transform.Find("Production Board Surface"), Is.Null);
            Assert.That(FindPart(b1Board.GetTileView(new GridPosition(1, 0)), "Cell Socket"),
                Is.Null);
        }

        [Test]
        public void B2SourceAndObjective_PreserveSemanticRolesAndTextFreeObjective()
        {
            CircuitTileView source = BuildTile(PoweredState(TileType.PowerSource));
            CircuitTileView objectiveOff = BuildTile(State(TileType.OutputLamp, 0, false));
            CircuitTileView objectiveOn = BuildPoweredObjective();

            Assert.That(source.CurrentCircuitColor, Is.EqualTo(b2.PowerSource));
            Assert.That(FindPart(source, "Source Chamber Frame Top"), Is.Not.Null);
            Assert.That(FindPart(source, "Source Inner Emission"), Is.Not.Null);
            Assert.That(objectiveOff.CurrentLabel, Is.Empty);
            Assert.That(objectiveOn.CurrentLabel, Is.Empty);
            Assert.That(objectiveOn.GetComponentInChildren<TextMesh>(true), Is.Null);
            Assert.That(FindPart(objectiveOn, "LED Receiver Backplate"), Is.Not.Null);
            Assert.That(Parts(objectiveOn, "LED Emitter 1"), Has.Length.EqualTo(1));
            Assert.That(FindPart(objectiveOff, "LED Objective Halo").gameObject.activeSelf,
                Is.False);
            Assert.That(FindPart(objectiveOn, "LED Objective Halo").gameObject.activeSelf,
                Is.True);
        }

        [TestCase(TileType.AndGate, "AND Gate Rounded Output", "OR Gate Point")]
        [TestCase(TileType.OrGate, "OR Gate Point", "AND Gate Rounded Output")]
        public void B2Gates_AreLabelFreeAndGeometryDistinct(TileType type, string ownPart,
            string otherPart)
        {
            CircuitTileView view = BuildTile(State(type, 0, true));
            Assert.That(view.CurrentLabel, Is.Empty);
            Assert.That(view.GetComponentInChildren<TextMesh>(true), Is.Null);
            Assert.That(FindPart(view, ownPart), Is.Not.Null);
            Assert.That(FindPart(view, otherPart), Is.Null);
            Assert.That(FindPart(view, type == TileType.AndGate
                ? "AND Gate Module Bed"
                : "OR Gate Module Bed"), Is.Not.Null);
        }

        [TestCase(TileType.AndGate, 0)]
        [TestCase(TileType.AndGate, 1)]
        [TestCase(TileType.AndGate, 2)]
        [TestCase(TileType.AndGate, 3)]
        [TestCase(TileType.OrGate, 0)]
        [TestCase(TileType.OrGate, 1)]
        [TestCase(TileType.OrGate, 2)]
        [TestCase(TileType.OrGate, 3)]
        public void B2GateRotations_PreserveAcceptedInputsAndOutput(TileType type, int rotation)
        {
            CardinalDirection inputs =
                (CardinalDirection.Left | CardinalDirection.Right).RotateClockwise(rotation);
            CardinalDirection output = CardinalDirection.Up.RotateClockwise(rotation);
            CircuitTileView view = BuildTile(State(type, rotation, true));

            Assert.That(TilePowerFlow.GetInputSides(type, rotation), Is.EqualTo(inputs));
            Assert.That(TilePowerFlow.GetOutputSides(type, rotation), Is.EqualTo(output));
            Assert.That(NormalizeDegrees(view.RotatingContentDegrees),
                Is.EqualTo(NormalizeDegrees(-90f * rotation)).Within(0.001f));
        }

        [Test]
        public void B2Switch_DiodeAndLockedTreatmentsRemainGeometryDriven()
        {
            CircuitTileState switchState = State(TileType.Switch, 0, false, false);
            CircuitTileView switchView = BuildTile(switchState);
            GameObject open = FindPart(switchView, "Switch Open Contact").gameObject;
            GameObject closed = FindPart(switchView, "Switch Closed Contact").gameObject;
            Assert.That(open.activeSelf, Is.True);
            Assert.That(closed.activeSelf, Is.False);
            Assert.That(FindPart(switchView, "Switch Mechanism Bed"), Is.Not.Null);
            switchState.TryApplyAction(new PuzzleAction(switchState.Position,
                PuzzleActionType.ToggleSwitch));
            switchView.Refresh(switchState, SpriteFor(switchView));
            Assert.That(open.activeSelf, Is.False);
            Assert.That(closed.activeSelf, Is.True);

            CircuitTileView diode = BuildTile(State(TileType.Diode, 0, false));
            Assert.That(FindPart(diode, "Diode Mechanism Plate"), Is.Not.Null);
            Assert.That(FindPart(diode, "Diode Chevron Upper"), Is.Not.Null);
            Assert.That(FindPart(diode, "Diode Blocking Bar"), Is.Not.Null);
            Assert.That(FindPart(diode, "Lock Clamp Top Left"), Is.Not.Null);
            Assert.That(FindPart(diode, "Lock Clamp Top Left Vertical"), Is.Not.Null);
            Assert.That(FindPart(diode, "Lock Clamp Bottom Right Vertical"), Is.Not.Null);
            Assert.That(FindPart(diode, "Lock Indicator"), Is.Null);
        }

        [TestCase(TileType.StraightWire)]
        [TestCase(TileType.CornerWire)]
        [TestCase(TileType.TJunction)]
        [TestCase(TileType.CrossJunction)]
        [TestCase(TileType.Diode)]
        [TestCase(TileType.AndGate)]
        [TestCase(TileType.OrGate)]
        public void B2LockedTreatment_IsStaticReadableAndGenericAcrossLockableFamilies(
            TileType type)
        {
            CircuitTileView locked = BuildTile(State(type, 0, false));
            CircuitTileView rotatable = BuildTile(State(type, 0, true));
            SpriteRenderer horizontal = FindPart(locked, "Lock Clamp Top Left")
                .GetComponent<SpriteRenderer>();
            SpriteRenderer vertical = FindPart(locked, "Lock Clamp Top Left Vertical")
                .GetComponent<SpriteRenderer>();
            SpriteRenderer bolt = FindPart(locked, "Lock Clamp Top Left Bolt")
                .GetComponent<SpriteRenderer>();

            Assert.That(horizontal.transform.localScale,
                Is.EqualTo(new Vector3(0.24f, 0.075f, 1f)));
            Assert.That(vertical.transform.localScale,
                Is.EqualTo(new Vector3(0.075f, 0.24f, 1f)));
            Assert.That(bolt.transform.localScale,
                Is.EqualTo(new Vector3(0.075f, 0.075f, 1f)));
            Assert.That(horizontal.color,
                Is.EqualTo(Color.Lerp(b2.InactiveConductor, b2.SecondaryBlue, 0.38f)));
            Assert.That(bolt.color, Is.EqualTo(b2.SecondaryBlue));
            Assert.That(horizontal.color, Is.Not.EqualTo(b2.Hint));
            Assert.That(FindPart(rotatable, "Lock Clamp Top Left"), Is.Null);
        }

        [Test]
        public void B2Hint_PreservesUnderlyingPoweredCircuitState()
        {
            CircuitTileView view = BuildTile(PoweredState(TileType.StraightWire));
            Color before = view.CurrentCircuitColor;
            bool powered = view.IsUnderlyingPowered;
            view.SetHintHighlighted(true);
            Assert.That(view.CurrentCircuitColor, Is.EqualTo(before));
            Assert.That(view.IsUnderlyingPowered, Is.EqualTo(powered));
            Assert.That(FindPart(view, "Hint Rim Top").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void B2LockedTreatment_RemainsSeparateFromHintRim()
        {
            CircuitTileView view = BuildTile(State(TileType.StraightWire, 0, false));
            SpriteRenderer clamp = FindPart(view, "Lock Clamp Top Left")
                .GetComponent<SpriteRenderer>();
            Color before = clamp.color;

            view.SetHintHighlighted(true);

            Assert.That(FindPart(view, "Hint Rim Top").gameObject.activeSelf, Is.True);
            Assert.That(clamp.gameObject.activeSelf, Is.True);
            Assert.That(clamp.color, Is.EqualTo(before));
            Assert.That(clamp.color, Is.Not.EqualTo(b2.Hint));
        }

        [Test]
        public void B2LockedTreatment_DoesNotAlterB1ClampGeometry()
        {
            CircuitTileView view = BuildTile(State(TileType.StraightWire, 0, false), b1);
            Transform clamp = FindPart(view, "Lock Clamp Top Left");

            Assert.That(clamp.localScale, Is.EqualTo(new Vector3(0.16f, 0.055f, 1f)));
            Assert.That(clamp.GetComponent<SpriteRenderer>().color,
                Is.EqualTo(b1.InactiveConductor));
            Assert.That(FindPart(view, "Lock Clamp Top Left Vertical"), Is.Null);
        }

        [Test]
        public void PrototypeDefinition_BindsB1B2PS01AndCG10()
        {
            GameplayVisualPrototypeDefinition definition = GameplayVisualPrototypeCatalog.Load();
            Assert.That(definition.IsConfigured, Is.True);
            Assert.That(definition.VisualTheme, Is.SameAs(b1));
            Assert.That(definition.ProductionVisualTheme, Is.SameAs(b2));
            Assert.That(definition.SimpleLevel.name, Is.EqualTo("PS_01"));
            Assert.That(definition.DenseLevel.name, Is.EqualTo("CG_10"));
        }

        [Test]
        public void PS01AndCG10_BuildWithB2WithoutChangingLevelData()
        {
            GameplayVisualPrototypeDefinition definition = GameplayVisualPrototypeCatalog.Load();
            BoardView ps01 = BuildBoard(definition.SimpleLevel, b2);
            BoardView cg10 = BuildBoard(definition.DenseLevel, b2);
            Assert.That(ps01.GetTileView(new GridPosition(0, 0)).CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.Source));
            Assert.That(ps01.GetTileView(new GridPosition(2, 0)).CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.LedObjectiveOff));
            Assert.That(cg10.GetComponentsInChildren<CircuitTileView>(),
                Has.All.Matches<CircuitTileView>(tile => tile.VisualTheme == b2));
            Assert.That(cg10.GetComponentsInChildren<CircuitTileView>()
                .Max(tile => tile.GetComponentsInChildren<Transform>(true).Length),
                Is.LessThan(70));
        }

        [TestCase("Levels/PowerStation/PS_01", 1080, 1920)]
        [TestCase("Levels/PowerStation/PS_01", 1080, 2340)]
        [TestCase("Levels/PowerStation/PS_01", 720, 1280)]
        [TestCase("Levels/CentralGrid/CG_10", 1080, 1920)]
        [TestCase("Levels/CentralGrid/CG_10", 1080, 2340)]
        [TestCase("Levels/CentralGrid/CG_10", 720, 1280)]
        public void B2Boards_RemainFramedAtAcceptedPortraitResolutions(string path,
            int width, int height)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(path);
            BoardView board = BuildBoard(level, b2);
            Physics2D.SyncTransforms();
            Bounds bounds = board.GetWorldBounds();
            BoardViewportFit fit = BoardViewportFitter.Calculate(bounds, width / (float)height,
                GameplayLayoutMetrics.BoardViewport, GameplayLayoutMetrics.BoardPaddingWorld);
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
        public void B2HudSkin_IsOptInAndProductionDefaultRemainsProgrammerArt()
        {
            LevelDefinition level = LoadPs01();
            var themedRoot = NewObject("B2 HUD");
            var themed = themedRoot.AddComponent<BoardController>();
            themed.Initialize(level, b2);
            GameplayHudView themedHud = themedRoot.GetComponent<GameplayHudView>();
            Assert.That(themedHud.UsesProductionSkin, Is.True);
            Assert.That(themedRoot.transform.Find("Gameplay HUD Canvas/Top HUD Keyline"),
                Is.Not.Null);
            Assert.That(themedRoot.transform.Find("Gameplay HUD Canvas/Restart Button")
                .GetComponent<Outline>(), Is.Not.Null);

            var productionRoot = NewObject("Production Default HUD");
            var production = productionRoot.AddComponent<BoardController>();
            production.Initialize(level);
            Assert.That(production.BoardView.UsesVisualTheme, Is.False);
            Assert.That(productionRoot.GetComponent<GameplayHudView>().UsesProductionSkin,
                Is.False);
            Assert.That(productionRoot.transform.Find("Gameplay HUD Canvas/Top HUD Keyline"),
                Is.Null);
            Assert.That(production.PerformPlayerAction(new GridPosition(1, 0)), Is.True);
            Assert.That(production.Session.IsCompleted, Is.True);
            Assert.That(production.IsCompletionPanelVisible, Is.True);
        }

        [Test]
        public void B2PrototypeCompletionHold_PreservesPoweredObjectiveAndCanRelease()
        {
            var root = NewObject("B2 Completion Hold");
            var controller = root.AddComponent<BoardController>();
            controller.Initialize(LoadPs01(), b2);
            controller.SetCompletionPresentationHeld(true);
            controller.PerformPlayerAction(new GridPosition(1, 0));
            Assert.That(controller.Session.IsCompleted, Is.True);
            Assert.That(controller.Session.Board.GetTile(new GridPosition(2, 0)).IsPowered,
                Is.True);
            Assert.That(controller.IsCompletionPanelVisible, Is.False);
            controller.SetCompletionPresentationHeld(false);
            Assert.That(controller.IsCompletionPanelVisible, Is.True);
        }

        [Test]
        public void B2Theme_IsReferencedOnlyByIsolatedPrototypeConfiguration()
        {
            string b2Guid = UnityEditor.AssetDatabase.AssetPathToGUID(
                "Assets/NeonGrid/Resources/VisualThemes/TechnicalNeonProductionPrototype.asset");
            string config = Path.Combine(Application.dataPath, "NeonGrid", "Resources",
                "VisualPrototypes", "M15_GameplayVisualPrototype.asset");
            string productionScene = Path.Combine(Application.dataPath, "NeonGrid", "Scenes",
                "M12_NeonGrid_Main.unity");
            string buildSettings = Path.Combine(Application.dataPath, "..", "ProjectSettings",
                "EditorBuildSettings.asset");
            Assert.That(File.ReadAllText(config), Does.Contain(b2Guid));
            Assert.That(File.ReadAllText(productionScene), Does.Not.Contain(b2Guid));
            Assert.That(File.ReadAllText(buildSettings), Does.Not.Contain(
                "M15_GameplayVisualPrototype.unity"));
        }

        private LevelDefinition LoadPs01() =>
            Resources.Load<LevelDefinition>("Levels/PowerStation/PS_01");

        private BoardView BuildBoard(LevelDefinition level, CircuitVisualThemeDefinition theme)
        {
            GameObject root = NewObject($"Board {level.name} {theme.ThemeId}");
            var board = root.AddComponent<BoardView>();
            board.Build(level.CreateBoardState(), _ => { }, theme);
            return board;
        }

        private CircuitTileView BuildPoweredObjective()
        {
            var board = new BoardState(2, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.OutputLamp, 0, false)
            });
            return BuildTile(new CircuitSimulation(board).Board.GetTile(new GridPosition(1, 0)));
        }

        private CircuitTileState PoweredState(TileType type)
        {
            if (type == TileType.PowerSource)
                return new CircuitSimulation(new BoardState(1, 1, new[]
                {
                    new TileDefinition(new GridPosition(0, 0), type, 0, false)
                })).Board.GetTile(new GridPosition(0, 0));
            var board = new BoardState(2, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), type, 1, false)
            });
            return new CircuitSimulation(board).Board.GetTile(new GridPosition(1, 0));
        }

        private CircuitTileView BuildTile(CircuitTileState state)
        {
            return BuildTile(state, b2);
        }

        private CircuitTileView BuildTile(CircuitTileState state,
            CircuitVisualThemeDefinition theme)
        {
            GameObject root = NewObject($"{theme.ThemeId} Tile {state.TileType}");
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

        private static Sprite SpriteFor(CircuitTileView view) =>
            view.GetComponentInChildren<SpriteRenderer>(true).sprite;

        private static SpriteRenderer[] Parts(CircuitTileView view, string name) =>
            view.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(part => part.gameObject.name == name).ToArray();

        private static Transform FindPart(CircuitTileView view, string name) =>
            view.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.gameObject.name == name);

        private static float MinDimension(SpriteRenderer part) =>
            Mathf.Min(part.transform.localScale.x, part.transform.localScale.y);

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
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }
    }
}

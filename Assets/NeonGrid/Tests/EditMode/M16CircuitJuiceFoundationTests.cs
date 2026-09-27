using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Editor;
using NeonGrid.Presentation;
using NeonGrid.Session;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class M16CircuitJuiceFoundationTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CircuitVisualThemeDefinition b2;
        private CircuitJuiceDefinition juice;

        [SetUp]
        public void SetUp()
        {
            b2 = CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype();
            juice = AssetDatabase.LoadAssetAtPath<CircuitJuiceDefinition>(
                M16CircuitJuicePrototypeBuilder.JuicePath);
            Assert.That(b2, Is.Not.Null);
            Assert.That(juice, Is.Not.Null);
            Assert.That(juice.IsConfigured, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void JuiceDisabledPreservesExactAcceptedB2Hierarchy()
        {
            BoardView view = BuildView(RotationLevel(), null);
            Assert.That(view.UsesProductionSkin, Is.True);
            Assert.That(view.UsesCircuitJuice, Is.False);
            Assert.That(view.GetComponent<CircuitJuiceCoordinator>(), Is.Null);
            Assert.That(view.GetComponentsInChildren<CircuitTileJuiceView>(true), Is.Empty);
            Assert.That(view.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name == "Juice Visual Root"), Is.False);
            Assert.That(view.GetTileView(new GridPosition(0, 0)).RotatingContentDegrees,
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void JuiceEnabledDoesNotChangeGameplayStateOrMoveAccounting()
        {
            LevelDefinition level = RotationLevel();
            BoardController controller = BuildController(level, juice);
            var reference = new GameplaySession(level, 8);
            cleanup.Add(reference.ActiveLevel == level ? null : reference.ActiveLevel);
            var action = new PuzzleAction(new GridPosition(0, 0),
                PuzzleActionType.RotateClockwise);

            Assert.That(controller.PerformPlayerAction(action.Position), Is.True);
            Assert.That(reference.PerformAction(action), Is.True);
            AssertBoardsEqual(reference.Board, controller.Session.Board);
            Assert.That(controller.Session.MoveCount, Is.EqualTo(reference.MoveCount));
            reference.Dispose();
        }

        [Test]
        public void AcceptedPressBeginsFeedbackAndRestoresCanonicalScaleExactly()
        {
            BoardView view = BuildView(RotationLevel(), juice);
            CircuitTileJuiceView tile = view.GetTileView(new GridPosition(0, 0)).JuiceView;
            view.PresentPressed(new GridPosition(0, 0));
            Assert.That(tile.LastEvent, Is.EqualTo(CircuitJuiceEventType.TilePressed));
            Assert.That(tile.IsAnimating, Is.True);
            view.JuiceCoordinator.Advance(juice.PressDuration * 0.5f);
            Assert.That(tile.CurrentScale.x, Is.LessThan(1f));
            view.JuiceCoordinator.Advance(juice.PressDuration + juice.PressSettleDuration);
            Assert.That(tile.IsAnimating, Is.False);
            Assert.That(tile.CurrentScale, Is.EqualTo(Vector3.one));
            Assert.That(tile.CurrentOffset, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void AcceptedRotationTargetsAuthoritativeCanonicalRotation()
        {
            BoardController controller = BuildController(RotationLevel(), juice);
            GridPosition position = new GridPosition(0, 0);
            Assert.That(controller.PerformPlayerAction(position), Is.True);
            CircuitTileJuiceView tile = controller.BoardView.GetTileView(position).JuiceView;
            Assert.That(tile.LastEvent, Is.EqualTo(CircuitJuiceEventType.RotationAccepted));
            Assert.That(controller.Session.Board.GetTile(position).Rotation, Is.EqualTo(1));
            controller.BoardView.JuiceCoordinator.Advance(juice.RotationDuration);
            Assert.That(tile.CurrentRotationDegrees, Is.EqualTo(-90f).Within(0.001f));
            Assert.That(tile.CanonicalRotationDegrees, Is.EqualTo(-90f));
        }

        [Test]
        public void FourRotationsAndRapidRetargetDoNotAccumulateAngleDrift()
        {
            BoardController controller = BuildController(RotationLevel(), juice);
            GridPosition position = new GridPosition(0, 0);
            for (int index = 0; index < 4; index++)
                Assert.That(controller.PerformPlayerAction(position), Is.True);
            CircuitTileJuiceView tile = controller.BoardView.GetTileView(position).JuiceView;
            Assert.That(controller.Session.Board.GetTile(position).Rotation, Is.Zero);
            controller.BoardView.JuiceCoordinator.Advance(juice.RotationDuration);
            Assert.That(tile.CurrentRotationDegrees, Is.Zero.Within(0.001f));
            Assert.That(tile.CanonicalRotationDegrees, Is.Zero);
            Assert.That(tile.VisualRoot.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void PowerActivationAndDeactivationFollowLogicWithoutOwningIt()
        {
            BoardController controller = BuildController(PowerTransitionLevel(), juice);
            GridPosition wirePosition = new GridPosition(1, 0);
            GridPosition lampPosition = new GridPosition(2, 0);

            Assert.That(controller.Session.Board.GetTile(lampPosition).IsPowered, Is.False);
            Assert.That(controller.PerformPlayerAction(wirePosition), Is.True);
            Assert.That(controller.Session.Board.GetTile(lampPosition).IsPowered, Is.True);
            Assert.That(controller.BoardView.GetTileView(lampPosition).JuiceView.LastEvent,
                Is.EqualTo(CircuitJuiceEventType.ObjectiveActivated));

            Assert.That(controller.PerformPlayerAction(wirePosition), Is.True);
            Assert.That(controller.Session.Board.GetTile(lampPosition).IsPowered, Is.False);
            Assert.That(controller.BoardView.GetTileView(lampPosition).JuiceView.LastEvent,
                Is.EqualTo(CircuitJuiceEventType.PowerDeactivated));
        }

        [Test]
        public void SourcePulseDoesNotAffectPropagationOrBoardState()
        {
            BoardController controller = BuildController(PowerTransitionLevel(), juice);
            var before = controller.Session.Board.AllTiles().Select(tile => (
                tile.Position, tile.TileType, tile.Rotation, tile.IsSwitchOn,
                tile.IsPowered, tile.EnergizedInputSides, tile.ActiveOutputSides)).ToArray();
            CircuitTileView source = controller.BoardView.GetTileView(new GridPosition(0, 0));
            source.PresentSourcePulse();
            Assert.That(source.JuiceView.LastEvent,
                Is.EqualTo(CircuitJuiceEventType.SourcePulse));
            var after = controller.Session.Board.AllTiles().Select(tile => (
                tile.Position, tile.TileType, tile.Rotation, tile.IsSwitchOn,
                tile.IsPowered, tile.EnergizedInputSides, tile.ActiveOutputSides)).ToArray();
            CollectionAssert.AreEqual(before, after);
        }

        [TestCase(TileType.AndGate)]
        [TestCase(TileType.OrGate)]
        public void SwitchAndGateFeedbackTrackAuthoritativeOutputWithoutChangingLogic(
            TileType gateType)
        {
            BoardController controller = BuildController(GateLevel(gateType), juice);
            GridPosition switchPosition = new GridPosition(1, 1);
            GridPosition gatePosition = new GridPosition(2, 1);
            Assert.That(controller.Session.Board.GetTile(gatePosition).ActiveOutputSides,
                Is.EqualTo(CardinalDirection.None));

            Assert.That(controller.PerformPlayerAction(switchPosition), Is.True);
            Assert.That(controller.Session.Board.GetTile(switchPosition).IsSwitchOn, Is.True);
            Assert.That(controller.BoardView.GetTileView(switchPosition).JuiceView.LastEvent,
                Is.EqualTo(CircuitJuiceEventType.SwitchChanged));
            Assert.That(controller.Session.Board.GetTile(gatePosition).ActiveOutputSides,
                Is.Not.EqualTo(CardinalDirection.None));
            Assert.That(controller.BoardView.GetTileView(gatePosition).JuiceView.LastEvent,
                Is.EqualTo(CircuitJuiceEventType.GateActivated));

            Assert.That(controller.PerformPlayerAction(switchPosition), Is.True);
            Assert.That(controller.Session.Board.GetTile(gatePosition).ActiveOutputSides,
                Is.EqualTo(CardinalDirection.None));
            Assert.That(controller.BoardView.GetTileView(gatePosition).JuiceView.LastEvent,
                Is.EqualTo(CircuitJuiceEventType.GateDeactivated));
        }

        [Test]
        public void LockedInteractionDoesNotIncrementMovesAndRestoresTransform()
        {
            LevelDefinition cg10 = Resources.Load<LevelDefinition>("Levels/CentralGrid/CG_10");
            BoardController controller = BuildController(cg10, juice);
            CircuitTileState locked = controller.Session.Board.AllTiles().First(state =>
                !state.IsRotatable && IsLockable(state.TileType));

            Assert.That(controller.PerformPlayerAction(locked.Position), Is.False);
            Assert.That(controller.Session.MoveCount, Is.Zero);
            CircuitTileJuiceView feedback = controller.BoardView.GetTileView(locked.Position)
                .JuiceView;
            Assert.That(feedback.LastEvent,
                Is.EqualTo(CircuitJuiceEventType.InteractionRejected));
            controller.BoardView.JuiceCoordinator.Advance(juice.RejectionDuration);
            Assert.That(feedback.CurrentScale, Is.EqualTo(Vector3.one));
            Assert.That(feedback.CurrentOffset, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void HintFeedbackPreservesSolverStateAndSemanticColor()
        {
            LevelDefinition level = RotationLevel();
            BoardController controller = BuildController(level, juice);
            GridPosition target = new GridPosition(0, 0);
            BoardPersistentSnapshot before = BoardPersistentSnapshot.Capture(
                controller.Session.Board);
            controller.BoardView.HighlightHint(target);
            CircuitTileView tile = controller.BoardView.GetTileView(target);
            Assert.That(tile.IsHintHighlighted, Is.True);
            Assert.That(tile.JuiceView.LastEvent,
                Is.EqualTo(CircuitJuiceEventType.HintTargeted));
            Assert.That(b2.Hint, Is.Not.EqualTo(b2.PoweredEnergy));
            AssertBoardsEqual(before.Restore(), controller.Session.Board);
            Assert.That(controller.Session.HintsUsed, Is.False);
        }

        [Test]
        public void UndoDuringAnimationRetargetsToAuthoritativeState()
        {
            BoardController controller = BuildController(RotationLevel(), juice);
            GridPosition position = new GridPosition(0, 0);
            Assert.That(controller.PerformPlayerAction(position), Is.True);
            controller.BoardView.JuiceCoordinator.Advance(juice.RotationDuration * 0.25f);
            Assert.That(controller.Undo(), Is.True);
            Assert.That(controller.Session.Board.GetTile(position).Rotation, Is.Zero);
            controller.BoardView.JuiceCoordinator.Advance(juice.RotationDuration);
            CircuitTileJuiceView tile = controller.BoardView.GetTileView(position).JuiceView;
            Assert.That(tile.CurrentRotationDegrees, Is.Zero.Within(0.001f));
            Assert.That(tile.CurrentScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void RestartCancelsAllTemporaryJuiceAndRestoresCanonicalPresentation()
        {
            BoardController controller = BuildController(RotationLevel(), juice);
            GridPosition position = new GridPosition(0, 0);
            Assert.That(controller.PerformPlayerAction(position), Is.True);
            controller.BoardView.JuiceCoordinator.Advance(juice.RotationDuration * 0.2f);
            controller.Restart();
            CircuitTileJuiceView tile = controller.BoardView.GetTileView(position).JuiceView;
            Assert.That(controller.Session.MoveCount, Is.Zero);
            Assert.That(controller.Session.Board.GetTile(position).Rotation, Is.Zero);
            Assert.That(controller.BoardView.JuiceCoordinator.ActiveAnimationCount, Is.Zero);
            Assert.That(tile.IsAnimating, Is.False);
            Assert.That(tile.CurrentScale, Is.EqualTo(Vector3.one));
            Assert.That(tile.CurrentOffset, Is.EqualTo(Vector3.zero));
            Assert.That(tile.CurrentRotationDegrees, Is.Zero.Within(0.001f));
            Assert.That(controller.BoardView.GetTileView(position).IsHintHighlighted, Is.False);
        }

        [Test]
        public void CompletionRemainsImmediateWhileFinalPresentationIsActive()
        {
            BoardController controller = BuildController(CompletableLevel(), juice);
            GridPosition position = new GridPosition(1, 0);
            int completionCount = 0;
            controller.Session.LevelCompleted += _ => completionCount++;
            Assert.That(controller.PerformPlayerAction(position), Is.True);
            Assert.That(controller.Session.IsCompleted, Is.True);
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(controller.IsCompletionPanelVisible, Is.True);
            Assert.That(controller.BoardView.JuiceCoordinator.ActiveAnimationCount,
                Is.GreaterThan(0));
            controller.BoardView.JuiceCoordinator.Advance(1f);
            Assert.That(controller.Session.IsCompleted, Is.True);
            Assert.That(completionCount, Is.EqualTo(1));
        }

        [Test]
        public void TutorialAnchorRemainsStableDuringAndAfterTileFeedback()
        {
            LevelDefinition level = RotationLevel();
            var tutorial = new LevelTutorialDefinition(new[]
            {
                new TutorialStepDefinition("Tap a wire to rotate it.",
                    new GridPosition(0, 0), TutorialCompletionCondition.RotateClockwise)
            });
            BoardController controller = BuildController(level, juice, tutorial);
            RectTransform panel = controller.transform
                .Find("Gameplay HUD Canvas/Tutorial Panel").GetComponent<RectTransform>();
            Vector2 anchored = panel.anchoredPosition;
            Vector3 tilePosition = controller.BoardView.GetTileView(new GridPosition(0, 0))
                .transform.localPosition;
            controller.BoardView.PresentPressed(new GridPosition(0, 0));
            controller.BoardView.JuiceCoordinator.Advance(juice.PressDuration * 0.5f);
            Assert.That(panel.anchoredPosition, Is.EqualTo(anchored));
            Assert.That(controller.BoardView.GetTileView(new GridPosition(0, 0))
                .transform.localPosition, Is.EqualTo(tilePosition));
            controller.BoardView.JuiceCoordinator.Advance(1f);
            Assert.That(panel.anchoredPosition, Is.EqualTo(anchored));
        }

        [Test]
        public void RepeatedRefreshDoesNotGrowHierarchyOrCreateMaterials()
        {
            LevelDefinition level = PowerTransitionLevel();
            BoardView baseline = BuildView(level, null);
            HashSet<int> baselineMaterials = baseline.GetComponentsInChildren<Renderer>(true)
                .SelectMany(item => item.sharedMaterials).Where(item => item != null)
                .Select(item => item.GetInstanceID()).ToHashSet();
            BoardView view = BuildView(level, juice);
            int transformCount = view.GetComponentsInChildren<Transform>(true).Length;
            for (int index = 0; index < 20; index++)
                view.Refresh(level.CreateBoardState(), CircuitJuiceTransition.Synchronize);
            Assert.That(view.GetComponentsInChildren<Transform>(true),
                Has.Length.EqualTo(transformCount));
            HashSet<int> juiceMaterials = view.GetComponentsInChildren<Renderer>(true)
                .SelectMany(item => item.sharedMaterials).Where(item => item != null)
                .Select(item => item.GetInstanceID()).ToHashSet();
            Assert.That(juiceMaterials, Is.SubsetOf(baselineMaterials));
        }

        [Test]
        public void SaveSchemaContainsNoJuiceOrPresentationState()
        {
            string[] fieldNames = typeof(CampaignSaveData)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.Name).ToArray();
            Assert.That(fieldNames, Has.None.Contains("juice"));
            Assert.That(fieldNames, Has.None.Contains("motion"));
            Assert.That(fieldNames, Has.None.Contains("animation"));
            Assert.That(typeof(GameplaySession).GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(CircuitJuiceDefinition)), Is.False);
        }

        [Test]
        public void MotionScaleZeroProducesStableNonAnimatedPresentation()
        {
            CircuitJuiceDefinition reduced = ScriptableObject.CreateInstance<CircuitJuiceDefinition>();
            reduced.SetMotionScale(0f);
            cleanup.Add(reduced);
            BoardController controller = BuildController(RotationLevel(), reduced);
            GridPosition position = new GridPosition(0, 0);
            Assert.That(controller.PerformPlayerAction(position), Is.True);
            CircuitTileJuiceView tile = controller.BoardView.GetTileView(position).JuiceView;
            Assert.That(tile.IsAnimating, Is.False);
            Assert.That(controller.BoardView.JuiceCoordinator.ActiveAnimationCount, Is.Zero);
            Assert.That(tile.CurrentScale, Is.EqualTo(Vector3.one));
            Assert.That(tile.CurrentOffset, Is.EqualTo(Vector3.zero));
            Assert.That(tile.CurrentRotationDegrees, Is.EqualTo(-90f).Within(0.001f));
        }

        [Test]
        public void DenseThirtySixTileBoardHasOneCoordinatorAndBoundedHierarchy()
        {
            LevelDefinition cg10 = Resources.Load<LevelDefinition>("Levels/CentralGrid/CG_10");
            BoardView baseline = BuildView(cg10, null);
            int baselineObjects = baseline.GetComponentsInChildren<Transform>(true).Length;
            BoardView view = BuildView(cg10, juice);
            Assert.That(cg10.Width * cg10.Height, Is.EqualTo(36));
            Assert.That(view.JuiceCoordinator.RegisteredTileCount, Is.EqualTo(36));
            Assert.That(view.GetComponentsInChildren<CircuitTileJuiceView>(true),
                Has.Length.EqualTo(36));
            Assert.That(view.GetComponentsInChildren<Transform>(true),
                Has.Length.EqualTo(baselineObjects + 36));
            Assert.That(view.GetComponentsInChildren<CircuitJuiceCoordinator>(true),
                Has.Length.EqualTo(1));
            Assert.That(view.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name == "Juice Visual Root"), Is.EqualTo(36));
        }

        [Test]
        public void PrototypeIsOptInAndProductionRemainsUnbound()
        {
            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            Assert.That(prototype.CircuitJuice, Is.SameAs(juice));
            Assert.That(campaign.GameplayVisualTheme, Is.SameAs(b2));
            Assert.That(typeof(CampaignDefinition).GetFields(BindingFlags.Instance |
                BindingFlags.NonPublic).Any(field =>
                field.FieldType == typeof(CircuitJuiceDefinition)), Is.False);
            BoardController production = BuildController(
                campaign.Chapters[0].Levels[0].LevelDefinition, null);
            Assert.That(production.BoardView.UsesCircuitJuice, Is.False);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo("Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity"));
        }

        [Test]
        public void BuilderIsIdempotentAndPrototypeDefinitionRemainsValid()
        {
            M16CircuitJuicePrototypeBuilder.Build();
            byte[] first = File.ReadAllBytes(M16CircuitJuicePrototypeBuilder.PrototypePath);
            M16CircuitJuicePrototypeBuilder.Build();
            byte[] second = File.ReadAllBytes(M16CircuitJuicePrototypeBuilder.PrototypePath);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(GameplayVisualPrototypeCatalog.Load().IsConfigured, Is.True);
            Assert.That(GameplayVisualPrototypeCatalog.Load().CircuitJuice.IsConfigured, Is.True);
        }

        [Test]
        public void JuiceUsesOneBoardUpdateAndNoPerTileUpdateMethod()
        {
            MethodInfo coordinatorUpdate = typeof(CircuitJuiceCoordinator).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo tileUpdate = typeof(CircuitTileJuiceView).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(coordinatorUpdate, Is.Not.Null);
            Assert.That(tileUpdate, Is.Null);
        }

        private BoardController BuildController(LevelDefinition level,
            CircuitJuiceDefinition definition, LevelTutorialDefinition tutorial = null)
        {
            GameObject root = NewObject("M16 A1 Board Controller");
            var controller = root.AddComponent<BoardController>();
            controller.Initialize(new GameplaySession(level, 8), null, tutorial, null, b2,
                definition);
            return controller;
        }

        private BoardView BuildView(LevelDefinition level, CircuitJuiceDefinition definition)
        {
            GameObject root = NewObject("M16 A1 Board View");
            var view = root.AddComponent<BoardView>();
            view.Build(level.CreateBoardState(), _ => { }, b2, definition);
            return view;
        }

        private GameObject NewObject(string name)
        {
            var result = new GameObject(name);
            cleanup.Add(result);
            return result;
        }

        private LevelDefinition RotationLevel()
        {
            return Level(2, 1,
                new TileDefinition(new GridPosition(0, 0), TileType.StraightWire, 0, true),
                new TileDefinition(new GridPosition(1, 0), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition PowerTransitionLevel()
        {
            return Level(3, 2,
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 0, true),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false),
                new TileDefinition(new GridPosition(0, 1), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition CompletableLevel()
        {
            return Level(3, 1,
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 0, true),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition GateLevel(TileType gateType)
        {
            return Level(4, 3,
                new TileDefinition(new GridPosition(0, 1), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 1), TileType.Switch, 0, false, false),
                new TileDefinition(new GridPosition(2, 1), gateType, 0, false),
                gateType == TileType.AndGate
                    ? new TileDefinition(new GridPosition(3, 1), TileType.PowerSource, 2, false)
                    : new TileDefinition(new GridPosition(3, 1), TileType.Empty, 0, false),
                new TileDefinition(new GridPosition(2, 2), TileType.OutputLamp, 3, false),
                new TileDefinition(new GridPosition(0, 2), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition Level(int width, int height, params TileDefinition[] tiles)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.SetData(width, height, tiles);
            cleanup.Add(level);
            return level;
        }

        private static bool IsLockable(TileType type)
        {
            return type == TileType.StraightWire || type == TileType.CornerWire ||
                   type == TileType.TJunction || type == TileType.CrossJunction ||
                   type == TileType.Diode || type == TileType.AndGate ||
                   type == TileType.OrGate;
        }

        private static void AssertBoardsEqual(BoardState expected, BoardState actual)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));
            foreach (CircuitTileState expectedTile in expected.AllTiles())
            {
                CircuitTileState actualTile = actual.GetTile(expectedTile.Position);
                Assert.That(actualTile.TileType, Is.EqualTo(expectedTile.TileType));
                Assert.That(actualTile.Rotation, Is.EqualTo(expectedTile.Rotation));
                Assert.That(actualTile.IsSwitchOn, Is.EqualTo(expectedTile.IsSwitchOn));
                Assert.That(actualTile.IsPowered, Is.EqualTo(expectedTile.IsPowered));
                Assert.That(actualTile.EnergizedInputSides,
                    Is.EqualTo(expectedTile.EnergizedInputSides));
                Assert.That(actualTile.ActiveOutputSides,
                    Is.EqualTo(expectedTile.ActiveOutputSides));
            }
        }
    }
}

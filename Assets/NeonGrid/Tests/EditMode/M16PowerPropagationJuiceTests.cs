using System.Collections.Generic;
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
    public sealed class M16PowerPropagationJuiceTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private CircuitVisualThemeDefinition theme;
        private CircuitJuiceDefinition juice;

        [SetUp]
        public void SetUp()
        {
            theme = CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype();
            juice = AssetDatabase.LoadAssetAtPath<CircuitJuiceDefinition>(
                M16CircuitJuicePrototypeBuilder.JuicePath);
            Assert.That(theme, Is.Not.Null);
            Assert.That(juice, Is.Not.Null);
            Assert.That(juice.IsConfigured, Is.True);
            Assert.That(juice.PropagationFeedback, Is.True);
            Assert.That(juice.CompletionFeedback, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void PlanUsesOnlyAuthoritativePoweredTilesAndElectricalDistance()
        {
            BoardState board = Simulate(PoweredLineLevel());
            var target = board.AllTiles().Select(tile => tile.Position).ToHashSet();
            PropagationPresentationPlan plan = PropagationPresentationPlan.Create(board,
                target, juice);

            Assert.That(plan.TileCount, Is.EqualTo(3));
            Assert.That(plan.Steps.Select(step => step.Depth), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(plan.Steps.SelectMany(step => step.Positions)
                .All(position => board.GetTile(position).IsPowered), Is.True);
            Assert.That(plan.Steps.SelectMany(step => step.Positions),
                Has.None.EqualTo(new GridPosition(0, 1)));
        }

        [Test]
        public void EqualDepthBranchesShareOnePresentationStep()
        {
            BoardState board = Simulate(BranchedLevel());
            var target = board.AllTiles().Where(tile => tile.IsPowered)
                .Select(tile => tile.Position).ToHashSet();
            PropagationPresentationPlan plan = PropagationPresentationPlan.Create(board,
                target, juice);
            PropagationPresentationStep branch = plan.Steps.Single(step => step.Depth == 3);
            Assert.That(branch.Positions, Is.EquivalentTo(new[]
            {
                new GridPosition(0, 2), new GridPosition(2, 2)
            }));
        }

        [Test]
        public void DiodeDirectionIsRespectedByPresentationDistance()
        {
            BoardState forward = Simulate(DiodeLevel(false));
            var forwardTargets = forward.AllTiles().Where(tile => tile.IsPowered)
                .Select(tile => tile.Position).ToHashSet();
            PropagationPresentationPlan forwardPlan = PropagationPresentationPlan.Create(
                forward, forwardTargets, juice);
            Assert.That(forwardPlan.TileCount, Is.EqualTo(3));

            BoardState reverse = Simulate(DiodeLevel(true));
            var requested = reverse.AllTiles().Select(tile => tile.Position).ToHashSet();
            PropagationPresentationPlan reversePlan = PropagationPresentationPlan.Create(
                reverse, requested, juice);
            Assert.That(reverse.GetTile(new GridPosition(1, 0)).IsPowered, Is.False);
            Assert.That(reversePlan.Steps.SelectMany(step => step.Positions),
                Has.None.EqualTo(new GridPosition(1, 0)));
        }

        [Test]
        public void AuthoritativePowerIsFinalBeforeVisualWaveFinishes()
        {
            BoardController controller = BuildController(TransitionLevel());
            GridPosition wire = new GridPosition(1, 0);
            Assert.That(controller.PerformPlayerAction(wire), Is.True);
            Assert.That(controller.Session.Board.GetTile(new GridPosition(2, 0)).IsPowered,
                Is.True);
            Assert.That(controller.Session.IsCompleted, Is.True);
            Assert.That(controller.BoardView.GetTileView(wire).JuiceView.PowerPresentationAmount,
                Is.Zero.Within(0.001f));
            Assert.That(controller.BoardView.JuiceCoordinator.PendingPropagationStepCount,
                Is.GreaterThan(0));
        }

        [Test]
        public void SourceStartsBeforeDownstreamAndActivationSettlesToB2()
        {
            BoardController controller = BuildController(TransitionLevel());
            controller.PerformPlayerAction(new GridPosition(1, 0));
            CircuitTileJuiceView source = controller.BoardView
                .GetTileView(new GridPosition(0, 0)).JuiceView;
            CircuitTileJuiceView wire = controller.BoardView
                .GetTileView(new GridPosition(1, 0)).JuiceView;
            Assert.That(source.LastEvent, Is.EqualTo(CircuitJuiceEventType.SourcePulse));
            Assert.That(wire.PowerPresentationAmount, Is.Zero.Within(0.001f));

            controller.BoardView.JuiceCoordinator.Advance(juice.PropagationDepthDelay);
            Assert.That(wire.PowerPresentationAmount, Is.Zero.Within(0.001f));
            controller.BoardView.JuiceCoordinator.Advance(
                juice.PropagationActivationDuration + juice.MaximumPropagationDelay + 1f);
            Assert.That(wire.PowerPresentationAmount, Is.EqualTo(1f).Within(0.001f));
            Assert.That(wire.SuccessPresentationAmount, Is.Zero.Within(0.001f));
        }

        [Test]
        public void DeactivationIsImmediateLogicalAndSettlesExactlyUnpowered()
        {
            BoardController controller = BuildController(TransitionLevel());
            GridPosition wirePosition = new GridPosition(1, 0);
            controller.PerformPlayerAction(wirePosition);
            controller.BoardView.JuiceCoordinator.Advance(2f);
            controller.Restart();
            controller.PerformPlayerAction(wirePosition);
            controller.BoardView.JuiceCoordinator.Advance(2f);
            Assert.That(controller.PerformPlayerAction(wirePosition), Is.False,
                "Completed attempts are intentionally non-interactive.");

            BoardController breakable = BuildController(BreakableLevel());
            GridPosition breakWire = new GridPosition(1, 0);
            Assert.That(breakable.Session.Board.GetTile(new GridPosition(2, 0)).IsPowered,
                Is.True);
            Assert.That(breakable.PerformPlayerAction(breakWire), Is.True);
            Assert.That(breakable.Session.Board.GetTile(new GridPosition(2, 0)).IsPowered,
                Is.False);
            CircuitTileJuiceView lamp = breakable.BoardView
                .GetTileView(new GridPosition(2, 0)).JuiceView;
            Assert.That(lamp.LastEvent, Is.EqualTo(CircuitJuiceEventType.PowerDeactivated));
            breakable.BoardView.JuiceCoordinator.Advance(juice.PropagationDeactivationDuration);
            Assert.That(lamp.PowerPresentationAmount, Is.EqualTo(1f));
            Assert.That(lamp.CurrentScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void RapidNewActionCancelsStaleWaveAndLeavesNoStaleCyanState()
        {
            BoardController controller = BuildController(BreakableLevel(false));
            GridPosition wire = new GridPosition(1, 0);
            controller.PerformPlayerAction(wire);
            Assert.That(controller.BoardView.JuiceCoordinator.PendingPropagationTileCount,
                Is.GreaterThan(0));
            controller.PerformPlayerAction(wire);
            Assert.That(controller.Session.Board.GetTile(new GridPosition(2, 0)).IsPowered,
                Is.False);
            Assert.That(controller.BoardView.JuiceCoordinator.PendingPropagationTileCount,
                Is.Zero);
            controller.BoardView.JuiceCoordinator.Advance(2f);
            CircuitTileJuiceView lamp = controller.BoardView
                .GetTileView(new GridPosition(2, 0)).JuiceView;
            Assert.That(lamp.PowerPresentationAmount, Is.EqualTo(1f));
            Assert.That(lamp.IsAnimating, Is.False);
        }

        [Test]
        public void UndoCancelsOldPlanAndTargetsRestoredAuthoritativeState()
        {
            BoardController controller = BuildController(BreakableLevel(false));
            GridPosition wire = new GridPosition(1, 0);
            controller.PerformPlayerAction(wire);
            Assert.That(controller.Undo(), Is.True);
            Assert.That(controller.Session.Board.GetTile(new GridPosition(2, 0)).IsPowered,
                Is.False);
            controller.BoardView.JuiceCoordinator.Advance(2f);
            Assert.That(controller.BoardView.GetTileView(new GridPosition(2, 0))
                .JuiceView.PowerPresentationAmount, Is.EqualTo(1f));
        }

        [Test]
        public void RestartCancelsPropagationAndCompletionWork()
        {
            BoardController controller = BuildController(TransitionLevel());
            controller.PerformPlayerAction(new GridPosition(1, 0));
            Assert.That(controller.BoardView.JuiceCoordinator.HasPendingCompletion, Is.True);
            controller.Restart();
            Assert.That(controller.BoardView.JuiceCoordinator.PendingPropagationStepCount,
                Is.Zero);
            Assert.That(controller.BoardView.JuiceCoordinator.HasPendingCompletion, Is.False);
            Assert.That(controller.BoardView.JuiceCoordinator.ActiveAnimationCount, Is.Zero);
            controller.BoardView.JuiceCoordinator.Advance(2f);
            Assert.That(controller.Session.IsCompleted, Is.False);
        }

        [TestCase(TileType.Switch)]
        [TestCase(TileType.AndGate)]
        [TestCase(TileType.OrGate)]
        public void StatefulComponentsScheduleDownstreamOnlyAfterAuthoritativeOutput(
            TileType type)
        {
            BoardController controller = BuildController(StatefulLevel(type));
            GridPosition action = type == TileType.Switch
                ? new GridPosition(1, 0)
                : new GridPosition(1, 1);
            GridPosition component = type == TileType.Switch
                ? action
                : new GridPosition(2, 1);
            GridPosition output = type == TileType.Switch
                ? new GridPosition(2, 0)
                : new GridPosition(2, 2);
            Assert.That(controller.Session.Board.GetTile(component).ActiveOutputSides,
                Is.EqualTo(CardinalDirection.None));
            controller.PerformPlayerAction(action);
            Assert.That(controller.Session.Board.GetTile(component).ActiveOutputSides,
                Is.Not.EqualTo(CardinalDirection.None));
            Assert.That(controller.Session.Board.GetTile(output).IsPowered, Is.True);
            Assert.That(controller.BoardView.JuiceCoordinator.LastPropagationPlan.Steps
                .SelectMany(step => step.Positions), Has.Member(output));
        }

        [Test]
        public void HintStateAndGoldSemanticRemainUnchangedDuringWave()
        {
            BoardController controller = BuildController(BreakableLevel(false));
            controller.PerformPlayerAction(new GridPosition(1, 0));
            GridPosition target = new GridPosition(1, 0);
            bool hintsUsed = controller.Session.HintsUsed;
            controller.BoardView.HighlightHint(target);
            controller.BoardView.JuiceCoordinator.Advance(juice.PropagationDepthDelay);
            Assert.That(theme.Hint, Is.Not.EqualTo(theme.PoweredEnergy));
            Assert.That(controller.BoardView.GetTileView(target).IsHintHighlighted, Is.True);
            Assert.That(controller.Session.HintsUsed, Is.EqualTo(hintsUsed));
        }

        [Test]
        public void CompletionIsImmediateButSuccessPulseWaitsForAuthoritativeEventAndWave()
        {
            BoardController controller = BuildController(TransitionLevel());
            CircuitJuiceCoordinator coordinator = controller.BoardView.JuiceCoordinator;
            Assert.That(coordinator.HasPendingCompletion, Is.False);
            controller.PerformPlayerAction(new GridPosition(1, 0));
            Assert.That(controller.Session.IsCompleted, Is.True);
            Assert.That(controller.IsCompletionPanelVisible, Is.True);
            Assert.That(coordinator.CompletionPulseStarted, Is.False);
            Assert.That(coordinator.HasPendingCompletion, Is.True);
            coordinator.Advance(coordinator.MaximumScheduledDelay);
            Assert.That(coordinator.CompletionPulseStarted, Is.True);
            coordinator.Advance(juice.CompletionPulseDuration * 0.5f);
            Assert.That(controller.BoardView.GetTileView(new GridPosition(0, 0))
                .JuiceView.SuccessPresentationAmount, Is.GreaterThan(0f));
        }

        [Test]
        public void CompletionPulseIsPresentationOnlyAndReturnsExactlyCanonical()
        {
            BoardController controller = BuildController(TransitionLevel());
            controller.PerformPlayerAction(new GridPosition(1, 0));
            int moves = controller.Session.MoveCount;
            controller.BoardView.JuiceCoordinator.Advance(2f);
            foreach (CircuitTileJuiceView tile in controller.BoardView
                         .GetComponentsInChildren<CircuitTileJuiceView>(true))
            {
                Assert.That(tile.SuccessPresentationAmount, Is.Zero.Within(0.001f));
                Assert.That(tile.PowerPresentationAmount, Is.EqualTo(1f).Within(0.001f));
                Assert.That(tile.CurrentScale, Is.EqualTo(Vector3.one));
                Assert.That(tile.CurrentOffset, Is.EqualTo(Vector3.zero));
            }
            Assert.That(controller.Session.MoveCount, Is.EqualTo(moves));
            Assert.That(controller.Session.IsCompleted, Is.True);
        }

        [Test]
        public void MultipleObjectivesRemainAuthoritativeAndShareNaturalArrival()
        {
            BoardState board = Simulate(BranchedLevel());
            var targets = board.AllTiles().Where(tile => tile.TileType == TileType.OutputLamp)
                .Select(tile => tile.Position).ToHashSet();
            PropagationPresentationPlan plan = PropagationPresentationPlan.Create(board,
                targets, juice);
            Assert.That(CircuitCompletion.IsCompleted(board), Is.True);
            Assert.That(plan.Steps, Has.Count.EqualTo(1));
            Assert.That(plan.Steps[0].Positions, Has.Count.EqualTo(2));
        }

        [Test]
        public void MotionScaleZeroRemovesTravelAndSuccessMotion()
        {
            CircuitJuiceDefinition reduced = ScriptableObject.CreateInstance<CircuitJuiceDefinition>();
            reduced.SetMotionScale(0f);
            cleanup.Add(reduced);
            BoardController controller = BuildController(TransitionLevel(), reduced);
            controller.PerformPlayerAction(new GridPosition(1, 0));
            Assert.That(controller.BoardView.JuiceCoordinator.PendingPropagationStepCount,
                Is.Zero);
            Assert.That(controller.BoardView.JuiceCoordinator.ActiveAnimationCount, Is.Zero);
            Assert.That(controller.BoardView.JuiceCoordinator.MaximumScheduledDelay, Is.Zero);
            foreach (CircuitTileJuiceView tile in controller.BoardView
                         .GetComponentsInChildren<CircuitTileJuiceView>(true))
            {
                Assert.That(tile.PowerPresentationAmount, Is.EqualTo(1f));
                Assert.That(tile.SuccessPresentationAmount, Is.Zero);
            }
        }

        [Test]
        public void Cg10ScheduleIsBoundedAndUsesOneCoordinator()
        {
            LevelDefinition cg10 = Resources.Load<LevelDefinition>("Levels/CentralGrid/CG_10");
            BoardState board = Simulate(cg10);
            var targets = board.AllTiles().Where(tile => tile.IsPowered)
                .Select(tile => tile.Position).ToHashSet();
            PropagationPresentationPlan plan = PropagationPresentationPlan.Create(board,
                targets, juice);
            Assert.That(plan.MaximumDelaySeconds,
                Is.LessThanOrEqualTo(juice.MaximumPropagationDelay + 0.0001f));
            Assert.That(plan.MaximumDelaySeconds + juice.PropagationActivationDuration,
                Is.LessThan(0.65f));
            BoardController controller = BuildController(cg10);
            Assert.That(controller.BoardView.GetComponentsInChildren<CircuitJuiceCoordinator>(true),
                Has.Length.EqualTo(1));
            Assert.That(controller.BoardView.JuiceCoordinator.RegisteredTileCount,
                Is.EqualTo(36));
        }

        [Test]
        public void RepeatedWavesDoNotGrowHierarchyOrCreateMaterials()
        {
            BoardController controller = BuildController(BreakableLevel(false));
            int transforms = controller.GetComponentsInChildren<Transform>(true).Length;
            HashSet<int> materials = controller.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null).Select(material => material.GetInstanceID())
                .ToHashSet();
            GridPosition wire = new GridPosition(1, 0);
            for (int index = 0; index < 12; index++)
            {
                controller.PerformPlayerAction(wire);
                controller.BoardView.JuiceCoordinator.Advance(0.02f);
            }
            controller.BoardView.JuiceCoordinator.Advance(2f);
            Assert.That(controller.GetComponentsInChildren<Transform>(true),
                Has.Length.EqualTo(transforms));
            HashSet<int> after = controller.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null).Select(material => material.GetInstanceID())
                .ToHashSet();
            Assert.That(after, Is.EqualTo(materials));
        }

        [Test]
        public void ProductionAndSaveSchemaRemainUnbound()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            Assert.That(prototype.CircuitJuice, Is.SameAs(juice));
            Assert.That(typeof(CampaignDefinition).GetFields(BindingFlags.Instance |
                BindingFlags.NonPublic).Any(field =>
                field.FieldType == typeof(CircuitJuiceDefinition)), Is.False);
            Assert.That(typeof(CampaignSaveData).GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic).Select(field => field.Name),
                Has.None.Contains("propagation"));
            GameObject root = NewObject("M16 A2 Production Reference");
            var production = root.AddComponent<BoardController>();
            production.Initialize(campaign.Chapters[0].Levels[0].LevelDefinition, theme);
            Assert.That(production.BoardView.UsesCircuitJuice, Is.False);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo("Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity"));
        }

        [Test]
        public void A1RotationLockedFeedbackAndTutorialAnchorsRemainCompatible()
        {
            LevelDefinition level = BreakableLevel(false);
            var tutorial = new LevelTutorialDefinition(new[]
            {
                new TutorialStepDefinition("Tap a wire to rotate it.",
                    new GridPosition(1, 0), TutorialCompletionCondition.RotateClockwise)
            });
            BoardController controller = BuildController(level, juice, tutorial);
            RectTransform panel = controller.transform
                .Find("Gameplay HUD Canvas/Tutorial Panel").GetComponent<RectTransform>();
            Vector2 anchor = panel.anchoredPosition;
            GridPosition wire = new GridPosition(1, 0);
            controller.BoardView.PresentPressed(wire);
            controller.BoardView.JuiceCoordinator.Advance(juice.PressDuration * 0.5f);
            Assert.That(panel.anchoredPosition, Is.EqualTo(anchor));
            for (int index = 0; index < 4; index++) controller.PerformPlayerAction(wire);
            controller.BoardView.JuiceCoordinator.Advance(2f);
            Assert.That(controller.BoardView.GetTileView(wire).JuiceView.CurrentRotationDegrees,
                Is.Zero.Within(0.001f));

            LevelDefinition cg10 = Resources.Load<LevelDefinition>("Levels/CentralGrid/CG_10");
            BoardController lockedController = BuildController(cg10);
            CircuitTileState locked = lockedController.Session.Board.AllTiles().First(tile =>
                !tile.IsRotatable && tile.TileType != TileType.Empty &&
                tile.TileType != TileType.PowerSource && tile.TileType != TileType.OutputLamp &&
                tile.TileType != TileType.Switch);
            Assert.That(lockedController.PerformPlayerAction(locked.Position), Is.False);
            Assert.That(lockedController.Session.MoveCount, Is.Zero);
            Assert.That(lockedController.BoardView.GetTileView(locked.Position).JuiceView.LastEvent,
                Is.EqualTo(CircuitJuiceEventType.InteractionRejected));
        }

        [Test]
        public void CompletionDuringRotationAndRepeatedRestartCyclesSettleWithoutStaleEvents()
        {
            BoardController controller = BuildController(TransitionLevel());
            GridPosition wire = new GridPosition(1, 0);
            for (int cycle = 0; cycle < 4; cycle++)
            {
                Assert.That(controller.PerformPlayerAction(wire), Is.True);
                Assert.That(controller.Session.IsCompleted, Is.True);
                controller.BoardView.JuiceCoordinator.Advance(0.04f);
                controller.Restart();
                Assert.That(controller.BoardView.JuiceCoordinator.HasPendingCompletion, Is.False);
                Assert.That(controller.BoardView.JuiceCoordinator.PendingPropagationStepCount,
                    Is.Zero);
            }
            controller.BoardView.JuiceCoordinator.Advance(2f);
            CircuitTileJuiceView view = controller.BoardView.GetTileView(wire).JuiceView;
            Assert.That(view.CurrentRotationDegrees, Is.Zero.Within(0.001f));
            Assert.That(view.CurrentScale, Is.EqualTo(Vector3.one));
            Assert.That(view.PowerPresentationAmount, Is.EqualTo(1f));
        }

        private BoardController BuildController(LevelDefinition level,
            CircuitJuiceDefinition definition = null, LevelTutorialDefinition tutorial = null)
        {
            GameObject root = NewObject("M16 A2 Board Controller");
            var controller = root.AddComponent<BoardController>();
            controller.Initialize(new GameplaySession(level, 8), null, tutorial, null, theme,
                definition ?? juice);
            return controller;
        }

        private BoardState Simulate(LevelDefinition level)
        {
            BoardState board = level.CreateBoardState();
            _ = new CircuitSimulation(board);
            return board;
        }

        private GameObject NewObject(string name)
        {
            var result = new GameObject(name);
            cleanup.Add(result);
            return result;
        }

        private LevelDefinition PoweredLineLevel()
        {
            return Level(3, 2,
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 1, false),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false),
                new TileDefinition(new GridPosition(0, 1), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition TransitionLevel()
        {
            return Level(3, 1,
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 0, true),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition BreakableLevel(bool startsPowered = true)
        {
            return Level(3, 2,
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire,
                    startsPowered ? 1 : 0, true),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false),
                new TileDefinition(new GridPosition(0, 1), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition BranchedLevel()
        {
            return Level(3, 3,
                new TileDefinition(new GridPosition(1, 0), TileType.PowerSource, 3, false),
                new TileDefinition(new GridPosition(1, 1), TileType.StraightWire, 0, false),
                new TileDefinition(new GridPosition(1, 2), TileType.TJunction, 2, false),
                new TileDefinition(new GridPosition(0, 2), TileType.OutputLamp, 2, false),
                new TileDefinition(new GridPosition(2, 2), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition DiodeLevel(bool reverse)
        {
            return reverse
                ? Level(3, 1,
                    new TileDefinition(new GridPosition(2, 0), TileType.PowerSource, 2, false),
                    new TileDefinition(new GridPosition(1, 0), TileType.Diode, 0, false),
                    new TileDefinition(new GridPosition(0, 0), TileType.OutputLamp, 2, false))
                : Level(3, 1,
                    new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                    new TileDefinition(new GridPosition(1, 0), TileType.Diode, 0, false),
                    new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition StatefulLevel(TileType type)
        {
            if (type == TileType.Switch)
                return Level(3, 2,
                    new TileDefinition(new GridPosition(0, 1), TileType.OutputLamp, 0, false),
                    new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                    new TileDefinition(new GridPosition(1, 0), TileType.Switch, 0, false, false),
                    new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false));
            return Level(4, 3,
                new TileDefinition(new GridPosition(0, 1), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 1), TileType.Switch, 0, false, false),
                new TileDefinition(new GridPosition(2, 1), type, 0, false),
                type == TileType.AndGate
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
    }
}

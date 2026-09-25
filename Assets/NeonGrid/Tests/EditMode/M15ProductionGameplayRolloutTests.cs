using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Session;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class M15ProductionGameplayRolloutTests
    {
        private static readonly string[] VerticalSliceCampaigns =
        {
            "PowerStation_VerticalSlice",
            "Substation_VerticalSlice",
            "ControlCenter_VerticalSlice",
            "AutomationPlant_VerticalSlice",
            "CentralGrid_VerticalSlice"
        };

        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CampaignDefinition campaign;
        private CircuitVisualThemeDefinition b1;
        private CircuitVisualThemeDefinition b2;

        [SetUp]
        public void SetUp()
        {
            campaign = Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
            b1 = CircuitVisualThemeCatalog.LoadTechnicalNeonPrototype();
            b2 = CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype();
            Assert.That(campaign, Is.Not.Null);
            Assert.That(b1, Is.Not.Null);
            Assert.That(b2, Is.Not.Null);
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
        public void ThemeResolution_IsExplicitForProductionAndPreservesFallbacksAndPrototype()
        {
            Assert.That(campaign.CampaignId, Is.EqualTo("neon_grid_main"));
            Assert.That(campaign.GameplayVisualTheme, Is.SameAs(b2));
            Assert.That(campaign.GameplayVisualTheme.UsesProductionTreatment, Is.True);

            foreach (string resourceName in VerticalSliceCampaigns)
            {
                CampaignDefinition verticalSlice = Resources.Load<CampaignDefinition>(
                    $"Campaigns/{resourceName}");
                Assert.That(verticalSlice, Is.Not.Null, resourceName);
                Assert.That(verticalSlice.GameplayVisualTheme, Is.Null,
                    $"{resourceName} must retain programmer-art fallback.");
            }

            BoardView fallback = BuildBoard(campaign.Chapters[0].Levels[0].LevelDefinition, null);
            Assert.That(fallback.UsesVisualTheme, Is.False);
            Assert.That(fallback.GetComponentsInChildren<CircuitTileView>(true),
                Has.All.Matches<CircuitTileView>(tile => !tile.IsUsingVisualTheme));

            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            Assert.That(prototype.VisualTheme, Is.SameAs(b1));
            Assert.That(prototype.ProductionVisualTheme, Is.SameAs(b2));
            Assert.That(prototype.SimpleLevel.name, Is.EqualTo("PS_01"));
            Assert.That(prototype.DenseLevel.name, Is.EqualTo("CG_10"));
        }

        [Test]
        public void ProductionRuntime_PassesB2AtBootstrapWithoutPrototypeQaControls()
        {
            CreateCamera();
            GameObject root = NewObject("M15-C Production Runtime");
            var runtime = root.AddComponent<CampaignRuntimeController>();
            runtime.Initialize(campaign, new MemoryStore(campaign));
            Assert.That(runtime.OpenChapter("power_station"), Is.True);
            Assert.That(runtime.StartLevel("power_01"), Is.True);

            BoardController board = root.GetComponentInChildren<BoardController>(true);
            Assert.That(board, Is.Not.Null);
            Assert.That(board.BoardView.VisualTheme, Is.SameAs(b2));
            Assert.That(board.BoardView.UsesProductionSkin, Is.True);
            Assert.That(board.GetComponent<GameplayHudView>().UsesProductionSkin, Is.True);
            Assert.That(board.IsCompletionPresentationHeld, Is.False);

            string[] names = root.GetComponentsInChildren<Transform>(true)
                .Select(item => item.name).ToArray();
            Assert.That(names, Has.None.EqualTo("Use B1 Theme"));
            Assert.That(names, Has.None.EqualTo("Use B2 Theme"));
            Assert.That(names, Has.None.EqualTo("Hold Completion"));
            Assert.That(names, Has.None.EqualTo("M15 Prototype Selector"));
        }

        [Test]
        public void AllFiftyProductionLevels_ConstructCompleteB2VocabularyWithoutMissingBranches()
        {
            var encounteredTypes = new HashSet<TileType>();
            int levelCount = 0;
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
            foreach (CampaignLevelEntry entry in chapter.Levels)
            {
                BoardState logical = entry.LevelDefinition.CreateBoardState();
                BoardView view = BuildBoard(entry.LevelDefinition, campaign.GameplayVisualTheme);
                CircuitTileView[] tiles = view.GetComponentsInChildren<CircuitTileView>(true);
                Assert.That(logical.Width, Is.EqualTo(entry.LevelDefinition.Width), entry.LevelId);
                Assert.That(logical.Height, Is.EqualTo(entry.LevelDefinition.Height), entry.LevelId);
                Assert.That(tiles, Has.Length.EqualTo(logical.Width * logical.Height), entry.LevelId);
                Assert.That(tiles, Has.All.Matches<CircuitTileView>(tile =>
                    tile.IsUsingVisualTheme && tile.VisualTheme == b2), entry.LevelId);
                Assert.That(view.transform.Find("Production Board Surface"), Is.Not.Null,
                    entry.LevelId);

                foreach (CircuitTileState state in logical.AllTiles())
                {
                    encounteredTypes.Add(state.TileType);
                    CircuitTileView tile = view.GetTileView(state.Position);
                    Assert.That(tile, Is.Not.Null, $"{entry.LevelId} {state.Position}");
                    Assert.That(FindPart(tile, ExpectedVisualPart(state.TileType)), Is.Not.Null,
                        $"{entry.LevelId} {state.Position} {state.TileType}");
                }

                BoardViewportFit fit = BoardViewportFitter.Calculate(view.GetWorldBounds(),
                    1080f / 1920f, GameplayLayoutMetrics.BoardViewport,
                    GameplayLayoutMetrics.BoardPaddingWorld);
                AssertContainsBoard(fit, view.GetWorldBounds(), entry.LevelId);
                levelCount++;
            }

            Assert.That(levelCount, Is.EqualTo(50));
            Assert.That(encounteredTypes.OrderBy(type => type),
                Is.EqualTo(Enum.GetValues(typeof(TileType)).Cast<TileType>().OrderBy(type => type)));
        }

        [TestCase("power_01", TileType.StraightWire, "Tap a wire to rotate it.")]
        [TestCase("power_02", TileType.CornerWire, "Corner wires redirect the current.")]
        [TestCase("power_06", TileType.TJunction,
            "T-junctions split power into multiple paths.")]
        [TestCase("power_09", TileType.Diode,
            "Diodes only allow power in one direction.")]
        [TestCase("substation_03", TileType.Switch,
            "Switches can open or close a circuit.")]
        [TestCase("control_center_01", TileType.AndGate,
            "AND gates require all inputs to be powered.")]
        [TestCase("control_center_03", TileType.OrGate,
            "OR gates activate when any input is powered.")]
        public void ProductionTutorialAnchors_ResolveLogicalB2TilesWithoutHierarchyCoupling(
            string levelId, TileType expectedType, string expectedMessage)
        {
            CampaignLevelEntry entry = FindLevel(levelId);
            TutorialStepDefinition step = entry.Tutorial.Steps.Single();
            Assert.That(step.Message, Is.EqualTo(expectedMessage));
            Assert.That(entry.LevelDefinition.CreateBoardState().GetTile(step.TargetPosition).TileType,
                Is.EqualTo(expectedType));

            BoardController controller = BuildController(entry, entry.Tutorial);
            CircuitTileView anchor = controller.BoardView.GetTileView(step.TargetPosition);
            Assert.That(anchor, Is.Not.Null);
            Assert.That(anchor.IsTutorialHighlighted, Is.True);
            Assert.That(anchor.VisualTheme, Is.SameAs(b2));
            Assert.That(anchor.GetComponent<BoxCollider2D>(), Is.Not.Null);
            Assert.That(anchor.GetComponent<CircuitTileInput>(), Is.Not.Null);

            bool hasTargetAction = controller.Session.Board.TryGetPlayerAction(
                step.TargetPosition, out _);
            Assert.That(hasTargetAction,
                Is.EqualTo(step.CompletionCondition != TutorialCompletionCondition.AnyAcceptedAction));
        }

        [Test]
        public void B2VisualHierarchy_DoesNotChangeTileInputContract()
        {
            BoardView view = BuildBoard(FindLevel("power_01").LevelDefinition, b2);
            foreach (CircuitTileView tile in view.GetComponentsInChildren<CircuitTileView>(true))
            {
                Assert.That(tile.GetComponent<BoxCollider2D>(), Is.Not.Null);
                Assert.That(tile.GetComponent<BoxCollider2D>().size, Is.EqualTo(Vector2.one * 0.9f));
                Assert.That(tile.GetComponent<CircuitTileInput>(), Is.Not.Null);
                Assert.That(tile.GetComponentsInChildren<Collider2D>(true), Has.Length.EqualTo(1));
            }
        }

        [TestCase(TileType.StraightWire)]
        [TestCase(TileType.CornerWire)]
        [TestCase(TileType.TJunction)]
        [TestCase(TileType.CrossJunction)]
        [TestCase(TileType.Diode)]
        [TestCase(TileType.AndGate)]
        [TestCase(TileType.OrGate)]
        public void RotatableProductionVocabulary_RotatesFunctionalContentWithoutRebuilding(
            TileType type)
        {
            LevelDefinition level = CreateLevel(1, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), type, 0, true)
            });
            BoardState board = level.CreateBoardState();
            BoardView view = BuildBoard(level, b2);
            CircuitTileView tile = view.GetTileView(new GridPosition(0, 0));
            int childCount = tile.GetComponentsInChildren<Transform>(true).Length;

            Assert.That(board.TryApplyAction(new PuzzleAction(new GridPosition(0, 0),
                PuzzleActionType.RotateClockwise)), Is.True);
            view.Refresh(board);

            Assert.That(board.GetTile(new GridPosition(0, 0)).Rotation, Is.EqualTo(1));
            Assert.That(NormalizeDegrees(tile.RotatingContentDegrees), Is.EqualTo(270f));
            Assert.That(tile.GetComponentsInChildren<Transform>(true),
                Has.Length.EqualTo(childCount));
            Assert.That(FindPart(tile, ExpectedVisualPart(type)), Is.Not.Null);
        }

        [Test]
        public void SwitchToggle_RefreshesB2GeometryWithoutHierarchyGrowth()
        {
            LevelDefinition level = CreateLevel(4, 2, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.Switch, 0, false, false),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false),
                new TileDefinition(new GridPosition(3, 1), TileType.OutputLamp, 0, false)
            });
            BoardController controller = BuildController(level);
            CircuitTileView view = controller.BoardView.GetTileView(new GridPosition(1, 0));
            int childCount = view.GetComponentsInChildren<Transform>(true).Length;
            Assert.That(view.CurrentFunctionalSymbol, Is.EqualTo(CircuitFunctionalSymbol.SwitchOpen));

            Assert.That(controller.PerformPlayerAction(new GridPosition(1, 0)), Is.True);
            Assert.That(view.CurrentFunctionalSymbol, Is.EqualTo(CircuitFunctionalSymbol.SwitchClosed));
            Assert.That(view.GetComponentsInChildren<Transform>(true),
                Has.Length.EqualTo(childCount));
            Assert.That(controller.Undo(), Is.True);
            Assert.That(view.CurrentFunctionalSymbol, Is.EqualTo(CircuitFunctionalSymbol.SwitchOpen));
            Assert.That(view.GetComponentsInChildren<Transform>(true),
                Has.Length.EqualTo(childCount));
        }

        [Test]
        public void B2PowerUndoRestartAndHint_RefreshStateWithoutMutationOrHierarchyGrowth()
        {
            LevelDefinition level = CreateLevel(4, 2, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 0, true),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false),
                new TileDefinition(new GridPosition(3, 1), TileType.OutputLamp, 0, false)
            });
            BoardController controller = BuildController(level);
            GridPosition wirePosition = new GridPosition(1, 0);
            GridPosition lampPosition = new GridPosition(2, 0);
            CircuitTileView wire = controller.BoardView.GetTileView(wirePosition);
            CircuitTileView lamp = controller.BoardView.GetTileView(lampPosition);
            int hierarchyCount = controller.BoardView
                .GetComponentsInChildren<Transform>(true).Length;

            Assert.That(wire.IsUnderlyingPowered, Is.False);
            Assert.That(lamp.CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.LedObjectiveOff));
            Assert.That(controller.PerformPlayerAction(wirePosition), Is.True);
            Assert.That(controller.Session.IsCompleted, Is.False);
            Assert.That(wire.IsUnderlyingPowered, Is.True);
            Assert.That(lamp.CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.LedObjectiveOn));

            controller.BoardView.HighlightHint(wirePosition);
            Assert.That(wire.IsHintHighlighted, Is.True);
            Assert.That(wire.IsUnderlyingPowered, Is.True);
            Assert.That(FindPart(wire, "Hint Rim Top").gameObject.activeSelf, Is.True);
            Assert.That(controller.BoardView.GetComponentsInChildren<Transform>(true),
                Has.Length.EqualTo(hierarchyCount));

            Assert.That(controller.Undo(), Is.True);
            Assert.That(wire.IsUnderlyingPowered, Is.False);
            Assert.That(lamp.CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.LedObjectiveOff));
            Assert.That(controller.PerformPlayerAction(wirePosition), Is.True);
            controller.Restart();
            Assert.That(controller.Session.MoveCount, Is.Zero);
            Assert.That(wire.IsUnderlyingPowered, Is.False);
            Assert.That(lamp.CurrentFunctionalSymbol,
                Is.EqualTo(CircuitFunctionalSymbol.LedObjectiveOff));
            Assert.That(controller.BoardView.VisualTheme, Is.SameAs(b2));
            Assert.That(controller.BoardView.GetComponentsInChildren<Transform>(true),
                Has.Length.EqualTo(hierarchyCount));
        }

        [Test]
        public void ProductionCompletion_IsImmediateAndResultInvariantUnderB2()
        {
            CampaignLevelEntry entry = FindLevel("power_01");
            BoardController themed = BuildController(entry, entry.Tutorial, true);
            BoardController fallback = BuildController(
                new GameplaySession(entry.LevelDefinition, entry.AuthoredOptimalMoves.Value),
                entry.Tutorial, true, null);

            Assert.That(themed.PerformPlayerAction(new GridPosition(1, 0)), Is.True);
            Assert.That(fallback.PerformPlayerAction(new GridPosition(1, 0)), Is.True);

            Assert.That(themed.Session.IsCompleted, Is.True);
            Assert.That(themed.IsCompletionPresentationHeld, Is.False);
            Assert.That(themed.IsCompletionPanelVisible, Is.True);
            Assert.That(themed.Session.CompletionResult.TotalMoves,
                Is.EqualTo(fallback.Session.CompletionResult.TotalMoves));
            Assert.That(themed.Session.CompletionResult.HintsUsed,
                Is.EqualTo(fallback.Session.CompletionResult.HintsUsed));
            Assert.That(themed.Session.CompletionResult.StarRating.Stars,
                Is.EqualTo(fallback.Session.CompletionResult.StarRating.Stars));
        }

        [Test]
        public void ThemeAssociation_DoesNotEnterSaveOrChangeRestorationReplayNavigation()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NeonGridM15C",
                Guid.NewGuid().ToString("N"));
            string savePath = CampaignSaveStore.BuildSavePath(directory, campaign.CampaignId);
            try
            {
                var progress = new CampaignProgressService(campaign);
                CampaignChapterDefinition chapter = campaign.Chapters[0];
                for (int index = 0; index < chapter.Levels.Count - 1; index++)
                {
                    CampaignLevelEntry entry = chapter.Levels[index];
                    Assert.That(progress.RecordCompletion(entry.LevelId,
                        CampaignTestFixture.Result(entry.LevelDefinition, 1, 1f, 3)).Accepted,
                        Is.True);
                }

                var store = new CampaignSaveStore(savePath);
                var flow = new CampaignFlowCoordinator(campaign, progress, store);
                Assert.That(flow.OpenChapter(chapter.ChapterId), Is.True);
                Assert.That(flow.StartLevel(chapter.Levels[9].LevelId), Is.True);
                Solve(flow.ActiveSession);
                Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.True);
                Assert.That(flow.ResultNavigation.ShowRetry, Is.True);
                Assert.That(flow.ResultNavigation.ShowMap, Is.True);
                Assert.That(flow.ResultNavigation.ShowLevels, Is.False);
                Assert.That(flow.ResultNavigation.ShowNext, Is.False);
                Assert.That(progress.PendingRestorationCount, Is.EqualTo(1));

                Assert.That(flow.Retry(), Is.True);
                Solve(flow.ActiveSession);
                Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.False);
                Assert.That(flow.ResultNavigation.ShowRetry, Is.True);
                Assert.That(flow.ResultNavigation.ShowLevels, Is.True);
                Assert.That(flow.ResultNavigation.ShowMap, Is.False);
                Assert.That(progress.PendingRestorationCount, Is.EqualTo(1));

                string json = File.ReadAllText(savePath);
                Assert.That(json, Does.Not.Contain("technical_neon"));
                Assert.That(json, Does.Not.Contain("gameplayVisualTheme"));
                Assert.That(new CampaignSaveStore(savePath).Load(campaign).Status,
                    Is.EqualTo(CampaignLoadStatus.Loaded));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [TestCase("power_01", 1080, 1920)]
        [TestCase("power_01", 1080, 2340)]
        [TestCase("power_01", 720, 1280)]
        [TestCase("substation_10", 1080, 1920)]
        [TestCase("substation_10", 1080, 2340)]
        [TestCase("substation_10", 720, 1280)]
        [TestCase("control_center_10", 1080, 1920)]
        [TestCase("control_center_10", 1080, 2340)]
        [TestCase("control_center_10", 720, 1280)]
        [TestCase("automation_plant_10", 1080, 1920)]
        [TestCase("automation_plant_10", 1080, 2340)]
        [TestCase("automation_plant_10", 720, 1280)]
        [TestCase("central_grid_10", 1080, 1920)]
        [TestCase("central_grid_10", 1080, 2340)]
        [TestCase("central_grid_10", 720, 1280)]
        public void RepresentativeProductionBoards_FitAcceptedPortraitResolutions(
            string levelId, int width, int height)
        {
            BoardView view = BuildBoard(FindLevel(levelId).LevelDefinition, b2);
            Physics2D.SyncTransforms();
            Bounds bounds = view.GetWorldBounds();
            BoardViewportFit fit = BoardViewportFitter.Calculate(bounds, width / (float)height,
                GameplayLayoutMetrics.BoardViewport, GameplayLayoutMetrics.BoardPaddingWorld);
            AssertContainsBoard(fit, bounds, $"{levelId} {width}x{height}");
        }

        [Test]
        public void RepresentativeHierarchyMetrics_RemainBoundedForPS01AndCG10()
        {
            foreach (string levelId in new[] { "power_01", "central_grid_10" })
            {
                BoardView board = BuildBoard(FindLevel(levelId).LevelDefinition, b2);
                CircuitTileView[] tiles = board.GetComponentsInChildren<CircuitTileView>(true);
                int maximumTileTransforms = tiles.Max(tile =>
                    tile.GetComponentsInChildren<Transform>(true).Length);
                int totalTransforms = board.GetComponentsInChildren<Transform>(true).Length;
                Assert.That(maximumTileTransforms, Is.LessThan(75));
                Assert.That(totalTransforms,
                    Is.LessThan(tiles.Length * 75 + 20));
                TestContext.WriteLine($"{levelId}: tiles={tiles.Length}, " +
                    $"totalTransforms={totalTransforms}, maxTileTransforms={maximumTileTransforms}");
            }
        }

        [Test]
        public void ProductionBuildPath_ExcludesM15PrototypeAndKeepsMainSceneAssociation()
        {
            const string productionScene = "Assets/NeonGrid/Scenes/M12_NeonGrid_Main.unity";
            const string prototypeScene =
                "Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity";
            Assert.That(EditorBuildSettings.scenes.Any(scene =>
                scene.enabled && scene.path == productionScene), Is.True);
            Assert.That(EditorBuildSettings.scenes.Any(scene =>
                scene.enabled && scene.path == prototypeScene), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(prototypeScene), Is.Not.Null);
        }

        private BoardController BuildController(CampaignLevelEntry entry,
            LevelTutorialDefinition tutorial, bool resultActions = false,
            CircuitVisualThemeDefinition theme = null)
        {
            var session = new GameplaySession(entry.LevelDefinition,
                entry.AuthoredOptimalMoves.Value);
            return BuildController(session, tutorial, resultActions, theme ?? b2);
        }

        private BoardController BuildController(LevelDefinition level)
        {
            var session = new GameplaySession(level, 1, new RejectingHintRunner());
            return BuildController(session, null, false, b2);
        }

        private BoardController BuildController(GameplaySession session,
            LevelTutorialDefinition tutorial, bool withResultActions,
            CircuitVisualThemeDefinition theme)
        {
            GameObject root = NewObject("M15-C Board Controller");
            var controller = root.AddComponent<BoardController>();
            GameplayResultActions actions = withResultActions
                ? new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                    () => { }, () => default)
                : null;
            controller.Initialize(session, actions, tutorial, 1, theme);
            return controller;
        }

        private BoardView BuildBoard(LevelDefinition level,
            CircuitVisualThemeDefinition theme)
        {
            GameObject root = NewObject($"M15-C Board {level.name}");
            var board = root.AddComponent<BoardView>();
            board.Build(level.CreateBoardState(), _ => { }, theme);
            return board;
        }

        private LevelDefinition CreateLevel(int width, int height,
            IEnumerable<TileDefinition> tiles)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.name = "M15-C Temporary Level";
            level.SetData(width, height, tiles);
            cleanup.Add(level);
            return level;
        }

        private CampaignLevelEntry FindLevel(string levelId)
        {
            return campaign.Chapters.SelectMany(chapter => chapter.Levels)
                .Single(level => level.LevelId == levelId);
        }

        private GameObject NewObject(string name)
        {
            var value = new GameObject(name);
            cleanup.Add(value);
            return value;
        }

        private void CreateCamera()
        {
            GameObject root = NewObject("M15-C Main Camera");
            root.tag = "MainCamera";
            root.AddComponent<Camera>().orthographic = true;
        }

        private static Transform FindPart(CircuitTileView view, string name) =>
            view.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == name);

        private static string ExpectedVisualPart(TileType type)
        {
            switch (type)
            {
                case TileType.Empty: return "Empty Center";
                case TileType.StraightWire:
                case TileType.CornerWire:
                case TileType.TJunction:
                case TileType.CrossJunction: return "Circuit Base";
                case TileType.PowerSource: return "Source Magenta Core";
                case TileType.OutputLamp: return "LED Receiver Backplate";
                case TileType.Diode: return "Diode Mechanism Plate";
                case TileType.Switch: return "Switch Mechanism Bed";
                case TileType.AndGate: return "AND Gate Module Bed";
                case TileType.OrGate: return "OR Gate Module Bed";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static void AssertContainsBoard(BoardViewportFit fit, Bounds bounds,
            string context)
        {
            Assert.That(fit.PlayableWorldRect.xMin,
                Is.LessThanOrEqualTo(bounds.min.x - GameplayLayoutMetrics.BoardPaddingWorld +
                                     0.0001f), context);
            Assert.That(fit.PlayableWorldRect.xMax,
                Is.GreaterThanOrEqualTo(bounds.max.x + GameplayLayoutMetrics.BoardPaddingWorld -
                                        0.0001f), context);
            Assert.That(fit.PlayableWorldRect.yMin,
                Is.LessThanOrEqualTo(bounds.min.y - GameplayLayoutMetrics.BoardPaddingWorld +
                                     0.0001f), context);
            Assert.That(fit.PlayableWorldRect.yMax,
                Is.GreaterThanOrEqualTo(bounds.max.y + GameplayLayoutMetrics.BoardPaddingWorld -
                                        0.0001f), context);
        }

        private static float NormalizeDegrees(float value)
        {
            value %= 360f;
            return value < 0f ? value + 360f : value;
        }

        private static void Solve(GameplaySession session)
        {
            PuzzleSolverResult result = new PuzzleSolver().Solve(session.Board,
                PuzzleSolverProfiles.AuthoringExact);
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            foreach (PuzzleAction action in result.Solution)
                Assert.That(session.PerformAction(action), Is.True);
            Assert.That(session.IsCompleted, Is.True);
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            private readonly CampaignDefinition campaign;
            public string SavePath => "memory://m15-c";

            public MemoryStore(CampaignDefinition campaign)
            {
                this.campaign = campaign;
            }

            public CampaignLoadResult Load(CampaignDefinition definition) =>
                new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(campaign), Array.Empty<string>());

            public CampaignSaveResult Save(CampaignProgressService progress) =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);

            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }

        private sealed class RejectingHintRunner : IHintSolverRunner
        {
            public void Start(BoardState boardSnapshot, PuzzleSolverOptions options,
                Action<PuzzleSolverResult> completed, Action<Exception> failed)
            {
                failed(new InvalidOperationException("Hint search was not expected."));
            }
        }
    }
}

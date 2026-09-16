using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Session;
using NeonGrid.Simulation;
using NeonGrid.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class ProductionControlCenterSliceTests
    {
        [TestCase("CC_01", "B92F17189119180B7E5C9302DB1E259A4CAE9920C40D99961FBA1362A079FB93", "A50A36C8E6AAB59399E879F2E0FA263F5A1BE298CD6AA49C99974DC99A2EC8C0")]
        [TestCase("CC_02", "FDA283A1DF76C04BF61A60E819D1058140D3667B4AC60AD7431178B5D92EB83D", "BBBB8330032E9B15AD2631A3F9B3F3D1C2BD13A7CEF33BDECD37CB21042AAF07")]
        [TestCase("CC_03", "A0B23FD6F56E3BCDE0C8AE1FA0FAA4064AFE92B1C29AEB2639A9F422F7BF80B7", "692D7510EFF3C90E3BC55E4CDE36B67D57EB1A0EAECE673A5F6A553892C45D64")]
        [TestCase("CC_04", "6C1A438D14A6E691E03F28F1600D225FE27294928CD212694172B23C7F11C2AE", "D47E7B9BCF54555DB4B58359800BD4F786CADE39B68AB142ADC90B888DD26025")]
        [TestCase("CC_05", "F74BBA0AF601AA6820F8887FCA1772D1DBD7A84D749277CAE0C84C4182C14471", "AC9BC1418A6C48623D4BB49727510FED5A8D0152AE117910F3C302609BFAA931")]
        [TestCase("CC_06", "726F1980AE8E802892EFED816081575DF6FF15802EC5CB383365ED91B2DCF0A6", "A96BAF0263A9C6C8C114787823055D0E3E77489A6BF2B5E1028EFF9CD8422B5D")]
        [TestCase("CC_07", "DB7CAEE5E9C554A69EBE6D9C25D32EF79C6DF2381CFF3CF3DDBF0B3276EC7B5A", "78998132DB33055D1520E30D2B5ADEA9D80B4ABDC195E31E37175FDE1A4042B8")]
        [TestCase("CC_08", "57A112B76D477F92778EA32FB43287E600DAF0B3B394CA9691C207D475920ABE", "90F4A0EE2CFEEF54413F582E76F281F1FDF4989DC285DA2EB9DAB4B3A6215D2B")]
        [TestCase("CC_09", "5C3634E9A0C9FFAC54739681AA4BBB415036BCB0A32A9EEA4113578F4D309271", "9A6C8C7A11DB74A58F57230133D910E065854968691051D28272D6289F45E6FC")]
        [TestCase("CC_10", "ADDD45125014693DFBD4B982740729A28C20226C70B68447EDFF781676000F2B", "1A35BC61A760F74B7FB93D6D148EBDC231F7B6F5EFD6E4A418A92F254DEE2F74")]
        public void ProductionLevel_AssetAndMetaHashesRemainImmutable(string name,
            string assetHash, string metaHash)
        {
            string prefix = Path.GetFullPath(
                $"Assets/NeonGrid/Resources/Levels/ControlCenter/{name}.asset");
            Assert.That(Hash(prefix), Is.EqualTo(assetHash));
            Assert.That(Hash(prefix + ".meta"), Is.EqualTo(metaHash));
        }

        [TestCase("CC_01", 4, 92, 4)]
        [TestCase("CC_02", 6, 1236, 6)]
        [TestCase("CC_03", 1, 2, 1)]
        [TestCase("CC_04", 3, 201, 3)]
        [TestCase("CC_05", 4, 193, 4)]
        [TestCase("CC_06", 3, 99, 3)]
        [TestCase("CC_07", 4, 252, 4)]
        [TestCase("CC_08", 7, 37559, 7)]
        [TestCase("CC_09", 6, 15614, 6)]
        [TestCase("CC_10", 8, 26986, 8)]
        public void ProductionLevel_IsValidAndMatchesExactAuthoringBaseline(string name,
            int minimum, int explored, int depth)
        {
            LevelDefinition level = LoadLevel(name);
            LevelValidationResult validation = new LevelValidator().Validate(level);
            Assert.That(validation.IsValid, Is.True,
                string.Join("\n", validation.Errors.Select(issue => issue.Message)));
            Assert.That(validation.Warnings, Is.Empty);
            var timer = Stopwatch.StartNew();
            PuzzleSolverResult result = new PuzzleSolver().Solve(level.CreateBoardState(),
                PuzzleSolverProfiles.AuthoringExact);
            timer.Stop();
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(result.MinimumMoveCount, Is.EqualTo(minimum));
            Assert.That(result.ExploredStateCount, Is.EqualTo(explored));
            Assert.That(result.DeepestSearchDepth, Is.EqualTo(depth));
            CampaignLevelEntry entry = LoadCampaign().Chapters[0].Levels.Single(candidate =>
                candidate.LevelDefinition == level);
            Assert.That(entry.AuthoredOptimalMoves, Is.EqualTo(minimum));
            TestContext.WriteLine($"{name} AuthoringExact: minimum={minimum}, explored={explored}, " +
                                  $"depth={depth}, ms={timer.Elapsed.TotalMilliseconds:F2}, " +
                                  $"solution={string.Join(" | ", result.Solution)}");
        }

        [Test]
        public void Campaign_UsesStableStructureReferencesTutorialsAndSequentialProgression()
        {
            CampaignDefinition campaign = LoadCampaign();
            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            Assert.That(validation.IsValid, Is.True,
                string.Join("\n", validation.Issues.Select(issue => issue.Message)));
            Assert.That(validation.Issues, Is.Empty);
            Assert.That(campaign.CampaignId, Is.EqualTo("control_center_vertical_slice"));
            Assert.That(campaign.Chapters, Has.Count.EqualTo(1));
            CampaignChapterDefinition chapter = campaign.Chapters[0];
            Assert.That(chapter.ChapterId, Is.EqualTo("control_center"));
            Assert.That(chapter.DisplayName, Is.EqualTo("Control Center"));
            Assert.That(chapter.Levels, Has.Count.EqualTo(10));
            Assert.That(chapter.Levels.Select(level => level.LevelId), Is.EqualTo(
                Enumerable.Range(1, 10).Select(index => $"control_center_{index:D2}")));
            for (int index = 0; index < 10; index++)
                Assert.That(chapter.Levels[index].LevelDefinition,
                    Is.SameAs(LoadLevel($"CC_{index + 1:D2}")));
            AssertFixedTutorial(chapter.Levels[0], TileType.AndGate,
                "AND gates require all inputs to be powered.");
            AssertFixedTutorial(chapter.Levels[2], TileType.OrGate,
                "OR gates activate when any input is powered.");
            for (int index = 0; index < 10; index++)
                if (index != 0 && index != 2) Assert.That(chapter.Levels[index].Tutorial, Is.Null);

            var progress = new CampaignProgressService(campaign);
            Assert.That(progress.MaximumCampaignStars, Is.EqualTo(30));
            for (int index = 0; index < 10; index++)
                Assert.That(progress.IsLevelUnlocked(chapter.Levels[index].LevelId),
                    Is.EqualTo(index == 0));
            for (int index = 0; index < 10; index++)
            {
                CampaignProgressUpdate update = Record(progress, chapter.Levels[index], 3);
                Assert.That(update.Accepted, Is.True);
                Assert.That(update.ChapterJustRestored, Is.EqualTo(index == 9));
                if (index < 9)
                    Assert.That(progress.IsLevelUnlocked(chapter.Levels[index + 1].LevelId), Is.True);
            }
            Assert.That(progress.GetChapterState("control_center"),
                Is.EqualTo(CampaignChapterState.Restored));
            Assert.That(progress.TotalStars, Is.EqualTo(30));
        }

        [Test]
        public void Navigation_IsGenericThroughCC10AndReplayUsesNormalFinalNavigation()
        {
            CampaignDefinition campaign = LoadCampaign();
            var flow = new CampaignFlowCoordinator(campaign,
                new CampaignProgressService(campaign), new MemoryStore());
            Assert.That(flow.OpenChapter("control_center"), Is.True);
            Assert.That(flow.StartLevel("control_center_01"), Is.True);
            for (int index = 0; index < 9; index++)
            {
                Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.False);
                Solve(flow.ActiveSession);
                AssertNormalNavigation(flow.ResultNavigation, true);
                Assert.That(flow.StartNextLevel(), Is.True);
            }
            Assert.That(flow.ActiveLevel.LevelId, Is.EqualTo("control_center_10"));
            Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.True);
            Solve(flow.ActiveSession);
            Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.True);
            Assert.That(flow.ResultNavigation.ShowRetry, Is.True);
            Assert.That(flow.ResultNavigation.ShowLevels, Is.False);
            Assert.That(flow.ResultNavigation.ShowMap, Is.True);
            Assert.That(flow.ResultNavigation.ShowNext, Is.False);
            Assert.That(flow.Retry(), Is.True);
            Solve(flow.ActiveSession);
            AssertNormalNavigation(flow.ResultNavigation, false);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void FixedGateTutorial_AnchorsWithoutActionAndDismissesOnAcceptedSolutionAction(
            int levelIndex)
        {
            CampaignLevelEntry entry = LoadCampaign().Chapters[0].Levels[levelIndex];
            TutorialStepDefinition step = entry.Tutorial.Steps[0];
            BoardState board = entry.LevelDefinition.CreateBoardState();
            CircuitTileState anchor = board.GetTile(step.TargetPosition);
            Assert.That(anchor.IsRotatable, Is.False);
            Assert.That(board.TryGetPlayerAction(step.TargetPosition, out _), Is.False);
            var root = new GameObject("Control Center Fixed Tutorial Test");
            try
            {
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(new GameplaySession(entry.LevelDefinition,
                        entry.AuthoredOptimalMoves.Value), EmptyActions(), entry.Tutorial,
                    levelIndex + 1);
                CircuitTileView anchorView = root.transform.Find("Tile 2,2")
                    .GetComponent<CircuitTileView>();
                Assert.That(controller.Tutorial.IsActive, Is.True);
                Assert.That(anchorView.IsTutorialHighlighted, Is.True);
                Assert.That(controller.Session.MoveCount, Is.Zero,
                    "Showing a tutorial must not execute an action.");
                Assert.That(controller.PerformPlayerAction(new GridPosition(0, 0)), Is.False);
                Assert.That(controller.Tutorial.IsActive, Is.True,
                    "An invalid interaction must not dismiss the tutorial.");
                PuzzleAction first = new PuzzleSolver().Solve(controller.Session.Board,
                    PuzzleSolverProfiles.AuthoringExact).Solution[0];
                Assert.That(controller.PerformPlayerAction(first.Position), Is.True);
                Assert.That(controller.Tutorial.IsActive, Is.False);
                Assert.That(anchorView.IsTutorialHighlighted, Is.False);
                Assert.That(controller.Session.MoveCount, Is.EqualTo(1));
                Assert.That(controller.Session.HintsUsed, Is.False);
                if (!controller.Session.IsCompleted)
                {
                    Assert.That(controller.Undo(), Is.True);
                    Assert.That(controller.Tutorial.IsActive, Is.False,
                        "Undo must not reopen a completed step in the same attempt.");
                }
                controller.Restart();
                Assert.That(controller.Tutorial.IsActive, Is.True,
                    "Restart retains the existing attempt-local reset behavior.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TutorialReplayAndReentry_FollowExistingAttemptLocalProgressRules()
        {
            CampaignDefinition campaign = LoadCampaign();
            CampaignChapterDefinition chapter = campaign.Chapters[0];
            var progress = new CampaignProgressService(campaign);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore());
            flow.OpenChapter("control_center");
            flow.StartLevel("control_center_01");
            Assert.That(flow.ActiveTutorial, Is.Not.Null);
            flow.ReturnToLevelSelection();
            flow.StartLevel("control_center_01");
            Assert.That(flow.ActiveTutorial, Is.Not.Null,
                "Incomplete leave and re-entry must show onboarding again.");
            Solve(flow.ActiveSession);
            flow.Retry();
            Assert.That(flow.ActiveTutorial, Is.Null,
                "Completed replay must skip onboarding.");

            Record(progress, chapter.Levels[1], 1);
            flow.ReturnToLevelSelection();
            flow.StartLevel("control_center_03");
            Assert.That(flow.ActiveTutorial, Is.Not.Null);
            Solve(flow.ActiveSession);
            flow.Retry();
            Assert.That(flow.ActiveTutorial, Is.Null);
        }

        [Test]
        public void PartialProgress_SaveLoadAndCampaignIsolationRemainGeneric()
        {
            CampaignDefinition control = LoadCampaign();
            CampaignDefinition power = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            CampaignDefinition substation = Resources.Load<CampaignDefinition>(
                "Campaigns/Substation_VerticalSlice");
            string root = Path.Combine(Path.GetTempPath(), "NeonGridM9Tests",
                Guid.NewGuid().ToString("N"));
            string path = CampaignSaveStore.BuildSavePath(root, control.CampaignId);
            try
            {
                Assert.That(path, Does.EndWith(Path.Combine("NeonGrid",
                    "control_center_vertical_slice", CampaignSaveStore.SaveFileName)));
                Assert.That(path, Is.Not.EqualTo(CampaignSaveStore.BuildSavePath(root,
                    power.CampaignId)));
                Assert.That(path, Is.Not.EqualTo(CampaignSaveStore.BuildSavePath(root,
                    substation.CampaignId)));
                var progress = new CampaignProgressService(control);
                for (int index = 0; index < 4; index++)
                    Assert.That(Record(progress, control.Chapters[0].Levels[index], index % 3 + 1)
                        .Accepted, Is.True);
                Assert.That(new CampaignSaveStore(path).Save(progress).Succeeded, Is.True);
                CampaignLoadResult load = new CampaignSaveStore(path).Load(control);
                Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
                for (int index = 0; index < 4; index++)
                {
                    LevelProgress restored = load.Progress.GetLevelProgress(
                        $"control_center_{index + 1:D2}");
                    Assert.That(restored.Completed, Is.True);
                    Assert.That(restored.BestStars, Is.EqualTo(index % 3 + 1));
                    Assert.That(restored.BestMoves, Is.EqualTo(index % 3 + 1));
                    Assert.That(restored.BestTimeSeconds, Is.EqualTo(1f));
                }
                Assert.That(load.Progress.IsLevelUnlocked("control_center_05"), Is.True);
                Assert.That(load.Progress.IsLevelUnlocked("control_center_06"), Is.False);
                Assert.That(load.Progress.GetChapterState("control_center"),
                    Is.EqualTo(CampaignChapterState.Available));
                Assert.That(new CampaignSaveStore(path).Load(power).Status,
                    Is.EqualTo(CampaignLoadStatus.CampaignMismatch));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestCase(0, "LEVEL 1")]
        [TestCase(1, "LEVEL 2")]
        [TestCase(2, "LEVEL 3")]
        [TestCase(3, "LEVEL 4")]
        [TestCase(4, "LEVEL 5")]
        [TestCase(5, "LEVEL 6")]
        [TestCase(6, "LEVEL 7")]
        [TestCase(7, "LEVEL 8")]
        [TestCase(8, "LEVEL 9")]
        [TestCase(9, "LEVEL 10")]
        public void GameplayHud_UsesChapterOrderOrdinal(int index, string expected)
        {
            CampaignChapterDefinition chapter = LoadCampaign().Chapters[0];
            CampaignLevelEntry entry = chapter.Levels[index];
            var root = new GameObject("Control Center HUD Test");
            try
            {
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(new GameplaySession(entry.LevelDefinition,
                        entry.AuthoredOptimalMoves.Value), EmptyActions(), entry.Tutorial,
                    CampaignRuntimeController.FindLevelOrdinal(chapter, entry));
                Text identity = root.transform.Find("Gameplay HUD Canvas/Level Identity")
                    .GetComponent<Text>();
                Assert.That(identity.text, Is.EqualTo(expected));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Selector_UsesAcceptedThreePlusThreePlusThreePlusOneTopology()
        {
            CampaignDefinition campaign = LoadCampaign();
            var root = new GameObject("Control Center Selector Test");
            try
            {
                var view = root.AddComponent<CampaignRuntimeView>();
                view.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { }, () => { });
                view.ShowChapter(campaign.Chapters[0]);
                Transform selection = root.transform.Find("Campaign Canvas/Level Selection");
                Transform grid = selection.Find("Generated Level Grid");
                Assert.That(grid.childCount, Is.EqualTo(4));
                for (int row = 0; row < 4; row++)
                {
                    Assert.That(grid.GetChild(row).childCount, Is.EqualTo(row == 3 ? 1 : 3));
                    foreach (Transform tile in grid.GetChild(row))
                        Assert.That(tile.GetComponent<RectTransform>().sizeDelta,
                            Is.EqualTo(new Vector2(220f, 220f)));
                }
                Assert.That(grid.GetChild(3).GetComponent<HorizontalLayoutGroup>().childAlignment,
                    Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(selection.GetComponentInChildren<ScrollRect>(true), Is.Null);
                Assert.That(selection.Find("Back To Map"), Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("CC_08", 37559, 7)]
        [TestCase("CC_09", 15614, 6)]
        [TestCase("CC_10", 26986, 8)]
        public void RuntimeHint_InitialStateStaysWithinExistingBudget(string name,
            int explored, int depth)
        {
            var timer = Stopwatch.StartNew();
            PuzzleSolverResult result = new PuzzleSolver().Solve(LoadLevel(name).CreateBoardState(),
                PuzzleSolverProfiles.RuntimeHint);
            timer.Stop();
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(result.ExploredStateCount, Is.EqualTo(explored));
            Assert.That(result.DeepestSearchDepth, Is.EqualTo(depth));
            Assert.That(result.ExploredStateCount,
                Is.LessThan(PuzzleSolverProfiles.RuntimeHint.MaximumExploredStates));
            TestContext.WriteLine($"{name} RuntimeHint initial: status={result.Status}, " +
                                  $"explored={explored}, depth={depth}, " +
                                  $"ms={timer.Elapsed.TotalMilliseconds:F2}, limitHit=False");
        }

        [Test]
        public void RuntimeHint_CC10MeaningfulMemoryOnlyScrambleDoesNotMutateAsset()
        {
            LevelDefinition level = LoadLevel("CC_10");
            string before = Hash(Path.GetFullPath(
                "Assets/NeonGrid/Resources/Levels/ControlCenter/CC_10.asset"));
            BoardState board = level.CreateBoardState();
            var scramble = new PuzzleAction(new GridPosition(2, 0),
                PuzzleActionType.ToggleSwitch);
            Assert.That(board.TryApplyAction(scramble), Is.True);
            var timer = Stopwatch.StartNew();
            PuzzleSolverResult result = new PuzzleSolver().Solve(board,
                PuzzleSolverProfiles.RuntimeHint);
            timer.Stop();
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(result.ExploredStateCount,
                Is.LessThan(PuzzleSolverProfiles.RuntimeHint.MaximumExploredStates));
            Assert.That(Hash(Path.GetFullPath(
                "Assets/NeonGrid/Resources/Levels/ControlCenter/CC_10.asset")),
                Is.EqualTo(before));
            TestContext.WriteLine($"CC_10 RuntimeHint after {scramble}: status={result.Status}, " +
                                  $"explored={result.ExploredStateCount}, " +
                                  $"depth={result.DeepestSearchDepth}, " +
                                  $"ms={timer.Elapsed.TotalMilliseconds:F2}, " +
                                  $"limitHit={result.Status == PuzzleSolverStatus.SearchLimitReached}");
        }

        [UnityTest]
        public IEnumerator CC10_RealBackgroundHintReachesTerminalStateThroughMainThreadPump()
        {
            var session = new GameplaySession(LoadLevel("CC_10"), 8);
            session.AdvanceTime(GameplaySession.HintUnlockSeconds);
            Assert.That(session.RequestHint().Status, Is.EqualTo(HintStatus.HintSearching));
            var watchdog = Stopwatch.StartNew();
            while (session.IsHintSearchInProgress && watchdog.Elapsed.TotalSeconds < 120d)
            {
                session.AdvanceTime(0.016f);
                session.UpdateHintRequest();
                yield return null;
            }
            Assert.That(session.IsHintSearchInProgress, Is.False);
            Assert.That(session.LastHint.Status, Is.EqualTo(HintStatus.HintAvailable));
            Assert.That(session.LastHint.SuggestedAction.HasValue, Is.True);
            TestContext.WriteLine($"CC_10 async RuntimeHint delivery: elapsedMs={watchdog.Elapsed.TotalMilliseconds:F2}, " +
                                  $"status={session.LastHint.Status}");
            session.Dispose();
        }

        [TestCase(7, 7)]
        [TestCase(8, 6)]
        [TestCase(9, 8)]
        public void RuntimeSession_UsesAuthoredBaselineWithoutStartupSearch(int index, int optimum)
        {
            CampaignLevelEntry entry = LoadCampaign().Chapters[0].Levels[index];
            var runner = new RejectingRunner();
            var session = new GameplaySession(entry.LevelDefinition,
                entry.AuthoredOptimalMoves.Value, runner,
                new PuzzleSolverOptions { MaximumExploredStates = 1, MaximumDepth = 0 });
            Assert.That(session.OptimalMoves, Is.EqualTo(optimum));
            Assert.That(session.OptimalSolverStatus, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(runner.StartCount, Is.Zero);
        }

        [Test]
        public void RuntimeScene_UsesControlCenterCampaignAndIsEnabledInBuildSettings()
        {
            const string path = "Assets/NeonGrid/Scenes/M9_ControlCenter_VerticalSlice.unity";
            CampaignDefinition campaign = LoadCampaign();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path),
                Is.True);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                CampaignRuntimeController controller = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<CampaignRuntimeController>())
                    .Single(component => component != null);
                var serialized = new SerializedObject(controller);
                Assert.That(serialized.FindProperty("campaign").objectReferenceValue,
                    Is.SameAs(campaign));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static string Hash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static LevelDefinition LoadLevel(string name)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(
                $"Levels/ControlCenter/{name}");
            Assert.That(level, Is.Not.Null, $"Missing {name}.");
            return level;
        }

        private static CampaignDefinition LoadCampaign()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/ControlCenter_VerticalSlice");
            Assert.That(campaign, Is.Not.Null);
            return campaign;
        }

        private static void AssertFixedTutorial(CampaignLevelEntry entry, TileType type,
            string message)
        {
            Assert.That(entry.Tutorial.Steps, Has.Count.EqualTo(1));
            TutorialStepDefinition step = entry.Tutorial.Steps[0];
            Assert.That(step.Message, Is.EqualTo(message));
            Assert.That(step.TargetPosition, Is.EqualTo(new GridPosition(2, 2)));
            Assert.That(step.CompletionCondition,
                Is.EqualTo(TutorialCompletionCondition.AnyAcceptedAction));
            CircuitTileState anchor = entry.LevelDefinition.CreateBoardState()
                .GetTile(step.TargetPosition);
            Assert.That(anchor.TileType, Is.EqualTo(type));
            Assert.That(anchor.IsRotatable, Is.False);
        }

        private static CampaignProgressUpdate Record(CampaignProgressService progress,
            CampaignLevelEntry entry, int stars)
        {
            return progress.RecordCompletion(entry.LevelId,
                CampaignTestFixture.Result(entry.LevelDefinition, stars, 1f, stars));
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

        private static void AssertNormalNavigation(CampaignResultNavigationState navigation,
            bool showNext)
        {
            Assert.That(navigation.ShowRetry, Is.True);
            Assert.That(navigation.ShowLevels, Is.True);
            Assert.That(navigation.ShowMap, Is.False);
            Assert.That(navigation.ShowNext, Is.EqualTo(showNext));
        }

        private static GameplayResultActions EmptyActions()
        {
            return new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                () => { }, () => default);
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public string SavePath => "memory://control-center";
            public CampaignLoadResult Load(CampaignDefinition campaign) =>
                new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(campaign), Array.Empty<string>());
            public CampaignSaveResult Save(CampaignProgressService progress) =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }

        private sealed class RejectingRunner : IHintSolverRunner
        {
            public int StartCount { get; private set; }
            public void Start(BoardState boardSnapshot, PuzzleSolverOptions options,
                Action<PuzzleSolverResult> completed, Action<Exception> failed)
            {
                StartCount++;
                failed(new InvalidOperationException("Search was not expected."));
            }
        }
    }
}

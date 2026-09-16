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
    public sealed class ProductionAutomationPlantSliceTests
    {
        [TestCase("AP_01", "6A6DC3D19C7262B4A8B43EAF6A5651645AD3FC2EB64A8666A2BC08A5714D66C3", "BF0C01B84750621D348C5A10624C62841118E9E113C35E8521D20BD022E966EE")]
        [TestCase("AP_02", "42D55D92B0709F0BEF43BB4F4E2EBD62F612945A909E778323B9E47F9F0BF8A8", "163548F8425790A08E34D859FBB858540E9070103372DC652808A8100441FDF9")]
        [TestCase("AP_03", "28D85E69372AF5F4C39608765D113C68307C5B1F8A793741C75ED6A269294D37", "3EF0A88B1F1B1DFA79EC0FE70CA4225F37D9D69DF694E489598D6BD50B1D1C0F")]
        [TestCase("AP_04", "E62DE3DC474DE76020D8B943B1953F0A78D567380618FC3BB0EBF03F75D14FD7", "83F5F43F08A2B3A890168C45BD5A18C9C02123897D1F2F2AED87890B2AEC753D")]
        [TestCase("AP_05", "F157D6F27713779841483D2CC9E4DED7EF9640307DF70A5FC9931F880DA99190", "F4492F5C373BDFDB2AF6967E0CB97B6981815635709C3436485BD11BDE3FC3C8")]
        [TestCase("AP_06", "AE51221C5A961A732C3EF71146AD565324F8A74A8D9960AFB6633B077A23CA25", "6BDD97745EF48283ABDBF9CB93E2ACB1B7C2422B905EF2729E6BEFAB824C4668")]
        [TestCase("AP_07", "6400912C6C38AEF10ABB999119C31D36EC0041E98F77CE897FD2D07044293FEE", "6C5BD95120C55AFDE18EB612E8E25A35A080B04CCCE77D705F7813509030BB67")]
        [TestCase("AP_08", "E922F5BF5752DFF7EEDB177485FFB1E1CAFAD4513FC6644A5FB96980DF903C41", "017022E8F17026D3D9E2240121B7A3C2BCEFB37EDAB21E1FA571967EF43EB346")]
        [TestCase("AP_09", "205C26ED7A1456EC247A263E332A2D3AF5BECA649CD83139E5A1605775953DE8", "D445A606BF9E63E49C5CF33BACC8357D9F2DEB2BF453D41B9C67FD43DAC0019C")]
        [TestCase("AP_10", "4A3AE56502AF3A678AA24CD4CBA88AE98EC20BC9E2010C77B1E4874D1EA5559C", "0B1FCAFA81C7798A7F89E7CE2C0C5D2A64BBE2B2AC4BB09A18868E3FFC906109")]
        public void ProductionLevel_AssetAndMetaHashesRemainImmutable(string name,
            string assetHash, string metaHash)
        {
            string path = Path.GetFullPath(
                $"Assets/NeonGrid/Resources/Levels/Automation Plant/{name}.asset");
            Assert.That(Hash(path), Is.EqualTo(assetHash));
            Assert.That(Hash(path + ".meta"), Is.EqualTo(metaHash));
        }

        [TestCase("AP_01", 5, 283, 5)]
        [TestCase("AP_02", 6, 564, 6)]
        [TestCase("AP_03", 8, 1095, 8)]
        [TestCase("AP_04", 5, 916, 5)]
        [TestCase("AP_05", 6, 1314, 6)]
        [TestCase("AP_06", 7, 1506, 7)]
        [TestCase("AP_07", 7, 4702, 7)]
        [TestCase("AP_08", 6, 739, 6)]
        [TestCase("AP_09", 6, 714, 6)]
        [TestCase("AP_10", 9, 2283, 9)]
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
        public void Campaign_UsesStableStructureNoTutorialsAndSequentialProgression()
        {
            CampaignDefinition campaign = LoadCampaign();
            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            Assert.That(validation.IsValid, Is.True,
                string.Join("\n", validation.Issues.Select(issue => issue.Message)));
            Assert.That(validation.Issues, Is.Empty);
            Assert.That(campaign.CampaignId, Is.EqualTo("automation_plant_vertical_slice"));
            Assert.That(campaign.Chapters, Has.Count.EqualTo(1));
            CampaignChapterDefinition chapter = campaign.Chapters[0];
            Assert.That(chapter.ChapterId, Is.EqualTo("automation_plant"));
            Assert.That(chapter.DisplayName, Is.EqualTo("Automation Plant"));
            Assert.That(chapter.Levels, Has.Count.EqualTo(10));
            Assert.That(chapter.Levels.Select(level => level.LevelId), Is.EqualTo(
                Enumerable.Range(1, 10).Select(index => $"automation_plant_{index:D2}")));
            for (int index = 0; index < 10; index++)
            {
                Assert.That(chapter.Levels[index].LevelDefinition,
                    Is.SameAs(LoadLevel($"AP_{index + 1:D2}")));
                Assert.That(chapter.Levels[index].Tutorial, Is.Null,
                    "Automation Plant must not add onboarding metadata.");
            }

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
            Assert.That(progress.GetChapterState("automation_plant"),
                Is.EqualTo(CampaignChapterState.Restored));
            Assert.That(progress.TotalStars, Is.EqualTo(30));
        }

        [Test]
        public void Navigation_IsGenericThroughAP10AndReplayUsesNormalFinalNavigation()
        {
            CampaignDefinition campaign = LoadCampaign();
            var flow = new CampaignFlowCoordinator(campaign,
                new CampaignProgressService(campaign), new MemoryStore());
            Assert.That(flow.OpenChapter("automation_plant"), Is.True);
            Assert.That(flow.StartLevel("automation_plant_01"), Is.True);
            for (int index = 0; index < 9; index++)
            {
                Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.False);
                Solve(flow.ActiveSession);
                AssertNormalNavigation(flow.ResultNavigation, true);
                Assert.That(flow.StartNextLevel(), Is.True);
            }
            Assert.That(flow.ActiveLevel.LevelId, Is.EqualTo("automation_plant_10"));
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

        [Test]
        public void PartialProgress_SaveLoadAndCampaignIsolationRemainGeneric()
        {
            CampaignDefinition automation = LoadCampaign();
            CampaignDefinition power = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            CampaignDefinition substation = Resources.Load<CampaignDefinition>(
                "Campaigns/Substation_VerticalSlice");
            CampaignDefinition control = Resources.Load<CampaignDefinition>(
                "Campaigns/ControlCenter_VerticalSlice");
            string root = Path.Combine(Path.GetTempPath(), "NeonGridM10Tests",
                Guid.NewGuid().ToString("N"));
            string path = CampaignSaveStore.BuildSavePath(root, automation.CampaignId);
            try
            {
                Assert.That(path, Does.EndWith(Path.Combine("NeonGrid",
                    "automation_plant_vertical_slice", CampaignSaveStore.SaveFileName)));
                Assert.That(path, Is.Not.EqualTo(CampaignSaveStore.BuildSavePath(root,
                    power.CampaignId)));
                Assert.That(path, Is.Not.EqualTo(CampaignSaveStore.BuildSavePath(root,
                    substation.CampaignId)));
                Assert.That(path, Is.Not.EqualTo(CampaignSaveStore.BuildSavePath(root,
                    control.CampaignId)));
                var progress = new CampaignProgressService(automation);
                for (int index = 0; index < 4; index++)
                    Assert.That(Record(progress, automation.Chapters[0].Levels[index],
                        index % 3 + 1).Accepted, Is.True);
                Assert.That(new CampaignSaveStore(path).Save(progress).Succeeded, Is.True);
                CampaignLoadResult load = new CampaignSaveStore(path).Load(automation);
                Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
                for (int index = 0; index < 4; index++)
                {
                    LevelProgress restored = load.Progress.GetLevelProgress(
                        $"automation_plant_{index + 1:D2}");
                    Assert.That(restored.Completed, Is.True);
                    Assert.That(restored.BestStars, Is.EqualTo(index % 3 + 1));
                    Assert.That(restored.BestMoves, Is.EqualTo(index % 3 + 1));
                    Assert.That(restored.BestTimeSeconds, Is.EqualTo(1f));
                }
                Assert.That(load.Progress.IsLevelUnlocked("automation_plant_05"), Is.True);
                Assert.That(load.Progress.IsLevelUnlocked("automation_plant_06"), Is.False);
                Assert.That(load.Progress.GetChapterState("automation_plant"),
                    Is.EqualTo(CampaignChapterState.Available));
                Assert.That(new CampaignSaveStore(path).Load(control).Status,
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
            var root = new GameObject("Automation Plant HUD Test");
            try
            {
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(new GameplaySession(entry.LevelDefinition,
                        entry.AuthoredOptimalMoves.Value), EmptyActions(), null,
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
            var root = new GameObject("Automation Plant Selector Test");
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

        [TestCase("AP_07", 4702, 7)]
        [TestCase("AP_08", 739, 6)]
        [TestCase("AP_09", 714, 6)]
        [TestCase("AP_10", 2283, 9)]
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
        public void RuntimeHint_AP10MeaningfulMemoryOnlyScrambleDoesNotMutateAsset()
        {
            LevelDefinition level = LoadLevel("AP_10");
            string path = Path.GetFullPath(
                "Assets/NeonGrid/Resources/Levels/Automation Plant/AP_10.asset");
            string before = Hash(path);
            BoardState board = level.CreateBoardState();
            var scramble = new PuzzleAction(new GridPosition(2, 0),
                PuzzleActionType.RotateClockwise);
            Assert.That(board.TryApplyAction(scramble), Is.True);
            var timer = Stopwatch.StartNew();
            PuzzleSolverResult result = new PuzzleSolver().Solve(board,
                PuzzleSolverProfiles.RuntimeHint);
            timer.Stop();
            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(result.ExploredStateCount,
                Is.LessThan(PuzzleSolverProfiles.RuntimeHint.MaximumExploredStates));
            Assert.That(Hash(path), Is.EqualTo(before));
            TestContext.WriteLine($"AP_10 RuntimeHint after {scramble}: status={result.Status}, " +
                                  $"explored={result.ExploredStateCount}, " +
                                  $"depth={result.DeepestSearchDepth}, " +
                                  $"ms={timer.Elapsed.TotalMilliseconds:F2}, " +
                                  $"limitHit={result.Status == PuzzleSolverStatus.SearchLimitReached}");
        }

        [UnityTest]
        public IEnumerator AP10_RealBackgroundHintReachesTerminalStateThroughMainThreadPump()
        {
            var session = new GameplaySession(LoadLevel("AP_10"), 9);
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
            TestContext.WriteLine($"AP_10 async RuntimeHint delivery: elapsedMs={watchdog.Elapsed.TotalMilliseconds:F2}, " +
                                  $"status={session.LastHint.Status}");
            session.Dispose();
        }

        [TestCase(6, 7)]
        [TestCase(7, 6)]
        [TestCase(8, 6)]
        [TestCase(9, 9)]
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
        public void RuntimeScene_UsesAutomationPlantCampaignAndIsEnabledInBuildSettings()
        {
            const string path = "Assets/NeonGrid/Scenes/M10_AutomationPlant_VerticalSlice.unity";
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
                $"Levels/Automation Plant/{name}");
            Assert.That(level, Is.Not.Null, $"Missing {name}.");
            return level;
        }

        private static CampaignDefinition LoadCampaign()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/AutomationPlant_VerticalSlice");
            Assert.That(campaign, Is.Not.Null);
            return campaign;
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
            public string SavePath => "memory://automation-plant";
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

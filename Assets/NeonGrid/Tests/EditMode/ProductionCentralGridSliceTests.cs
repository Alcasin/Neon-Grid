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
    public sealed class ProductionCentralGridSliceTests
    {
        [TestCase("CG_01", "29C8EB6EFACEF8253FFF111B51B3863580E17D9652CEB8869A93721257FE95C8", "A8D9FD9D0157A11BD66B62DAB8498BDFDA0B33C077F098E347F488770C3BB806")]
        [TestCase("CG_02", "A486AE430E5BBE5D20214B491FE680E45BF434192D086F7BF2BBA1889CF8C73C", "0A77EC9494D3BF485E757D31DE2455ECCF033552B3B981601157101D2D666335")]
        [TestCase("CG_03", "BD707B1F1EE3D19EBA3B0AEF7645E0C587C582774854563E404EF29473C6A595", "2F3206F3B16CC7145F6A6010B5B018F9F0BA64ECD2A959804270C69388114C46")]
        [TestCase("CG_04", "5BB9AA143EE45740965B1A681E183D29F539EBB7DBE8FCA6BA8EBF852712A20A", "06CA561768CE39D03657C252782E11A8736883939DBA98209549D7FCCABA855B")]
        [TestCase("CG_05", "1EB706EC00B118215D765AB8E02CC80BE679FF83D180265AB4AA64F335098FE3", "BE902013EE2BEB6D3DEA89943119489A32D2A8CF1D5FF50D3F351352D855F75E")]
        [TestCase("CG_06", "A8F5B72F332BA1268D6CCC6FA1F5E4809A075C47CA1BC051C4B29040E30D5439", "83D340CE7816B59AFB0F97DD10546F4A0917CCB84FC5781E29572CAB6A27A050")]
        [TestCase("CG_07", "D264971F3A4A025B1B0EC422CF9CE95A57DE27E355B7F3FCEAE9B009009CD576", "80F2C4DA5E1AA101981B1B65AB620F29E783D82BD1BFA1611DADADCD00DF8897")]
        [TestCase("CG_08", "F6B4F2D823D46B1F1A5ECE032578F783C394CDEA458FE4ABA52301383277DFF6", "44349C89305EA2B95AAEB1D6AEEEFEA8A94FAD3400FEA958E93ECAC640C52C00")]
        [TestCase("CG_09", "43007DCAA962409EEE638D9E58EBF02580EDFF6D2E3EFFDF3EBDAA7B5655E96D", "4D43FA7FF804F832C7C5E9F96BE2CD14CA7392AE23B09A59695F83763AC85E53")]
        [TestCase("CG_10", "525FC7F9C02FE3940A1EB88DDC2F79141F9883E21D55FF34FF175119F7005CC6", "653408B6763F9F683A35E38D988A08F72E22DF0F1732DF52951189F638DC5C32")]
        public void ProductionLevel_AssetAndMetaHashesRemainImmutable(string name,
            string assetHash, string metaHash)
        {
            string path = Path.GetFullPath(
                $"Assets/NeonGrid/Resources/Levels/CentralGrid/{name}.asset");
            Assert.That(Hash(path), Is.EqualTo(assetHash));
            Assert.That(Hash(path + ".meta"), Is.EqualTo(metaHash));
        }

        [TestCase("CG_01", 5, 5975, 5)]
        [TestCase("CG_02", 6, 21609, 6)]
        [TestCase("CG_03", 6, 14100, 6)]
        [TestCase("CG_04", 6, 23443, 6)]
        [TestCase("CG_05", 5, 5582, 5)]
        [TestCase("CG_06", 7, 4278, 7)]
        [TestCase("CG_07", 7, 11152, 7)]
        [TestCase("CG_08", 9, 17798, 9)]
        [TestCase("CG_09", 9, 15941, 9)]
        [TestCase("CG_10", 10, 45741, 10)]
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
            Assert.That(campaign.CampaignId, Is.EqualTo("central_grid_vertical_slice"));
            Assert.That(campaign.Chapters, Has.Count.EqualTo(1));
            CampaignChapterDefinition chapter = campaign.Chapters[0];
            Assert.That(chapter.ChapterId, Is.EqualTo("central_grid"));
            Assert.That(chapter.DisplayName, Is.EqualTo("Central Grid"));
            Assert.That(chapter.Levels, Has.Count.EqualTo(10));
            Assert.That(chapter.Levels.Select(level => level.LevelId), Is.EqualTo(
                Enumerable.Range(1, 10).Select(index => $"central_grid_{index:D2}")));
            for (int index = 0; index < 10; index++)
            {
                Assert.That(chapter.Levels[index].LevelDefinition,
                    Is.SameAs(LoadLevel($"CG_{index + 1:D2}")));
                Assert.That(chapter.Levels[index].Tutorial, Is.Null,
                    "Central Grid must not add onboarding metadata.");
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
            Assert.That(progress.GetChapterState("central_grid"),
                Is.EqualTo(CampaignChapterState.Restored));
            Assert.That(progress.TotalStars, Is.EqualTo(30));
        }

        [Test]
        public void Navigation_IsGenericThroughCG10AndReplayUsesNormalFinalNavigation()
        {
            CampaignDefinition campaign = LoadCampaign();
            var flow = new CampaignFlowCoordinator(campaign,
                new CampaignProgressService(campaign), new MemoryStore());
            Assert.That(flow.OpenChapter("central_grid"), Is.True);
            Assert.That(flow.StartLevel("central_grid_01"), Is.True);
            for (int index = 0; index < 9; index++)
            {
                Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.False);
                Solve(flow.ActiveSession);
                AssertNormalNavigation(flow.ResultNavigation, true);
                Assert.That(flow.StartNextLevel(), Is.True);
            }
            Assert.That(flow.ActiveLevel.LevelId, Is.EqualTo("central_grid_10"));
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
            CampaignDefinition central = LoadCampaign();
            string[] otherCampaigns =
            {
                "PowerStation_VerticalSlice", "Substation_VerticalSlice",
                "ControlCenter_VerticalSlice", "AutomationPlant_VerticalSlice"
            };
            string root = Path.Combine(Path.GetTempPath(), "NeonGridM11Tests",
                Guid.NewGuid().ToString("N"));
            string path = CampaignSaveStore.BuildSavePath(root, central.CampaignId);
            try
            {
                Assert.That(path, Does.EndWith(Path.Combine("NeonGrid",
                    "central_grid_vertical_slice", CampaignSaveStore.SaveFileName)));
                foreach (string resourceName in otherCampaigns)
                {
                    CampaignDefinition other = Resources.Load<CampaignDefinition>(
                        $"Campaigns/{resourceName}");
                    Assert.That(other, Is.Not.Null);
                    Assert.That(path, Is.Not.EqualTo(CampaignSaveStore.BuildSavePath(root,
                        other.CampaignId)));
                }
                var progress = new CampaignProgressService(central);
                for (int index = 0; index < 4; index++)
                    Assert.That(Record(progress, central.Chapters[0].Levels[index],
                        index % 3 + 1).Accepted, Is.True);
                Assert.That(new CampaignSaveStore(path).Save(progress).Succeeded, Is.True);
                CampaignLoadResult load = new CampaignSaveStore(path).Load(central);
                Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
                for (int index = 0; index < 4; index++)
                {
                    LevelProgress restored = load.Progress.GetLevelProgress(
                        $"central_grid_{index + 1:D2}");
                    Assert.That(restored.Completed, Is.True);
                    Assert.That(restored.BestStars, Is.EqualTo(index % 3 + 1));
                    Assert.That(restored.BestMoves, Is.EqualTo(index % 3 + 1));
                    Assert.That(restored.BestTimeSeconds, Is.EqualTo(1f));
                }
                Assert.That(load.Progress.IsLevelUnlocked("central_grid_05"), Is.True);
                Assert.That(load.Progress.IsLevelUnlocked("central_grid_06"), Is.False);
                Assert.That(load.Progress.GetChapterState("central_grid"),
                    Is.EqualTo(CampaignChapterState.Available));
                CampaignDefinition mismatch = Resources.Load<CampaignDefinition>(
                    "Campaigns/AutomationPlant_VerticalSlice");
                Assert.That(new CampaignSaveStore(path).Load(mismatch).Status,
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
            var root = new GameObject("Central Grid HUD Test");
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
            var root = new GameObject("Central Grid Selector Test");
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

        [TestCase("CG_07", 11152, 7)]
        [TestCase("CG_08", 17798, 9)]
        [TestCase("CG_09", 15941, 9)]
        [TestCase("CG_10", 45741, 10)]
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
        public void RuntimeHint_CG10MeaningfulMemoryOnlyScrambleDoesNotMutateAsset()
        {
            LevelDefinition level = LoadLevel("CG_10");
            string path = Path.GetFullPath(
                "Assets/NeonGrid/Resources/Levels/CentralGrid/CG_10.asset");
            string before = Hash(path);
            BoardState board = level.CreateBoardState();
            var scramble = new PuzzleAction(new GridPosition(1, 0),
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
            TestContext.WriteLine($"CG_10 RuntimeHint after {scramble}: status={result.Status}, " +
                                  $"explored={result.ExploredStateCount}, " +
                                  $"depth={result.DeepestSearchDepth}, " +
                                  $"ms={timer.Elapsed.TotalMilliseconds:F2}, " +
                                  $"limitHit={result.Status == PuzzleSolverStatus.SearchLimitReached}");
        }

        [UnityTest]
        public IEnumerator CG10_RealBackgroundHintReachesTerminalStateThroughMainThreadPump()
        {
            var session = new GameplaySession(LoadLevel("CG_10"), 10);
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
            TestContext.WriteLine($"CG_10 async RuntimeHint delivery: elapsedMs={watchdog.Elapsed.TotalMilliseconds:F2}, " +
                                  $"status={session.LastHint.Status}");
            session.Dispose();
        }

        [TestCase(6, 7)]
        [TestCase(7, 9)]
        [TestCase(8, 9)]
        [TestCase(9, 10)]
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
        public void RuntimeScene_UsesCentralGridCampaignAndIsEnabledInBuildSettings()
        {
            const string path = "Assets/NeonGrid/Scenes/M11_CentralGrid_VerticalSlice.unity";
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
                $"Levels/CentralGrid/{name}");
            Assert.That(level, Is.Not.Null, $"Missing {name}.");
            return level;
        }

        private static CampaignDefinition LoadCampaign()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/CentralGrid_VerticalSlice");
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
            public string SavePath => "memory://central-grid";
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

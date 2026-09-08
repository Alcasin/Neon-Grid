using System;
using System.Collections.Generic;
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
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class ProductionSubstationSliceTests
    {
        [TestCase("S_01", "AC62A981D072576E081A9F28B948CABACAD2B734569BA256A53ECAD1863301CD")]
        [TestCase("S_02", "BA5B2A015404D24782A221E41847E2F74C0A52F6A3343AF6AA25C015F5679642")]
        [TestCase("S_03", "6FA4E85600075FC5065FB09C2203985421ED8E9CCB80E0D272B8DF8BF248FA8D")]
        [TestCase("S_04", "82EAE1DED6B3A1E4AB99BBB9D7D74CC301F74ECCF95C618CAF1B3EA83393E929")]
        [TestCase("S_05", "23B6A453134000A8B998C5C965B42C03326464E1087843413DD1237D8F0C98EE")]
        [TestCase("S_06", "53D848BC4FDA43BDE3F7D847DF271A7E96AE3E1C0AF63AF6FCEE1A6756930BEA")]
        public void ProductionLevel_AssetHashRemainsImmutable(string assetName,
            string expectedHash)
        {
            string path = Path.GetFullPath(
                $"Assets/NeonGrid/Resources/Levels/Substation/{assetName}.asset");
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                string actualHash = BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty);
                Assert.That(actualHash, Is.EqualTo(expectedHash));
            }
        }

        [TestCase("S_01", 3)]
        [TestCase("S_02", 7)]
        [TestCase("S_03", 3)]
        [TestCase("S_04", 3)]
        [TestCase("S_05", 3)]
        [TestCase("S_06", 3)]
        public void ProductionLevel_LoadsValidAndMatchesExactSolverMinimum(
            string assetName, int expectedMinimumMoves)
        {
            LevelDefinition level = LoadLevel(assetName);
            LevelValidationResult validation = new LevelValidator().Validate(level);
            Assert.That(validation.IsValid, Is.True,
                string.Join("\n", validation.Errors.Select(issue => issue.Message)));
            Assert.That(validation.Warnings, Is.Empty);

            PuzzleSolverResult solution = new PuzzleSolver().Solve(level.CreateBoardState());
            Assert.That(solution.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(solution.MinimumMoveCount, Is.EqualTo(expectedMinimumMoves));
        }

        [Test]
        public void Campaign_UsesSixProductionAssetsAndUnlocksSequentially()
        {
            CampaignDefinition campaign = LoadCampaign();
            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            Assert.That(validation.IsValid, Is.True,
                string.Join("\n", validation.Issues.Select(issue => issue.Message)));
            Assert.That(validation.Issues, Is.Empty);
            Assert.That(campaign.CampaignId, Is.EqualTo("substation_vertical_slice"));
            Assert.That(campaign.Chapters, Has.Count.EqualTo(1));

            CampaignChapterDefinition chapter = campaign.Chapters[0];
            Assert.That(chapter.ChapterId, Is.EqualTo("substation"));
            Assert.That(chapter.DisplayName, Is.EqualTo("Substation"));
            Assert.That(chapter.Levels, Has.Count.EqualTo(6));
            Assert.That(chapter.Levels.Select(level => level.LevelId), Is.EqualTo(new[]
            {
                "substation_01", "substation_02", "substation_03",
                "substation_04", "substation_05", "substation_06"
            }));
            Assert.That(chapter.Levels.Select(level => level.DisplayName), Is.EqualTo(new[]
            {
                "Substation Circuit 1", "Substation Circuit 2", "Substation Circuit 3",
                "Substation Circuit 4", "Substation Circuit 5", "Substation Circuit 6"
            }));
            for (int index = 0; index < chapter.Levels.Count; index++)
                Assert.That(chapter.Levels[index].LevelDefinition,
                    Is.SameAs(LoadLevel($"S_{index + 1:D2}")));
            Assert.That(chapter.Levels[0].Tutorial, Is.Null);
            Assert.That(chapter.Levels[1].Tutorial, Is.Null);
            AssertSwitchTutorial(chapter.Levels[2]);
            for (int index = 3; index < chapter.Levels.Count; index++)
                Assert.That(chapter.Levels[index].Tutorial, Is.Null);

            var progress = new CampaignProgressService(campaign);
            Assert.That(progress.MaximumCampaignStars, Is.EqualTo(18));
            Assert.That(progress.IsLevelUnlocked("substation_01"), Is.True);
            for (int index = 1; index < chapter.Levels.Count; index++)
                Assert.That(progress.IsLevelUnlocked(chapter.Levels[index].LevelId), Is.False);

            for (int index = 0; index < chapter.Levels.Count; index++)
            {
                CampaignProgressUpdate update = Record(progress, chapter.Levels[index], 1);
                Assert.That(update.Accepted, Is.True);
                Assert.That(update.ChapterJustRestored, Is.EqualTo(index == 5));
                if (index + 1 < chapter.Levels.Count)
                    Assert.That(progress.IsLevelUnlocked(chapter.Levels[index + 1].LevelId),
                        Is.True, "Completing the preceding level must unlock the next level.");
                if (index == 2)
                {
                    Assert.That(progress.IsLevelUnlocked("substation_04"), Is.True);
                    Assert.That(progress.GetChapterState("substation"),
                        Is.EqualTo(CampaignChapterState.Available),
                        "Completing the former final level must not restore the expanded chapter.");
                }
            }

            Assert.That(progress.GetChapterState("substation"),
                Is.EqualTo(CampaignChapterState.Restored));
        }

        [Test]
        public void Navigation_UsesSixLevelOrderingAndRestoresOnlyAfterS06()
        {
            CampaignDefinition campaign = LoadCampaign();
            var flow = new CampaignFlowCoordinator(campaign,
                new CampaignProgressService(campaign), new MemoryStore());

            Assert.That(flow.OpenChapter("substation"), Is.True);
            Assert.That(flow.StartLevel("substation_01"), Is.True);
            for (int index = 0; index <= 4; index++)
            {
                Assert.That(flow.ActiveLevel.LevelId,
                    Is.EqualTo($"substation_{index + 1:D2}"));
                Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.False);
                Solve(flow.ActiveSession);
                Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.False);
                AssertNormalNavigation(flow.ResultNavigation, true);
                Assert.That(flow.StartNextLevel(), Is.True);
            }

            Assert.That(flow.ActiveLevel.LevelId, Is.EqualTo("substation_06"));
            Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.True);
            Solve(flow.ActiveSession);
            Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.True);
            Assert.That(flow.Progress.GetChapterState("substation"),
                Is.EqualTo(CampaignChapterState.Restored));
            Assert.That(flow.ResultNavigation.ShowRetry, Is.True);
            Assert.That(flow.ResultNavigation.ShowLevels, Is.False);
            Assert.That(flow.ResultNavigation.ShowMap, Is.True);
            Assert.That(flow.ResultNavigation.ShowNext, Is.False);

            Assert.That(flow.Retry(), Is.True);
            Solve(flow.ActiveSession);
            Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.False);
            AssertNormalNavigation(flow.ResultNavigation, false);
        }

        [Test]
        public void PreviousThreeLevelSave_PreservesBestsUnlocksS04AndIsNotRestored()
        {
            CampaignDefinition campaign = LoadCampaign();
            string directory = Path.Combine(Path.GetTempPath(), "NeonGridM8Tests",
                Guid.NewGuid().ToString("N"));
            string savePath = Path.Combine(directory, CampaignSaveStore.SaveFileName);
            Directory.CreateDirectory(directory);
            try
            {
                var entries = new List<LevelProgressSaveEntry>();
                for (int index = 1; index <= 3; index++)
                    entries.Add(new LevelProgressSaveEntry($"substation_{index:D2}", true,
                        index, index + 4, index * 11f));
                File.WriteAllText(savePath, JsonUtility.ToJson(new CampaignSaveData
                {
                    version = CampaignSaveStore.CurrentVersion,
                    campaignId = "substation_vertical_slice",
                    levelProgressEntries = entries
                }, true));

                CampaignLoadResult load = new CampaignSaveStore(savePath).Load(campaign);

                Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
                Assert.That(load.Diagnostics, Is.Empty);
                for (int index = 1; index <= 3; index++)
                {
                    LevelProgress restored = load.Progress.GetLevelProgress(
                        $"substation_{index:D2}");
                    Assert.That(restored.Completed, Is.True);
                    Assert.That(restored.BestStars, Is.EqualTo(index));
                    Assert.That(restored.BestMoves, Is.EqualTo(index + 4));
                    Assert.That(restored.BestTimeSeconds, Is.EqualTo(index * 11f));
                }

                for (int index = 4; index <= 6; index++)
                    Assert.That(load.Progress.GetLevelProgress($"substation_{index:D2}").Completed,
                        Is.False);
                Assert.That(load.Progress.IsLevelUnlocked("substation_04"), Is.True);
                Assert.That(load.Progress.IsLevelUnlocked("substation_05"), Is.False);
                Assert.That(load.Progress.IsLevelUnlocked("substation_06"), Is.False);
                Assert.That(load.Progress.GetChapterState("substation"),
                    Is.EqualTo(CampaignChapterState.Available));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SwitchTutorial_IsDataDrivenAndUsesAcceptedAttemptSemantics()
        {
            CampaignDefinition campaign = LoadCampaign();
            CampaignChapterDefinition chapter = campaign.Chapters[0];
            CampaignLevelEntry entry = chapter.Levels[2];
            AssertSwitchTutorial(entry);
            GridPosition target = new GridPosition(2, 1);
            var root = new GameObject("Substation Switch Tutorial Test");
            try
            {
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(new GameplaySession(entry.LevelDefinition),
                    new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                        () => { }, () => default), entry.Tutorial, 3);
                CircuitTileView targetView = root.transform.Find("Tile 2,1")
                    .GetComponent<CircuitTileView>();

                Assert.That(controller.Tutorial.IsActive, Is.True);
                Assert.That(targetView.IsTutorialHighlighted, Is.True);
                Assert.That(controller.Session.MoveCount, Is.Zero);
                Assert.That(controller.Session.Board.GetTile(target).IsSwitchOn, Is.False,
                    "Showing the tutorial must not execute its target action.");

                Assert.That(controller.Session.Board.TryGetPlayerAction(new GridPosition(1, 1),
                    out PuzzleAction unrelated), Is.True);
                Assert.That(unrelated.ActionType, Is.EqualTo(PuzzleActionType.RotateClockwise));
                Assert.That(controller.PerformPlayerAction(unrelated.Position), Is.True);
                Assert.That(controller.Tutorial.IsActive, Is.True);
                Assert.That(targetView.IsTutorialHighlighted, Is.True);

                Assert.That(controller.Session.Board.TryGetPlayerAction(target,
                    out PuzzleAction toggle), Is.True);
                Assert.That(toggle.ActionType, Is.EqualTo(PuzzleActionType.ToggleSwitch));
                Assert.That(controller.PerformPlayerAction(target), Is.True);
                Assert.That(controller.Tutorial.IsActive, Is.False);
                Assert.That(targetView.IsTutorialHighlighted, Is.False);
                Assert.That(controller.Session.MoveCount, Is.EqualTo(2));
                Assert.That(controller.Session.HintsUsed, Is.False);
                Assert.That(controller.Session.IsCompleted, Is.False,
                    "The remaining wrong wire must prevent the tutorial toggle from solving S_03.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var progress = new CampaignProgressService(campaign);
            Record(progress, chapter.Levels[0], 1);
            Record(progress, chapter.Levels[1], 1);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore());
            flow.OpenChapter("substation");
            flow.StartLevel("substation_03");
            Assert.That(flow.ActiveTutorial, Is.Not.Null);
            Solve(flow.ActiveSession);
            Assert.That(flow.Retry(), Is.True);
            Assert.That(flow.ActiveTutorial, Is.Null,
                "Completed replay must skip attempt-local onboarding.");

            var freshProgress = new CampaignProgressService(campaign);
            Record(freshProgress, chapter.Levels[0], 1);
            Record(freshProgress, chapter.Levels[1], 1);
            var freshFlow = new CampaignFlowCoordinator(campaign, freshProgress, new MemoryStore());
            freshFlow.OpenChapter("substation");
            freshFlow.StartLevel("substation_03");
            Assert.That(freshFlow.ActiveTutorial, Is.Not.Null,
                "Fresh/reset progress must restore the Switch tutorial.");
        }

        [Test]
        public void CampaignSaveStores_AreIsolatedAndPreserveCampaignMismatchSemantics()
        {
            CampaignDefinition substation = LoadCampaign();
            CampaignDefinition powerStation = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            string root = Path.Combine(Path.GetTempPath(), "NeonGridSubstationTests",
                Guid.NewGuid().ToString("N"));
            string substationPath = CampaignSaveStore.BuildSavePath(root,
                substation.CampaignId);
            string powerStationPath = CampaignSaveStore.BuildSavePath(root,
                powerStation.CampaignId);

            try
            {
                Assert.That(substationPath, Does.EndWith(Path.Combine("NeonGrid",
                    "substation_vertical_slice", CampaignSaveStore.SaveFileName)));
                Assert.That(powerStationPath, Is.Not.EqualTo(substationPath));

                var substationProgress = new CampaignProgressService(substation);
                Record(substationProgress, substation.Chapters[0].Levels[0], 2);
                var substationStore = new CampaignSaveStore(substationPath);
                Assert.That(substationStore.Save(substationProgress).Succeeded, Is.True);

                var powerStationStore = new CampaignSaveStore(powerStationPath);
                CampaignLoadResult untouchedPowerStation = powerStationStore.Load(powerStation);
                Assert.That(untouchedPowerStation.Status,
                    Is.EqualTo(CampaignLoadStatus.NoSaveFound));
                Assert.That(untouchedPowerStation.Progress.GetLevelProgress("power_01").Completed,
                    Is.False);

                CampaignLoadResult restoredSubstation = substationStore.Load(substation);
                Assert.That(restoredSubstation.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
                Assert.That(restoredSubstation.Progress
                    .GetLevelProgress("substation_01").Completed, Is.True);
                Assert.That(restoredSubstation.Progress.GetLevelProgress("power_01"), Is.Null);

                CampaignLoadResult mismatch = substationStore.Load(powerStation);
                Assert.That(mismatch.Status, Is.EqualTo(CampaignLoadStatus.CampaignMismatch));
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
        public void GameplayHud_UsesSubstationChapterOrdinal(int levelIndex, string expectedLabel)
        {
            CampaignDefinition campaign = LoadCampaign();
            CampaignChapterDefinition chapter = campaign.Chapters[0];
            CampaignLevelEntry entry = chapter.Levels[levelIndex];
            var root = new GameObject("Substation HUD Ordinal Test");

            try
            {
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(new GameplaySession(entry.LevelDefinition),
                    new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                        () => { }, () => default), entry.Tutorial,
                    CampaignRuntimeController.FindLevelOrdinal(chapter, entry));
                Transform identity = root.transform.Find(
                    "Gameplay HUD Canvas/Level Identity");
                Assert.That(identity, Is.Not.Null);
                Assert.That(identity.GetComponent<Text>().text, Is.EqualTo(expectedLabel));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Selector_UsesTwoRowsOfThreeWithoutScrolling()
        {
            CampaignDefinition campaign = LoadCampaign();
            var progress = new CampaignProgressService(campaign);
            var root = new GameObject("Substation Selector Test");

            try
            {
                var view = root.AddComponent<CampaignRuntimeView>();
                view.Build(campaign, progress, _ => { }, _ => { }, () => { });
                view.ShowChapter(campaign.Chapters[0]);
                Transform selection = root.transform.Find("Campaign Canvas/Level Selection");
                Transform grid = selection.Find("Generated Level Grid");
                Assert.That(grid.childCount, Is.EqualTo(2));
                foreach (Transform row in grid)
                {
                    Assert.That(row.childCount, Is.EqualTo(3));
                    foreach (Transform tile in row)
                        Assert.That(tile.GetComponent<RectTransform>().sizeDelta,
                            Is.EqualTo(new Vector2(220f, 220f)));
                }
                Assert.That(selection.GetComponentInChildren<ScrollRect>(true), Is.Null);
                Assert.That(selection.Find("Back To Map"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RuntimeScene_UsesSubstationCampaignAndIsEnabledInBuildSettings()
        {
            const string scenePath =
                "Assets/NeonGrid/Scenes/M8_Substation_VerticalSlice.unity";
            CampaignDefinition campaign = LoadCampaign();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Any(scene =>
                scene.enabled && scene.path == scenePath), Is.True);

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                CampaignRuntimeController controller = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<CampaignRuntimeController>())
                    .Single(component => component != null);
                var serializedController = new SerializedObject(controller);
                Assert.That(serializedController.FindProperty("campaign").objectReferenceValue,
                    Is.SameAs(campaign));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static LevelDefinition LoadLevel(string assetName)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(
                $"Levels/Substation/{assetName}");
            Assert.That(level, Is.Not.Null, $"Missing production level asset {assetName}.");
            return level;
        }

        private static CampaignDefinition LoadCampaign()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/Substation_VerticalSlice");
            Assert.That(campaign, Is.Not.Null);
            return campaign;
        }

        private static CampaignProgressUpdate Record(CampaignProgressService progress,
            CampaignLevelEntry level, int stars)
        {
            return progress.RecordCompletion(level.LevelId,
                CampaignTestFixture.Result(level.LevelDefinition, stars, 1f, stars));
        }

        private static void Solve(GameplaySession session)
        {
            PuzzleSolverResult solution = new PuzzleSolver().Solve(session.Board);
            Assert.That(solution.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            foreach (PuzzleAction action in solution.Solution)
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

        private static void AssertSwitchTutorial(CampaignLevelEntry level)
        {
            Assert.That(level.Tutorial, Is.Not.Null);
            Assert.That(level.Tutorial.Steps, Has.Count.EqualTo(1));
            TutorialStepDefinition step = level.Tutorial.Steps[0];
            Assert.That(step.Message, Is.EqualTo("Switches can open or close a circuit."));
            Assert.That(step.TargetPosition, Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(step.CompletionCondition,
                Is.EqualTo(TutorialCompletionCondition.ToggleSwitch));
            Assert.That(level.LevelDefinition.CreateBoardState()
                .GetTile(step.TargetPosition).TileType, Is.EqualTo(TileType.Switch));
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public string SavePath => "memory://substation";

            public CampaignLoadResult Load(CampaignDefinition definition)
            {
                return new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(definition), Array.Empty<string>());
            }

            public CampaignSaveResult Save(CampaignProgressService progress)
            {
                return new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }

            public CampaignSaveResult Delete()
            {
                return new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }
        }
    }
}

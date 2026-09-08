using System;
using System.IO;
using System.Linq;
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
        [TestCase("S_01", 3)]
        [TestCase("S_02", 7)]
        [TestCase("S_03", 3)]
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
        public void Campaign_UsesThreeProductionAssetsAndUnlocksSequentially()
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
            Assert.That(chapter.Levels, Has.Count.EqualTo(3));
            Assert.That(chapter.Levels.Select(level => level.LevelId), Is.EqualTo(new[]
            {
                "substation_01", "substation_02", "substation_03"
            }));
            Assert.That(chapter.Levels.Select(level => level.DisplayName), Is.EqualTo(new[]
            {
                "Substation Circuit 1", "Substation Circuit 2", "Substation Circuit 3"
            }));
            for (int index = 0; index < chapter.Levels.Count; index++)
                Assert.That(chapter.Levels[index].LevelDefinition,
                    Is.SameAs(LoadLevel($"S_{index + 1:D2}")));
            Assert.That(chapter.Levels[0].Tutorial, Is.Null);
            Assert.That(chapter.Levels[1].Tutorial, Is.Null);
            AssertSwitchTutorial(chapter.Levels[2]);

            var progress = new CampaignProgressService(campaign);
            Assert.That(progress.MaximumCampaignStars, Is.EqualTo(9));
            Assert.That(progress.IsLevelUnlocked("substation_01"), Is.True);
            Assert.That(progress.IsLevelUnlocked("substation_02"), Is.False);
            Assert.That(progress.IsLevelUnlocked("substation_03"), Is.False);

            CampaignProgressUpdate first = Record(progress, chapter.Levels[0], 1);
            Assert.That(first.Accepted, Is.True);
            Assert.That(first.ChapterJustRestored, Is.False);
            Assert.That(progress.IsLevelUnlocked("substation_02"), Is.True,
                "One star must be sufficient because stars do not gate progression.");

            CampaignProgressUpdate second = Record(progress, chapter.Levels[1], 1);
            Assert.That(second.Accepted, Is.True);
            Assert.That(second.ChapterJustRestored, Is.False);
            Assert.That(progress.IsLevelUnlocked("substation_03"), Is.True);
            Assert.That(progress.GetChapterState("substation"),
                Is.EqualTo(CampaignChapterState.Available));

            CampaignProgressUpdate third = Record(progress, chapter.Levels[2], 1);
            Assert.That(third.Accepted, Is.True);
            Assert.That(third.ChapterJustRestored, Is.True,
                "S_03 is final only in this isolated three-level development campaign.");
        }

        [Test]
        public void Navigation_UsesGenericTemporaryThreeLevelFinalBehavior()
        {
            CampaignDefinition campaign = LoadCampaign();
            var flow = new CampaignFlowCoordinator(campaign,
                new CampaignProgressService(campaign), new MemoryStore());

            Assert.That(flow.OpenChapter("substation"), Is.True);
            Assert.That(flow.StartLevel("substation_01"), Is.True);
            Solve(flow.ActiveSession);
            AssertNormalNavigation(flow.ResultNavigation, true);

            Assert.That(flow.StartNextLevel(), Is.True);
            Assert.That(flow.ActiveLevel.LevelId, Is.EqualTo("substation_02"));
            Solve(flow.ActiveSession);
            AssertNormalNavigation(flow.ResultNavigation, true);

            Assert.That(flow.StartNextLevel(), Is.True);
            Assert.That(flow.ActiveLevel.LevelId, Is.EqualTo("substation_03"));
            Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.True);
            Solve(flow.ActiveSession);
            Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.True);
            Assert.That(flow.ResultNavigation.ShowRetry, Is.True);
            Assert.That(flow.ResultNavigation.ShowLevels, Is.False);
            Assert.That(flow.ResultNavigation.ShowMap, Is.True);
            Assert.That(flow.ResultNavigation.ShowNext, Is.False);
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
        public void Selector_ReusesCompactThreeColumnArchitecture()
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
                Assert.That(grid.childCount, Is.EqualTo(1));
                Assert.That(grid.GetChild(0).childCount, Is.EqualTo(3));
                foreach (Transform tile in grid.GetChild(0))
                    Assert.That(tile.GetComponent<RectTransform>().sizeDelta,
                        Is.EqualTo(new Vector2(220f, 220f)));
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

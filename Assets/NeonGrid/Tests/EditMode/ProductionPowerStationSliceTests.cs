using System;
using System.Collections.Generic;
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

namespace NeonGrid.Tests
{
    public sealed class ProductionPowerStationSliceTests
    {
        [TestCase("PS_01", 1)]
        [TestCase("PS_02", 1)]
        [TestCase("PS_03", 4)]
        [TestCase("PS_04", 5)]
        [TestCase("PS_05", 4)]
        [TestCase("PS_06", 3)]
        [TestCase("PS_07", 4)]
        [TestCase("PS_08", 4)]
        [TestCase("PS_09", 2)]
        [TestCase("PS_10", 5)]
        public void ProductionLevel_LoadsValidAndMatchesExactSolverMinimum(
            string assetName, int expectedMinimumMoves)
        {
            LevelDefinition level = LoadLevel(assetName);
            LevelValidationResult validation = new LevelValidator().Validate(level);
            Assert.That(validation.IsValid, Is.True,
                string.Join("\n", validation.Errors.Select(issue => issue.Message)));
            Assert.That(validation.Warnings, Is.Empty);

            PuzzleSolverResult solution = new PuzzleSolver().Solve(level.CreateBoardState(),
                PuzzleSolverProfiles.AuthoringExact);
            Assert.That(solution.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(solution.MinimumMoveCount, Is.EqualTo(expectedMinimumMoves));
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            CampaignLevelEntry entry = campaign.Chapters[0].Levels.Single(candidate =>
                candidate.LevelDefinition == level);
            Assert.That(entry.AuthoredOptimalMoves, Is.EqualTo(expectedMinimumMoves),
                "Runtime baseline metadata must stay synchronized with exact authoring verification.");
        }

        [Test]
        public void VerticalSliceCampaign_UsesProductionAssetsAndUnlocksSequentially()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            Assert.That(campaign, Is.Not.Null);

            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            Assert.That(validation.IsValid, Is.True,
                string.Join("\n", validation.Issues.Select(issue => issue.Message)));
            Assert.That(validation.Issues, Is.Empty);
            Assert.That(campaign.CampaignId, Is.EqualTo("power_station_vertical_slice"));
            Assert.That(campaign.Chapters, Has.Count.EqualTo(1));

            CampaignChapterDefinition chapter = campaign.Chapters[0];
            Assert.That(chapter.ChapterId, Is.EqualTo("power_station"));
            Assert.That(chapter.DisplayName, Is.EqualTo("Power Station"));
            Assert.That(chapter.Levels, Has.Count.EqualTo(10));
            Assert.That(chapter.Levels.Select(level => level.LevelId),
                Is.EqualTo(Enumerable.Range(1, 10).Select(index => $"power_{index:D2}")));
            Assert.That(chapter.Levels.Select(level => level.DisplayName),
                Is.EqualTo(Enumerable.Range(1, 10).Select(index => $"Power Circuit {index:D2}")));
            for (int index = 0; index < chapter.Levels.Count; index++)
                Assert.That(chapter.Levels[index].LevelDefinition,
                    Is.SameAs(LoadLevel($"PS_{index + 1:D2}")));

            AssertTutorial(chapter.Levels[0], "Tap a wire to rotate it.",
                new GridPosition(1, 0));
            AssertTutorial(chapter.Levels[1], "Corner wires redirect the current.",
                new GridPosition(1, 0));
            Assert.That(chapter.Levels[2].Tutorial, Is.Null,
                "PS_03 intentionally tests transferred learning without a tutorial.");
            Assert.That(chapter.Levels[3].Tutorial, Is.Null);
            Assert.That(chapter.Levels[4].Tutorial, Is.Null);
            AssertTutorial(chapter.Levels[5],
                "T-junctions split power into multiple paths.", new GridPosition(2, 2));
            Assert.That(chapter.Levels[6].Tutorial, Is.Null);
            Assert.That(chapter.Levels[7].Tutorial, Is.Null);
            AssertTutorial(chapter.Levels[8],
                "Diodes only allow power in one direction.", new GridPosition(2, 2));
            Assert.That(chapter.Levels[9].Tutorial, Is.Null);

            var progress = new CampaignProgressService(campaign);
            Assert.That(progress.MaximumCampaignStars, Is.EqualTo(30));
            Assert.That(progress.IsLevelUnlocked("power_01"), Is.True);
            for (int index = 1; index < chapter.Levels.Count; index++)
                Assert.That(progress.IsLevelUnlocked(chapter.Levels[index].LevelId), Is.False);

            for (int index = 0; index < chapter.Levels.Count; index++)
            {
                CampaignProgressUpdate update = Record(progress, chapter.Levels[index]);
                Assert.That(update.Accepted, Is.True);
                Assert.That(update.ChapterJustRestored, Is.EqualTo(index == 9));
                if (index + 1 < chapter.Levels.Count)
                    Assert.That(progress.IsLevelUnlocked(chapter.Levels[index + 1].LevelId), Is.True);
                if (index == 5)
                {
                    Assert.That(progress.IsLevelUnlocked("power_07"), Is.True);
                    Assert.That(progress.GetChapterState("power_station"),
                        Is.EqualTo(CampaignChapterState.Available));
                }
            }

            Assert.That(progress.GetChapterState("power_station"),
                Is.EqualTo(CampaignChapterState.Restored));
        }

        [Test]
        public void VerticalSliceNavigation_UsesTenLevelOrderingAndRestoresOnlyAfterPs10()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            var progress = new CampaignProgressService(campaign);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore());
            CampaignChapterDefinition chapter = campaign.Chapters[0];
            for (int index = 0; index < 5; index++) Record(progress, chapter.Levels[index]);

            Assert.That(flow.OpenChapter("power_station"), Is.True);
            Assert.That(flow.StartLevel("power_06"), Is.True);

            for (int index = 5; index <= 8; index++)
            {
                Assert.That(flow.ActiveLevel.LevelId, Is.EqualTo($"power_{index + 1:D2}"));
                Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.False);
                Solve(flow.ActiveSession);
                AssertNormalNavigation(flow.ResultNavigation, true);
                Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.False);
                Assert.That(flow.StartNextLevel(), Is.True);
            }

            Assert.That(flow.ActiveLevel.LevelId, Is.EqualTo("power_10"));
            Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.True);
            Solve(flow.ActiveSession);
            Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.True);
            Assert.That(progress.GetChapterState("power_station"),
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
        public void PreviousThreeLevelSave_LoadsStableProgressAndLeavesNewLevelsFresh()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            string directory = Path.Combine(Path.GetTempPath(), "NeonGridM7Tests",
                Guid.NewGuid().ToString("N"));
            string savePath = Path.Combine(directory, CampaignSaveStore.SaveFileName);
            Directory.CreateDirectory(directory);
            try
            {
                var entries = new List<LevelProgressSaveEntry>();
                for (int index = 1; index <= 3; index++)
                    entries.Add(new LevelProgressSaveEntry($"power_{index:D2}", true,
                        index, index + 1, index * 10f));
                var priorSave = new CampaignSaveData
                {
                    version = CampaignSaveStore.CurrentVersion,
                    campaignId = "power_station_vertical_slice",
                    levelProgressEntries = entries
                };
                File.WriteAllText(savePath, JsonUtility.ToJson(priorSave, true));

                CampaignLoadResult load = new CampaignSaveStore(savePath).Load(campaign);

                Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
                Assert.That(load.Diagnostics, Is.Empty);
                for (int index = 1; index <= 3; index++)
                {
                    LevelProgress restored = load.Progress.GetLevelProgress($"power_{index:D2}");
                    Assert.That(restored.Completed, Is.True);
                    Assert.That(restored.BestStars, Is.EqualTo(index));
                    Assert.That(restored.BestMoves, Is.EqualTo(index + 1));
                    Assert.That(restored.BestTimeSeconds, Is.EqualTo(index * 10f));
                }

                for (int index = 4; index <= 10; index++)
                    Assert.That(load.Progress.GetLevelProgress($"power_{index:D2}").Completed,
                        Is.False);
                Assert.That(load.Progress.IsLevelUnlocked("power_04"), Is.True);
                Assert.That(load.Progress.IsLevelUnlocked("power_05"), Is.False);
                Assert.That(load.Progress.IsLevelUnlocked("power_06"), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PreviousSixLevelSave_PreservesBestsUnlocksPs07AndIsNotRestored()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            string directory = Path.Combine(Path.GetTempPath(), "NeonGridM7Tests",
                Guid.NewGuid().ToString("N"));
            string savePath = Path.Combine(directory, CampaignSaveStore.SaveFileName);
            Directory.CreateDirectory(directory);
            try
            {
                var entries = new List<LevelProgressSaveEntry>();
                for (int index = 1; index <= 6; index++)
                    entries.Add(new LevelProgressSaveEntry($"power_{index:D2}", true,
                        (index - 1) % 3 + 1, index + 2, index * 7f));
                File.WriteAllText(savePath, JsonUtility.ToJson(new CampaignSaveData
                {
                    version = CampaignSaveStore.CurrentVersion,
                    campaignId = "power_station_vertical_slice",
                    levelProgressEntries = entries
                }, true));

                CampaignLoadResult load = new CampaignSaveStore(savePath).Load(campaign);

                Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
                Assert.That(load.Diagnostics, Is.Empty);
                for (int index = 1; index <= 6; index++)
                {
                    LevelProgress restored = load.Progress.GetLevelProgress($"power_{index:D2}");
                    Assert.That(restored.Completed, Is.True);
                    Assert.That(restored.BestStars, Is.EqualTo((index - 1) % 3 + 1));
                    Assert.That(restored.BestMoves, Is.EqualTo(index + 2));
                    Assert.That(restored.BestTimeSeconds, Is.EqualTo(index * 7f));
                }

                for (int index = 7; index <= 10; index++)
                    Assert.That(load.Progress.GetLevelProgress($"power_{index:D2}").Completed,
                        Is.False);
                Assert.That(load.Progress.IsLevelUnlocked("power_07"), Is.True);
                Assert.That(load.Progress.IsLevelUnlocked("power_08"), Is.False);
                Assert.That(load.Progress.GetChapterState("power_station"),
                    Is.EqualTo(CampaignChapterState.Available),
                    "Restoration must be derived from all ten current campaign entries.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void VerticalSliceScene_UsesCampaignAndIsEnabledInBuildSettings()
        {
            const string scenePath =
                "Assets/NeonGrid/Scenes/M7_PowerStation_VerticalSlice.unity";
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
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
                $"Levels/PowerStation/{assetName}");
            Assert.That(level, Is.Not.Null, $"Missing production level asset {assetName}.");
            return level;
        }

        private static CampaignProgressUpdate Record(CampaignProgressService progress,
            CampaignLevelEntry level)
        {
            return progress.RecordCompletion(level.LevelId,
                CampaignTestFixture.Result(level.LevelDefinition, 1, 1f, 1));
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

        private static void AssertTutorial(CampaignLevelEntry level, string message,
            GridPosition target)
        {
            Assert.That(level.Tutorial, Is.Not.Null);
            Assert.That(level.Tutorial.Steps, Has.Count.EqualTo(1));
            TutorialStepDefinition step = level.Tutorial.Steps[0];
            Assert.That(step.Message, Is.EqualTo(message));
            Assert.That(step.TargetPosition, Is.EqualTo(target));
            Assert.That(step.CompletionCondition,
                Is.EqualTo(TutorialCompletionCondition.RotateClockwise));
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public string SavePath => "memory://production-power-station";

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

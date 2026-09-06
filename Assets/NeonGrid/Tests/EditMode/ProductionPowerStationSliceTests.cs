using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
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
            Assert.That(chapter.Levels, Has.Count.EqualTo(3));
            Assert.That(chapter.Levels.Select(level => level.LevelId),
                Is.EqualTo(new[] { "power_01", "power_02", "power_03" }));
            Assert.That(chapter.Levels.Select(level => level.DisplayName),
                Is.EqualTo(new[] { "Power Circuit 01", "Power Circuit 02", "Power Circuit 03" }));
            Assert.That(chapter.Levels[0].LevelDefinition, Is.SameAs(LoadLevel("PS_01")));
            Assert.That(chapter.Levels[1].LevelDefinition, Is.SameAs(LoadLevel("PS_02")));
            Assert.That(chapter.Levels[2].LevelDefinition, Is.SameAs(LoadLevel("PS_03")));

            var progress = new CampaignProgressService(campaign);
            Assert.That(progress.MaximumCampaignStars, Is.EqualTo(9));
            Assert.That(progress.IsLevelUnlocked("power_01"), Is.True);
            Assert.That(progress.IsLevelUnlocked("power_02"), Is.False);
            Assert.That(progress.IsLevelUnlocked("power_03"), Is.False);

            CampaignProgressUpdate first = Record(progress, chapter.Levels[0]);
            Assert.That(first.Accepted, Is.True);
            Assert.That(progress.IsLevelUnlocked("power_02"), Is.True);
            Assert.That(progress.IsLevelUnlocked("power_03"), Is.False);

            CampaignProgressUpdate second = Record(progress, chapter.Levels[1]);
            Assert.That(second.Accepted, Is.True);
            Assert.That(progress.IsLevelUnlocked("power_03"), Is.True);

            CampaignProgressUpdate third = Record(progress, chapter.Levels[2]);
            Assert.That(third.Accepted, Is.True);
            Assert.That(third.ChapterJustRestored, Is.True);
            Assert.That(progress.GetChapterState("power_station"),
                Is.EqualTo(CampaignChapterState.Restored));
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
    }
}

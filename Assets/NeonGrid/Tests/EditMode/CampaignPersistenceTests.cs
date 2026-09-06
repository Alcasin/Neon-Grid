using System;
using System.Collections.Generic;
using System.IO;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class CampaignPersistenceTests
    {
        private CampaignTestFixture fixture;
        private string temporaryDirectory;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            fixture = new CampaignTestFixture();
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NeonGridM6Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            savePath = Path.Combine(temporaryDirectory, "campaign_progress.json");
        }

        [TearDown]
        public void TearDown()
        {
            fixture.Dispose();
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }

        [Test]
        public void NoFile_ReturnsFreshProgress()
        {
            CampaignLoadResult load = new CampaignSaveStore(savePath).Load(fixture.Campaign);

            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.NoSaveFound));
            Assert.That(load.Progress.TotalStars, Is.Zero);
            Assert.That(load.Progress.IsLevelUnlocked("power_01"), Is.True);
            Assert.That(load.Progress.IsLevelUnlocked("power_02"), Is.False);
        }

        [Test]
        public void SaveLoadRoundTrip_RestoresCompletionAndIndependentBests()
        {
            var store = new CampaignSaveStore(savePath);
            var progress = new CampaignProgressService(fixture.Campaign);
            Record(progress, "power_01", 12, 70f, 2);
            Record(progress, "power_01", 15, 60f, 3);
            Record(progress, "power_01", 10, 110f, 1);

            Assert.That(store.Save(progress).Succeeded, Is.True);
            CampaignLoadResult load = new CampaignSaveStore(savePath).Load(fixture.Campaign);

            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            LevelProgress restored = load.Progress.GetLevelProgress("power_01");
            Assert.That(restored.Completed, Is.True);
            Assert.That(restored.BestStars, Is.EqualTo(3));
            Assert.That(restored.BestMoves, Is.EqualTo(10));
            Assert.That(restored.BestTimeSeconds, Is.EqualTo(60f));
            Assert.That(load.Progress.IsLevelUnlocked("power_02"), Is.True);
        }

        [Test]
        public void RepeatedLoad_IsDeterministic()
        {
            var store = new CampaignSaveStore(savePath);
            var progress = new CampaignProgressService(fixture.Campaign);
            Record(progress, "power_01", 3, 9f, 2);
            store.Save(progress);

            CampaignLoadResult first = store.Load(fixture.Campaign);
            CampaignLoadResult second = store.Load(fixture.Campaign);

            Assert.That(second.Status, Is.EqualTo(first.Status));
            Assert.That(second.Progress.TotalStars, Is.EqualTo(first.Progress.TotalStars));
            Assert.That(second.Progress.GetLevelProgress("power_01").BestMoves,
                Is.EqualTo(first.Progress.GetLevelProgress("power_01").BestMoves));
            Assert.That(second.Diagnostics, Is.EqualTo(first.Diagnostics));
        }

        [Test]
        public void CorruptJson_ReturnsDistinctStatusAndFreshFallback()
        {
            File.WriteAllText(savePath, "{ definitely not valid json");

            CampaignLoadResult load = new CampaignSaveStore(savePath).Load(fixture.Campaign);

            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Corrupt));
            Assert.That(load.Diagnostics, Is.Not.Empty);
            Assert.That(load.Progress.TotalStars, Is.Zero);
        }

        [Test]
        public void UnsupportedVersion_ReturnsDistinctStatusAndFreshFallback()
        {
            WriteData(new CampaignSaveData
            {
                version = CampaignSaveStore.CurrentVersion + 1,
                campaignId = fixture.Campaign.CampaignId,
                levelProgressEntries = new List<LevelProgressSaveEntry>()
            });

            CampaignLoadResult load = new CampaignSaveStore(savePath).Load(fixture.Campaign);

            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.UnsupportedVersion));
            Assert.That(load.Progress.TotalStars, Is.Zero);
        }

        [Test]
        public void UnknownSavedLevelId_IsIgnoredWithDiagnostic()
        {
            WriteData(new CampaignSaveData
            {
                version = CampaignSaveStore.CurrentVersion,
                campaignId = fixture.Campaign.CampaignId,
                levelProgressEntries = new List<LevelProgressSaveEntry>
                {
                    new LevelProgressSaveEntry("power_01", true, 2, 4, 20f),
                    new LevelProgressSaveEntry("removed_level", true, 3, 1, 1f)
                }
            });

            CampaignLoadResult load = new CampaignSaveStore(savePath).Load(fixture.Campaign);

            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            Assert.That(load.Progress.GetLevelProgress("power_01").Completed, Is.True);
            Assert.That(load.Progress.GetLevelProgress("removed_level"), Is.Null);
            Assert.That(load.Diagnostics, Has.Count.EqualTo(1));
        }

        [Test]
        public void NewlyAddedLevelAbsentFromSave_BeginsFresh()
        {
            CampaignDefinition smallCampaign = fixture.CreateCampaign("test_campaign",
                fixture.Chapter("power_station", "Power Station", "power_01"));
            var store = new CampaignSaveStore(savePath);
            var smallProgress = new CampaignProgressService(smallCampaign);
            smallProgress.RecordCompletion("power_01",
                CampaignTestFixture.Result(fixture.Level("power_01"), 2, 8f, 3));
            store.Save(smallProgress);

            CampaignLoadResult expanded = store.Load(fixture.Campaign);

            Assert.That(expanded.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            Assert.That(expanded.Progress.GetLevelProgress("power_01").Completed, Is.True);
            Assert.That(expanded.Progress.GetLevelProgress("power_02").Completed, Is.False);
            Assert.That(expanded.Progress.GetLevelProgress("metro_03").Completed, Is.False);
        }

        [Test]
        public void StableLevelIdsRestoreProgressAfterCampaignReordering()
        {
            var store = new CampaignSaveStore(savePath);
            var original = new CampaignProgressService(fixture.Campaign);
            Record(original, "power_01", 5, 20f, 1);
            Record(original, "power_02", 3, 12f, 3);
            store.Save(original);
            CampaignDefinition reordered = fixture.CreateReorderedCampaign();

            CampaignLoadResult load = store.Load(reordered);

            LevelProgress power02 = load.Progress.GetLevelProgress("power_02");
            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            Assert.That(power02.Completed, Is.True);
            Assert.That(power02.BestStars, Is.EqualTo(3));
            Assert.That(power02.BestMoves, Is.EqualTo(3));
            Assert.That(load.Progress.GetLevelProgress("power_03").Completed, Is.False);
        }

        [Test]
        public void RepeatedSaveAtomicallyReplacesExistingFile()
        {
            var store = new CampaignSaveStore(savePath);
            var progress = new CampaignProgressService(fixture.Campaign);
            Record(progress, "power_01", 5, 20f, 1);
            Assert.That(store.Save(progress).Succeeded, Is.True);
            Record(progress, "power_01", 3, 15f, 3);

            Assert.That(store.Save(progress).Succeeded, Is.True);
            CampaignLoadResult load = store.Load(fixture.Campaign);
            Assert.That(load.Progress.GetLevelProgress("power_01").BestStars, Is.EqualTo(3));
            Assert.That(File.Exists(savePath + ".tmp"), Is.False);
        }

        [Test]
        public void SaveFailure_IsReportedWithoutDiscardingInMemoryProgress()
        {
            var progress = new CampaignProgressService(fixture.Campaign);
            Record(progress, "power_01", 2, 10f, 2);
            var invalidStore = new CampaignSaveStore(temporaryDirectory);

            CampaignSaveResult result = invalidStore.Save(progress);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveStatus.Failed));
            Assert.That(progress.GetLevelProgress("power_01").Completed, Is.True);
            Assert.That(progress.TotalStars, Is.EqualTo(2));
        }

        [Test]
        public void Delete_RemovesOnlyTheConfiguredCampaignSave()
        {
            var store = new CampaignSaveStore(savePath);
            var progress = new CampaignProgressService(fixture.Campaign);
            Record(progress, "power_01", 2, 10f, 2);
            store.Save(progress);
            Assert.That(File.Exists(savePath), Is.True);

            CampaignSaveResult result = store.Delete();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(savePath), Is.False);
            Assert.That(store.Load(fixture.Campaign).Status, Is.EqualTo(CampaignLoadStatus.NoSaveFound));
        }

        [Test]
        public void CampaignSpecificPaths_IsolateCampaignsWithOverlappingLevelIds()
        {
            CampaignDefinition campaignA = fixture.CreateCampaign("campaign_a",
                fixture.Chapter("chapter_a", "A", "power_01"));
            CampaignDefinition campaignB = fixture.CreateCampaign("campaign_b",
                fixture.Chapter("chapter_b", "B", "power_01"));
            var storeA = new CampaignSaveStore(
                CampaignSaveStore.BuildSavePath(temporaryDirectory, campaignA.CampaignId));
            var storeB = new CampaignSaveStore(
                CampaignSaveStore.BuildSavePath(temporaryDirectory, campaignB.CampaignId));

            var progressA = new CampaignProgressService(campaignA);
            Record(progressA, "power_01", 5, 20f, 1);
            Assert.That(storeA.Save(progressA).Succeeded, Is.True);

            CampaignLoadResult freshB = storeB.Load(campaignB);
            Assert.That(freshB.Status, Is.EqualTo(CampaignLoadStatus.NoSaveFound));
            Assert.That(freshB.Progress.GetLevelProgress("power_01").Completed, Is.False);

            var progressB = freshB.Progress;
            Record(progressB, "power_01", 2, 8f, 3);
            Assert.That(storeB.Save(progressB).Succeeded, Is.True);

            CampaignLoadResult reloadedA = storeA.Load(campaignA);
            CampaignLoadResult reloadedB = storeB.Load(campaignB);
            Assert.That(reloadedA.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            Assert.That(reloadedB.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            Assert.That(reloadedA.Progress.GetLevelProgress("power_01").BestMoves, Is.EqualTo(5));
            Assert.That(reloadedA.Progress.GetLevelProgress("power_01").BestStars, Is.EqualTo(1));
            Assert.That(reloadedB.Progress.GetLevelProgress("power_01").BestMoves, Is.EqualTo(2));
            Assert.That(reloadedB.Progress.GetLevelProgress("power_01").BestStars, Is.EqualTo(3));
            Assert.That(storeA.SavePath, Is.Not.EqualTo(storeB.SavePath));
        }

        [Test]
        public void MismatchedCampaignId_ReturnsExplicitStatusAndFreshProgress()
        {
            CampaignDefinition campaignA = fixture.CreateCampaign("campaign_a",
                fixture.Chapter("chapter_a", "A", "power_01"));
            CampaignDefinition campaignB = fixture.CreateCampaign("campaign_b",
                fixture.Chapter("chapter_b", "B", "power_01"));
            var store = new CampaignSaveStore(savePath);
            var progressA = new CampaignProgressService(campaignA);
            Record(progressA, "power_01", 2, 8f, 3);
            Assert.That(store.Save(progressA).Succeeded, Is.True);

            CampaignLoadResult load = store.Load(campaignB);

            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.CampaignMismatch));
            Assert.That(load.Diagnostics, Is.Not.Empty);
            Assert.That(load.Progress.GetLevelProgress("power_01").Completed, Is.False);
            Assert.That(load.Progress.TotalStars, Is.Zero);
        }

        [TestCase("../campaign")]
        [TestCase("campaign\\other")]
        [TestCase("campaign:other")]
        [TestCase("Campaign")]
        public void CampaignSpecificPath_RejectsUnsafeCampaignId(string campaignId)
        {
            Assert.Throws<ArgumentException>(() =>
                CampaignSaveStore.BuildSavePath(temporaryDirectory, campaignId));
        }

        private void WriteData(CampaignSaveData data)
        {
            File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        }

        private void Record(CampaignProgressService progress, string levelId, int moves,
            float seconds, int? stars)
        {
            CampaignProgressUpdate update = progress.RecordCompletion(levelId,
                CampaignTestFixture.Result(fixture.Level(levelId), moves, seconds, stars));
            Assert.That(update.Accepted, Is.True);
        }
    }
}

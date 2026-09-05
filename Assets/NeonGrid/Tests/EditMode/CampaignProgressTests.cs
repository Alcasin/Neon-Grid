using NeonGrid.Campaign;
using NUnit.Framework;

namespace NeonGrid.Tests
{
    public sealed class CampaignProgressTests
    {
        private CampaignTestFixture fixture;
        private CampaignProgressService progress;

        [SetUp]
        public void SetUp()
        {
            fixture = new CampaignTestFixture();
            progress = new CampaignProgressService(fixture.Campaign);
        }

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void FreshCampaign_UnlocksOnlyFirstLevelOfFirstChapter()
        {
            Assert.That(progress.GetChapterState("power_station"), Is.EqualTo(CampaignChapterState.Available));
            Assert.That(progress.GetChapterState("metro"), Is.EqualTo(CampaignChapterState.Locked));
            Assert.That(progress.IsLevelUnlocked("power_01"), Is.True);
            Assert.That(progress.IsLevelUnlocked("power_02"), Is.False);
            Assert.That(progress.IsLevelUnlocked("power_03"), Is.False);
            Assert.That(progress.IsLevelUnlocked("metro_01"), Is.False);
            Assert.That(progress.TotalStars, Is.Zero);
            Assert.That(progress.MaximumCampaignStars, Is.EqualTo(18));
        }

        [Test]
        public void CompletingLevels_UnlocksSequentiallyWithoutStars()
        {
            CampaignProgressUpdate first = Record("power_01", 5, 30f, null);
            Assert.That(first.Accepted, Is.True);
            Assert.That(progress.IsLevelUnlocked("power_02"), Is.True);
            Assert.That(progress.IsLevelUnlocked("power_03"), Is.False);

            Record("power_02", 5, 30f, 1);
            Assert.That(progress.IsLevelUnlocked("power_03"), Is.True);
            Assert.That(progress.GetChapterState("metro"), Is.EqualTo(CampaignChapterState.Locked));
        }

        [Test]
        public void FinalLevel_RestoresChapterAndUnlocksNextChapter()
        {
            Record("power_01", 1, 10f, 1);
            Assert.That(Record("power_02", 1, 10f, 1).ChapterJustRestored, Is.False);

            CampaignProgressUpdate update = Record("power_03", 1, 10f, 1);

            Assert.That(update.ChapterJustRestored, Is.True);
            Assert.That(update.RestoredChapterId, Is.EqualTo("power_station"));
            Assert.That(update.NextChapterUnlocked, Is.True);
            Assert.That(progress.GetChapterState("power_station"), Is.EqualTo(CampaignChapterState.Restored));
            Assert.That(progress.GetChapterState("metro"), Is.EqualTo(CampaignChapterState.Available));
            Assert.That(progress.IsLevelUnlocked("metro_01"), Is.True);
            Assert.That(progress.IsLevelUnlocked("metro_02"), Is.False);
        }

        [Test]
        public void ReplayingFinalLevel_DoesNotRetriggerRestorationOrRelockContent()
        {
            Record("power_01", 1, 10f, 1);
            Record("power_02", 1, 10f, 1);
            Record("power_03", 1, 10f, 1);

            CampaignProgressUpdate replay = Record("power_03", 2, 20f, 1);

            Assert.That(replay.ChapterJustRestored, Is.False);
            Assert.That(replay.NextChapterUnlocked, Is.False);
            Assert.That(progress.IsLevelUnlocked("power_01"), Is.True);
            Assert.That(progress.IsLevelUnlocked("power_02"), Is.True);
            Assert.That(progress.IsLevelUnlocked("power_03"), Is.True);
            Assert.That(progress.IsLevelUnlocked("metro_01"), Is.True);
        }

        [Test]
        public void BestMetricsImproveIndependentlyAndNeverDowngrade()
        {
            Record("power_01", 12, 70f, 2);
            Record("power_01", 15, 60f, 3);
            Record("power_01", 10, 110f, 1);

            LevelProgress best = progress.GetLevelProgress("power_01");
            Assert.That(best.Completed, Is.True);
            Assert.That(best.BestStars, Is.EqualTo(3));
            Assert.That(best.BestMoves, Is.EqualTo(10));
            Assert.That(best.BestTimeSeconds, Is.EqualTo(60f));
        }

        [Test]
        public void UnknownStarReplay_PreservesKnownStarsButCanImproveOtherMetrics()
        {
            Record("power_01", 8, 50f, 2);
            Record("power_01", 6, 40f, null);

            LevelProgress best = progress.GetLevelProgress("power_01");
            Assert.That(best.BestStars, Is.EqualTo(2));
            Assert.That(best.BestMoves, Is.EqualTo(6));
            Assert.That(best.BestTimeSeconds, Is.EqualTo(40f));
        }

        [Test]
        public void UnknownStarsStillCompleteAndUnlockNextLevel()
        {
            CampaignProgressUpdate update = Record("power_01", 4, 20f, null);

            Assert.That(update.LevelFirstCompleted, Is.True);
            Assert.That(progress.GetLevelProgress("power_01").Completed, Is.True);
            Assert.That(progress.GetLevelProgress("power_01").BestStars, Is.Zero);
            Assert.That(progress.IsLevelUnlocked("power_02"), Is.True);
        }

        [Test]
        public void TotalStarsUseHistoricalBestAndReplayImprovementOnlyIncreasesTotal()
        {
            Record("power_01", 4, 20f, 2);
            Record("power_02", 4, 20f, 1);
            Assert.That(progress.TotalStars, Is.EqualTo(3));

            Record("power_01", 8, 30f, 1);
            Assert.That(progress.TotalStars, Is.EqualTo(3));

            Record("power_01", 3, 18f, 3);
            Assert.That(progress.TotalStars, Is.EqualTo(4));
        }

        [Test]
        public void NullIncompleteOrMismatchedResults_AreRejected()
        {
            Assert.That(progress.RecordCompletion("power_01", null).Accepted, Is.False);
            Assert.That(progress.RecordCompletion("power_01",
                CampaignTestFixture.Result(fixture.Level("power_02"), 1, 1f, 3)).Accepted, Is.False);
            Assert.That(progress.RecordCompletion("power_02",
                CampaignTestFixture.Result(fixture.Level("power_02"), 1, 1f, 3)).Accepted, Is.False,
                "Locked campaign levels cannot be recorded through the authoritative service.");
            Assert.That(progress.TotalStars, Is.Zero);
        }

        private CampaignProgressUpdate Record(string levelId, int moves, float seconds, int? stars)
        {
            return progress.RecordCompletion(levelId,
                CampaignTestFixture.Result(fixture.Level(levelId), moves, seconds, stars));
        }
    }
}

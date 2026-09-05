using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Session;
using NUnit.Framework;

namespace NeonGrid.Tests
{
    public sealed class CampaignFlowTests
    {
        private CampaignTestFixture fixture;
        private CampaignProgressService progress;
        private MemoryStore store;
        private CampaignFlowCoordinator flow;

        [SetUp]
        public void SetUp()
        {
            fixture = new CampaignTestFixture();
            progress = new CampaignProgressService(fixture.Campaign);
            store = new MemoryStore();
            flow = new CampaignFlowCoordinator(fixture.Campaign, progress, store);
        }

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void LockedChapterAndLevel_CannotBeSelectedOrStarted()
        {
            Assert.That(flow.OpenChapter("metro"), Is.False);
            Assert.That(flow.OpenChapter("power_station"), Is.True);
            Assert.That(flow.StartLevel("power_02"), Is.False);
            Assert.That(flow.StartLevel("metro_01"), Is.False);
            Assert.That(flow.CurrentScreen, Is.EqualTo(CampaignFlowScreen.LevelSelection));
        }

        [Test]
        public void GenericSelectedEntry_CreatesGameplaySessionAndRecordsCompletion()
        {
            flow.OpenChapter("power_station");
            Assert.That(flow.StartLevel("power_01"), Is.True);
            Assert.That(flow.ActiveSession.ActiveLevel, Is.SameAs(fixture.Level("power_01")));

            CampaignTestFixture.Solve(flow.ActiveSession);

            Assert.That(flow.ActiveSession.IsCompleted, Is.True);
            Assert.That(progress.GetLevelProgress("power_01").Completed, Is.True);
            Assert.That(progress.GetLevelProgress("power_01").BestMoves, Is.EqualTo(1));
            Assert.That(progress.GetLevelProgress("power_01").BestStars, Is.EqualTo(3));
            Assert.That(progress.GetLevelProgress("power_01").BestTimeSeconds, Is.GreaterThanOrEqualTo(0f));
            Assert.That(progress.IsLevelUnlocked("power_02"), Is.True);
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(flow.LastSaveResult.Succeeded, Is.True);
        }

        [Test]
        public void NormalCompletion_OffersLevelsAndNextAndReturnsToSelectedChapter()
        {
            flow.OpenChapter("power_station");
            CampaignChapterDefinition selectedChapter = flow.SelectedChapter;
            flow.StartLevel("power_01");

            CampaignTestFixture.Solve(flow.ActiveSession);

            CampaignResultNavigationState navigation = flow.ResultNavigation;
            Assert.That(navigation.ShowRetry, Is.True);
            Assert.That(navigation.ShowLevels, Is.True);
            Assert.That(navigation.ShowMap, Is.False);
            Assert.That(navigation.ShowNext, Is.True);
            Assert.That(flow.CurrentScreen, Is.EqualTo(CampaignFlowScreen.Gameplay),
                "Normal completion waits for an explicit result action instead of flattening to Map.");

            Assert.That(flow.ReturnToLevelSelection(), Is.True);

            Assert.That(flow.CurrentScreen, Is.EqualTo(CampaignFlowScreen.LevelSelection));
            Assert.That(flow.SelectedChapter, Is.SameAs(selectedChapter));
            Assert.That(flow.ActiveLevel, Is.Null);
            Assert.That(flow.ActiveSession, Is.Null);
            Assert.That(progress.GetLevelProgress("power_01").Completed, Is.True);
            Assert.That(progress.IsLevelUnlocked("power_02"), Is.True);
        }

        [Test]
        public void RetryCreatesFreshAttemptWithoutErasingHistoricalBest()
        {
            flow.OpenChapter("power_station");
            flow.StartLevel("power_01");
            CampaignTestFixture.Solve(flow.ActiveSession);
            var firstSession = flow.ActiveSession;

            Assert.That(flow.Retry(), Is.True);

            Assert.That(flow.ActiveSession, Is.Not.SameAs(firstSession));
            Assert.That(flow.ActiveSession.MoveCount, Is.Zero);
            Assert.That(flow.ActiveSession.IsCompleted, Is.False);
            Assert.That(progress.GetLevelProgress("power_01").Completed, Is.True);
            Assert.That(progress.GetLevelProgress("power_01").BestMoves, Is.EqualTo(1));
            Assert.That(progress.IsLevelUnlocked("power_02"), Is.True);
        }

        [Test]
        public void NextStartsUnlockedNextLevelWithinSameChapter()
        {
            flow.OpenChapter("power_station");
            flow.StartLevel("power_01");
            CampaignTestFixture.Solve(flow.ActiveSession);

            Assert.That(flow.CanStartNextLevel(), Is.True);
            Assert.That(flow.StartNextLevel(), Is.True);
            Assert.That(flow.ActiveLevel.LevelId, Is.EqualTo("power_02"));
            Assert.That(flow.ActiveSession.ActiveLevel, Is.SameAs(fixture.Level("power_02")));
            Assert.That(flow.CurrentScreen, Is.EqualTo(CampaignFlowScreen.Gameplay));
        }

        [Test]
        public void FinalChapterLevelRequiresMapAndExposesOneRestorationSignal()
        {
            Record("power_01");
            Record("power_02");
            flow.OpenChapter("power_station");
            flow.StartLevel("power_03");

            CampaignTestFixture.Solve(flow.ActiveSession);

            Assert.That(flow.IsFinalLevelInSelectedChapter(), Is.True);
            Assert.That(flow.CanStartNextLevel(), Is.False,
                "NEXT never crosses a chapter boundary.");
            Assert.That(flow.StartNextLevel(), Is.False);
            Assert.That(flow.PendingRestorationChapterId, Is.EqualTo("power_station"));
            Assert.That(progress.IsChapterUnlocked("metro"), Is.True);
            Assert.That(flow.ResultNavigation.ShowRetry, Is.True);
            Assert.That(flow.ResultNavigation.ShowLevels, Is.False);
            Assert.That(flow.ResultNavigation.ShowMap, Is.True);
            Assert.That(flow.ResultNavigation.ShowNext, Is.False);

            flow.ReturnToMap();
            Assert.That(flow.CurrentScreen, Is.EqualTo(CampaignFlowScreen.Map));
            Assert.That(flow.ConsumePendingRestoration(), Is.EqualTo("power_station"));
            Assert.That(flow.ConsumePendingRestoration(), Is.Null);
        }

        [Test]
        public void FinalLevelDetection_UsesCurrentChapterOrderingInsteadOfLevelId()
        {
            CampaignDefinition reordered = fixture.CreateReorderedCampaign();
            var reorderedProgress = new CampaignProgressService(reordered);
            var reorderedFlow = new CampaignFlowCoordinator(reordered, reorderedProgress, new MemoryStore());
            reorderedProgress.RecordCompletion("power_03",
                CampaignTestFixture.Result(fixture.Level("power_03"), 1, 1f, 1));
            reorderedProgress.RecordCompletion("power_01",
                CampaignTestFixture.Result(fixture.Level("power_01"), 1, 1f, 1));

            Assert.That(reorderedFlow.OpenChapter("power_station"), Is.True);
            Assert.That(reorderedFlow.StartLevel("power_02"), Is.True);
            Assert.That(reorderedFlow.IsFinalLevelInSelectedChapter(), Is.True);
            Assert.That(reorderedFlow.CanStartNextLevel(), Is.False);
        }

        [Test]
        public void RetryingFirstRestoration_KeepsFeedbackPendingUntilMapConsumesIt()
        {
            Record("power_01");
            Record("power_02");
            flow.OpenChapter("power_station");
            flow.StartLevel("power_03");
            CampaignTestFixture.Solve(flow.ActiveSession);

            Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.True);
            Assert.That(flow.PendingRestorationChapterId, Is.EqualTo("power_station"));
            Assert.That(flow.Retry(), Is.True);
            Assert.That(flow.PendingRestorationChapterId, Is.EqualTo("power_station"));

            CampaignTestFixture.Solve(flow.ActiveSession);

            Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.False);
            Assert.That(flow.ResultNavigation.ShowLevels, Is.True,
                "A completed retry is an already-restored final replay.");
            Assert.That(flow.ResultNavigation.ShowMap, Is.False);
            Assert.That(flow.PendingRestorationChapterId, Is.EqualTo("power_station"));
            Assert.That(flow.ReturnToLevelSelection(), Is.True);
            flow.ReturnToMap();
            Assert.That(flow.ConsumePendingRestoration(), Is.EqualTo("power_station"));
            Assert.That(flow.ConsumePendingRestoration(), Is.Null);
        }

        [Test]
        public void AlreadyRestoredFinalReplay_OffersRetryAndLevelsWithoutFalseRestoration()
        {
            Record("power_01");
            Record("power_02");
            Record("power_03");
            flow.OpenChapter("power_station");
            flow.StartLevel("power_03");

            CampaignTestFixture.Solve(flow.ActiveSession);

            Assert.That(flow.LastProgressUpdate.ChapterJustRestored, Is.False);
            Assert.That(flow.LastProgressUpdate.NextChapterUnlocked, Is.False);
            Assert.That(flow.PendingRestorationChapterId, Is.Null);
            Assert.That(flow.ResultNavigation.ShowRetry, Is.True);
            Assert.That(flow.ResultNavigation.ShowLevels, Is.True);
            Assert.That(flow.ResultNavigation.ShowMap, Is.False);
            Assert.That(flow.ResultNavigation.ShowNext, Is.False);
            Assert.That(progress.IsChapterUnlocked("metro"), Is.True);
        }

        [Test]
        public void LeavingIncompleteAttempt_DiscardsOnlyAttemptAndKeepsHistoricalProgress()
        {
            Record("power_01");
            flow.OpenChapter("power_station");
            flow.StartLevel("power_02");
            GameplaySession abandonedSession = flow.ActiveSession;
            abandonedSession.AdvanceTime(GameplaySession.HintUnlockSeconds);
            abandonedSession.RequestHint();

            Assert.That(abandonedSession.IsCompleted, Is.False);
            Assert.That(flow.ReturnToLevelSelection(), Is.True);

            Assert.That(flow.CurrentScreen, Is.EqualTo(CampaignFlowScreen.LevelSelection));
            Assert.That(flow.SelectedChapter.ChapterId, Is.EqualTo("power_station"));
            Assert.That(flow.ActiveLevel, Is.Null);
            Assert.That(flow.ActiveSession, Is.Null);
            Assert.That(progress.GetLevelProgress("power_01").Completed, Is.True);
            Assert.That(progress.GetLevelProgress("power_02").Completed, Is.False);
            Assert.That(store.SaveCount, Is.Zero,
                "An incomplete attempt is not persisted as campaign completion.");
        }

        [Test]
        public void SaveFailureAfterCompletion_IsExposedWhileProgressRemainsUpdated()
        {
            store.FailSaves = true;
            flow.OpenChapter("power_station");
            flow.StartLevel("power_01");

            CampaignTestFixture.Solve(flow.ActiveSession);

            Assert.That(flow.LastSaveResult.Status, Is.EqualTo(CampaignSaveStatus.Failed));
            Assert.That(progress.GetLevelProgress("power_01").Completed, Is.True);
        }

        private void Record(string levelId)
        {
            CampaignProgressUpdate update = progress.RecordCompletion(levelId,
                CampaignTestFixture.Result(fixture.Level(levelId), 1, 1f, 1));
            Assert.That(update.Accepted, Is.True);
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public string SavePath => "memory://campaign";
            public int SaveCount { get; private set; }
            public bool FailSaves { get; set; }

            public CampaignLoadResult Load(CampaignDefinition campaign)
            {
                return new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(campaign), new string[0]);
            }

            public CampaignSaveResult Save(CampaignProgressService campaignProgress)
            {
                SaveCount++;
                return FailSaves
                    ? new CampaignSaveResult(CampaignSaveStatus.Failed, "Simulated failure")
                    : new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }

            public CampaignSaveResult Delete()
            {
                return new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }
        }
    }
}

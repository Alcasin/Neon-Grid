using System;
using System.IO;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class ChapterRestorationEventTests
    {
        private CampaignTestFixture fixture;
        private string temporaryDirectory;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            fixture = new CampaignTestFixture();
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NeonGridM13C1Tests",
                Guid.NewGuid().ToString("N"));
            savePath = Path.Combine(temporaryDirectory, CampaignSaveStore.SaveFileName);
        }

        [TearDown]
        public void TearDown()
        {
            fixture.Dispose();
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
        }

        [Test]
        public void FreshCampaignHasNoPendingRestoration()
        {
            var progress = new CampaignProgressService(fixture.Campaign);
            Assert.That(progress.PendingRestoration, Is.Null);
            Assert.That(progress.PendingRestorationCount, Is.Zero);
        }

        [Test]
        public void FirstRestorationCreatesEventWithOrderedNextChapter()
        {
            var progress = new CampaignProgressService(fixture.Campaign);
            CompleteChapter(progress, 0, 1, 8, 20f);

            ChapterRestorationEvent pending = progress.PendingRestoration;
            Assert.That(progress.PendingRestorationCount, Is.EqualTo(1));
            Assert.That(pending.RestoredChapterId, Is.EqualTo("power_station"));
            Assert.That(pending.RestoredChapterIndex, Is.Zero);
            Assert.That(pending.HasNextChapter, Is.True);
            Assert.That(pending.NextChapterId, Is.EqualTo("metro"));
            Assert.That(pending.IsCampaignComplete, Is.False);
        }

        [Test]
        public void ConsumeReturnsEventOnceAndPersistsItsRemoval()
        {
            var store = new CampaignSaveStore(savePath);
            var progress = new CampaignProgressService(fixture.Campaign);
            CompleteChapter(progress, 0, 1, 8, 20f);
            Assert.That(store.Save(progress).Succeeded, Is.True);
            var flow = new CampaignFlowCoordinator(fixture.Campaign, progress, store);

            Assert.That(flow.TryConsumePendingRestoration(out ChapterRestorationEvent first), Is.True);
            Assert.That(first.RestoredChapterId, Is.EqualTo("power_station"));
            Assert.That(flow.TryConsumePendingRestoration(out ChapterRestorationEvent second), Is.False);
            Assert.That(second, Is.Null);
            Assert.That(store.Load(fixture.Campaign).Progress.PendingRestoration, Is.Null);
        }

        [Test]
        public void PendingEventSurvivesSaveAndReloadBeforeConsumption()
        {
            var store = new CampaignSaveStore(savePath);
            var progress = new CampaignProgressService(fixture.Campaign);
            CompleteChapter(progress, 0, 2, 6, 12f);
            Assert.That(store.Save(progress).Succeeded, Is.True);

            CampaignLoadResult load = store.Load(fixture.Campaign);

            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            Assert.That(load.Progress.PendingRestoration.RestoredChapterId,
                Is.EqualTo("power_station"));
            Assert.That(load.Progress.PendingRestoration.NextChapterId, Is.EqualTo("metro"));
        }

        [Test]
        public void AlreadyRestoredReplayAndStatImprovementDoNotCreateEvent()
        {
            var progress = new CampaignProgressService(fixture.Campaign);
            CompleteChapter(progress, 0, 1, 10, 30f);
            var flow = new CampaignFlowCoordinator(fixture.Campaign, progress, new MemoryStore());
            Assert.That(flow.TryConsumePendingRestoration(out _), Is.True);

            CampaignLevelEntry final = fixture.Campaign.Chapters[0].Levels[2];
            CampaignProgressUpdate replay = progress.RecordCompletion(final.LevelId,
                CampaignTestFixture.Result(final.LevelDefinition, 2, 8f, 3));

            Assert.That(replay.Accepted, Is.True);
            Assert.That(replay.ChapterJustRestored, Is.False);
            Assert.That(replay.LevelImproved, Is.True);
            Assert.That(progress.PendingRestoration, Is.Null);
            LevelProgress best = progress.GetLevelProgress(final.LevelId);
            Assert.That(best.BestStars, Is.EqualTo(3));
            Assert.That(best.BestMoves, Is.EqualTo(2));
            Assert.That(best.BestTimeSeconds, Is.EqualTo(8f));
        }

        [Test]
        public void SequentialRestorationsCreateDistinctEventsInOrder()
        {
            var progress = new CampaignProgressService(fixture.Campaign);
            CompleteChapter(progress, 0, 1, 4, 10f);
            CompleteChapter(progress, 1, 2, 5, 12f);

            Assert.That(progress.PendingRestorationCount, Is.EqualTo(2));
            var flow = new CampaignFlowCoordinator(fixture.Campaign, progress, new MemoryStore());
            Assert.That(flow.TryConsumePendingRestoration(out ChapterRestorationEvent power), Is.True);
            Assert.That(power.RestoredChapterId, Is.EqualTo("power_station"));
            Assert.That(flow.TryConsumePendingRestoration(out ChapterRestorationEvent metro), Is.True);
            Assert.That(metro.RestoredChapterId, Is.EqualTo("metro"));
            Assert.That(metro.HasNextChapter, Is.False);
            Assert.That(metro.NextChapterId, Is.Null);
            Assert.That(metro.IsCampaignComplete, Is.True);
            Assert.That(progress.IsCampaignComplete, Is.True);
        }

        [Test]
        public void ReturningToMapDoesNotConsumePendingEvent()
        {
            var progress = new CampaignProgressService(fixture.Campaign);
            CompleteChapter(progress, 0, 1, 4, 10f);
            var flow = new CampaignFlowCoordinator(fixture.Campaign, progress, new MemoryStore());

            flow.ReturnToMap();

            Assert.That(flow.PeekPendingRestoration(), Is.SameAs(progress.PendingRestoration));
            Assert.That(flow.PendingRestorationChapterId, Is.EqualTo("power_station"));
        }

        [Test]
        public void OldSaveWithoutRestorationFieldLoadsWithoutFalseEvent()
        {
            Directory.CreateDirectory(temporaryDirectory);
            File.WriteAllText(savePath,
                "{\"version\":1,\"campaignId\":\"test_campaign\",\"levelProgressEntries\":[]}");

            CampaignLoadResult load = new CampaignSaveStore(savePath).Load(fixture.Campaign);

            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            Assert.That(load.Progress.PendingRestoration, Is.Null);
            Assert.That(load.Progress.TotalStars, Is.Zero);
        }

        [Test]
        public void NavigationRemainsMapOnFirstRestorationAndLevelsOnReplay()
        {
            var progress = new CampaignProgressService(fixture.Campaign);
            Record(progress, fixture.Campaign.Chapters[0].Levels[0], 1, 1, 1f);
            Record(progress, fixture.Campaign.Chapters[0].Levels[1], 1, 1, 1f);
            var flow = new CampaignFlowCoordinator(fixture.Campaign, progress, new MemoryStore());
            Assert.That(flow.OpenChapter("power_station"), Is.True);
            Assert.That(flow.StartLevel("power_03"), Is.True);
            CampaignTestFixture.Solve(flow.ActiveSession);
            Assert.That(flow.ResultNavigation.ShowMap, Is.True);
            Assert.That(flow.ResultNavigation.ShowLevels, Is.False);
            Assert.That(flow.TryConsumePendingRestoration(out _), Is.True);

            Assert.That(flow.Retry(), Is.True);
            CampaignTestFixture.Solve(flow.ActiveSession);
            Assert.That(flow.ResultNavigation.ShowMap, Is.False);
            Assert.That(flow.ResultNavigation.ShowLevels, Is.True);
            Assert.That(flow.PeekPendingRestoration(), Is.Null);
        }

        [Test]
        public void StaticMapStateDoesNotDependOnConsumingEvent()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            var progress = new CampaignProgressService(campaign);
            CompleteChapter(progress, campaign.Chapters[0], 2, 2, 2f);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore());
            var root = new GameObject("M13-C1 Static Map Test");
            try
            {
                var view = root.AddComponent<CampaignRuntimeView>();
                view.Build(campaign, progress, _ => { }, _ => { }, () => { });
                view.ShowMap();
                CityChapterNodeView[] nodes =
                    root.GetComponentsInChildren<CityChapterNodeView>(true);
                Assert.That(nodes[0].VisualState, Is.EqualTo(ChapterMapVisualState.Restored));
                Assert.That(nodes[1].VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage1));
                Assert.That(flow.PeekPendingRestoration(), Is.Not.Null);
                Assert.That(flow.TryConsumePendingRestoration(out _), Is.True);
                view.ShowMap();
                Assert.That(nodes[0].VisualState, Is.EqualTo(ChapterMapVisualState.Restored));
                Assert.That(nodes[1].VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void OneChapterVerticalSliceCreatesValidFinalRestorationEvent()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            var progress = new CampaignProgressService(campaign);
            CompleteChapter(progress, campaign.Chapters[0], 3, 1, 1f);

            ChapterRestorationEvent pending = progress.PendingRestoration;
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.RestoredChapterId, Is.EqualTo("power_station"));
            Assert.That(pending.RestoredChapterIndex, Is.Zero);
            Assert.That(pending.HasNextChapter, Is.False);
            Assert.That(pending.IsCampaignComplete, Is.True);
        }

        [Test]
        public void FailedConsumptionSaveKeepsEventPending()
        {
            var progress = new CampaignProgressService(fixture.Campaign);
            CompleteChapter(progress, 0, 1, 1, 1f);
            var flow = new CampaignFlowCoordinator(fixture.Campaign, progress,
                new MemoryStore { FailSaves = true });

            Assert.That(flow.TryConsumePendingRestoration(out ChapterRestorationEvent result),
                Is.False);
            Assert.That(result, Is.Null);
            Assert.That(progress.PendingRestoration.RestoredChapterId,
                Is.EqualTo("power_station"));
        }

        private static void CompleteChapter(CampaignProgressService progress,
            int chapterIndex, int stars, int moves, float seconds)
        {
            CompleteChapter(progress, progress.Campaign.Chapters[chapterIndex], stars, moves, seconds);
        }

        private static void CompleteChapter(CampaignProgressService progress,
            CampaignChapterDefinition chapter, int stars, int moves, float seconds)
        {
            foreach (CampaignLevelEntry entry in chapter.Levels)
            {
                CampaignProgressUpdate update = Record(progress, entry, stars, moves, seconds);
                if (update.ChapterJustRestored)
                    progress.QueuePendingRestoration(update.RestoredChapterId);
            }
        }

        private static CampaignProgressUpdate Record(CampaignProgressService progress,
            CampaignLevelEntry entry,
            int stars, int moves, float seconds)
        {
            CampaignProgressUpdate update = progress.RecordCompletion(entry.LevelId,
                CampaignTestFixture.Result(entry.LevelDefinition, moves, seconds, stars));
            Assert.That(update.Accepted, Is.True, entry.LevelId);
            return update;
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public string SavePath => "memory://restoration-events";
            public bool FailSaves { get; set; }
            public CampaignLoadResult Load(CampaignDefinition campaign) =>
                new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(campaign), Array.Empty<string>());
            public CampaignSaveResult Save(CampaignProgressService progress) => FailSaves
                ? new CampaignSaveResult(CampaignSaveStatus.Failed, "Simulated failure")
                : new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }
    }
}

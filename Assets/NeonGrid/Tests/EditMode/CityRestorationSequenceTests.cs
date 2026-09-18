using System;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class CityRestorationSequenceTests
    {
        private CampaignDefinition campaign;
        private CampaignProgressService progress;
        private MemoryStore store;
        private CampaignFlowCoordinator flow;
        private GameObject root;
        private CampaignRuntimeView view;
        private CityRestorationSequenceController sequence;

        [SetUp]
        public void SetUp()
        {
            campaign = Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
            progress = new CampaignProgressService(campaign);
            store = new MemoryStore();
            flow = new CampaignFlowCoordinator(campaign, progress, store);
            root = new GameObject("City Restoration Sequence Test");
            view = root.AddComponent<CampaignRuntimeView>();
            view.Build(campaign, progress, _ => { }, _ => { }, () => { });
            sequence = root.AddComponent<CityRestorationSequenceController>();
            sequence.Initialize(view, flow);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void NoPendingEvent_ShowsAuthoritativeMapWithoutSequence()
        {
            Assert.That(sequence.PreparePendingRestoration(), Is.False);
            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(view.IsMapInteractionEnabled, Is.True);
            Assert.That(Nodes()[0].VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage1));
        }

        [Test]
        public void NormalRestoration_PreparesCorrectNodesAndRoutedPathWithoutMutatingProgress()
        {
            CompleteChapter(0, true, 2);
            int stars = progress.TotalStars;

            Assert.That(sequence.PreparePendingRestoration(), Is.True);

            CityRestorationSequencePlan plan = sequence.CurrentPlan;
            Assert.That(plan.RestoredChapterId, Is.EqualTo(campaign.Chapters[0].ChapterId));
            Assert.That(plan.NextChapterId, Is.EqualTo(campaign.Chapters[1].ChapterId));
            Assert.That(plan.EnergyPathIndex, Is.Zero);
            Assert.That(Nodes()[0].VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage3));
            Assert.That(Nodes()[1].VisualState, Is.EqualTo(ChapterMapVisualState.Locked));
            Assert.That(Paths()[0].State, Is.EqualTo(CityEnergyPathState.Locked));
            Assert.That(progress.GetChapterState(campaign.Chapters[0].ChapterId),
                Is.EqualTo(CampaignChapterState.Restored));
            Assert.That(progress.GetChapterState(campaign.Chapters[1].ChapterId),
                Is.EqualTo(CampaignChapterState.Available));
            Assert.That(progress.TotalStars, Is.EqualTo(stars));
            Assert.That(flow.PeekPendingRestoration(), Is.Not.Null);
        }

        [Test]
        public void SequencePhases_AnimateOnlyTargetBuildingPathAndNextNode()
        {
            CompleteChapter(0, true, 3);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);

            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            Assert.That(Nodes()[0].VisualState, Is.EqualTo(ChapterMapVisualState.Restored));

            sequence.ApplyPhase(CityRestorationSequencePhase.EnergyTravel, 0.5f);
            Assert.That(Paths()[0].IsEnergyTraveling, Is.True);
            Assert.That(Paths()[0].TravelProgress, Is.EqualTo(0.5f));
            Assert.That(Paths().Skip(1).All(path => !path.IsEnergyTraveling), Is.True);

            sequence.ApplyPhase(CityRestorationSequencePhase.NextChapterReveal, 1f);
            Assert.That(Nodes()[1].VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage1));
            Assert.That(Nodes()[1].IsPulsing, Is.True);
            Assert.That(Nodes().Skip(2).All(node =>
                node.VisualState == ChapterMapVisualState.Locked), Is.True);
        }

        [Test]
        public void SequenceLocksMapAndConsumesOnlyAfterSuccessfulCompletion()
        {
            CompleteChapter(0, true, 2);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(view.IsMapInteractionEnabled, Is.False);
            Assert.That(flow.PeekPendingRestoration(), Is.Not.Null);

            Assert.That(sequence.CompletePreparedSequence(), Is.True);

            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(view.IsMapInteractionEnabled, Is.True);
            Assert.That(flow.PeekPendingRestoration(), Is.Null);
            Assert.That(Nodes()[0].VisualState, Is.EqualTo(ChapterMapVisualState.Restored));
            Assert.That(Nodes()[1].VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage1));
            Assert.That(Paths()[0].State, Is.EqualTo(CityEnergyPathState.Frontier));
            Assert.That(Nodes()[0].Label.text, Does.Contain("★ 20 / 30"));
            Assert.That(TotalStars().text, Is.EqualTo("★ 20 / 150"));
        }

        [Test]
        public void CancelledSequence_RestoresInputAndLeavesEventPending()
        {
            CompleteChapter(0, true, 1);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 0.5f);

            sequence.CancelPreparedSequence();

            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(view.IsMapInteractionEnabled, Is.True);
            Assert.That(flow.PeekPendingRestoration(), Is.Not.Null);
            Assert.That(Nodes()[0].VisualState, Is.EqualTo(ChapterMapVisualState.Restored));
        }

        [Test]
        public void ConsumeFailure_RestoresAuthoritativeMapAndLeavesInputUsable()
        {
            CompleteChapter(0, true, 1);
            store.FailSaves = true;
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "restoration sequence completed.*could not be durably consumed"));

            Assert.That(sequence.CompletePreparedSequence(), Is.False);

            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(view.IsMapInteractionEnabled, Is.True);
            Assert.That(flow.PeekPendingRestoration(), Is.Not.Null);
            Assert.That(Nodes()[0].VisualState, Is.EqualTo(ChapterMapVisualState.Restored));
        }

        [Test]
        public void ConsumedEvent_DoesNotReplayOnRevisit()
        {
            CompleteChapter(0, true, 1);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CompletePreparedSequence(), Is.True);

            Assert.That(sequence.PreparePendingRestoration(), Is.False);
            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(view.IsMapInteractionEnabled, Is.True);
        }

        [Test]
        public void RestoredChapterReplay_DoesNotPrepareSequence()
        {
            CompleteChapter(0, true, 1);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CompletePreparedSequence(), Is.True);
            CampaignLevelEntry final = campaign.Chapters[0].Levels[9];
            CampaignProgressUpdate replay = progress.RecordCompletion(final.LevelId,
                CampaignTestFixture.Result(final.LevelDefinition, 1, 1f, 3));

            Assert.That(replay.ChapterJustRestored, Is.False);
            Assert.That(sequence.PreparePendingRestoration(), Is.False);
        }

        [Test]
        public void FinalChapterSequence_HasNoNextNodeOrPathAndConsumesNormally()
        {
            for (int index = 0; index < campaign.Chapters.Count - 1; index++)
                CompleteChapter(index, false, 1);
            CompleteChapter(campaign.Chapters.Count - 1, true, 2);

            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CurrentPlan.HasNextChapter, Is.False);
            Assert.That(sequence.CurrentPlan.EnergyPathIndex, Is.EqualTo(-1));
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            sequence.ApplyPhase(CityRestorationSequencePhase.EnergyTravel, 1f);
            sequence.ApplyPhase(CityRestorationSequencePhase.NextChapterReveal, 1f);
            Assert.That(sequence.CompletePreparedSequence(), Is.True);
            Assert.That(progress.IsCampaignComplete, Is.True);
            Assert.That(Nodes().All(node => node.VisualState == ChapterMapVisualState.Restored),
                Is.True);
            Assert.That(Paths().All(path => path.State == CityEnergyPathState.Restored), Is.True);
        }

        [Test]
        public void MultiplePendingEvents_AreDeferredWithoutOverlap()
        {
            CompleteChapter(0, true, 1);
            CompleteChapter(1, true, 1);
            Assert.That(progress.PendingRestorationCount, Is.EqualTo(2));

            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CurrentPlan.RestoredChapterIndex, Is.Zero);
            Assert.That(sequence.CompletePreparedSequence(), Is.True);
            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(progress.PendingRestorationCount, Is.EqualTo(1));

            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CurrentPlan.RestoredChapterIndex, Is.EqualTo(1));
        }

        [TestCase("PowerStation_VerticalSlice")]
        [TestCase("Substation_VerticalSlice")]
        [TestCase("ControlCenter_VerticalSlice")]
        [TestCase("AutomationPlant_VerticalSlice")]
        [TestCase("CentralGrid_VerticalSlice")]
        public void FallbackCampaign_UsesStaticMapAndConsumesUnsupportedPresentation(
            string resourceName)
        {
            UnityEngine.Object.DestroyImmediate(root);
            CampaignDefinition fallback = Resources.Load<CampaignDefinition>(
                $"Campaigns/{resourceName}");
            var fallbackProgress = new CampaignProgressService(fallback);
            CompleteChapter(fallbackProgress, fallback.Chapters[0], true, 1);
            var fallbackFlow = new CampaignFlowCoordinator(fallback, fallbackProgress,
                new MemoryStore());
            root = new GameObject("Fallback Restoration Test");
            view = root.AddComponent<CampaignRuntimeView>();
            view.Build(fallback, fallbackProgress, _ => { }, _ => { }, () => { });
            sequence = root.AddComponent<CityRestorationSequenceController>();
            sequence.Initialize(view, fallbackFlow);

            Assert.That(sequence.PreparePendingRestoration(), Is.False);
            Assert.That(view.UsesCityMap, Is.False);
            Assert.That(view.IsMapInteractionEnabled, Is.True);
            Assert.That(fallbackFlow.PeekPendingRestoration(), Is.Null);
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(720f, 1280f)]
        public void SequenceUsesExistingResponsiveGeometry(float width, float height)
        {
            CompleteChapter(0, true, 1);
            Vector2[] nodePositions = Nodes().Select(node => node.HitArea.anchoredPosition).ToArray();
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 0.5f);
            sequence.ApplyPhase(CityRestorationSequencePhase.EnergyTravel, 0.5f);

            Assert.That(Nodes().Select(node => node.HitArea.anchoredPosition),
                Is.EqualTo(nodePositions));
            foreach (CityMapLayoutEntry entry in CityMapLayoutCatalog.Production.Entries)
            {
                Rect rect = CityMapLayoutCatalog.Production.CalculateHitRect(entry, width, height);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(width));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(height));
            }
        }

        [Test]
        public void SequenceTiming_IsWithinFunctionalTargetWindow()
        {
            float normalDuration = ProgrammerUiMetrics.CityRestorationFocusSeconds +
                                   ProgrammerUiMetrics.CityRestorationPowerUpSeconds +
                                   ProgrammerUiMetrics.CityRestorationEnergyTravelSeconds +
                                   ProgrammerUiMetrics.CityRestorationRevealSeconds +
                                   ProgrammerUiMetrics.CityRestorationSettleSeconds;
            Assert.That(normalDuration, Is.EqualTo(2.85f).Within(0.001f));
            Assert.That(normalDuration, Is.InRange(2.5f, 3.5f));
        }

        private void CompleteChapter(int chapterIndex, bool queue, int stars)
        {
            CompleteChapter(progress, campaign.Chapters[chapterIndex], queue, stars);
        }

        private static void CompleteChapter(CampaignProgressService target,
            CampaignChapterDefinition chapter, bool queue, int stars)
        {
            foreach (CampaignLevelEntry entry in chapter.Levels)
            {
                CampaignProgressUpdate update = target.RecordCompletion(entry.LevelId,
                    CampaignTestFixture.Result(entry.LevelDefinition, 1, 1f, stars));
                Assert.That(update.Accepted, Is.True, entry.LevelId);
                if (queue && update.ChapterJustRestored)
                    target.QueuePendingRestoration(update.RestoredChapterId);
            }
        }

        private CityChapterNodeView[] Nodes() =>
            root.GetComponentsInChildren<CityChapterNodeView>(true)
                .OrderBy(node => node.transform.GetSiblingIndex()).ToArray();

        private CityEnergyPathView[] Paths() =>
            root.GetComponentsInChildren<CityEnergyPathView>(true)
                .OrderBy(path => path.FromIndex).ToArray();

        private Text TotalStars() => root.transform
            .Find("Campaign Canvas/Campaign Map/Total Stars").GetComponent<Text>();

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public string SavePath => "memory://city-restoration-sequence";
            public bool FailSaves { get; set; }
            public CampaignLoadResult Load(CampaignDefinition definition) =>
                new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(definition), Array.Empty<string>());
            public CampaignSaveResult Save(CampaignProgressService target) => FailSaves
                ? new CampaignSaveResult(CampaignSaveStatus.Failed, "Simulated failure")
                : new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }
    }
}

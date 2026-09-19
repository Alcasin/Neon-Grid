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
            Assert.That(plan.IncludesFinalNetworkPulse, Is.False);
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
            sequence.ApplyPhase(CityRestorationSequencePhase.FinalNetworkPulse, 0.5f);
            Assert.That(Nodes().All(node => node.NetworkPulseStrength == 0f), Is.True);
            Assert.That(Paths().All(path => path.NetworkPulseStrength == 0f), Is.True);
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
        public void BuildingPowerUp_SynchronizesLabelAndReturnsToAuthoredScale()
        {
            CompleteChapter(0, true, 2);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            CityChapterNodeView restored = Nodes()[0];
            Vector3 baseScale = restored.BaseVisualScale;
            Assert.That(restored.Label.text, Does.Contain("POWERING UP"));
            Assert.That(restored.Label.text, Does.Not.Contain("RESTORED"));

            sequence.ApplyPhase(CityRestorationSequencePhase.Focus, 0.5f);
            Assert.That(restored.VisualScale, Is.Not.EqualTo(baseScale));
            sequence.ApplyPhase(CityRestorationSequencePhase.Focus, 1f);
            Assert.That(restored.VisualScale, Is.EqualTo(baseScale));
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 0.75f);
            Assert.That(restored.VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage3));
            Assert.That(restored.Label.text, Does.Contain("POWERING UP"));

            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);

            Assert.That(restored.VisualState, Is.EqualTo(ChapterMapVisualState.Restored));
            Assert.That(restored.Label.text, Does.Contain("RESTORED"));
            Assert.That(restored.Label.text, Does.Contain("★ 20 / 30"));
            Assert.That(restored.VisualScale, Is.EqualTo(baseScale));
            Assert.That(restored.IsPulsing, Is.False);
        }

        [Test]
        public void NextReveal_SynchronizesLabelArrivalCueAndAvailablePulse()
        {
            CompleteChapter(0, true, 1);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            CityChapterNodeView[] nodes = Nodes();
            CityChapterNodeView next = nodes[1];
            Vector3 baseScale = next.BaseVisualScale;
            Assert.That(next.Label.text, Does.Contain("LOCKED"));
            Assert.That(next.IsPulsing, Is.False);

            sequence.ApplyPhase(CityRestorationSequencePhase.NextChapterReveal, 0.5f);

            Assert.That(next.Label.text, Does.Contain("LOCKED"));
            Assert.That(next.VisualState, Is.EqualTo(ChapterMapVisualState.Locked));
            Assert.That(next.IsPulsing, Is.False);
            Assert.That(next.ArrivalCueStrength, Is.GreaterThan(0f));
            Assert.That(nodes.Where(node => node != next)
                .All(node => node.ArrivalCueStrength == 0f), Is.True);

            sequence.ApplyPhase(CityRestorationSequencePhase.NextChapterReveal, 1f);

            Assert.That(next.VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage1));
            Assert.That(next.Label.text, Does.Not.Contain("LOCKED"));
            Assert.That(next.Label.text, Does.Contain("0 / 10"));
            Assert.That(next.Label.text, Does.Contain("★ 0 / 30"));
            Assert.That(next.IsPulsing, Is.True);
            Assert.That(next.ArrivalCueStrength, Is.Zero);
            Assert.That(next.VisualScale, Is.EqualTo(baseScale));
            Assert.That(view.IsMapInteractionEnabled, Is.False);
        }

        [Test]
        public void EnergyTravel_IsMonotonicKeepsTraversedRouteLitAndSettlesAuthoritatively()
        {
            CompleteChapter(0, true, 1);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            CityEnergyPathView target = Paths()[0];
            Color initiallyLocked = target.GetSegmentColor(0);

            sequence.ApplyPhase(CityRestorationSequencePhase.EnergyTravel, 0.25f);
            float firstProgress = target.TravelProgress;
            Vector2 firstPosition = target.FrontierPosition;
            sequence.ApplyPhase(CityRestorationSequencePhase.EnergyTravel, 0.75f);
            float laterProgress = target.TravelProgress;
            Color traversedColor = target.GetSegmentColor(0);

            Assert.That(laterProgress, Is.GreaterThan(firstProgress));
            Assert.That(target.IsFrontierVisible, Is.True);
            Assert.That(target.FrontierPosition, Is.Not.EqualTo(firstPosition));
            Assert.That(traversedColor, Is.Not.EqualTo(initiallyLocked));
            sequence.ApplyPhase(CityRestorationSequencePhase.EnergyTravel, 0.9f);
            Assert.That(target.GetSegmentColor(0), Is.EqualTo(traversedColor));
            Assert.That(Paths().Skip(1).All(path => !path.IsEnergyTraveling), Is.True);

            Assert.That(sequence.CompletePreparedSequence(), Is.True);
            Assert.That(target.State, Is.EqualTo(CityEnergyPathState.Frontier));
            Assert.That(target.IsFrontierVisible, Is.False);
            Assert.That(target.NetworkPulseStrength, Is.Zero);
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
            Assert.That(Nodes().All(node => node.VisualScale == node.BaseVisualScale), Is.True);
            Assert.That(Nodes().All(node => node.ArrivalCueStrength == 0f &&
                                             node.NetworkPulseStrength == 0f), Is.True);
            Assert.That(Paths().All(path => !path.IsFrontierVisible &&
                                             path.NetworkPulseStrength == 0f), Is.True);
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
            Assert.That(Nodes().All(node => node.VisualScale == node.BaseVisualScale), Is.True);
            Assert.That(Paths().All(path => !path.IsFrontierVisible), Is.True);
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
            Assert.That(sequence.CurrentPlan.IncludesFinalNetworkPulse, Is.True);
            CityChapterNodeView central = Nodes()[campaign.Chapters.Count - 1];
            Assert.That(central.BaseVisualScale, Is.EqualTo(Vector3.one * 1.25f));
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            sequence.ApplyPhase(CityRestorationSequencePhase.FinalNetworkPulse, 0.5f);
            Assert.That(Nodes().All(node => node.NetworkPulseStrength > 0f), Is.True);
            Assert.That(Paths().All(path => path.NetworkPulseStrength > 0f), Is.True);
            sequence.ApplyPhase(CityRestorationSequencePhase.FinalNetworkPulse, 1f);
            Assert.That(Nodes().All(node => node.NetworkPulseStrength == 0f &&
                                             node.VisualScale == node.BaseVisualScale), Is.True);
            Assert.That(Paths().All(path => path.NetworkPulseStrength == 0f), Is.True);
            Assert.That(sequence.CompletePreparedSequence(), Is.True);
            Assert.That(progress.IsCampaignComplete, Is.True);
            Assert.That(Nodes().All(node => node.VisualState == ChapterMapVisualState.Restored),
                Is.True);
            Assert.That(Paths().All(path => path.State == CityEnergyPathState.Restored), Is.True);
            Assert.That(central.VisualScale, Is.EqualTo(central.BaseVisualScale));
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
            CompleteChapter(0, true, 1);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            CityRestorationSequencePlan plan = sequence.CurrentPlan;
            Assert.That(plan.TotalDuration, Is.EqualTo(2.85f).Within(0.001f));
            Assert.That(plan.TotalDuration, Is.InRange(2.5f, 3.2f));
            Assert.That(plan.Phases, Is.EqualTo(new[]
            {
                CityRestorationSequencePhase.Focus,
                CityRestorationSequencePhase.BuildingPowerUp,
                CityRestorationSequencePhase.EnergyTravel,
                CityRestorationSequencePhase.NextChapterReveal,
                CityRestorationSequencePhase.Settle
            }));
        }

        [Test]
        public void FinalSequencePlan_HasGenericNetworkPulseAndPolishedDuration()
        {
            for (int index = 0; index < campaign.Chapters.Count - 1; index++)
                CompleteChapter(index, false, 1);
            CompleteChapter(campaign.Chapters.Count - 1, true, 1);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            CityRestorationSequencePlan plan = sequence.CurrentPlan;

            Assert.That(plan.IncludesFinalNetworkPulse, Is.True);
            Assert.That(plan.TotalDuration, Is.EqualTo(2.3f).Within(0.001f));
            Assert.That(plan.TotalDuration, Is.InRange(2f, 2.5f));
            Assert.That(plan.Phases, Is.EqualTo(new[]
            {
                CityRestorationSequencePhase.Focus,
                CityRestorationSequencePhase.BuildingPowerUp,
                CityRestorationSequencePhase.PreNetworkSettle,
                CityRestorationSequencePhase.FinalNetworkPulse,
                CityRestorationSequencePhase.Settle
            }));
        }

        [Test]
        public void EasingFunctions_AreClampedMonotonicAndKeepExactEndpoints()
        {
            foreach (Func<float, float> easing in new Func<float, float>[]
                     {
                         CityRestorationEasing.EaseOutCubic,
                         CityRestorationEasing.EaseInOutCubic,
                         CityRestorationEasing.SmoothStep
                     })
            {
                Assert.That(easing(-1f), Is.Zero);
                Assert.That(easing(0f), Is.Zero);
                Assert.That(easing(0.25f), Is.LessThan(easing(0.75f)));
                Assert.That(easing(1f), Is.EqualTo(1f));
                Assert.That(easing(2f), Is.EqualTo(1f));
            }
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

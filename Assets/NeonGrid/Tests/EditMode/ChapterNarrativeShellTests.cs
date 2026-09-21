using System;
using System.Collections.Generic;
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
    public sealed class ChapterNarrativeShellTests
    {
        private CampaignDefinition campaign;
        private CampaignNarrativeDefinition narrative;
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            campaign = Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
            narrative = Resources.Load<CampaignNarrativeDefinition>(
                "Narratives/NeonGrid_Main_Narrative");
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void ProductionNarrative_HasFiveUniqueEntriesAssociatedByStableChapterId()
        {
            Assert.That(narrative, Is.Not.Null);
            Assert.That(narrative.ChapterNarratives, Has.Count.EqualTo(5));
            Assert.That(narrative.HasUniqueChapterIds(), Is.True);
            Assert.That(narrative.ChapterNarratives.Select(entry => entry.ChapterId),
                Is.EqualTo(campaign.Chapters.Select(chapter => chapter.ChapterId)));
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
            {
                Assert.That(narrative.TryGetChapterNarrative(chapter.ChapterId, out var entry),
                    Is.True, chapter.ChapterId);
                Assert.That(entry.HasBriefing, Is.True, chapter.ChapterId);
            }
        }

        [Test]
        public void DuplicateChapterIds_AreRejectedByNarrativeValidation()
        {
            var definition = ScriptableObject.CreateInstance<CampaignNarrativeDefinition>();
            try
            {
                definition.SetChapterNarratives(new[]
                {
                    new CampaignChapterNarrativeEntry("duplicate", "A", "A"),
                    new CampaignChapterNarrativeEntry("duplicate", "B", "B")
                });
                Assert.That(definition.HasUniqueChapterIds(), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void ProductionBriefings_HaveExactAuthoredContent()
        {
            AssertBriefing("power_station", "POWER STATION",
                "Primary generation is offline.\nRestore the local circuits.");
            AssertBriefing("substation", "SUBSTATION",
                "Power is available,\nbut distribution remains offline.");
            AssertBriefing("control_center", "CONTROL CENTER",
                "Grid control remains isolated.\nRestore command routing.");
            AssertBriefing("automation_plant", "AUTOMATION PLANT",
                "Automated infrastructure is offline.\nRestore system coordination.");
            AssertBriefing("central_grid", "CENTRAL GRID",
                "All major systems are online.\nReconnect the city's core grid.");
        }

        [Test]
        public void ProductionRestorationStatuses_HaveExactFourMessagesAndNoFinalMessage()
        {
            AssertRestoration("power_station", "POWER STATION RESTORED",
                "Generation stable.\nDistribution route established.");
            AssertRestoration("substation", "SUBSTATION RESTORED",
                "Distribution network stabilized.\nControl systems are now reachable.");
            AssertRestoration("control_center", "CONTROL CENTER RESTORED",
                "Command network online.\nAutomated infrastructure detected.");
            AssertRestoration("automation_plant", "AUTOMATION PLANT RESTORED",
                "Automation synchronized.\nCentral Grid access established.");
            Assert.That(narrative.TryGetChapterNarrative("central_grid", out var final), Is.True);
            Assert.That(final.HasRestorationStatus, Is.False);
            Assert.That(narrative.ChapterNarratives.Count(entry => entry.HasRestorationStatus),
                Is.EqualTo(4));
        }

        [Test]
        public void AllProductionSelectors_BindCorrectBriefingByChapterId()
        {
            CampaignRuntimeView view = BuildView(campaign, new CampaignProgressService(campaign),
                out _);
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
            {
                view.ShowChapter(chapter);
                Assert.That(view.HasVisibleChapterBriefing, Is.True, chapter.ChapterId);
                Assert.That(narrative.TryGetChapterNarrative(chapter.ChapterId, out var expected),
                    Is.True);
                Assert.That(view.ChapterBriefingTitle.text,
                    Is.EqualTo($"SYSTEM BRIEFING  /  {expected.BriefingTitle}"));
                Assert.That(view.ChapterBriefingBody.text, Is.EqualTo(expected.BriefingBody));
            }
        }

        [TestCase("PowerStation_VerticalSlice")]
        [TestCase("Substation_VerticalSlice")]
        [TestCase("ControlCenter_VerticalSlice")]
        [TestCase("AutomationPlant_VerticalSlice")]
        [TestCase("CentralGrid_VerticalSlice")]
        public void VerticalSliceSelector_OmitsBriefingWithoutPlaceholderOrLayoutChange(
            string resourceName)
        {
            CampaignDefinition slice = Resources.Load<CampaignDefinition>($"Campaigns/{resourceName}");
            CampaignRuntimeView view = BuildView(slice, new CampaignProgressService(slice), out var root);

            view.ShowChapter(slice.Chapters[0]);

            Assert.That(CampaignNarrativeCatalog.LoadForCampaign(slice.CampaignId), Is.Null);
            Assert.That(view.HasVisibleChapterBriefing, Is.False);
            Assert.That(view.LevelGrid.anchoredPosition.y,
                Is.EqualTo(-ProgrammerUiMetrics.SelectorGridTopInset));
            Assert.That(root.transform.Find("Campaign Canvas/Level Selection/Back To Map")
                .GetComponent<Button>().interactable, Is.True);
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(720f, 1280f)]
        public void SelectorBriefing_FitsWithoutChangingAcceptedGrid(float width, float height)
        {
            CampaignRuntimeView view = BuildView(campaign, new CampaignProgressService(campaign),
                out GameObject root);
            view.ShowChapter(campaign.Chapters[0]);
            float virtualHeight = VirtualHeight(width, height);
            RectTransform title = root.transform.Find(
                "Campaign Canvas/Level Selection/Generated Chapter Title")
                .GetComponent<RectTransform>();
            RectTransform briefingTitle = view.ChapterBriefingTitle.rectTransform;
            RectTransform briefingBody = view.ChapterBriefingBody.rectTransform;
            RectTransform grid = view.LevelGrid;
            RectTransform back = root.transform.Find(
                "Campaign Canvas/Level Selection/Back To Map").GetComponent<RectTransform>();

            Assert.That(BottomFromTop(title), Is.LessThan(TopFromTop(briefingTitle)));
            Assert.That(BottomFromTop(briefingTitle), Is.LessThan(TopFromTop(briefingBody)));
            Assert.That(BottomFromTop(briefingBody), Is.LessThan(-grid.anchoredPosition.y));
            Assert.That(-grid.anchoredPosition.y + grid.sizeDelta.y,
                Is.LessThan(virtualHeight - back.anchoredPosition.y - back.sizeDelta.y * 0.5f));
            Assert.That(view.ChapterBriefingTitle.fontSize, Is.EqualTo(36));
            Assert.That(view.ChapterBriefingBody.fontSize, Is.EqualTo(46));
            Assert.That(-grid.anchoredPosition.y, Is.EqualTo(380f));
            Assert.That(back.anchoredPosition.y, Is.EqualTo(170f));
            Assert.That(back.sizeDelta, Is.EqualTo(new Vector2(500f, 120f)));
            Assert.That(back.anchoredPosition.y, Is.LessThan(360f));
            Assert.That(grid.sizeDelta.x, Is.EqualTo(724f));
            HorizontalLayoutGroup[] rows =
                grid.GetComponentsInChildren<HorizontalLayoutGroup>(true);
            Assert.That(rows, Has.Length.EqualTo(4));
            int[] rowCounts = rows
                .Select(row => row.transform.childCount).ToArray();
            Assert.That(rowCounts, Is.EqualTo(new[] { 3, 3, 3, 1 }));
            Assert.That(rows[3].childAlignment, Is.EqualTo(TextAnchor.MiddleCenter));
            foreach (LayoutElement tile in grid.GetComponentsInChildren<LayoutElement>(true)
                         .Where(element => element.gameObject.name.StartsWith("Generated Level ") &&
                                           !element.gameObject.name.StartsWith("Generated Level Row")))
            {
                Assert.That(tile.preferredWidth, Is.EqualTo(220f));
                Assert.That(tile.preferredHeight, Is.EqualTo(220f));
            }
            foreach (Text label in grid.GetComponentsInChildren<Text>(true)
                         .Where(text => text.gameObject.name == "Level Number"))
            {
                RectTransform labelRect = label.rectTransform;
                Assert.That(label.fontSize, Is.EqualTo(76));
                Assert.That(Mathf.Abs(labelRect.anchoredPosition.x) +
                            labelRect.sizeDelta.x * 0.5f, Is.LessThanOrEqualTo(110f));
                Assert.That(Mathf.Abs(labelRect.anchoredPosition.y) +
                            labelRect.sizeDelta.y * 0.5f, Is.LessThanOrEqualTo(110f));
            }
            Assert.That(root.GetComponentInChildren<ScrollRect>(true), Is.Null);
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(720f, 1280f)]
        public void RestorationStatus_FitsBetweenMapHeaderAndCriticalNodeGeometry(
            float width, float height)
        {
            CampaignRuntimeView view = BuildView(campaign, new CampaignProgressService(campaign),
                out _);
            RectTransform panel = view.RestorationStatus.PanelRect;
            float virtualWidth = VirtualWidth(width, height);
            float virtualHeight = VirtualHeight(width, height);
            float left = virtualWidth * 0.5f - panel.sizeDelta.x * 0.5f;
            float right = left + panel.sizeDelta.x;
            float panelBottomFromTop = -panel.anchoredPosition.y + panel.sizeDelta.y;
            CityMapLayoutEntry highestNode = CityMapLayoutCatalog.Production.Entries
                .OrderByDescending(entry => entry.Position.y + entry.HitSize.y * 0.5f).First();
            float highestNodeTopFromTop = virtualHeight * 0.5f -
                (CityMapLayoutDefinition.CompositionOffset.y + highestNode.Position.y +
                 highestNode.HitSize.y * 0.5f);

            Assert.That(left, Is.GreaterThanOrEqualTo(0f));
            Assert.That(right, Is.LessThanOrEqualTo(virtualWidth));
            Assert.That(-panel.anchoredPosition.y, Is.GreaterThan(247f));
            Assert.That(panelBottomFromTop, Is.LessThan(highestNodeTopFromTop));
            Assert.That(panelBottomFromTop, Is.LessThan(virtualHeight));
            Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(880f, 180f)));
            Assert.That(view.RestorationStatus.TitleText.fontSize, Is.EqualTo(40));
            Assert.That(view.RestorationStatus.BodyText.fontSize, Is.EqualTo(34));
        }

        [Test]
        public void RestorationStatus_AppearsOnlyAfterRestoredTransitionAndUsesRestoredId()
        {
            BuildSequence(0, out CampaignRuntimeView view, out var flow, out var sequence,
                out _);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(view.RestorationStatus.IsVisible, Is.False);

            sequence.ApplyPhase(CityRestorationSequencePhase.Focus, 1f);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 0.999f);
            Assert.That(view.RestorationStatus.IsVisible, Is.False);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);

            Assert.That(view.RestorationStatus.IsVisible, Is.True);
            Assert.That(view.RestorationStatus.CurrentChapterId,
                Is.EqualTo(sequence.CurrentPlan.RestoredChapterId));
            Assert.That(view.RestorationStatus.CurrentChapterId,
                Is.Not.EqualTo(sequence.CurrentPlan.NextChapterId));
            Assert.That(view.RestorationStatus.TitleText.text,
                Is.EqualTo("POWER STATION RESTORED"));
            Assert.That(view.RestorationStatus.BodyText.text,
                Is.EqualTo("Generation stable.\nDistribution route established."));
            Assert.That(flow.PeekPendingRestoration(), Is.Not.Null);
        }

        [Test]
        public void RestorationStatus_RemainsThroughUnlockedPostSequenceTail()
        {
            BuildSequence(0, out CampaignRuntimeView view, out var flow, out var sequence,
                out _);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            sequence.ApplyPhase(CityRestorationSequencePhase.EnergyTravel, 0.5f);
            Assert.That(view.RestorationStatus.IsVisible, Is.True);
            sequence.ApplyPhase(CityRestorationSequencePhase.NextChapterReveal, 1f);
            Assert.That(view.RestorationStatus.IsVisible, Is.True);

            Assert.That(sequence.CompletePreparedSequence(), Is.True);

            Assert.That(view.RestorationStatus.IsVisible, Is.True);
            Assert.That(sequence.IsStatusTailRunning, Is.True);
            Assert.That(sequence.StatusTailHoldSeconds, Is.EqualTo(1.65f));
            Assert.That(sequence.StatusTailFadeSeconds, Is.EqualTo(0.3f));
            Assert.That(view.IsMapInteractionEnabled, Is.True);
            Assert.That(flow.PeekPendingRestoration(), Is.Null);
            sequence.ApplyStatusTailFade(0.5f);
            Assert.That(view.RestorationStatus.Opacity, Is.EqualTo(0.5f).Within(0.001f));
            sequence.CancelStatusTail();
            Assert.That(view.RestorationStatus.IsVisible, Is.False);
            Assert.That(sequence.PreparePendingRestoration(), Is.False);
            Assert.That(view.RestorationStatus.IsVisible, Is.False);
        }

        [Test]
        public void NavigatingAway_CancelsTailAndHidesStatusImmediately()
        {
            BuildSequence(0, out CampaignRuntimeView view, out _, out var sequence, out _);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            Assert.That(sequence.CompletePreparedSequence(), Is.True);
            Assert.That(sequence.IsStatusTailRunning, Is.True);

            sequence.CancelStatusTail();
            view.ShowChapter(campaign.Chapters[0]);

            Assert.That(sequence.IsStatusTailRunning, Is.False);
            Assert.That(view.RestorationStatus.IsVisible, Is.False);
            Assert.That(view.HasVisibleChapterBriefing, Is.True);
        }

        [Test]
        public void CancelledRestoration_HidesStatusAndLeavesEventPending()
        {
            BuildSequence(0, out CampaignRuntimeView view, out var flow, out var sequence,
                out _);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);

            sequence.CancelPreparedSequence();

            Assert.That(view.RestorationStatus.IsVisible, Is.False);
            Assert.That(flow.PeekPendingRestoration(), Is.Not.Null);
            Assert.That(view.IsMapInteractionEnabled, Is.True);
        }

        [Test]
        public void ConsumeFailure_HidesStatusAndLeavesEventPending()
        {
            BuildSequence(0, out CampaignRuntimeView view, out var flow, out var sequence,
                out MemoryStore store);
            store.FailSaves = true;
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "restoration sequence completed.*could not be durably consumed"));

            Assert.That(sequence.CompletePreparedSequence(), Is.False);

            Assert.That(view.RestorationStatus.IsVisible, Is.False);
            Assert.That(flow.PeekPendingRestoration(), Is.Not.Null);
            Assert.That(view.IsMapInteractionEnabled, Is.True);
        }

        [Test]
        public void FinalChapter_HasBriefingButNoStatusAndKeepsNetworkPulseTiming()
        {
            BuildSequence(campaign.Chapters.Count - 1, out CampaignRuntimeView view, out _,
                out var sequence, out _, true);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CurrentPlan.IncludesFinalNetworkPulse, Is.True);
            Assert.That(sequence.CurrentPlan.TotalDuration, Is.EqualTo(2.3f).Within(0.001f));
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            Assert.That(view.RestorationStatus.IsVisible, Is.False);
            sequence.ApplyPhase(CityRestorationSequencePhase.FinalNetworkPulse, 0.5f);
            Assert.That(view.RestorationStatus.IsVisible, Is.False);
        }

        [Test]
        public void NormalRestorationDuration_RemainsExactlyAcceptedValue()
        {
            BuildSequence(0, out _, out _, out var sequence, out _);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CurrentPlan.TotalDuration, Is.EqualTo(2.85f).Within(0.001f));
        }

        [Test]
        public void IntroPagesAndCompletionPersistenceRemainUnchanged()
        {
            Assert.That(narrative.IntroPages, Has.Count.EqualTo(4));
            Assert.That(narrative.IntroPages.Select(page => page.Title), Is.EqualTo(new[]
            {
                "CITY GRID FAILURE", "SYSTEM STATUS", "MANUAL GRID ACCESS",
                "RESTORATION PRIORITY"
            }));
            var progress = new CampaignProgressService(campaign);
            var store = new MemoryStore();
            var flow = new CampaignFlowCoordinator(campaign, progress, store);
            Assert.That(flow.TryCompleteIntro(), Is.True);
            Assert.That(progress.IntroCompleted, Is.True);
            Assert.That(store.SaveCount, Is.EqualTo(1));
        }

        private CampaignRuntimeView BuildView(CampaignDefinition definition,
            CampaignProgressService progress, out GameObject root)
        {
            root = new GameObject("M14-C View Test");
            cleanup.Add(root);
            var view = root.AddComponent<CampaignRuntimeView>();
            view.Build(definition, progress, _ => { }, _ => { }, () => { });
            return view;
        }

        private void BuildSequence(int restoredIndex, out CampaignRuntimeView view,
            out CampaignFlowCoordinator flow, out CityRestorationSequenceController sequence,
            out MemoryStore store, bool completeEarlierChapters = false)
        {
            var progress = new CampaignProgressService(campaign);
            if (completeEarlierChapters)
                for (int index = 0; index < restoredIndex; index++)
                    CompleteChapter(progress, campaign.Chapters[index], false);
            CompleteChapter(progress, campaign.Chapters[restoredIndex], true);
            store = new MemoryStore();
            flow = new CampaignFlowCoordinator(campaign, progress, store);
            view = BuildView(campaign, progress, out GameObject root);
            sequence = root.AddComponent<CityRestorationSequenceController>();
            sequence.Initialize(view, flow);
        }

        private static void CompleteChapter(CampaignProgressService progress,
            CampaignChapterDefinition chapter, bool queue)
        {
            foreach (CampaignLevelEntry level in chapter.Levels)
            {
                CampaignProgressUpdate update = progress.RecordCompletion(level.LevelId,
                    CampaignTestFixture.Result(level.LevelDefinition, 1, 1f, 1));
                Assert.That(update.Accepted, Is.True, level.LevelId);
                if (queue && update.ChapterJustRestored)
                    progress.QueuePendingRestoration(update.RestoredChapterId);
            }
        }

        private void AssertBriefing(string chapterId, string title, string body)
        {
            Assert.That(narrative.TryGetChapterNarrative(chapterId, out var entry), Is.True);
            Assert.That(entry.BriefingTitle, Is.EqualTo(title));
            Assert.That(entry.BriefingBody, Is.EqualTo(body));
        }

        private void AssertRestoration(string chapterId, string title, string body)
        {
            Assert.That(narrative.TryGetChapterNarrative(chapterId, out var entry), Is.True);
            Assert.That(entry.RestoredTitle, Is.EqualTo(title));
            Assert.That(entry.RestoredBody, Is.EqualTo(body));
            Assert.That(entry.HasRestorationStatus, Is.True);
        }

        private static float VirtualScale(float width, float height) =>
            Mathf.Sqrt((width / 1080f) * (height / 1920f));
        private static float VirtualWidth(float width, float height) =>
            width / VirtualScale(width, height);
        private static float VirtualHeight(float width, float height) =>
            height / VirtualScale(width, height);
        private static float TopFromTop(RectTransform rect) =>
            -rect.anchoredPosition.y - rect.sizeDelta.y * (1f - rect.pivot.y);
        private static float BottomFromTop(RectTransform rect) =>
            -rect.anchoredPosition.y + rect.sizeDelta.y * rect.pivot.y;

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public int SaveCount { get; private set; }
            public bool FailSaves { get; set; }
            public string SavePath => "memory://m14-c";
            public CampaignLoadResult Load(CampaignDefinition definition) =>
                new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(definition), Array.Empty<string>());
            public CampaignSaveResult Save(CampaignProgressService progress)
            {
                SaveCount++;
                return FailSaves
                    ? new CampaignSaveResult(CampaignSaveStatus.Failed, "Simulated failure")
                    : new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }
            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }
    }
}

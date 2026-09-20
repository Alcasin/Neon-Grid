using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class CampaignIntroTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void ProductionNarrative_HasExactOrderedFourPageCopy()
        {
            CampaignNarrativeDefinition narrative = LoadNarrative();

            Assert.That(narrative.CampaignId, Is.EqualTo("neon_grid_main"));
            Assert.That(narrative.IntroPages, Has.Count.EqualTo(4));
            AssertPage(narrative, 0, "CITY GRID FAILURE",
                "A cascading power failure has\ndisconnected the city's core systems.", "NEXT");
            AssertPage(narrative, 1, "SYSTEM STATUS",
                "Most districts are offline.\nAutomatic recovery has failed.", "NEXT");
            AssertPage(narrative, 2, "MANUAL GRID ACCESS",
                "Operator authorization confirmed.\nManual restoration is available.", "NEXT");
            AssertPage(narrative, 3, "RESTORATION PRIORITY",
                "Restore the Power Station.\n\nBring the city back online.",
                "BEGIN RESTORATION");
        }

        [Test]
        public void Sequence_StartsAtFirstPage_AdvancesExactlyOnce_AndStopsAtFinalPage()
        {
            var sequence = new CampaignIntroSequence(LoadNarrative());

            Assert.That(sequence.CurrentPageIndex, Is.Zero);
            Assert.That(sequence.CurrentPage.Title, Is.EqualTo("CITY GRID FAILURE"));
            Assert.That(sequence.MoveNext(), Is.True);
            Assert.That(sequence.CurrentPageIndex, Is.EqualTo(1));
            Assert.That(sequence.MoveNext(), Is.True);
            Assert.That(sequence.MoveNext(), Is.True);
            Assert.That(sequence.IsFinalPage, Is.True);
            Assert.That(sequence.CurrentPage.PrimaryAction, Is.EqualTo("BEGIN RESTORATION"));
            Assert.That(sequence.MoveNext(), Is.False);
            Assert.That(sequence.CurrentPageIndex, Is.EqualTo(3));
        }

        [Test]
        public void IntroView_PrimaryAndSkipHaveSingleTransitionAndFailedSaveCanRetry()
        {
            int completions = 0;
            bool allowCompletion = false;
            CampaignIntroView view = CreateIntroView(() =>
            {
                completions++;
                return allowCompletion;
            });

            view.PressPrimary();
            Assert.That(view.CurrentPageIndex, Is.EqualTo(1));
            view.PressSkip();
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.IsInteractionEnabled, Is.True);

            allowCompletion = true;
            view.PressSkip();
            view.PressSkip();
            Assert.That(completions, Is.EqualTo(2));
            Assert.That(view.IsInteractionEnabled, Is.False);
        }

        [Test]
        public void FreshProductionStartup_ShowsIntroBeforeHiddenMap()
        {
            CampaignRuntimeController controller = CreateController(LoadMain(), out MemoryStore store);

            Assert.That(store.Progress.IntroCompleted, Is.False);
            Assert.That(controller.IntroView, Is.Not.Null);
            Assert.That(controller.IntroView.IsVisible, Is.True);
            Assert.That(controller.IntroView.CurrentPageIndex, Is.Zero);
            Assert.That(controller.CampaignView.IsVisible, Is.False);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void Skip_FromEarlyOrMiddlePage_PersistsAndEntersFreshMap(int startingPage)
        {
            CampaignRuntimeController controller = CreateController(LoadMain(), out MemoryStore store);
            for (int index = 0; index < startingPage; index++) controller.IntroView.PressPrimary();

            controller.IntroView.PressSkip();

            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.Progress.IntroCompleted, Is.True);
            Assert.That(controller.IntroView.IsVisible, Is.False);
            AssertFreshMap(controller);
        }

        [Test]
        public void FinalAction_PersistsOnceAndEntersMap()
        {
            CampaignRuntimeController controller = CreateController(LoadMain(), out MemoryStore store);
            controller.IntroView.PressPrimary();
            controller.IntroView.PressPrimary();
            controller.IntroView.PressPrimary();
            Assert.That(controller.IntroView.PrimaryButton.transform.Find("Label")
                .GetComponent<Text>().text, Is.EqualTo("BEGIN RESTORATION"));

            controller.IntroView.PressPrimary();
            controller.IntroView.PressPrimary();

            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.Progress.IntroCompleted, Is.True);
            AssertFreshMap(controller);
        }

        [Test]
        public void FailedCompletionSave_RemainsInteractiveInIntro_ThenSafeRetryEntersMap()
        {
            CampaignRuntimeController controller = CreateController(LoadMain(), out MemoryStore store);
            store.FailSaves = true;
            LogAssert.Expect(LogType.Warning, "Could not save intro completion.");

            controller.IntroView.PressSkip();

            Assert.That(store.Progress.IntroCompleted, Is.False);
            Assert.That(controller.IntroView.IsVisible, Is.True);
            Assert.That(controller.IntroView.IsInteractionEnabled, Is.True);
            Assert.That(controller.CampaignView.IsVisible, Is.False);

            store.FailSaves = false;
            controller.IntroView.PressSkip();
            Assert.That(store.SaveCount, Is.EqualTo(2));
            Assert.That(store.Progress.IntroCompleted, Is.True);
            AssertFreshMap(controller);
        }

        [Test]
        public void CompletedIntro_ReopenGoesDirectlyToMapWithoutIntroView()
        {
            CampaignRuntimeController first = CreateController(LoadMain(), out MemoryStore store);
            first.IntroView.PressSkip();
            UnityEngine.Object.DestroyImmediate(first.gameObject);

            CampaignRuntimeController reopened = CreateController(LoadMain(), store);

            Assert.That(reopened.IntroView, Is.Null);
            AssertFreshMap(reopened);
        }

        [Test]
        public void InterruptedIntro_ReopenRestartsAtPageOne()
        {
            CampaignRuntimeController first = CreateController(LoadMain(), out MemoryStore store);
            first.IntroView.PressPrimary();
            first.IntroView.PressPrimary();
            Assert.That(store.SaveCount, Is.Zero);
            UnityEngine.Object.DestroyImmediate(first.gameObject);

            CampaignRuntimeController reopened = CreateController(LoadMain(), store);

            Assert.That(store.Progress.IntroCompleted, Is.False);
            Assert.That(reopened.IntroView.CurrentPageIndex, Is.Zero);
            Assert.That(reopened.CampaignView.IsVisible, Is.False);
        }

        [Test]
        public void VerticalSliceCampaigns_HaveNoNarrativeAndOpenMapDirectly()
        {
            string[] names =
            {
                "PowerStation_VerticalSlice", "Substation_VerticalSlice",
                "ControlCenter_VerticalSlice", "AutomationPlant_VerticalSlice",
                "CentralGrid_VerticalSlice"
            };
            foreach (string name in names)
            {
                CampaignDefinition campaign = Resources.Load<CampaignDefinition>($"Campaigns/{name}");
                Assert.That(campaign, Is.Not.Null, name);
                Assert.That(CampaignNarrativeCatalog.LoadForCampaign(campaign.CampaignId), Is.Null,
                    name);
                CampaignRuntimeController controller = CreateController(campaign, out _);
                Assert.That(controller.IntroView, Is.Null, name);
                Assert.That(controller.CampaignView.IsVisible, Is.True, name);
            }
        }

        [Test]
        public void IntroCompletion_DoesNotCreateOrConsumeRestorationEvents()
        {
            using (var fixture = new CampaignTestFixture())
            {
                var progress = new CampaignProgressService(fixture.Campaign);
                foreach (CampaignLevelEntry level in fixture.Campaign.Chapters[0].Levels)
                    Assert.That(progress.RecordCompletion(level.LevelId,
                        CampaignTestFixture.Result(level.LevelDefinition, 1, 1f, 1)).Accepted,
                        Is.True);
                progress.QueuePendingRestoration(fixture.Campaign.Chapters[0].ChapterId);
                var store = new MemoryStore(progress);
                var flow = new CampaignFlowCoordinator(fixture.Campaign, progress, store);
                ChapterRestorationEvent before = progress.PendingRestoration;

                Assert.That(flow.TryCompleteIntro(), Is.True);

                Assert.That(progress.PendingRestorationCount, Is.EqualTo(1));
                Assert.That(progress.PendingRestoration, Is.SameAs(before));
            }
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2340)]
        [TestCase(720, 1280)]
        public void IntroLayout_RemainsOnScreenWithoutOverlapOrScrollRect(int width, int height)
        {
            CampaignIntroView view = CreateIntroView(() => true);
            CanvasScaler scaler = view.GetComponentInChildren<CanvasScaler>(true);
            float scale = Mathf.Sqrt((width / CampaignIntroView.ReferenceWidth) *
                                     (height / CampaignIntroView.ReferenceHeight));
            float virtualWidth = width / scale;
            float virtualHeight = height / scale;

            Assert.That(scaler.referenceResolution,
                Is.EqualTo(new Vector2(CampaignIntroView.ReferenceWidth,
                    CampaignIntroView.ReferenceHeight)));
            Assert.That(view.GetComponentInChildren<ScrollRect>(true), Is.Null);
            AssertWithin(view.TitleText.rectTransform, virtualWidth, virtualHeight);
            AssertWithin(view.BodyText.rectTransform, virtualWidth, virtualHeight);
            AssertWithin(view.IndicatorText.rectTransform, virtualWidth, virtualHeight);
            AssertWithin(view.SkipButton.GetComponent<RectTransform>(), virtualWidth, virtualHeight);
            AssertWithin(view.PrimaryButton.GetComponent<RectTransform>(), virtualWidth, virtualHeight);
            Assert.That(Top(view.BodyText.rectTransform, virtualHeight),
                Is.LessThan(Bottom(view.TitleText.rectTransform, virtualHeight)));
            Assert.That(Top(view.IndicatorText.rectTransform, virtualHeight),
                Is.LessThan(Bottom(view.BodyText.rectTransform, virtualHeight)));
            Assert.That(Top(view.PrimaryButton.GetComponent<RectTransform>(), virtualHeight),
                Is.LessThan(Bottom(view.IndicatorText.rectTransform, virtualHeight)));
        }

        [Test]
        public void SaveSchema_NewAndLegacyIntroRulesAreBackwardCompatible()
        {
            using (var fixture = new CampaignTestFixture())
            {
                string directory = Path.Combine(Path.GetTempPath(), "NeonGridM14B",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, CampaignSaveStore.SaveFileName);
                try
                {
                    Write(path, fixture.Campaign.CampaignId, false, false,
                        new List<LevelProgressSaveEntry>());
                    Assert.That(new CampaignSaveStore(path).Load(fixture.Campaign)
                        .Progress.IntroCompleted, Is.False, "legacy empty");

                    var progressed = new List<LevelProgressSaveEntry>
                    {
                        new LevelProgressSaveEntry("power_01", true, 2, 3, 4f)
                    };
                    Write(path, fixture.Campaign.CampaignId, false, false, progressed);
                    CampaignLoadResult legacy = new CampaignSaveStore(path).Load(fixture.Campaign);
                    Assert.That(legacy.Progress.IntroCompleted, Is.True, "legacy progressed");
                    Assert.That(legacy.Progress.GetLevelProgress("power_01").BestStars, Is.EqualTo(2));

                    Write(path, fixture.Campaign.CampaignId, true, false,
                        new List<LevelProgressSaveEntry>());
                    Assert.That(new CampaignSaveStore(path).Load(fixture.Campaign)
                        .Progress.IntroCompleted, Is.False, "new pending");
                    Write(path, fixture.Campaign.CampaignId, true, true,
                        new List<LevelProgressSaveEntry>());
                    Assert.That(new CampaignSaveStore(path).Load(fixture.Campaign)
                        .Progress.IntroCompleted, Is.True, "new completed");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private CampaignIntroView CreateIntroView(Func<bool> complete)
        {
            var root = new GameObject("Intro Test Root");
            cleanup.Add(root);
            CampaignIntroView view = root.AddComponent<CampaignIntroView>();
            view.Build(LoadNarrative(), complete);
            return view;
        }

        private CampaignRuntimeController CreateController(CampaignDefinition campaign,
            out MemoryStore store)
        {
            store = new MemoryStore(new CampaignProgressService(campaign));
            return CreateController(campaign, store);
        }

        private CampaignRuntimeController CreateController(CampaignDefinition campaign,
            MemoryStore store)
        {
            var root = new GameObject($"Controller Test - {campaign.CampaignId}");
            root.SetActive(false);
            cleanup.Add(root);
            CampaignRuntimeController controller = root.AddComponent<CampaignRuntimeController>();
            controller.Initialize(campaign, store);
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (camera.transform.parent == null && !cleanup.Contains(camera.gameObject))
                    cleanup.Add(camera.gameObject);
            return controller;
        }

        private static CampaignNarrativeDefinition LoadNarrative()
        {
            CampaignNarrativeDefinition narrative =
                Resources.Load<CampaignNarrativeDefinition>(
                    "Narratives/NeonGrid_Main_Narrative");
            Assert.That(narrative, Is.Not.Null);
            return narrative;
        }

        private static CampaignDefinition LoadMain()
        {
            CampaignDefinition campaign =
                Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
            Assert.That(campaign, Is.Not.Null);
            return campaign;
        }

        private static void AssertPage(CampaignNarrativeDefinition narrative, int index,
            string title, string body, string action)
        {
            Assert.That(narrative.IntroPages[index].Title, Is.EqualTo(title));
            Assert.That(narrative.IntroPages[index].Body, Is.EqualTo(body));
            Assert.That(narrative.IntroPages[index].PrimaryAction, Is.EqualTo(action));
        }

        private static void AssertFreshMap(CampaignRuntimeController controller)
        {
            Assert.That(controller.CampaignView.IsVisible, Is.True);
            Assert.That(controller.Flow.Progress.TotalStars, Is.Zero);
            Assert.That(controller.Flow.Progress.MaximumCampaignStars, Is.EqualTo(150));
            Assert.That(controller.Flow.Progress.GetChapterState("power_station"),
                Is.EqualTo(CampaignChapterState.Available));
            foreach (CampaignChapterDefinition chapter in controller.Flow.Campaign.Chapters.Skip(1))
                Assert.That(controller.Flow.Progress.GetChapterState(chapter.ChapterId),
                    Is.EqualTo(CampaignChapterState.Locked), chapter.ChapterId);
        }

        private static void AssertWithin(RectTransform rect, float width, float height)
        {
            float anchorX = rect.anchorMin.x * width - width * 0.5f;
            float anchorY = rect.anchorMin.y * height - height * 0.5f;
            float left = anchorX + rect.anchoredPosition.x - rect.sizeDelta.x * rect.pivot.x;
            float right = left + rect.sizeDelta.x;
            float bottom = anchorY + rect.anchoredPosition.y - rect.sizeDelta.y * rect.pivot.y;
            float top = bottom + rect.sizeDelta.y;
            Assert.That(left, Is.GreaterThanOrEqualTo(-width * 0.5f));
            Assert.That(right, Is.LessThanOrEqualTo(width * 0.5f));
            Assert.That(bottom, Is.GreaterThanOrEqualTo(-height * 0.5f));
            Assert.That(top, Is.LessThanOrEqualTo(height * 0.5f));
        }

        private static float Top(RectTransform rect, float height) =>
            rect.anchorMin.y * height - height * 0.5f + rect.anchoredPosition.y +
            rect.sizeDelta.y * (1f - rect.pivot.y);
        private static float Bottom(RectTransform rect, float height) =>
            rect.anchorMin.y * height - height * 0.5f + rect.anchoredPosition.y -
            rect.sizeDelta.y * rect.pivot.y;

        private static void Write(string path, string campaignId, bool hasState,
            bool completed, List<LevelProgressSaveEntry> entries)
        {
            var data = new CampaignSaveData
            {
                version = CampaignSaveStore.CurrentVersion,
                campaignId = campaignId,
                hasIntroCompletionState = hasState,
                introCompleted = completed,
                levelProgressEntries = entries
            };
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public CampaignProgressService Progress { get; }
            public bool FailSaves { get; set; }
            public int SaveCount { get; private set; }
            public string SavePath => "memory://m14-b";

            public MemoryStore(CampaignProgressService progress)
            {
                Progress = progress;
            }

            public CampaignLoadResult Load(CampaignDefinition campaign) =>
                new CampaignLoadResult(CampaignLoadStatus.NoSaveFound, Progress,
                    Array.Empty<string>());

            public CampaignSaveResult Save(CampaignProgressService progress)
            {
                SaveCount++;
                return FailSaves
                    ? new CampaignSaveResult(CampaignSaveStatus.Failed,
                        "Could not save intro completion.")
                    : new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }

            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class CampaignEndingTests
    {
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject item in cleanup)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            cleanup.Clear();
        }

        [Test]
        public void ProductionNarrative_HasExactAuthoredEnding()
        {
            CampaignNarrativeDefinition narrative = LoadNarrative();
            CampaignEndingNarrative ending = narrative.EndingNarrative;

            Assert.That(ending, Is.Not.Null);
            Assert.That(ending.IsConfigured, Is.True);
            Assert.That(ending.Title, Is.EqualTo("GRID RESTORED"));
            Assert.That(ending.Body, Is.EqualTo("All primary city systems are stable."));
            Assert.That(ending.StatusTitle, Is.EqualTo("CITY STATUS: ONLINE"));
            Assert.That(ending.StatusBody, Is.EqualTo("Restoration complete."));
            Assert.That(ending.ReturnButtonLabel, Is.EqualTo("RETURN TO CITY"));
        }

        [Test]
        public void EndingRequirement_IsCampaignCompletionBasedAndIndependentOfStars()
        {
            CampaignProgressService progress = CompleteCampaign(LoadMain(), 1);

            Assert.That(progress.IsCampaignComplete, Is.True);
            Assert.That(progress.TotalStars, Is.EqualTo(50));
            Assert.That(progress.TotalStars, Is.LessThan(progress.MaximumCampaignStars));
            Assert.That(progress.IsEndingRequired, Is.True);

            progress.SetEndingCompleted(true);
            Assert.That(progress.IsEndingRequired, Is.False);
            CampaignLevelEntry final = progress.Campaign.Chapters.Last().Levels.Last();
            CampaignProgressUpdate replay = progress.RecordCompletion(final.LevelId,
                CampaignTestFixture.Result(final.LevelDefinition, 0, 0.5f, 3));
            Assert.That(replay.Accepted, Is.True);
            Assert.That(replay.ChapterJustRestored, Is.False);
            Assert.That(progress.EndingCompleted, Is.True);
        }

        [Test]
        public void FreshAndPartialCampaigns_DoNotRequireEnding()
        {
            CampaignDefinition campaign = LoadMain();
            var fresh = new CampaignProgressService(campaign);
            Assert.That(fresh.IntroCompleted, Is.False);
            Assert.That(fresh.IsEndingRequired, Is.False);

            CampaignLevelEntry first = campaign.Chapters[0].Levels[0];
            fresh.RecordCompletion(first.LevelId,
                CampaignTestFixture.Result(first.LevelDefinition, 2, 3f, 1));
            Assert.That(fresh.IsCampaignComplete, Is.False);
            Assert.That(fresh.IsEndingRequired, Is.False);
        }

        [Test]
        public void FinalRestoration_ConsumesEventThenHandsOffWithoutInteractiveMapOrFinalStatus()
        {
            CampaignDefinition campaign = LoadMain();
            CampaignProgressService progress = CompleteCampaign(campaign, 1);
            progress.QueuePendingRestoration(campaign.Chapters.Last().ChapterId);
            var store = new MemoryStore(progress);
            var flow = new CampaignFlowCoordinator(campaign, progress, store);
            GameObject root = CreateRoot("Final Restoration Ending Handoff");
            var view = root.AddComponent<CampaignRuntimeView>();
            view.Build(campaign, progress, _ => { }, _ => { }, () => { });
            int handoffs = 0;
            var sequence = root.AddComponent<CityRestorationSequenceController>();
            sequence.Initialize(view, flow, () =>
            {
                handoffs++;
                Assert.That(flow.PeekPendingRestoration(), Is.Null);
                Assert.That(progress.EndingCompleted, Is.False);
                return true;
            });

            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CurrentPlan.IncludesFinalNetworkPulse, Is.True);
            Assert.That(sequence.CurrentPlan.TotalDuration, Is.EqualTo(2.3f).Within(0.001f));
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            Assert.That(view.RestorationStatus.IsVisible, Is.False);
            sequence.ApplyPhase(CityRestorationSequencePhase.FinalNetworkPulse, 1f);
            Assert.That(sequence.CompletePreparedSequence(), Is.True);

            Assert.That(handoffs, Is.EqualTo(1));
            Assert.That(view.IsMapInteractionEnabled, Is.False);
            Assert.That(progress.IsEndingRequired, Is.True);
            Assert.That(store.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void ReturnToCity_PersistsEndingAndOpensFullyRestoredNavigableMap()
        {
            CampaignProgressService progress = CompleteCampaign(LoadMain(), 1);
            progress.SetIntroCompleted(true);
            var store = new MemoryStore(progress);
            CampaignRuntimeController controller = CreateController(progress.Campaign, store);

            Assert.That(controller.EndingView, Is.Not.Null);
            Assert.That(controller.EndingView.IsVisible, Is.True);
            Assert.That(controller.CampaignView.IsVisible, Is.False);
            controller.EndingView.PressReturnToCity();

            Assert.That(progress.EndingCompleted, Is.True);
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(controller.EndingView.IsVisible, Is.False);
            Assert.That(controller.CampaignView.IsVisible, Is.True);
            Assert.That(controller.CampaignView.IsMapInteractionEnabled, Is.True);
            Assert.That(progress.TotalStars, Is.EqualTo(50));
            Assert.That(controller.OpenChapter(progress.Campaign.Chapters[0].ChapterId), Is.True);
        }

        [Test]
        public void FailedReturnSave_RollsBackKeepsEndingVisibleAndAllowsOneRetry()
        {
            CampaignProgressService progress = CompleteCampaign(LoadMain(), 1);
            progress.SetIntroCompleted(true);
            var store = new MemoryStore(progress) { FailSaves = true };
            CampaignRuntimeController controller = CreateController(progress.Campaign, store);
            LogAssert.Expect(LogType.Warning, "Simulated ending save failure");

            controller.EndingView.PressReturnToCity();

            Assert.That(progress.EndingCompleted, Is.False);
            Assert.That(controller.EndingView.IsVisible, Is.True);
            Assert.That(controller.EndingView.IsInteractionEnabled, Is.True);
            Assert.That(controller.CampaignView.IsVisible, Is.False);
            store.FailSaves = false;
            controller.EndingView.PressReturnToCity();
            controller.EndingView.PressReturnToCity();
            Assert.That(store.SaveCount, Is.EqualTo(2));
            Assert.That(progress.EndingCompleted, Is.True);
            Assert.That(controller.CampaignView.IsVisible, Is.True);
        }

        [Test]
        public void InterruptedEnding_ReopensDirectlyWithoutRestorationReplay()
        {
            CampaignProgressService progress = CompleteCampaign(LoadMain(), 1);
            progress.SetIntroCompleted(true);
            Assert.That(progress.PendingRestorationCount, Is.Zero);
            var store = new MemoryStore(progress);

            CampaignRuntimeController first = CreateController(progress.Campaign, store);
            Assert.That(first.EndingView.IsVisible, Is.True);
            GameObject firstRoot = first.gameObject;
            UnityEngine.Object.DestroyImmediate(firstRoot);
            cleanup.Remove(firstRoot);
            CampaignRuntimeController reopened = CreateController(progress.Campaign, store);

            Assert.That(reopened.IntroView, Is.Null);
            Assert.That(reopened.EndingView.IsVisible, Is.True);
            Assert.That(reopened.CampaignView.IsVisible, Is.False);
            Assert.That(progress.PendingRestorationCount, Is.Zero);
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void CompletedEnding_ReopenAndFinalReplayGoDirectlyToMap()
        {
            CampaignProgressService progress = CompleteCampaign(LoadMain(), 2);
            progress.SetIntroCompleted(true);
            progress.SetEndingCompleted(true);
            CampaignLevelEntry final = progress.Campaign.Chapters.Last().Levels.Last();
            Assert.That(progress.RecordCompletion(final.LevelId,
                CampaignTestFixture.Result(final.LevelDefinition, 1, 1f, 3)).Accepted, Is.True);

            CampaignRuntimeController controller = CreateController(progress.Campaign,
                new MemoryStore(progress));

            Assert.That(controller.EndingView, Is.Null);
            Assert.That(controller.CampaignView.IsVisible, Is.True);
            Assert.That(progress.EndingCompleted, Is.True);
        }

        [Test]
        public void StartupPrecedence_IntroComesBeforeEndingState()
        {
            CampaignProgressService progress = CompleteCampaign(LoadMain(), 1);
            CampaignRuntimeController controller = CreateController(progress.Campaign,
                new MemoryStore(progress));

            Assert.That(progress.IntroCompleted, Is.False);
            Assert.That(progress.IsEndingRequired, Is.True);
            Assert.That(controller.IntroView.IsVisible, Is.True);
            Assert.That(controller.EndingView, Is.Null);
            Assert.That(controller.CampaignView.IsVisible, Is.False);
        }

        [Test]
        public void SaveSchema_LegacyAndExplicitEndingRulesAreBackwardCompatible()
        {
            CampaignDefinition campaign = LoadMain();
            string directory = Path.Combine(Path.GetTempPath(), "NeonGridM14D",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, CampaignSaveStore.SaveFileName);
            try
            {
                CampaignProgressService completed = CompleteCampaign(campaign, 1);
                WriteSave(path, campaign.CampaignId, completed.ExportProgress(), false, false);
                Assert.That(new CampaignSaveStore(path).Load(campaign).Progress.EndingCompleted,
                    Is.True, "legacy completed campaign");

                var partial = new CampaignProgressService(campaign);
                CampaignLevelEntry first = campaign.Chapters[0].Levels[0];
                partial.RecordCompletion(first.LevelId,
                    CampaignTestFixture.Result(first.LevelDefinition, 1, 1f, 1));
                WriteSave(path, campaign.CampaignId, partial.ExportProgress(), false, false);
                CampaignProgressService legacyPartial = new CampaignSaveStore(path).Load(campaign)
                    .Progress;
                Assert.That(legacyPartial.EndingCompleted, Is.False);
                Assert.That(legacyPartial.IsEndingRequired, Is.False);

                WriteSave(path, campaign.CampaignId, completed.ExportProgress(), true, false);
                CampaignProgressService explicitPending = new CampaignSaveStore(path).Load(campaign)
                    .Progress;
                Assert.That(explicitPending.EndingCompleted, Is.False);
                Assert.That(explicitPending.IsEndingRequired, Is.True);

                WriteSave(path, campaign.CampaignId, completed.ExportProgress(), true, true);
                Assert.That(new CampaignSaveStore(path).Load(campaign).Progress.EndingCompleted,
                    Is.True);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void EndingCompletion_DoesNotCreateOrConsumeRestorationEvents()
        {
            CampaignDefinition campaign = LoadMain();
            CampaignProgressService progress = CompleteCampaign(campaign, 1);
            progress.QueuePendingRestoration(campaign.Chapters.Last().ChapterId);
            ChapterRestorationEvent pending = progress.PendingRestoration;
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore(progress));

            Assert.That(flow.TryCompleteEnding(), Is.True);

            Assert.That(progress.PendingRestorationCount, Is.EqualTo(1));
            Assert.That(progress.PendingRestoration, Is.SameAs(pending));
            Assert.That(progress.EndingCompleted, Is.True);
        }

        [TestCase("PowerStation_VerticalSlice")]
        [TestCase("Substation_VerticalSlice")]
        [TestCase("ControlCenter_VerticalSlice")]
        [TestCase("AutomationPlant_VerticalSlice")]
        [TestCase("CentralGrid_VerticalSlice")]
        public void VerticalSlices_HaveNoEndingAssociationOrView(string resourceName)
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                $"Campaigns/{resourceName}");
            Assert.That(CampaignNarrativeCatalog.LoadForCampaign(campaign.CampaignId), Is.Null);
            CampaignRuntimeController controller = CreateController(campaign,
                new MemoryStore(new CampaignProgressService(campaign)));
            Assert.That(controller.EndingView, Is.Null);
            Assert.That(controller.CampaignView.IsVisible, Is.True);
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2340)]
        [TestCase(720, 1280)]
        public void EndingLayout_IsReadableOnScreenWithoutOverlapOrScroll(int width, int height)
        {
            GameObject root = CreateRoot("Ending Layout");
            var view = root.AddComponent<CampaignEndingView>();
            view.Build(LoadNarrative().EndingNarrative, () => true);
            CanvasScaler scaler = root.GetComponentInChildren<CanvasScaler>(true);
            float scale = Mathf.Sqrt((width / CampaignEndingView.ReferenceWidth) *
                                     (height / CampaignEndingView.ReferenceHeight));
            float virtualWidth = width / scale;
            float virtualHeight = height / scale;

            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1080f, 1920f)));
            Assert.That(root.GetComponentInChildren<ScrollRect>(true), Is.Null);
            RectTransform[] elements =
            {
                view.TitleText.rectTransform, view.BodyText.rectTransform,
                view.StatusTitleText.rectTransform, view.StatusBodyText.rectTransform,
                view.PrimaryButton.GetComponent<RectTransform>()
            };
            foreach (RectTransform element in elements)
                AssertWithin(element, virtualWidth, virtualHeight);
            Assert.That(Bottom(view.TitleText.rectTransform, virtualHeight),
                Is.GreaterThan(Top(view.BodyText.rectTransform, virtualHeight)));
            Assert.That(Bottom(view.BodyText.rectTransform, virtualHeight),
                Is.GreaterThan(Top(view.StatusTitleText.rectTransform, virtualHeight)));
            Assert.That(Bottom(view.StatusTitleText.rectTransform, virtualHeight),
                Is.GreaterThan(Top(view.StatusBodyText.rectTransform, virtualHeight)));
            Assert.That(view.TitleText.fontSize, Is.EqualTo(72));
            Assert.That(view.BodyText.fontSize, Is.EqualTo(42));
            Assert.That(view.StatusTitleText.fontSize, Is.EqualTo(46));
            Assert.That(view.PrimaryButton.GetComponent<RectTransform>().sizeDelta.y,
                Is.GreaterThanOrEqualTo(120f));
        }

        private CampaignRuntimeController CreateController(CampaignDefinition campaign,
            MemoryStore store)
        {
            GameObject root = CreateRoot($"M14-D Controller - {campaign.CampaignId}");
            root.SetActive(false);
            CampaignRuntimeController controller = root.AddComponent<CampaignRuntimeController>();
            controller.Initialize(campaign, store);
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (camera.transform.parent == null && !cleanup.Contains(camera.gameObject))
                    cleanup.Add(camera.gameObject);
            return controller;
        }

        private GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            cleanup.Add(root);
            return root;
        }

        private static CampaignProgressService CompleteCampaign(CampaignDefinition campaign,
            int stars)
        {
            var progress = new CampaignProgressService(campaign);
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
            foreach (CampaignLevelEntry level in chapter.Levels)
                Assert.That(progress.RecordCompletion(level.LevelId,
                    CampaignTestFixture.Result(level.LevelDefinition, 4, 12f, stars)).Accepted,
                    Is.True, level.LevelId);
            return progress;
        }

        private static CampaignDefinition LoadMain()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            Assert.That(campaign, Is.Not.Null);
            return campaign;
        }

        private static CampaignNarrativeDefinition LoadNarrative()
        {
            CampaignNarrativeDefinition narrative = Resources.Load<CampaignNarrativeDefinition>(
                "Narratives/NeonGrid_Main_Narrative");
            Assert.That(narrative, Is.Not.Null);
            return narrative;
        }

        private static void WriteSave(string path, string campaignId,
            IReadOnlyList<LevelProgressSaveEntry> entries, bool hasEndingState,
            bool endingCompleted)
        {
            var data = new CampaignSaveData
            {
                version = CampaignSaveStore.CurrentVersion,
                campaignId = campaignId,
                hasIntroCompletionState = true,
                introCompleted = true,
                hasEndingCompletionState = hasEndingState,
                endingCompleted = endingCompleted,
                levelProgressEntries = new List<LevelProgressSaveEntry>(entries)
            };
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
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

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public CampaignProgressService Progress { get; }
            public bool FailSaves { get; set; }
            public int SaveCount { get; private set; }
            public string SavePath => "memory://m14-d";

            public MemoryStore(CampaignProgressService progress)
            {
                Progress = progress;
            }

            public CampaignLoadResult Load(CampaignDefinition campaign) =>
                new CampaignLoadResult(CampaignLoadStatus.Loaded, Progress,
                    Array.Empty<string>());

            public CampaignSaveResult Save(CampaignProgressService progress)
            {
                SaveCount++;
                return FailSaves
                    ? new CampaignSaveResult(CampaignSaveStatus.Failed,
                        "Simulated ending save failure")
                    : new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }

            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }
    }
}

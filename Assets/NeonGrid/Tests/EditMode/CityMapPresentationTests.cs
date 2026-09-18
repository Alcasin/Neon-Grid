using System.Collections.Generic;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class CityMapPresentationTests
    {
        private static readonly string[] ExpectedChapterIds =
        {
            "power_station", "substation", "control_center", "automation_plant", "central_grid"
        };

        [Test]
        public void ProductionLayout_HasFiveOrderedUniqueChapterEntries()
        {
            CityMapLayoutDefinition layout = CityMapLayoutCatalog.Production;
            Assert.That(layout.Entries.Count, Is.EqualTo(5));
            Assert.That(layout.Entries.Select(entry => entry.ChapterId),
                Is.EqualTo(ExpectedChapterIds));
            Assert.That(layout.Entries.Select(entry => entry.ChapterId).Distinct().Count(),
                Is.EqualTo(5));
            Assert.That(layout.Matches(LoadMain()), Is.True);
            Assert.That(layout.Entries[4].Scale, Is.EqualTo(1.25f));
            Assert.That(layout.Entries[4].VisualSize.x,
                Is.GreaterThan(layout.Entries.Take(4).Max(entry => entry.VisualSize.x)));
        }

        [TestCase(0, ChapterMapVisualState.ProgressStage1)]
        [TestCase(1, ChapterMapVisualState.ProgressStage1)]
        [TestCase(3, ChapterMapVisualState.ProgressStage1)]
        [TestCase(4, ChapterMapVisualState.ProgressStage2)]
        [TestCase(6, ChapterMapVisualState.ProgressStage2)]
        [TestCase(7, ChapterMapVisualState.ProgressStage3)]
        [TestCase(9, ChapterMapVisualState.ProgressStage3)]
        [TestCase(10, ChapterMapVisualState.Restored)]
        public void ProgressBands_AreDerivedGenerically(int completed,
            ChapterMapVisualState expected)
        {
            CampaignChapterState chapterState = completed == 10
                ? CampaignChapterState.Restored
                : CampaignChapterState.Available;
            Assert.That(CityMapPresentationModel.GetChapterVisualState(
                chapterState, completed, 10), Is.EqualTo(expected));
            Assert.That(CityMapPresentationModel.GetChapterVisualState(
                CampaignChapterState.Locked, completed, 10),
                Is.EqualTo(ChapterMapVisualState.Locked));
        }

        [Test]
        public void FreshProductionMap_ShowsOneAvailableAndFourLockedNodes()
        {
            CampaignDefinition campaign = LoadMain();
            var progress = new CampaignProgressService(campaign);
            using (ViewScope scope = BuildView(campaign, progress, out GameObject root))
            {
                CampaignRuntimeView view = scope.View;
                view.ShowMap();
                CityChapterNodeView[] nodes = Nodes(root);
                Assert.That(view.UsesCityMap, Is.True);
                Assert.That(nodes, Has.Length.EqualTo(5));
                Assert.That(nodes.Select(node => node.ChapterId), Is.EqualTo(ExpectedChapterIds));
                Assert.That(nodes[0].VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage1));
                Assert.That(nodes[0].Button.interactable, Is.True);
                Assert.That(nodes[0].IsPulsing, Is.True);
                Assert.That(nodes.Skip(1).All(node =>
                    node.VisualState == ChapterMapVisualState.Locked && !node.Button.interactable),
                    Is.True);
                Assert.That(nodes.Skip(1).All(node => node.Label.text.Contains("LOCKED")), Is.True);
                Assert.That(FindMap(root).Find("Total Stars").GetComponent<Text>().text,
                    Is.EqualTo("★ 0 / 150"));
            }
        }

        [Test]
        public void AvailableNode_NavigatesWithLargeHitArea_WhileLockedNodeCannotNavigate()
        {
            CampaignDefinition campaign = LoadMain();
            var progress = new CampaignProgressService(campaign);
            string opened = null;
            var root = new GameObject("City Navigation Test");
            try
            {
                var view = root.AddComponent<CampaignRuntimeView>();
                view.Build(campaign, progress, id => opened = id, _ => { }, () => { });
                view.ShowMap();
                CityChapterNodeView[] nodes = Nodes(root);
                nodes[0].Button.onClick.Invoke();
                Assert.That(opened, Is.EqualTo("power_station"));
                Assert.That(nodes[0].HitArea.sizeDelta.x,
                    Is.GreaterThan(CityMapLayoutCatalog.Production.Entries[0].VisualSize.x));
                Assert.That(nodes[1].Button.interactable, Is.False);
                Assert.That(progress.IsChapterUnlocked(nodes[1].ChapterId), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PartialAndRestoredNodes_ShowProgressStarsAndRemainInteractable()
        {
            CampaignDefinition campaign = LoadMain();
            var progress = new CampaignProgressService(campaign);
            CompleteLevels(progress, campaign.Chapters[0], 6, 2);
            using (ViewScope scope = BuildView(campaign, progress, out GameObject root))
            {
                CampaignRuntimeView view = scope.View;
                view.ShowMap();
                CityChapterNodeView power = Nodes(root)[0];
                Assert.That(power.VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage2));
                Assert.That(power.Label.text, Does.Contain("6 / 10"));
                Assert.That(power.Label.text, Does.Contain("★ 12 / 30"));
                Assert.That(FindMap(root).Find("Total Stars").GetComponent<Text>().text,
                    Is.EqualTo("★ 12 / 150"));
            }

            CompleteLevels(progress, campaign.Chapters[0], 10, 2);
            using (ViewScope scope = BuildView(campaign, progress, out GameObject root))
            {
                CampaignRuntimeView view = scope.View;
                view.ShowMap();
                CityChapterNodeView power = Nodes(root)[0];
                Assert.That(power.VisualState, Is.EqualTo(ChapterMapVisualState.Restored));
                Assert.That(power.Button.interactable, Is.True);
                Assert.That(power.IsPulsing, Is.False);
                Assert.That(power.Label.text, Does.Contain("RESTORED"));
                Assert.That(power.Label.text, Does.Contain("★ 20 / 30"));
            }
        }

        [Test]
        public void EnergyPaths_DeriveLockedFrontierAndRestoredStatesFromChapterProgress()
        {
            CampaignDefinition campaign = LoadMain();
            var progress = new CampaignProgressService(campaign);
            using (ViewScope scope = BuildView(campaign, progress, out GameObject root))
            {
                CampaignRuntimeView view = scope.View;
                view.ShowMap();
                Assert.That(Paths(root).All(path => path.State == CityEnergyPathState.Locked), Is.True);
            }

            CompleteLevels(progress, campaign.Chapters[0], 10, 3);
            using (ViewScope scope = BuildView(campaign, progress, out GameObject root))
            {
                CampaignRuntimeView view = scope.View;
                view.ShowMap();
                CityEnergyPathView[] paths = Paths(root);
                Assert.That(paths[0].State, Is.EqualTo(CityEnergyPathState.Frontier));
                Assert.That(paths.Skip(1).All(path => path.State == CityEnergyPathState.Locked), Is.True);
            }

            for (int chapterIndex = 1; chapterIndex < campaign.Chapters.Count; chapterIndex++)
                CompleteLevels(progress, campaign.Chapters[chapterIndex], 10, 3);
            using (ViewScope scope = BuildView(campaign, progress, out GameObject root))
            {
                CampaignRuntimeView view = scope.View;
                view.ShowMap();
                Assert.That(Paths(root).All(path => path.State == CityEnergyPathState.Restored), Is.True);
                Assert.That(progress.IsCampaignComplete, Is.True);
            }
        }

        [Test]
        public void ProductionMapAndSelector_PreserveBidirectionalNavigationCallbacks()
        {
            CampaignDefinition campaign = LoadMain();
            var progress = new CampaignProgressService(campaign);
            int backCount = 0;
            var root = new GameObject("City Selector Navigation Test");
            try
            {
                var view = root.AddComponent<CampaignRuntimeView>();
                view.Build(campaign, progress, _ => { }, _ => { }, () => backCount++);
                view.ShowChapter(campaign.Chapters[0]);
                Transform selection = root.transform.Find("Campaign Canvas/Level Selection");
                Assert.That(selection.gameObject.activeSelf, Is.True);
                selection.Find("Back To Map").GetComponent<Button>().onClick.Invoke();
                Assert.That(backCount, Is.EqualTo(1));
                view.ShowMap();
                Assert.That(FindMap(root).gameObject.activeSelf, Is.True);
                Assert.That(FindMap(root).GetComponentInChildren<ScrollRect>(true), Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(720f, 1280f)]
        public void ProductionLayout_HitAreasStayInsidePortraitBoundsWithoutOverlap(
            float width, float height)
        {
            CityMapLayoutDefinition layout = CityMapLayoutCatalog.Production;
            var bounds = new List<Rect>();
            foreach (CityMapLayoutEntry entry in layout.Entries)
            {
                Rect rect = layout.CalculateHitRect(entry, width, height);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0f), entry.ChapterId);
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0f), entry.ChapterId);
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(width), entry.ChapterId);
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(height), entry.ChapterId);
                foreach (Rect previous in bounds)
                    Assert.That(rect.Overlaps(previous), Is.False, entry.ChapterId);
                bounds.Add(rect);
            }
        }

        [TestCase("PowerStation_VerticalSlice")]
        [TestCase("Substation_VerticalSlice")]
        [TestCase("ControlCenter_VerticalSlice")]
        [TestCase("AutomationPlant_VerticalSlice")]
        [TestCase("CentralGrid_VerticalSlice")]
        public void VerticalSliceCampaigns_UseValidFallbackMap(string resourceName)
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                $"Campaigns/{resourceName}");
            var progress = new CampaignProgressService(campaign);
            using (ViewScope scope = BuildView(campaign, progress, out GameObject root))
            {
                CampaignRuntimeView view = scope.View;
                view.ShowMap();
                Assert.That(view.UsesCityMap, Is.False);
                Transform chapter = FindMap(root).Find("Chapter 1");
                Assert.That(chapter, Is.Not.Null);
                Assert.That(chapter.GetComponent<Button>().interactable, Is.True);
            }
        }

        private static CampaignDefinition LoadMain() =>
            Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");

        private static ViewScope BuildView(CampaignDefinition campaign,
            CampaignProgressService progress, out GameObject root)
        {
            root = new GameObject("City Map Presentation Test");
            var view = root.AddComponent<CampaignRuntimeView>();
            view.Build(campaign, progress, _ => { }, _ => { }, () => { });
            return new ViewScope(root, view);
        }

        private static void CompleteLevels(CampaignProgressService progress,
            CampaignChapterDefinition chapter, int targetCount, int stars)
        {
            int completed = progress.GetCompletedLevelCount(chapter.ChapterId);
            for (int index = completed; index < targetCount; index++)
            {
                CampaignLevelEntry entry = chapter.Levels[index];
                Assert.That(progress.RecordCompletion(entry.LevelId,
                    CampaignTestFixture.Result(entry.LevelDefinition, 1, 1f, stars)).Accepted,
                    Is.True, entry.LevelId);
            }
        }

        private static Transform FindMap(GameObject root) =>
            root.transform.Find("Campaign Canvas/Campaign Map");

        private static CityChapterNodeView[] Nodes(GameObject root) =>
            FindMap(root).GetComponentsInChildren<CityChapterNodeView>(true)
                .OrderBy(node => node.transform.GetSiblingIndex()).ToArray();

        private static CityEnergyPathView[] Paths(GameObject root) =>
            FindMap(root).GetComponentsInChildren<CityEnergyPathView>(true)
                .OrderBy(path => path.FromIndex).ToArray();

        private readonly struct ViewScope : System.IDisposable
        {
            private readonly GameObject root;
            public CampaignRuntimeView View { get; }
            public ViewScope(GameObject root, CampaignRuntimeView view)
            {
                this.root = root;
                View = view;
            }
            public void Dispose() { if (root != null) Object.DestroyImmediate(root); }
        }
    }
}

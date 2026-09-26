using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Editor;
using NeonGrid.Presentation;
using NeonGrid.Session;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class ProductionCityBuildingArtRolloutTests
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/NeonGrid_Main.asset";
        private const string PowerArtPath =
            "Assets/NeonGrid/Art/CityBuildings/PowerStation/PowerStation_Final.asset";
        private const string CentralArtPath =
            "Assets/NeonGrid/Art/CityBuildings/CentralGrid/CentralGrid_Final.asset";
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CampaignDefinition campaign;
        private CityBuildingArtDefinition powerArt;
        private CityBuildingArtDefinition centralArt;

        [SetUp]
        public void SetUp()
        {
            campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            powerArt = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(PowerArtPath);
            centralArt = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(CentralArtPath);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void ProductionCampaign_MapsExactlyTwoFinalDefinitionsByStableChapterId()
        {
            Assert.That(campaign.CityBuildingArt, Has.Count.EqualTo(2));
            Assert.That(campaign.CityBuildingArt.Select(binding => binding.ChapterId),
                Is.EqualTo(new[] { "power_station", "central_grid" }));
            Assert.That(campaign.GetCityBuildingArt("power_station"), Is.SameAs(powerArt));
            Assert.That(campaign.GetCityBuildingArt("central_grid"), Is.SameAs(centralArt));
            Assert.That(campaign.GetCityBuildingArt("Power Station"), Is.Null);
            Assert.That(campaign.GetCityBuildingArt("substation"), Is.Null);
            Assert.That(campaign.GetCityBuildingArt("control_center"), Is.Null);
            Assert.That(campaign.GetCityBuildingArt("automation_plant"), Is.Null);
            Assert.That(new CampaignValidator().Validate(campaign).IsValid, Is.True);
        }

        [Test]
        public void MainCampaignBuilder_IsIdempotentAndPreservesFinalArtAssociations()
        {
            MainCampaignBuilder.Build();
            byte[] first = File.ReadAllBytes(CampaignPath);
            MainCampaignBuilder.Build();
            byte[] second = File.ReadAllBytes(CampaignPath);
            Assert.That(second, Is.EqualTo(first));
            CampaignDefinition rebuilt = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(
                CampaignPath);
            Assert.That(rebuilt.GetCityBuildingArt("power_station"), Is.SameAs(powerArt));
            Assert.That(rebuilt.GetCityBuildingArt("central_grid"), Is.SameAs(centralArt));
            Assert.That(rebuilt.CityBuildingArt, Has.Count.EqualTo(2));
        }

        [Test]
        public void FreshProductionMap_UsesFinalPowerStageOneAndLockedFinalCentral()
        {
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            CityChapterNodeView power = Node("power_station");
            CityChapterNodeView central = Node("central_grid");
            Assert.That(power.VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage1));
            Assert.That(power.BuildingArtView.VisualState,
                Is.EqualTo(ChapterMapVisualState.ProgressStage1));
            Assert.That(central.VisualState, Is.EqualTo(ChapterMapVisualState.Locked));
            Assert.That(central.BuildingArtView.VisualState,
                Is.EqualTo(ChapterMapVisualState.Locked));
        }

        [TestCase("power_station", 0, ChapterMapVisualState.ProgressStage1)]
        [TestCase("power_station", 1, ChapterMapVisualState.ProgressStage1)]
        [TestCase("power_station", 3, ChapterMapVisualState.ProgressStage1)]
        [TestCase("power_station", 4, ChapterMapVisualState.ProgressStage2)]
        [TestCase("power_station", 6, ChapterMapVisualState.ProgressStage2)]
        [TestCase("power_station", 7, ChapterMapVisualState.ProgressStage3)]
        [TestCase("power_station", 9, ChapterMapVisualState.ProgressStage3)]
        [TestCase("power_station", 10, ChapterMapVisualState.Restored)]
        [TestCase("central_grid", 0, ChapterMapVisualState.ProgressStage1)]
        [TestCase("central_grid", 3, ChapterMapVisualState.ProgressStage1)]
        [TestCase("central_grid", 4, ChapterMapVisualState.ProgressStage2)]
        [TestCase("central_grid", 6, ChapterMapVisualState.ProgressStage2)]
        [TestCase("central_grid", 7, ChapterMapVisualState.ProgressStage3)]
        [TestCase("central_grid", 9, ChapterMapVisualState.ProgressStage3)]
        [TestCase("central_grid", 10, ChapterMapVisualState.Restored)]
        public void FinalArt_UsesSharedFiveStateProgressResolver(string chapterId, int completed,
            ChapterMapVisualState expected)
        {
            CampaignProgressService progress = ProgressAt(chapterId, completed, 2);
            CampaignRuntimeView view = BuildView(progress);
            view.ShowMap();
            CityChapterNodeView node = Node(chapterId);
            Assert.That(node.VisualState, Is.EqualTo(expected));
            Assert.That(node.BuildingArtView.VisualState, Is.EqualTo(expected));
            Assert.That(node.BuildingArtView.Definition,
                Is.SameAs(chapterId == "power_station" ? powerArt : centralArt));
        }

        [Test]
        public void StarsDoNotDetermineFinalBuildingState()
        {
            CampaignProgressService oneStar = ProgressAt("power_station", 4, 1);
            CampaignProgressService threeStars = ProgressAt("power_station", 4, 3);
            CampaignRuntimeView first = BuildView(oneStar);
            first.ShowMap();
            ChapterMapVisualState firstState = Node("power_station").BuildingArtView.VisualState;
            DestroyRoots();
            CampaignRuntimeView second = BuildView(threeStars);
            second.ShowMap();
            Assert.That(firstState, Is.EqualTo(ChapterMapVisualState.ProgressStage2));
            Assert.That(Node("power_station").BuildingArtView.VisualState,
                Is.EqualTo(firstState));
        }

        [Test]
        public void ProductionMap_IsIntentionallyMixedFinalAndFallbackWithoutPrototypeControls()
        {
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            CityChapterNodeView[] nodes = Nodes();
            Assert.That(nodes.Single(node => node.ChapterId == "power_station")
                .BuildingArtView.UsesArt, Is.True);
            Assert.That(nodes.Single(node => node.ChapterId == "central_grid")
                .BuildingArtView.UsesArt, Is.True);
            foreach (string id in new[] { "substation", "control_center", "automation_plant" })
            {
                CityChapterNodeView node = nodes.Single(candidate => candidate.ChapterId == id);
                Assert.That(node.BuildingArtView, Is.Null);
                RectTransform silhouette = node.transform.Find("Building Silhouette") as RectTransform;
                Assert.That(silhouette.GetComponentsInChildren<Image>(true)
                    .Count(image => image.enabled), Is.GreaterThan(0));
            }
            Assert.That(view.GetComponentsInChildren<CityBuildingArtPrototypeController>(true),
                Is.Empty);
        }

        [Test]
        public void InvalidOptionalDefinition_FallsBackToVisibleProgrammerSilhouette()
        {
            CampaignDefinition invalidCampaign = UnityEngine.Object.Instantiate(campaign);
            cleanup.Add(invalidCampaign);
            CityBuildingArtDefinition invalid = ScriptableObject.CreateInstance<
                CityBuildingArtDefinition>();
            cleanup.Add(invalid);
            invalid.SetData("power_station", null, null, null, null,
                new Vector2(0.9f, 0.7f));
            invalidCampaign.SetCityBuildingArt(new[]
            {
                new CampaignCityBuildingArtBinding("power_station", invalid),
                new CampaignCityBuildingArtBinding("central_grid", centralArt)
            });
            campaign = invalidCampaign;
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            CityChapterNodeView power = Node("power_station");
            Assert.That(power.BuildingArtView, Is.Not.Null);
            Assert.That(power.BuildingArtView.UsesArt, Is.False);
            RectTransform silhouette = power.transform.Find("Building Silhouette") as RectTransform;
            Assert.That(silhouette.Find("Authored Building Art"), Is.Null);
            Assert.That(silhouette.GetComponentsInChildren<Image>(true)
                .Count(image => image.enabled), Is.GreaterThan(0));
        }

        [Test]
        public void AssociationValidationRejectsDuplicateUnknownNullAndMismatchedEntries()
        {
            CampaignDefinition invalid = UnityEngine.Object.Instantiate(campaign);
            cleanup.Add(invalid);
            invalid.SetCityBuildingArt(new CampaignCityBuildingArtBinding[]
            {
                new CampaignCityBuildingArtBinding("power_station", powerArt),
                new CampaignCityBuildingArtBinding("power_station", powerArt),
                new CampaignCityBuildingArtBinding("unknown", centralArt),
                new CampaignCityBuildingArtBinding("central_grid", null),
                null
            });
            CampaignValidationReport report = new CampaignValidator().Validate(invalid);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(
                CampaignValidationCode.DuplicateCityBuildingArtChapterId));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(
                CampaignValidationCode.UnknownCityBuildingArtChapterId));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(
                CampaignValidationCode.MissingCityBuildingArtDefinition));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(
                CampaignValidationCode.NullCityBuildingArtBinding));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(
                CampaignValidationCode.MismatchedCityBuildingArtDefinition));
        }

        [Test]
        public void PowerStationRestoration_UpdatesFinalArtAndSettlesWithoutDrift()
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign,
                NarrativeQaPreset.FirstRestorationPending);
            CampaignRuntimeView view = BuildView(progress);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore(progress));
            CityRestorationSequenceController sequence = Root().AddComponent<
                CityRestorationSequenceController>();
            sequence.Initialize(view, flow);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            CityChapterNodeView power = Node("power_station");
            Vector3 baseScale = power.BaseVisualScale;
            Transform[] layers = power.BuildingArtView.ArtRoot.Cast<Transform>().ToArray();
            sequence.ApplyPhase(CityRestorationSequencePhase.Focus, 0.5f);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            Assert.That(power.BuildingArtView.VisualState,
                Is.EqualTo(ChapterMapVisualState.Restored));
            Assert.That(sequence.CompletePreparedSequence(), Is.True);
            Assert.That(power.VisualScale, Is.EqualTo(baseScale));
            Assert.That(layers.All(layer => layer.localPosition == Vector3.zero), Is.True);
            Assert.That(Node("substation").VisualState,
                Is.EqualTo(ChapterMapVisualState.ProgressStage1));
        }

        [Test]
        public void CentralGridFinalRestoration_HasNoStatusAndHandsOffToEnding()
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign,
                NarrativeQaPreset.FinalRestorationPending);
            CampaignRuntimeView view = BuildView(progress);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore(progress));
            bool endingRequested = false;
            CityRestorationSequenceController sequence = Root().AddComponent<
                CityRestorationSequenceController>();
            sequence.Initialize(view, flow, () => endingRequested = true);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CurrentPlan.RestoredChapterId, Is.EqualTo("central_grid"));
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            Assert.That(Node("central_grid").BuildingArtView.VisualState,
                Is.EqualTo(ChapterMapVisualState.Restored));
            Assert.That(view.ShowRestorationStatus("central_grid"), Is.False);
            Assert.That(sequence.CompletePreparedSequence(), Is.True);
            Assert.That(endingRequested, Is.True);
        }

        [Test]
        public void SaveLoadAndReplay_DeriveRestoredArtWithoutNewSchemaState()
        {
            CampaignProgressService progress = ProgressAt("power_station", 10, 3);
            string path = Path.Combine(Path.GetTempPath(), "NeonGridE1C",
                Guid.NewGuid().ToString("N"), "progress.json");
            try
            {
                var store = new CampaignSaveStore(path);
                Assert.That(store.Save(progress).Succeeded, Is.True);
                CampaignLoadResult load = store.Load(campaign);
                CampaignLevelEntry replay = campaign.Chapters[0].Levels[0];
                CampaignProgressUpdate update = load.Progress.RecordCompletion(replay.LevelId,
                    Result(replay, 1));
                Assert.That(update.ChapterJustRestored, Is.False);
                CampaignRuntimeView view = BuildView(load.Progress);
                view.ShowMap();
                Assert.That(Node("power_station").BuildingArtView.VisualState,
                    Is.EqualTo(ChapterMapVisualState.Restored));
                string[] saveFields = typeof(CampaignSaveData).GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(field => field.Name).ToArray();
                Assert.That(saveFields, Has.None.Contains("art"));
                Assert.That(saveFields, Has.None.Contains("sprite"));
                Assert.That(saveFields, Has.None.Contains("opacity"));
            }
            finally
            {
                string directory = Path.GetDirectoryName(path);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void TenMapSelectorCycles_DoNotGrowFinalArtHierarchy()
        {
            CampaignProgressService progress = ProgressAt("power_station", 4, 2);
            CampaignRuntimeView view = BuildView(progress);
            view.ShowMap();
            CityBuildingArtView[] art = Root().GetComponentsInChildren<CityBuildingArtView>(true);
            int[] roots = art.Select(item => item.ArtRoot.GetInstanceID()).ToArray();
            for (int cycle = 0; cycle < 10; cycle++)
            {
                view.ShowChapter(campaign.Chapters[0]);
                view.ShowMap();
                Assert.That(Root().GetComponentsInChildren<CityBuildingArtView>(true),
                    Has.Length.EqualTo(2));
                Assert.That(Root().GetComponentsInChildren<CityBuildingArtView>(true)
                    .Select(item => item.ArtRoot.GetInstanceID()), Is.EqualTo(roots));
                Assert.That(art.All(item => item.ArtRoot.childCount == 4), Is.True);
            }
        }

        [Test]
        public void NodeGeometryHitTargetsAndThemedCentralSafeAreaRemainAuthoritative()
        {
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            CityChapterNodeView power = Node("power_station");
            CityChapterNodeView central = Node("central_grid");
            Assert.That(power.HitArea.anchoredPosition, Is.EqualTo(new Vector2(-310f, -510f)));
            Assert.That(power.HitArea.sizeDelta, Is.EqualTo(new Vector2(280f, 280f)));
            Assert.That(central.HitArea.anchoredPosition, Is.EqualTo(new Vector2(40f, -300f)));
            Assert.That(central.HitArea.sizeDelta, Is.EqualTo(new Vector2(350f, 330f)));
            RectTransform centralVisual = central.transform.Find("Building Silhouette")
                as RectTransform;
            Assert.That(centralVisual.localScale.x, Is.EqualTo(1.075f).Within(0.0001f));
            Assert.That(centralVisual.anchoredPosition.y, Is.EqualTo(76f));
            Bounds art = BoundsIn(central.BuildingArtView.ArtRoot, central.transform);
            Bounds label = BoundsIn(central.Label.rectTransform, central.transform);
            Assert.That(art.min.y, Is.GreaterThan(label.max.y + 6f));
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(720f, 1280f)]
        public void MixedProductionMap_RemainsInsidePortraitBounds(float width, float height)
        {
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            foreach (CityMapLayoutEntry entry in CityMapLayoutCatalog.Production.Entries)
            {
                Rect rect = CityMapLayoutCatalog.Production.CalculateHitRect(entry, width, height);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(width));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(height));
            }
            Assert.That(Node("power_station").BuildingArtView.ArtRoot.childCount, Is.EqualTo(4));
            Assert.That(Node("central_grid").BuildingArtView.ArtRoot.childCount, Is.EqualTo(4));
        }

        [Test]
        public void VerticalSliceCampaignsRemainFallbackOnlyAndPrototypeSceneStaysExcluded()
        {
            foreach (string name in new[] { "PowerStation_VerticalSlice",
                         "Substation_VerticalSlice", "ControlCenter_VerticalSlice",
                         "AutomationPlant_VerticalSlice", "CentralGrid_VerticalSlice" })
            {
                CampaignDefinition source = Resources.Load<CampaignDefinition>("Campaigns/" + name);
                Assert.That(source.CityBuildingArt, Is.Empty);
                Assert.That(source.GetCityBuildingArt(source.Chapters[0].ChapterId), Is.Null);
            }
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Has.None.EqualTo(
                M15CityBuildingArtPrototypeBuilder.ScenePath));
        }

        [TestCase(NarrativeQaPreset.PowerStationStage1, "power_station", 1)]
        [TestCase(NarrativeQaPreset.PowerStationStage2, "power_station", 4)]
        [TestCase(NarrativeQaPreset.PowerStationStage3, "power_station", 7)]
        [TestCase(NarrativeQaPreset.PowerStationRestored, "power_station", 10)]
        [TestCase(NarrativeQaPreset.CentralGridStage1, "central_grid", 1)]
        [TestCase(NarrativeQaPreset.CentralGridStage2, "central_grid", 4)]
        [TestCase(NarrativeQaPreset.CentralGridStage3, "central_grid", 7)]
        public void NarrativeQaProvidesDeterministicProductionArtPresets(
            NarrativeQaPreset preset, string chapterId, int expectedCompleted)
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign, preset);
            Assert.That(progress.GetCompletedLevelCount(chapterId), Is.EqualTo(expectedCompleted));
            Assert.That(progress.IntroCompleted, Is.True);
        }

        private CampaignRuntimeView BuildView(CampaignProgressService progress)
        {
            GameObject root = new GameObject("E1C Production Map");
            cleanup.Add(root);
            CampaignRuntimeView view = root.AddComponent<CampaignRuntimeView>();
            view.Build(campaign, progress, _ => { }, _ => { }, () => { },
                campaign.CampaignUiTheme);
            return view;
        }

        private CampaignProgressService ProgressAt(string chapterId, int completed, int stars)
        {
            var progress = new CampaignProgressService(campaign);
            int chapterIndex = campaign.Chapters.ToList().FindIndex(chapter =>
                chapter.ChapterId == chapterId);
            for (int index = 0; index < chapterIndex; index++)
                Complete(progress, campaign.Chapters[index], campaign.Chapters[index].Levels.Count,
                    stars);
            Complete(progress, campaign.Chapters[chapterIndex], completed, stars);
            return progress;
        }

        private static void Complete(CampaignProgressService progress,
            CampaignChapterDefinition chapter, int count, int stars)
        {
            for (int index = 0; index < count; index++)
            {
                CampaignLevelEntry entry = chapter.Levels[index];
                Assert.That(progress.RecordCompletion(entry.LevelId, Result(entry, stars)).Accepted,
                    Is.True);
            }
        }

        private static SessionCompletionResult Result(CampaignLevelEntry entry, int stars) =>
            new SessionCompletionResult(entry.LevelDefinition, 1, 1f,
                entry.AuthoredOptimalMoves, false,
                new StarEvaluationResult(StarEvaluationStatus.Rated, stars));

        private GameObject Root() => cleanup.OfType<GameObject>().Last(root =>
            root.GetComponent<CampaignRuntimeView>() != null);

        private CityChapterNodeView[] Nodes() => Root()
            .GetComponentsInChildren<CityChapterNodeView>(true)
            .OrderBy(node => node.transform.GetSiblingIndex()).ToArray();

        private CityChapterNodeView Node(string chapterId) => Nodes().Single(node =>
            node.ChapterId == chapterId);

        private void DestroyRoots()
        {
            foreach (GameObject root in cleanup.OfType<GameObject>().ToArray())
            {
                cleanup.Remove(root);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Bounds BoundsIn(RectTransform rect, Transform relativeTo)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bounds = new Bounds(relativeTo.InverseTransformPoint(corners[0]), Vector3.zero);
            foreach (Vector3 corner in corners)
                bounds.Encapsulate(relativeTo.InverseTransformPoint(corner));
            return bounds;
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            private readonly CampaignProgressService progress;
            public string SavePath => "memory://m15-e1c";

            public MemoryStore(CampaignProgressService progress)
            {
                this.progress = progress;
            }

            public CampaignLoadResult Load(CampaignDefinition definition) =>
                new CampaignLoadResult(CampaignLoadStatus.Loaded, progress,
                    Array.Empty<string>());
            public CampaignSaveResult Save(CampaignProgressService target) =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }
    }
}

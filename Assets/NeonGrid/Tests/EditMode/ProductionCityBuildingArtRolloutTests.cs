using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
        private const string SubstationArtPath =
            "Assets/NeonGrid/Art/CityBuildings/Substation/Substation_Final.asset";
        private const string ControlCenterArtPath =
            "Assets/NeonGrid/Art/CityBuildings/ControlCenter/ControlCenter_Final.asset";
        private const string AutomationPlantArtPath =
            "Assets/NeonGrid/Art/CityBuildings/AutomationPlant/AutomationPlant_Final.asset";
        private const string CentralArtPath =
            "Assets/NeonGrid/Art/CityBuildings/CentralGrid/CentralGrid_Final.asset";
        private static readonly string[] ChapterIds =
        {
            "power_station", "substation", "control_center", "automation_plant", "central_grid"
        };
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CampaignDefinition campaign;
        private Dictionary<string, CityBuildingArtDefinition> artById;

        [SetUp]
        public void SetUp()
        {
            campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            artById = new Dictionary<string, CityBuildingArtDefinition>
            {
                ["power_station"] = LoadArt(PowerArtPath),
                ["substation"] = LoadArt(SubstationArtPath),
                ["control_center"] = LoadArt(ControlCenterArtPath),
                ["automation_plant"] = LoadArt(AutomationPlantArtPath),
                ["central_grid"] = LoadArt(CentralArtPath)
            };
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void ProductionCampaign_MapsExactlyFiveFinalDefinitionsByStableChapterId()
        {
            Assert.That(campaign.CityBuildingArt, Has.Count.EqualTo(5));
            Assert.That(campaign.CityBuildingArt.Select(binding => binding.ChapterId),
                Is.EqualTo(ChapterIds));
            foreach (string chapterId in ChapterIds)
            {
                Assert.That(artById[chapterId].ChapterId, Is.EqualTo(chapterId));
                Assert.That(campaign.GetCityBuildingArt(chapterId), Is.SameAs(artById[chapterId]));
                Assert.That(CityBuildingArtAssetValidator.Validate(artById[chapterId], chapterId),
                    Is.Empty);
            }
            Assert.That(campaign.GetCityBuildingArt("Power Station"), Is.Null);
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
            Assert.That(rebuilt.CityBuildingArt.Select(binding => binding.ChapterId),
                Is.EqualTo(ChapterIds));
            foreach (string chapterId in ChapterIds)
                Assert.That(rebuilt.GetCityBuildingArt(chapterId), Is.SameAs(artById[chapterId]));
            Assert.That(rebuilt.CityBuildingArt, Has.Count.EqualTo(5));
        }

        [Test]
        public void FreshProductionMap_UsesFinalPowerStageOneAndLocksAllLaterFinalBuildings()
        {
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            CityChapterNodeView power = Node("power_station");
            Assert.That(power.VisualState, Is.EqualTo(ChapterMapVisualState.ProgressStage1));
            Assert.That(power.BuildingArtView.VisualState,
                Is.EqualTo(ChapterMapVisualState.ProgressStage1));
            foreach (string chapterId in ChapterIds.Skip(1))
            {
                CityChapterNodeView node = Node(chapterId);
                Assert.That(node.VisualState, Is.EqualTo(ChapterMapVisualState.Locked));
                Assert.That(node.BuildingArtView.VisualState,
                    Is.EqualTo(ChapterMapVisualState.Locked));
            }
        }

        [TestCase("power_station", 0, ChapterMapVisualState.ProgressStage1)]
        [TestCase("power_station", 4, ChapterMapVisualState.ProgressStage2)]
        [TestCase("power_station", 7, ChapterMapVisualState.ProgressStage3)]
        [TestCase("power_station", 10, ChapterMapVisualState.Restored)]
        [TestCase("substation", 0, ChapterMapVisualState.ProgressStage1)]
        [TestCase("substation", 4, ChapterMapVisualState.ProgressStage2)]
        [TestCase("substation", 7, ChapterMapVisualState.ProgressStage3)]
        [TestCase("substation", 10, ChapterMapVisualState.Restored)]
        [TestCase("control_center", 0, ChapterMapVisualState.ProgressStage1)]
        [TestCase("control_center", 4, ChapterMapVisualState.ProgressStage2)]
        [TestCase("control_center", 7, ChapterMapVisualState.ProgressStage3)]
        [TestCase("control_center", 10, ChapterMapVisualState.Restored)]
        [TestCase("automation_plant", 0, ChapterMapVisualState.ProgressStage1)]
        [TestCase("automation_plant", 4, ChapterMapVisualState.ProgressStage2)]
        [TestCase("automation_plant", 7, ChapterMapVisualState.ProgressStage3)]
        [TestCase("automation_plant", 10, ChapterMapVisualState.Restored)]
        [TestCase("central_grid", 0, ChapterMapVisualState.ProgressStage1)]
        [TestCase("central_grid", 4, ChapterMapVisualState.ProgressStage2)]
        [TestCase("central_grid", 7, ChapterMapVisualState.ProgressStage3)]
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
            Assert.That(node.BuildingArtView.Definition, Is.SameAs(artById[chapterId]));
        }

        [TestCase("power_station")]
        [TestCase("substation")]
        [TestCase("control_center")]
        [TestCase("automation_plant")]
        [TestCase("central_grid")]
        public void StarsDoNotDetermineFinalBuildingState(string chapterId)
        {
            CampaignProgressService oneStar = ProgressAt(chapterId, 4, 1);
            CampaignProgressService threeStars = ProgressAt(chapterId, 4, 3);
            CampaignRuntimeView first = BuildView(oneStar);
            first.ShowMap();
            ChapterMapVisualState firstState = Node(chapterId).BuildingArtView.VisualState;
            DestroyRoots();
            CampaignRuntimeView second = BuildView(threeStars);
            second.ShowMap();
            Assert.That(firstState, Is.EqualTo(ChapterMapVisualState.ProgressStage2));
            Assert.That(Node(chapterId).BuildingArtView.VisualState,
                Is.EqualTo(firstState));
        }

        [Test]
        public void ProductionMap_UsesAllFiveFinalDefinitionsWithoutPrototypeControls()
        {
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            CityChapterNodeView[] nodes = Nodes();
            foreach (string id in ChapterIds)
            {
                CityChapterNodeView node = nodes.Single(candidate => candidate.ChapterId == id);
                Assert.That(node.BuildingArtView, Is.Not.Null);
                Assert.That(node.BuildingArtView.UsesArt, Is.True);
                Assert.That(node.BuildingArtView.Definition, Is.SameAs(artById[id]));
                Assert.That(node.BuildingArtView.ArtRoot.childCount, Is.EqualTo(4));
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
                new CampaignCityBuildingArtBinding("substation", artById["substation"]),
                new CampaignCityBuildingArtBinding("control_center", artById["control_center"]),
                new CampaignCityBuildingArtBinding("automation_plant", artById["automation_plant"]),
                new CampaignCityBuildingArtBinding("central_grid", artById["central_grid"])
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
        public void MissingAndMismatchedOptionalDefinitionsRemainVisibleFallbacks()
        {
            CampaignDefinition missingCampaign = UnityEngine.Object.Instantiate(campaign);
            cleanup.Add(missingCampaign);
            missingCampaign.SetCityBuildingArt(campaign.CityBuildingArt.Where(binding =>
                binding.ChapterId != "control_center"));
            campaign = missingCampaign;
            CampaignRuntimeView missingView = BuildView(new CampaignProgressService(campaign));
            missingView.ShowMap();
            CityChapterNodeView missing = Node("control_center");
            Assert.That(missing.BuildingArtView, Is.Null);
            AssertVisibleFallback(missing);

            DestroyRoots();
            CampaignDefinition mismatchedCampaign = UnityEngine.Object.Instantiate(missingCampaign);
            cleanup.Add(mismatchedCampaign);
            mismatchedCampaign.SetCityBuildingArt(new[]
            {
                new CampaignCityBuildingArtBinding("power_station", artById["power_station"]),
                new CampaignCityBuildingArtBinding("substation", artById["power_station"]),
                new CampaignCityBuildingArtBinding("control_center", artById["control_center"]),
                new CampaignCityBuildingArtBinding("automation_plant", artById["automation_plant"]),
                new CampaignCityBuildingArtBinding("central_grid", artById["central_grid"])
            });
            campaign = mismatchedCampaign;
            CampaignRuntimeView mismatchedView = BuildView(new CampaignProgressService(campaign));
            mismatchedView.ShowMap();
            CityChapterNodeView mismatched = Node("substation");
            Assert.That(mismatched.BuildingArtView, Is.Not.Null);
            Assert.That(mismatched.BuildingArtView.UsesArt, Is.False);
            AssertVisibleFallback(mismatched);
        }

        [Test]
        public void AssociationValidationRejectsDuplicateUnknownNullAndMismatchedEntries()
        {
            CampaignDefinition invalid = UnityEngine.Object.Instantiate(campaign);
            cleanup.Add(invalid);
            invalid.SetCityBuildingArt(new CampaignCityBuildingArtBinding[]
            {
                new CampaignCityBuildingArtBinding("power_station", artById["power_station"]),
                new CampaignCityBuildingArtBinding("power_station", artById["power_station"]),
                new CampaignCityBuildingArtBinding("unknown", artById["central_grid"]),
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

        [TestCase(NarrativeQaPreset.FirstRestorationPending, "power_station", "substation")]
        [TestCase(NarrativeQaPreset.SubstationRestorationPending, "substation", "control_center")]
        [TestCase(NarrativeQaPreset.ControlCenterRestorationPending, "control_center",
            "automation_plant")]
        [TestCase(NarrativeQaPreset.AutomationPlantRestorationPending, "automation_plant",
            "central_grid")]
        public void IntermediateRestoration_UsesFinalArtAndSettlesWithoutDrift(
            NarrativeQaPreset preset, string restoredChapterId, string nextChapterId)
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign,
                preset);
            CampaignRuntimeView view = BuildView(progress);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore(progress));
            CityRestorationSequenceController sequence = Root().AddComponent<
                CityRestorationSequenceController>();
            sequence.Initialize(view, flow);
            Assert.That(sequence.PreparePendingRestoration(), Is.True);
            Assert.That(sequence.CurrentPlan.RestoredChapterId, Is.EqualTo(restoredChapterId));
            Assert.That(sequence.CurrentPlan.NextChapterId, Is.EqualTo(nextChapterId));
            CityChapterNodeView restored = Node(restoredChapterId);
            Vector3 baseScale = restored.BaseVisualScale;
            Vector3 artPosition = restored.BuildingArtView.ArtRoot.localPosition;
            Vector3 artScale = restored.BuildingArtView.ArtRoot.localScale;
            Transform[] layers = restored.BuildingArtView.ArtRoot.Cast<Transform>().ToArray();
            Assert.That(layers, Has.Length.EqualTo(4));
            sequence.ApplyPhase(CityRestorationSequencePhase.Focus, 0.5f);
            sequence.ApplyPhase(CityRestorationSequencePhase.BuildingPowerUp, 1f);
            Assert.That(restored.BuildingArtView.VisualState,
                Is.EqualTo(ChapterMapVisualState.Restored));
            sequence.ApplyPhase(CityRestorationSequencePhase.EnergyTravel, 1f);
            sequence.ApplyPhase(CityRestorationSequencePhase.NextChapterReveal, 1f);
            Assert.That(sequence.CompletePreparedSequence(), Is.True);
            Assert.That(restored.VisualScale, Is.EqualTo(baseScale));
            Assert.That(restored.BuildingArtView.ArtRoot.localPosition, Is.EqualTo(artPosition));
            Assert.That(restored.BuildingArtView.ArtRoot.localScale, Is.EqualTo(artScale));
            Assert.That(layers.All(layer => layer.localPosition == Vector3.zero), Is.True);
            Assert.That(Node(nextChapterId).VisualState,
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

        [TestCase(NarrativeQaPreset.FreshMap)]
        [TestCase(NarrativeQaPreset.PowerStationStage2)]
        [TestCase(NarrativeQaPreset.PowerStationRestored)]
        [TestCase(NarrativeQaPreset.SubstationStage2)]
        [TestCase(NarrativeQaPreset.SubstationRestored)]
        [TestCase(NarrativeQaPreset.ControlCenterStage2)]
        [TestCase(NarrativeQaPreset.ControlCenterRestored)]
        [TestCase(NarrativeQaPreset.AutomationPlantStage2)]
        [TestCase(NarrativeQaPreset.AutomationPlantRestored)]
        [TestCase(NarrativeQaPreset.CentralGridStage2)]
        [TestCase(NarrativeQaPreset.EndingPending)]
        [TestCase(NarrativeQaPreset.PostEndingComplete)]
        public void SaveLoad_DerivesAllFiveArtStatesWithoutNewSchemaState(
            NarrativeQaPreset preset)
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign, preset);
            var expected = campaign.Chapters.ToDictionary(chapter => chapter.ChapterId,
                chapter => CityMapPresentationModel.GetChapterVisualState(
                    progress.GetChapterState(chapter.ChapterId),
                    progress.GetCompletedLevelCount(chapter.ChapterId), chapter.Levels.Count));
            string path = Path.Combine(Path.GetTempPath(), "NeonGridE2D",
                Guid.NewGuid().ToString("N"), "progress.json");
            try
            {
                var store = new CampaignSaveStore(path);
                Assert.That(store.Save(progress).Succeeded, Is.True);
                CampaignLoadResult load = store.Load(campaign);
                CampaignRuntimeView view = BuildView(load.Progress);
                view.ShowMap();
                foreach (string chapterId in ChapterIds)
                    Assert.That(Node(chapterId).BuildingArtView.VisualState,
                        Is.EqualTo(expected[chapterId]));
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

        [TestCase("power_station")]
        [TestCase("substation")]
        [TestCase("control_center")]
        [TestCase("automation_plant")]
        [TestCase("central_grid")]
        public void ReplayingCompletedLevelPreservesRestoredArtWithoutDuplicateEvent(
            string chapterId)
        {
            CampaignProgressService progress = ProgressAt(chapterId, 10, 3);
            CampaignChapterDefinition chapter = campaign.Chapters.Single(item =>
                item.ChapterId == chapterId);
            CampaignProgressUpdate update = progress.RecordCompletion(chapter.Levels[0].LevelId,
                Result(chapter.Levels[0], 1));
            Assert.That(update.Accepted, Is.True);
            Assert.That(update.ChapterJustRestored, Is.False);
            Assert.That(progress.PendingRestoration, Is.Null);
            CampaignRuntimeView view = BuildView(progress);
            view.ShowMap();
            Assert.That(Node(chapterId).BuildingArtView.VisualState,
                Is.EqualTo(ChapterMapVisualState.Restored));
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
                    Has.Length.EqualTo(5));
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
            var expected = new Dictionary<string, (Vector2 position, Vector2 hit)>
            {
                ["power_station"] = (new Vector2(-310f, -510f), new Vector2(280f, 280f)),
                ["substation"] = (new Vector2(-340f, 0f), new Vector2(280f, 280f)),
                ["control_center"] = (new Vector2(60f, 420f), new Vector2(280f, 300f)),
                ["automation_plant"] = (new Vector2(370f, 0f), new Vector2(280f, 280f)),
                ["central_grid"] = (new Vector2(40f, -300f), new Vector2(350f, 330f))
            };
            foreach (string chapterId in ChapterIds)
            {
                CityChapterNodeView node = Node(chapterId);
                Assert.That(node.HitArea.anchoredPosition,
                    Is.EqualTo(expected[chapterId].position));
                Assert.That(node.HitArea.sizeDelta, Is.EqualTo(expected[chapterId].hit));
                AssertArtContainedAndLabelSafe(node);
            }
            CityChapterNodeView central = Node("central_grid");
            RectTransform centralVisual = central.transform.Find("Building Silhouette")
                as RectTransform;
            Assert.That(centralVisual.localScale.x, Is.EqualTo(1.075f).Within(0.0001f));
            Assert.That(centralVisual.anchoredPosition.y, Is.EqualTo(76f));
            CityChapterNodeView control = Node("control_center");
            Assert.That(control.BuildingArtView.ArtRoot.anchoredPosition,
                Is.EqualTo(artById["control_center"].LocalOffset));
            Assert.That(control.BuildingArtView.ArtRoot.anchoredPosition.y, Is.EqualTo(24f));
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(720f, 1280f)]
        public void FullProductionMap_RemainsInsidePortraitBounds(float width, float height)
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
            foreach (string chapterId in ChapterIds)
            {
                CityChapterNodeView node = Node(chapterId);
                Assert.That(node.BuildingArtView.ArtRoot.childCount, Is.EqualTo(4));
                AssertArtContainedAndLabelSafe(node);
            }
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

        [Test]
        public void AcceptedFinalArtFilesRemainByteIdentical()
        {
            var expected = new Dictionary<string, string>
            {
                ["PowerStation/PowerStation_Base.png"] = "C713B39D3E579A13DDC3F4672896D7F49E32D203C918FDCA5D7F7698E05CC343",
                ["PowerStation/PowerStation_WarmLights.png"] = "5A3DB47C66BF7B402B715CBCFFC697312676AC3F1B3F21C89DF9EAF1EF7B68FD",
                ["PowerStation/PowerStation_Energy.png"] = "807A8A5DE750DE30CDF75A9B0283FFE4EF92037F29E4FEFA8431F0B0911BC259",
                ["PowerStation/PowerStation_Core.png"] = "5595628B97124B194E743D27897841B208D5A2118676A47010B48095DEF3B39D",
                ["PowerStation/PowerStation_Final.asset"] = "8DC4D71D0215824B027D05E74F966CB49579D420E4A5D4303B37ACC7198C7EBB",
                ["Substation/Substation_Base.png"] = "322F6AF5DE965E48E46935BA9146D50A76C24B839FAFB813DFA10670B2BBE7C1",
                ["Substation/Substation_WarmLights.png"] = "A5155443B19F90FC4D90C4631A3B1E661FB8179296A8C344837EE634BEE6D1BE",
                ["Substation/Substation_Energy.png"] = "6486F40D17D9A99BEBA4CA859BC9C656EF18070CE1DA0E6B2345DAA59C8F3622",
                ["Substation/Substation_Core.png"] = "EE2B834F406704E38CAEC3522B9F0CDA47B96EE79C41011DE14F49FA105E2F7B",
                ["Substation/Substation_Final.asset"] = "4BC66B050FA64A2A21D3B808C05F89CE0538DD50263746BD909FD3C0B9839CAE",
                ["ControlCenter/ControlCenter_Base.png"] = "875A3BC234FA15A947A008BA4998B8CB61228C086D846026E0ADACD9ECFD3F1E",
                ["ControlCenter/ControlCenter_WarmLights.png"] = "090FA396DED630BF68EE3412249ECA837A81F25D6D8C6633D443DD8248F58EF4",
                ["ControlCenter/ControlCenter_Energy.png"] = "75000086CEBDE87FCFDF79278F3B3B9990BE4416824DCED3EF07937772E3DD73",
                ["ControlCenter/ControlCenter_Core.png"] = "77FEE7278FA97D448D72645B96E6313C1A85B8B4090013AB8CF252FA7A7618C9",
                ["ControlCenter/ControlCenter_Final.asset"] = "F4314B297146F0DC1C27BC58E58762F5D16C8E5F107A7B4E586DF9928A73C0F2",
                ["AutomationPlant/AutomationPlant_Base.png"] = "88812D7AC6883866E37637362BFE84AF28C171B3F44AA13D6F03D397D59D0A9E",
                ["AutomationPlant/AutomationPlant_WarmLights.png"] = "81429C19BEA2366FF0E6D402F87E0B4F9971D65BB9669404C3B151F17F730698",
                ["AutomationPlant/AutomationPlant_Energy.png"] = "D299A92A01F79FEB84B1B7D4D630978847FCC0598F2F943DEE27264018E344EC",
                ["AutomationPlant/AutomationPlant_Core.png"] = "ADAEDDDD837E0C930FDF67B102AFD100874F8DDDA287B732638E98C4246C60DB",
                ["AutomationPlant/AutomationPlant_Final.asset"] = "336982B9DA6E638A37D13F38FE5ED347440688F07688F464A9D0C4626FE32D35",
                ["CentralGrid/CentralGrid_Base.png"] = "C42FA4773C5A0430D7E6DF2A559B895F065B66F6181AF52B51BAAAE1858B3ECB",
                ["CentralGrid/CentralGrid_WarmLights.png"] = "73705CEA9FA32291F8E9710C3D41502B137B4464833ACB01B67034D0481E08E9",
                ["CentralGrid/CentralGrid_Energy.png"] = "3C68055D61D9BB5F49A4EA58D013A92989657F9C28CD9F91BDF2D1DC96B3035C",
                ["CentralGrid/CentralGrid_Core.png"] = "8C64BA9AC693E88F3A7D233BC0AB92B3809A00A333F9AC77F37E83BE34812F2B",
                ["CentralGrid/CentralGrid_Final.asset"] = "1AA9A2BA527F4E0C4B54E35E9BD26E988C8370D3AFF95DA1C3869EC81597AD63"
            };
            const string root = "Assets/NeonGrid/Art/CityBuildings/";
            foreach (KeyValuePair<string, string> item in expected)
                Assert.That(Hash(root + item.Key), Is.EqualTo(item.Value), item.Key);
        }

        [TestCase(NarrativeQaPreset.PowerStationStage1, "power_station", 1)]
        [TestCase(NarrativeQaPreset.PowerStationStage2, "power_station", 4)]
        [TestCase(NarrativeQaPreset.PowerStationStage3, "power_station", 7)]
        [TestCase(NarrativeQaPreset.PowerStationRestored, "power_station", 10)]
        [TestCase(NarrativeQaPreset.SubstationStage1, "substation", 1)]
        [TestCase(NarrativeQaPreset.SubstationStage2, "substation", 4)]
        [TestCase(NarrativeQaPreset.SubstationStage3, "substation", 7)]
        [TestCase(NarrativeQaPreset.SubstationRestored, "substation", 10)]
        [TestCase(NarrativeQaPreset.ControlCenterStage1, "control_center", 1)]
        [TestCase(NarrativeQaPreset.ControlCenterStage2, "control_center", 4)]
        [TestCase(NarrativeQaPreset.ControlCenterStage3, "control_center", 7)]
        [TestCase(NarrativeQaPreset.ControlCenterRestored, "control_center", 10)]
        [TestCase(NarrativeQaPreset.AutomationPlantStage1, "automation_plant", 1)]
        [TestCase(NarrativeQaPreset.AutomationPlantStage2, "automation_plant", 4)]
        [TestCase(NarrativeQaPreset.AutomationPlantStage3, "automation_plant", 7)]
        [TestCase(NarrativeQaPreset.AutomationPlantRestored, "automation_plant", 10)]
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

        [TestCase(NarrativeQaPreset.FirstRestorationPending, "power_station")]
        [TestCase(NarrativeQaPreset.SubstationRestorationPending, "substation")]
        [TestCase(NarrativeQaPreset.ControlCenterRestorationPending, "control_center")]
        [TestCase(NarrativeQaPreset.AutomationPlantRestorationPending, "automation_plant")]
        [TestCase(NarrativeQaPreset.FinalRestorationPending, "central_grid")]
        public void NarrativeQaProvidesEveryRestorationPendingState(
            NarrativeQaPreset preset, string chapterId)
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign, preset);
            Assert.That(progress.PendingRestoration, Is.Not.Null);
            Assert.That(progress.PendingRestoration.RestoredChapterId, Is.EqualTo(chapterId));
        }

        private CampaignRuntimeView BuildView(CampaignProgressService progress)
        {
            GameObject root = new GameObject("E2D Production Map");
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

        private static CityBuildingArtDefinition LoadArt(string path) =>
            AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(path);

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty);
        }

        private static void AssertArtContainedAndLabelSafe(CityChapterNodeView node)
        {
            Assert.That(node.BuildingArtView, Is.Not.Null, node.ChapterId);
            Assert.That(node.BuildingArtView.UsesArt, Is.True, node.ChapterId);
            Bounds art = BoundsIn(node.BuildingArtView.ArtRoot, node.transform);
            Bounds label = BoundsIn(node.Label.rectTransform, node.transform);
            Assert.That(art.min.y, Is.GreaterThan(label.max.y + 6f), node.ChapterId);
            Assert.That(art.min.x, Is.GreaterThan(node.HitArea.rect.xMin), node.ChapterId);
            Assert.That(art.max.x, Is.LessThan(node.HitArea.rect.xMax), node.ChapterId);
            Assert.That(art.max.y, Is.LessThan(node.HitArea.rect.yMax), node.ChapterId);
        }

        private static void AssertVisibleFallback(CityChapterNodeView node)
        {
            RectTransform silhouette = node.transform.Find("Building Silhouette") as RectTransform;
            Assert.That(silhouette, Is.Not.Null);
            Assert.That(silhouette.GetComponentsInChildren<Image>(true)
                .Any(image => image.enabled), Is.True, node.ChapterId);
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
            public string SavePath => "memory://m15-e2d";

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

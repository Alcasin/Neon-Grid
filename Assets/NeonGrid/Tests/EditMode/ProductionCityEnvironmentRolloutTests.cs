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
    public sealed class ProductionCityEnvironmentRolloutTests
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/NeonGrid_Main.asset";
        private const string EnvironmentPath =
            "Assets/NeonGrid/Art/CityEnvironment/CityEnvironment_Final.asset";
        private const string ScenePath = "Assets/NeonGrid/Scenes/M12_NeonGrid_Main.unity";
        private static readonly string[] ChapterIds =
        {
            CityEnvironmentArtDefinition.PowerStationId,
            CityEnvironmentArtDefinition.SubstationId,
            CityEnvironmentArtDefinition.ControlCenterId,
            CityEnvironmentArtDefinition.AutomationPlantId,
            CityEnvironmentArtDefinition.CentralGridId
        };

        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CampaignDefinition campaign;
        private CityEnvironmentArtDefinition environment;

        [SetUp]
        public void SetUp()
        {
            campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            environment = AssetDatabase.LoadAssetAtPath<CityEnvironmentArtDefinition>(
                EnvironmentPath);
            Assert.That(campaign, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void ProductionCampaignReferencesExactlyOneValidFinalEnvironment()
        {
            Assert.That(campaign.CityEnvironmentArt, Is.SameAs(environment));
            Assert.That(environment.IsConfigured, Is.True);
            Assert.That(environment.Districts.Select(layer => layer.ChapterId),
                Is.EqualTo(ChapterIds));
            Assert.That(environment.Districts.Select(layer => layer.ChapterId).Distinct().Count(),
                Is.EqualTo(5));
            Assert.That(campaign.CityBuildingArt, Has.Count.EqualTo(5));
            Assert.That(new CampaignValidator().Validate(campaign).IsValid, Is.True);
        }

        [Test]
        public void MainCampaignBuilderAuthorsEnvironmentAndIsIdempotent()
        {
            MainCampaignBuilder.Build();
            byte[] first = File.ReadAllBytes(CampaignPath);
            MainCampaignBuilder.Build();
            byte[] second = File.ReadAllBytes(CampaignPath);
            Assert.That(second, Is.EqualTo(first));
            CampaignDefinition rebuilt = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(
                CampaignPath);
            Assert.That(rebuilt.CityEnvironmentArt, Is.SameAs(environment));
            Assert.That(rebuilt.CityBuildingArt, Has.Count.EqualTo(5));
            Assert.That(rebuilt.CityBuildingArt.Select(binding => binding.ChapterId),
                Is.EqualTo(ChapterIds));
        }

        [Test]
        public void MainCampaignBuilderRejectsInvalidEnvironmentBeforeAuthoring()
        {
            CityEnvironmentArtDefinition invalid =
                ScriptableObject.CreateInstance<CityEnvironmentArtDefinition>();
            cleanup.Add(invalid);
            MethodInfo guard = typeof(MainCampaignBuilder).GetMethod(
                "RequireFinalEnvironment",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(guard, Is.Not.Null);

            TargetInvocationException missing = Assert.Throws<TargetInvocationException>(
                () => guard.Invoke(null, new object[] { null }));
            Assert.That(missing.InnerException, Is.TypeOf<InvalidOperationException>());
            TargetInvocationException invalidDefinition =
                Assert.Throws<TargetInvocationException>(
                    () => guard.Invoke(null, new object[] { invalid }));
            Assert.That(invalidDefinition.InnerException,
                Is.TypeOf<InvalidOperationException>());
            Assert.DoesNotThrow(() => guard.Invoke(null, new object[] { environment }));
        }

        [Test]
        public void ProductionCreatesOneSevenLayerNonRaycastEnvironmentInRequiredOrder()
        {
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            CityEnvironmentArtView[] environments = Root()
                .GetComponentsInChildren<CityEnvironmentArtView>(true);
            Assert.That(environments, Has.Length.EqualTo(1));
            Assert.That(view.CityEnvironmentView, Is.SameAs(environments[0]));
            Assert.That(environments[0].Definition, Is.SameAs(environment));
            Assert.That(environments[0].LayerCount, Is.EqualTo(7));
            Assert.That(environments[0].ArtRoot.GetComponentsInChildren<Image>(true),
                Has.All.Matches<Image>(image => !image.raycastTarget));
            Assert.That(environments[0].ArtRoot.GetSiblingIndex(), Is.EqualTo(1));

            RectTransform composition = Composition(view);
            Assert.That(composition.Find("City Ground").GetSiblingIndex(), Is.EqualTo(0));
            foreach (CityEnergyPathView path in composition
                         .GetComponentsInChildren<CityEnergyPathView>(true))
                Assert.That(path.transform.GetSiblingIndex(),
                    Is.GreaterThan(environments[0].ArtRoot.GetSiblingIndex()));
            foreach (CityChapterNodeView node in composition
                         .GetComponentsInChildren<CityChapterNodeView>(true))
                Assert.That(node.transform.GetSiblingIndex(),
                    Is.GreaterThan(environments[0].ArtRoot.GetSiblingIndex()));
        }

        [Test]
        public void FinalEnvironmentSuppressesOnlyLegacyBackdropAndPreservesMapContent()
        {
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            RectTransform composition = Composition(view);
            Image[] blocks = composition.Cast<Transform>()
                .Where(child => child.name.StartsWith("City Block ", StringComparison.Ordinal))
                .Select(child => child.GetComponent<Image>()).ToArray();
            Assert.That(blocks, Has.Length.EqualTo(13));
            Assert.That(blocks, Has.All.Matches<Image>(image => !image.enabled));
            foreach (string road in new[] { "Road Horizontal", "Road Vertical", "Road Diagonal" })
                Assert.That(composition.Find(road).GetComponent<Image>().enabled, Is.False, road);

            CityEnergyPathView[] paths = composition
                .GetComponentsInChildren<CityEnergyPathView>(true);
            Assert.That(paths, Has.Length.EqualTo(4));
            Assert.That(paths, Has.All.Matches<CityEnergyPathView>(path =>
                path.gameObject.activeInHierarchy && path.GetComponentsInChildren<Image>(true)
                    .All(image => image.enabled)));
            CityChapterNodeView[] nodes = composition
                .GetComponentsInChildren<CityChapterNodeView>(true);
            Assert.That(nodes, Has.Length.EqualTo(5));
            Assert.That(nodes, Has.All.Matches<CityChapterNodeView>(node =>
                node.gameObject.activeInHierarchy && node.Label.enabled &&
                node.Button.targetGraphic.raycastTarget && node.BuildingArtView != null &&
                node.BuildingArtView.UsesArt));
            Assert.That(Root().GetComponentsInChildren<CityEnvironmentArtPrototypeController>(true),
                Is.Empty);
            Assert.That(Root().GetComponentsInChildren<Transform>(true)
                .Any(item => item.name == "E3A Developer Controls"), Is.False);
        }

        [Test]
        public void FreshAndPartialMapsDoNotActivateDistricts()
        {
            CampaignProgressService fresh = new CampaignProgressService(campaign);
            CampaignRuntimeView freshView = BuildView(fresh);
            freshView.ShowMap();
            Assert.That(freshView.CityEnvironmentView.ActiveDistrictCount, Is.Zero);
            Assert.That(Node(freshView, ChapterIds[0]).VisualState,
                Is.EqualTo(ChapterMapVisualState.ProgressStage1));

            DestroyRoots();
            CampaignProgressService partial = new CampaignProgressService(campaign);
            CompleteLevels(partial, campaign.Chapters[0], 9, 2);
            CampaignRuntimeView partialView = BuildView(partial);
            partialView.ShowMap();
            Assert.That(partialView.CityEnvironmentView.ActiveDistrictCount, Is.Zero);
            Assert.That(partialView.CityEnvironmentView.IsDistrictActive(ChapterIds[0]), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RestoredChaptersActivateExactDistrictCountAndFinalAccent(int count)
        {
            CampaignProgressService progress = RestoredFirst(count, 2);
            CampaignRuntimeView view = BuildView(progress);
            view.ShowMap();
            CityEnvironmentArtView environmentView = view.CityEnvironmentView;
            Assert.That(environmentView.ActiveDistrictCount, Is.EqualTo(count));
            for (int index = 0; index < ChapterIds.Length; index++)
                Assert.That(environmentView.IsDistrictActive(ChapterIds[index]),
                    Is.EqualTo(index < count), ChapterIds[index]);
            Assert.That(environmentView.ArtRoot.Find("Final City Accent")
                    .GetComponent<Image>().enabled,
                Is.EqualTo(count == 5));
        }

        [Test]
        public void StarsDoNotAffectProductionEnvironmentState()
        {
            CampaignRuntimeView oneStar = BuildView(RestoredFirst(3, 1));
            oneStar.ShowMap();
            bool[] one = ChapterIds.Select(oneStar.CityEnvironmentView.IsDistrictActive).ToArray();
            DestroyRoots();
            CampaignRuntimeView threeStars = BuildView(RestoredFirst(3, 3));
            threeStars.ShowMap();
            Assert.That(ChapterIds.Select(threeStars.CityEnvironmentView.IsDistrictActive),
                Is.EqualTo(one));
        }

        [Test]
        public void SaveLoadDerivesEnvironmentWithoutChangingSaveSchema()
        {
            string savePath = Path.Combine(Application.temporaryCachePath,
                "neon_grid_e3c_environment_test.json");
            cleanup.Add(ScriptableObject.CreateInstance<TemporarySaveCleanup>()
                .Initialize(savePath));
            CampaignProgressService source = RestoredFirst(3, 2);
            CampaignSaveStore store = new CampaignSaveStore(savePath);
            Assert.That(store.Save(source).Succeeded, Is.True);
            CampaignLoadResult load = store.Load(campaign);
            Assert.That(load.Status, Is.EqualTo(CampaignLoadStatus.Loaded));
            CampaignRuntimeView view = BuildView(load.Progress);
            view.ShowMap();
            Assert.That(view.CityEnvironmentView.ActiveDistrictCount, Is.EqualTo(3));
            Assert.That(typeof(CampaignSaveData).GetFields(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => field.Name.IndexOf("environment", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              field.Name.IndexOf("district", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              field.Name.IndexOf("accent", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void ReplayingCompletedLevelPreservesEnvironmentAndDoesNotQueueEvent()
        {
            CampaignProgressService progress = RestoredFirst(1, 3);
            CampaignLevelEntry replay = campaign.Chapters[0].Levels[0];
            CampaignProgressUpdate update = progress.RecordCompletion(replay.LevelId,
                Result(replay, 1));
            Assert.That(update.Accepted, Is.True);
            Assert.That(update.ChapterJustRestored, Is.False);
            Assert.That(progress.GetChapterState(ChapterIds[0]),
                Is.EqualTo(CampaignChapterState.Restored));
            CampaignRuntimeView view = BuildView(progress);
            view.ShowMap();
            Assert.That(view.CityEnvironmentView.ActiveDistrictCount, Is.EqualTo(1));
            Assert.That(view.CityEnvironmentView.IsDistrictActive(ChapterIds[0]), Is.True);
        }

        [Test]
        public void TenMapSelectorCyclesDoNotGrowOrDriftEnvironmentHierarchy()
        {
            CampaignRuntimeView view = BuildView(RestoredFirst(2, 2));
            view.ShowMap();
            CityEnvironmentArtView environmentView = view.CityEnvironmentView;
            int componentId = environmentView.GetInstanceID();
            int rootId = environmentView.ArtRoot.GetInstanceID();
            Vector2 position = environmentView.ArtRoot.anchoredPosition;
            Vector3 scale = environmentView.ArtRoot.localScale;
            for (int cycle = 0; cycle < 10; cycle++)
            {
                view.ShowChapter(campaign.Chapters[0]);
                view.ShowMap();
                Assert.That(Root().GetComponentsInChildren<CityEnvironmentArtView>(true),
                    Has.Length.EqualTo(1));
                Assert.That(view.CityEnvironmentView.GetInstanceID(), Is.EqualTo(componentId));
                Assert.That(view.CityEnvironmentView.ArtRoot.GetInstanceID(), Is.EqualTo(rootId));
                Assert.That(view.CityEnvironmentView.LayerCount, Is.EqualTo(7));
                Assert.That(view.CityEnvironmentView.ActiveDistrictCount, Is.EqualTo(2));
                Assert.That(view.CityEnvironmentView.ArtRoot.anchoredPosition,
                    Is.EqualTo(position));
                Assert.That(view.CityEnvironmentView.ArtRoot.localScale, Is.EqualTo(scale));
                Assert.That(Root().GetComponentsInChildren<CityBuildingArtView>(true),
                    Has.Length.EqualTo(5));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrInvalidEnvironmentFallsBackToLegacyBackdrop(bool invalid)
        {
            CampaignDefinition fallback = UnityEngine.Object.Instantiate(campaign);
            cleanup.Add(fallback);
            if (invalid)
            {
                CityEnvironmentArtDefinition bad =
                    ScriptableObject.CreateInstance<CityEnvironmentArtDefinition>();
                cleanup.Add(bad);
                fallback.SetCityEnvironmentArt(bad);
                CampaignValidationReport report = new CampaignValidator().Validate(fallback);
                Assert.That(report.IsValid, Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code ==
                    CampaignValidationCode.InvalidCityEnvironmentArtDefinition), Is.True);
            }
            else fallback.SetCityEnvironmentArt(null);

            CampaignRuntimeView view = BuildView(new CampaignProgressService(fallback), fallback);
            view.ShowMap();
            Assert.That(view.CityEnvironmentView, Is.Null);
            Assert.That(Root().GetComponentsInChildren<CityEnvironmentArtView>(true), Is.Empty);
            RectTransform composition = Composition(view);
            foreach (string road in new[] { "Road Horizontal", "Road Vertical", "Road Diagonal" })
                Assert.That(composition.Find(road).GetComponent<Image>().enabled, Is.True, road);
            Assert.That(composition.Cast<Transform>()
                .Where(child => child.name.StartsWith("City Block ", StringComparison.Ordinal))
                .Select(child => child.GetComponent<Image>()),
                Has.All.Matches<Image>(image => image.enabled));
            Assert.That(composition.GetComponentsInChildren<CityEnergyPathView>(true),
                Has.Length.EqualTo(4));
            Assert.That(composition.GetComponentsInChildren<CityChapterNodeView>(true),
                Has.Length.EqualTo(5));
            Assert.That(composition.GetComponentsInChildren<CityBuildingArtView>(true),
                Has.Length.EqualTo(5));
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(720f, 1280f)]
        public void ProductionEnvironmentPreservesAcceptedPortraitGeometry(float width, float height)
        {
            CampaignRuntimeView view = BuildView(new CampaignProgressService(campaign));
            view.ShowMap();
            Assert.That(view.CityEnvironmentView.ArtRoot.sizeDelta,
                Is.EqualTo(new Vector2(1020f, 1500f)));
            Assert.That(view.CityEnvironmentView.ArtRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
            var expected = new[]
            {
                new Vector2(-310f, -510f), new Vector2(-340f, 0f),
                new Vector2(60f, 420f), new Vector2(370f, 0f),
                new Vector2(40f, -300f)
            };
            for (int index = 0; index < CityMapLayoutCatalog.Production.Entries.Count; index++)
            {
                CityMapLayoutEntry entry = CityMapLayoutCatalog.Production.Entries[index];
                Assert.That(entry.Position, Is.EqualTo(expected[index]));
                Rect rect = CityMapLayoutCatalog.Production.CalculateHitRect(entry, width, height);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(width));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(height));
            }
        }

        [TestCase(NarrativeQaPreset.FreshMap, 0)]
        [TestCase(NarrativeQaPreset.PowerStationRestored, 1)]
        [TestCase(NarrativeQaPreset.SubstationRestored, 2)]
        [TestCase(NarrativeQaPreset.ControlCenterRestored, 3)]
        [TestCase(NarrativeQaPreset.AutomationPlantRestored, 4)]
        [TestCase(NarrativeQaPreset.FirstRestorationPending, 1)]
        [TestCase(NarrativeQaPreset.SubstationRestorationPending, 2)]
        [TestCase(NarrativeQaPreset.ControlCenterRestorationPending, 3)]
        [TestCase(NarrativeQaPreset.AutomationPlantRestorationPending, 4)]
        [TestCase(NarrativeQaPreset.FinalRestorationPending, 5)]
        [TestCase(NarrativeQaPreset.EndingPending, 5)]
        [TestCase(NarrativeQaPreset.PostEndingComplete, 5)]
        public void NarrativeQaPresetsDriveAuthoritativeEnvironmentState(
            NarrativeQaPreset preset, int restoredCount)
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign, preset);
            CampaignRuntimeView view = BuildView(progress);
            view.ShowMap();
            Assert.That(view.CityEnvironmentView.ActiveDistrictCount, Is.EqualTo(restoredCount));
        }

        [Test]
        public void AcceptedEnvironmentAndBuildingHashesRemainUnchanged()
        {
            var environmentHashes = new Dictionary<string, string>
            {
                ["CityEnvironment_Source.png"] = "DCA0BAC7F9FE180DFDE6F60A94E2A516A9344AD44D08FC7ADF48E4A9F75D1648",
                ["CityEnvironment_Base.png"] = "E7B3860AA4174BB5B1182ABB86E8E9583F399C5E913F167668F8683DC35B38C4",
                ["PowerStation_District.png"] = "283AE2CDEB8564EFCFA1FD19C5A058748BD516A945B147929466F6F12E1540C9",
                ["Substation_District.png"] = "F6FB458DBF5965F933E33250D7AFAC204240421B5405F4198523288A4AC85FFE",
                ["ControlCenter_District.png"] = "CD1E39B1E2C68E6A64ED64D1271E3623990E490465F397E614427E0395619763",
                ["AutomationPlant_District.png"] = "A4D3945D0C105780AD10F7DC04E87F3828C9D28945B7A7991D9ABAA4BCBE1778",
                ["CentralGrid_District.png"] = "EC79C4A532C734DF2E8411C55DDE5FF6BBDFCDD013662C03030C670C5F4FA257",
                ["FinalAccent.png"] = "6522414ED8596161207E854B958E8A12C011DF72275C9C3E040A152CE47FE64C"
            };
            const string environmentRoot = "Assets/NeonGrid/Art/CityEnvironment/";
            foreach (KeyValuePair<string, string> item in environmentHashes)
                Assert.That(Hash(environmentRoot + item.Key), Is.EqualTo(item.Value), item.Key);

            var buildingHashes = new Dictionary<string, string>
            {
                ["PowerStation/PowerStation_Base.png"] = "C713B39D3E579A13DDC3F4672896D7F49E32D203C918FDCA5D7F7698E05CC343",
                ["PowerStation/PowerStation_WarmLights.png"] = "5A3DB47C66BF7B402B715CBCFFC697312676AC3F1B3F21C89DF9EAF1EF7B68FD",
                ["PowerStation/PowerStation_Energy.png"] = "807A8A5DE750DE30CDF75A9B0283FFE4EF92037F29E4FEFA8431F0B0911BC259",
                ["PowerStation/PowerStation_Core.png"] = "5595628B97124B194E743D27897841B208D5A2118676A47010B48095DEF3B39D",
                ["Substation/Substation_Base.png"] = "322F6AF5DE965E48E46935BA9146D50A76C24B839FAFB813DFA10670B2BBE7C1",
                ["Substation/Substation_WarmLights.png"] = "A5155443B19F90FC4D90C4631A3B1E661FB8179296A8C344837EE634BEE6D1BE",
                ["Substation/Substation_Energy.png"] = "6486F40D17D9A99BEBA4CA859BC9C656EF18070CE1DA0E6B2345DAA59C8F3622",
                ["Substation/Substation_Core.png"] = "EE2B834F406704E38CAEC3522B9F0CDA47B96EE79C41011DE14F49FA105E2F7B",
                ["ControlCenter/ControlCenter_Base.png"] = "875A3BC234FA15A947A008BA4998B8CB61228C086D846026E0ADACD9ECFD3F1E",
                ["ControlCenter/ControlCenter_WarmLights.png"] = "090FA396DED630BF68EE3412249ECA837A81F25D6D8C6633D443DD8248F58EF4",
                ["ControlCenter/ControlCenter_Energy.png"] = "75000086CEBDE87FCFDF79278F3B3B9990BE4416824DCED3EF07937772E3DD73",
                ["ControlCenter/ControlCenter_Core.png"] = "77FEE7278FA97D448D72645B96E6313C1A85B8B4090013AB8CF252FA7A7618C9",
                ["AutomationPlant/AutomationPlant_Base.png"] = "88812D7AC6883866E37637362BFE84AF28C171B3F44AA13D6F03D397D59D0A9E",
                ["AutomationPlant/AutomationPlant_WarmLights.png"] = "81429C19BEA2366FF0E6D402F87E0B4F9971D65BB9669404C3B151F17F730698",
                ["AutomationPlant/AutomationPlant_Energy.png"] = "D299A92A01F79FEB84B1B7D4D630978847FCC0598F2F943DEE27264018E344EC",
                ["AutomationPlant/AutomationPlant_Core.png"] = "ADAEDDDD837E0C930FDF67B102AFD100874F8DDDA287B732638E98C4246C60DB",
                ["CentralGrid/CentralGrid_Base.png"] = "C42FA4773C5A0430D7E6DF2A559B895F065B66F6181AF52B51BAAAE1858B3ECB",
                ["CentralGrid/CentralGrid_WarmLights.png"] = "73705CEA9FA32291F8E9710C3D41502B137B4464833ACB01B67034D0481E08E9",
                ["CentralGrid/CentralGrid_Energy.png"] = "3C68055D61D9BB5F49A4EA58D013A92989657F9C28CD9F91BDF2D1DC96B3035C",
                ["CentralGrid/CentralGrid_Core.png"] = "8C64BA9AC693E88F3A7D233BC0AB92B3809A00A333F9AC77F37E83BE34812F2B"
            };
            const string buildingRoot = "Assets/NeonGrid/Art/CityBuildings/";
            foreach (KeyValuePair<string, string> item in buildingHashes)
                Assert.That(Hash(buildingRoot + item.Key), Is.EqualTo(item.Value), item.Key);
        }

        [Test]
        public void ProductionSceneAndBuildSettingsRemainAuthoritativeAndPrototypeFree()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Does.Contain(ScenePath));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Has.None.EqualTo(
                M15CityEnvironmentArtPrototypeBuilder.ScenePath));
            string[] productionDependencies = AssetDatabase.GetDependencies(ScenePath, true);
            Assert.That(productionDependencies, Has.None.EqualTo(
                M15CityEnvironmentArtPrototypeBuilder.ScenePath));
        }

        private CampaignRuntimeView BuildView(CampaignProgressService progress,
            CampaignDefinition definition = null)
        {
            var root = new GameObject("E3C Production Environment Test");
            cleanup.Add(root);
            CampaignRuntimeView view = root.AddComponent<CampaignRuntimeView>();
            CampaignDefinition target = definition ?? campaign;
            view.Build(target, progress, _ => { }, _ => { }, () => { },
                target.CampaignUiTheme);
            return view;
        }

        private GameObject Root() => cleanup.OfType<GameObject>().Last();

        private static RectTransform Composition(CampaignRuntimeView view) => view
            .GetComponentsInChildren<RectTransform>(true)
            .Single(rect => rect.name == "City Composition");

        private static CityChapterNodeView Node(CampaignRuntimeView view, string chapterId) => view
            .GetComponentsInChildren<CityChapterNodeView>(true)
            .Single(node => node.ChapterId == chapterId);

        private CampaignProgressService RestoredFirst(int count, int stars)
        {
            var progress = new CampaignProgressService(campaign);
            for (int index = 0; index < count; index++)
                CompleteLevels(progress, campaign.Chapters[index],
                    campaign.Chapters[index].Levels.Count, stars);
            return progress;
        }

        private static void CompleteLevels(CampaignProgressService progress,
            CampaignChapterDefinition chapter, int count, int stars)
        {
            for (int index = 0; index < count; index++)
            {
                CampaignLevelEntry entry = chapter.Levels[index];
                Assert.That(progress.RecordCompletion(entry.LevelId, Result(entry, stars)).Accepted,
                    Is.True, entry.LevelId);
            }
        }

        private static SessionCompletionResult Result(CampaignLevelEntry entry, int stars) =>
            new SessionCompletionResult(entry.LevelDefinition, 1, 1f,
                entry.AuthoredOptimalMoves, false,
                new StarEvaluationResult(StarEvaluationStatus.Rated, stars));

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty);
        }

        private void DestroyRoots()
        {
            foreach (GameObject root in cleanup.OfType<GameObject>().ToArray())
            {
                cleanup.Remove(root);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class TemporarySaveCleanup : ScriptableObject
        {
            private string path;

            public TemporarySaveCleanup Initialize(string savePath)
            {
                path = savePath;
                return this;
            }

            private void OnDestroy()
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
        }
    }
}

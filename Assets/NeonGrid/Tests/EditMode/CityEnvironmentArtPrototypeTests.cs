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
    public sealed class CityEnvironmentArtPrototypeTests
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/NeonGrid_Main.asset";
        private static readonly string[] ChapterIds =
        {
            CityEnvironmentArtDefinition.PowerStationId,
            CityEnvironmentArtDefinition.SubstationId,
            CityEnvironmentArtDefinition.ControlCenterId,
            CityEnvironmentArtDefinition.AutomationPlantId,
            CityEnvironmentArtDefinition.CentralGridId
        };

        private readonly List<GameObject> cleanup = new List<GameObject>();
        private CampaignDefinition campaign;
        private CityEnvironmentArtDefinition definition;

        [SetUp]
        public void SetUp()
        {
            campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            definition = AssetDatabase.LoadAssetAtPath<CityEnvironmentArtDefinition>(
                M15CityEnvironmentArtPrototypeBuilder.DefinitionPath);
            Assert.That(campaign, Is.Not.Null);
            Assert.That(definition, Is.Not.Null,
                "Run Neon Grid/M15/Build City Environment Art Prototype.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject root in cleanup)
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            cleanup.Clear();
        }

        [Test]
        public void Definition_ValidatesBaseFiveStableDistrictsAndRegisteredLayers()
        {
            Assert.That(definition.IsConfigured, Is.True);
            Assert.That(definition.BaseCity, Is.Not.Null);
            Assert.That(definition.Districts, Has.Count.EqualTo(5));
            Assert.That(definition.Districts.Select(layer => layer.ChapterId),
                Is.EqualTo(ChapterIds));
            foreach (CityEnvironmentDistrictLayer layer in definition.Districts)
            {
                Assert.That(layer.Overlay, Is.Not.Null, layer.ChapterId);
                Assert.That(layer.Overlay.rect, Is.EqualTo(definition.BaseCity.rect));
                Assert.That(layer.Overlay.pivot, Is.EqualTo(definition.BaseCity.pivot));
            }
            Assert.That(definition.FinalAccent.rect, Is.EqualTo(definition.BaseCity.rect));
        }

        [Test]
        public void PrototypeSceneExistsAndIsExcludedFromBuildSettings()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                M15CityEnvironmentArtPrototypeBuilder.ScenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Has.None.EqualTo(
                M15CityEnvironmentArtPrototypeBuilder.ScenePath));
        }

        [Test]
        public void EnvironmentLayersAreStaticNonRaycastAndUseOneReusableHierarchy()
        {
            CityEnvironmentArtView environment = BuildEnvironment(
                new CampaignProgressService(campaign), out _);
            Assert.That(environment.LayerCount, Is.EqualTo(7));
            Assert.That(environment.ArtRoot.GetComponentsInChildren<Image>(true),
                Has.All.Matches<Image>(image => !image.raycastTarget));
            Assert.That(typeof(CityEnvironmentArtView).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Is.Null);
            Assert.That(environment.GetComponents<Canvas>(), Is.Empty);
        }

        [Test]
        public void EnvironmentIsBehindAmbientPathsBuildingsAndLabels()
        {
            CityEnvironmentArtView environment = BuildEnvironment(
                new CampaignProgressService(campaign), out CampaignRuntimeView map);
            RectTransform composition = environment.ArtRoot.parent as RectTransform;
            Assert.That(environment.ArtRoot.GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(composition.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "Road Horizontal").GetSiblingIndex(),
                Is.GreaterThan(environment.ArtRoot.GetSiblingIndex()));
            Assert.That(composition.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name.StartsWith("Energy Path", StringComparison.Ordinal))
                .All(rect => rect.GetSiblingIndex() > environment.ArtRoot.GetSiblingIndex()), Is.True);
            foreach (CityChapterNodeView node in map.GetComponentsInChildren<CityChapterNodeView>(true))
            {
                Assert.That(node.transform.GetSiblingIndex(),
                    Is.GreaterThan(environment.ArtRoot.GetSiblingIndex()), node.ChapterId);
                Assert.That(node.BuildingArtView, Is.Not.Null, node.ChapterId);
                Assert.That(node.Label.transform.IsChildOf(node.transform), Is.True, node.ChapterId);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AuthoritativeRestoredChaptersEnableExactlyTheirDistricts(int restoredCount)
        {
            CampaignProgressService progress = ProgressWithRestoredChapters(restoredCount, 2);
            CityEnvironmentArtView environment = BuildEnvironment(progress, out _);
            Assert.That(environment.ActiveDistrictCount, Is.EqualTo(restoredCount));
            for (int index = 0; index < ChapterIds.Length; index++)
                Assert.That(environment.IsDistrictActive(ChapterIds[index]),
                    Is.EqualTo(index < restoredCount), ChapterIds[index]);
            Image accent = environment.ArtRoot.Find("Final City Accent").GetComponent<Image>();
            Assert.That(accent.enabled, Is.EqualTo(restoredCount == 5));
        }

        [TestCase("power_station")]
        [TestCase("substation")]
        [TestCase("control_center")]
        [TestCase("automation_plant")]
        [TestCase("central_grid")]
        public void PartialChapterProgressDoesNotEnableDistrict(string chapterId)
        {
            int chapterIndex = Array.IndexOf(ChapterIds, chapterId);
            CampaignProgressService progress = ProgressWithRestoredChapters(chapterIndex, 3);
            Complete(progress, campaign.Chapters[chapterIndex], 9, 3);
            Assert.That(progress.GetChapterState(chapterId), Is.Not.EqualTo(
                CampaignChapterState.Restored));
            CityEnvironmentArtView environment = BuildEnvironment(progress, out _);
            Assert.That(environment.IsDistrictActive(chapterId), Is.False);
        }

        [Test]
        public void StarsDoNotAffectRestoredEnvironmentState()
        {
            CityEnvironmentArtView oneStar = BuildEnvironment(
                ProgressWithRestoredChapters(2, 1), out _);
            CityEnvironmentArtView threeStars = BuildEnvironment(
                ProgressWithRestoredChapters(2, 3), out _);
            Assert.That(oneStar.ActiveDistrictCount, Is.EqualTo(2));
            Assert.That(threeStars.ActiveDistrictCount, Is.EqualTo(2));
            Assert.That(ChapterIds.Select(oneStar.IsDistrictActive),
                Is.EqualTo(ChapterIds.Select(threeStars.IsDistrictActive)));
        }

        [Test]
        public void SaveLoadDerivesEnvironmentWithoutSaveSchemaChange()
        {
            CampaignProgressService progress = ProgressWithRestoredChapters(3, 2);
            string path = Path.Combine(Path.GetTempPath(), "NeonGridE3A",
                Guid.NewGuid().ToString("N"), "progress.json");
            try
            {
                var store = new CampaignSaveStore(path);
                Assert.That(store.Save(progress).Succeeded, Is.True);
                CampaignLoadResult load = store.Load(campaign);
                CityEnvironmentArtView environment = BuildEnvironment(load.Progress, out _);
                Assert.That(environment.ActiveDistrictCount, Is.EqualTo(3));
                string[] fields = typeof(CampaignSaveData).GetFields(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic).Select(field => field.Name)
                    .ToArray();
                Assert.That(fields, Has.None.Contains("environment"));
                Assert.That(fields, Has.None.Contains("district"));
            }
            finally
            {
                string directory = Path.GetDirectoryName(path);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void RepeatedStateChangesDoNotGrowHierarchy()
        {
            CityEnvironmentArtView environment = BuildEnvironment(
                new CampaignProgressService(campaign), out _);
            int rootId = environment.ArtRoot.GetInstanceID();
            int[] layerIds = environment.ArtRoot.Cast<Transform>()
                .Select(child => child.GetInstanceID()).ToArray();
            for (int pass = 0; pass < 10; pass++)
            {
                environment.PresentRestoredDistricts(ChapterIds.Take(pass % 6));
                Assert.That(environment.ArtRoot.GetInstanceID(), Is.EqualTo(rootId));
                Assert.That(environment.ArtRoot.childCount, Is.EqualTo(7));
                Assert.That(environment.ArtRoot.Cast<Transform>()
                    .Select(child => child.GetInstanceID()), Is.EqualTo(layerIds));
            }
        }

        [Test]
        public void RepeatedMapSelectorCyclesDoNotDuplicateEnvironmentRoot()
        {
            CityEnvironmentArtView environment = BuildEnvironment(
                new CampaignProgressService(campaign), out CampaignRuntimeView map);
            int rootId = environment.ArtRoot.GetInstanceID();
            Transform host = environment.ArtRoot.parent;
            for (int cycle = 0; cycle < 10; cycle++)
            {
                map.ShowChapter(campaign.Chapters[0]);
                map.ShowMap();
                Assert.That(host.Cast<Transform>().Count(child =>
                    child.name == "City Environment Art"), Is.EqualTo(1));
                Assert.That(environment.ArtRoot.GetInstanceID(), Is.EqualTo(rootId));
            }
        }

        [Test]
        public void AcceptedMapNodeGeometryAndLabelSafeRegionsRemainUnchanged()
        {
            CityEnvironmentArtView environment = BuildEnvironment(
                new CampaignProgressService(campaign), out CampaignRuntimeView map);
            var expected = new Dictionary<string, (Vector2 position, Vector2 hit)>
            {
                ["power_station"] = (new Vector2(-310f, -510f), new Vector2(280f, 280f)),
                ["substation"] = (new Vector2(-340f, 0f), new Vector2(280f, 280f)),
                ["control_center"] = (new Vector2(60f, 420f), new Vector2(280f, 300f)),
                ["automation_plant"] = (new Vector2(370f, 0f), new Vector2(280f, 280f)),
                ["central_grid"] = (new Vector2(40f, -300f), new Vector2(350f, 330f))
            };
            foreach (CityChapterNodeView node in map.GetComponentsInChildren<CityChapterNodeView>(true))
            {
                Assert.That(node.HitArea.anchoredPosition, Is.EqualTo(expected[node.ChapterId].position));
                Assert.That(node.HitArea.sizeDelta, Is.EqualTo(expected[node.ChapterId].hit));
                Bounds art = BoundsIn(node.BuildingArtView.ArtRoot, node.transform);
                Bounds label = BoundsIn(node.Label.rectTransform, node.transform);
                Assert.That(art.min.y, Is.GreaterThan(label.max.y + 6f), node.ChapterId);
            }
            Assert.That(environment.ArtRoot.sizeDelta, Is.EqualTo(new Vector2(1020f, 1500f)));
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(720f, 1280f)]
        public void EnvironmentAndAcceptedNodesFitPortraitLayouts(float width, float height)
        {
            CityEnvironmentArtView environment = BuildEnvironment(
                new CampaignProgressService(campaign), out _);
            foreach (CityMapLayoutEntry entry in CityMapLayoutCatalog.Production.Entries)
            {
                Rect rect = CityMapLayoutCatalog.Production.CalculateHitRect(entry, width, height);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0f), entry.ChapterId);
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(width), entry.ChapterId);
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0f), entry.ChapterId);
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(height), entry.ChapterId);
            }
            Assert.That(environment.ArtRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(environment.ArtRoot.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(environment.ArtRoot.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void ProductionCampaignAndRuntimeRemainEnvironmentPrototypeFree()
        {
            Assert.That(typeof(CampaignDefinition).GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(CityEnvironmentArtDefinition)), Is.False);
            string[] dependencies = AssetDatabase.GetDependencies(CampaignPath, true);
            Assert.That(dependencies, Has.None.EqualTo(
                M15CityEnvironmentArtPrototypeBuilder.DefinitionPath));
            Assert.That(dependencies.Any(path => path.StartsWith(
                M15CityEnvironmentArtPrototypeBuilder.DirectoryPath + "/",
                StringComparison.Ordinal)), Is.False);
            GameObject root = new GameObject("Production isolation");
            cleanup.Add(root);
            CampaignRuntimeView map = root.AddComponent<CampaignRuntimeView>();
            map.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { },
                () => { }, campaign.CampaignUiTheme);
            map.ShowMap();
            Assert.That(root.GetComponentsInChildren<CityEnvironmentArtView>(true), Is.Empty);
        }

        [Test]
        public void AcceptedFinalBuildingPngsRemainByteIdentical()
        {
            var expected = new Dictionary<string, string>
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
            const string root = "Assets/NeonGrid/Art/CityBuildings/";
            foreach (KeyValuePair<string, string> item in expected)
                Assert.That(Hash(root + item.Key), Is.EqualTo(item.Value), item.Key);
        }

        private CityEnvironmentArtView BuildEnvironment(CampaignProgressService progress,
            out CampaignRuntimeView map)
        {
            GameObject root = new GameObject("E3A Test Map");
            cleanup.Add(root);
            map = root.AddComponent<CampaignRuntimeView>();
            map.Build(campaign, progress, _ => { }, _ => { }, () => { },
                campaign.CampaignUiTheme);
            map.ShowMap();
            RectTransform composition = map.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "City Composition");
            CityEnvironmentArtView environment = root.AddComponent<CityEnvironmentArtView>();
            environment.Initialize(definition, composition, 1);
            environment.Present(campaign, progress);
            return environment;
        }

        private CampaignProgressService ProgressWithRestoredChapters(int restoredCount, int stars)
        {
            var progress = new CampaignProgressService(campaign);
            for (int index = 0; index < restoredCount; index++)
                Complete(progress, campaign.Chapters[index], campaign.Chapters[index].Levels.Count,
                    stars);
            return progress;
        }

        private static void Complete(CampaignProgressService progress,
            CampaignChapterDefinition chapter, int count, int stars)
        {
            for (int index = 0; index < count; index++)
            {
                CampaignLevelEntry entry = chapter.Levels[index];
                Assert.That(progress.RecordCompletion(entry.LevelId,
                    new SessionCompletionResult(entry.LevelDefinition, 1, 1f,
                        entry.AuthoredOptimalMoves, false,
                        new StarEvaluationResult(StarEvaluationStatus.Rated, stars))).Accepted,
                    Is.True, entry.LevelId);
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

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty);
        }
    }
}

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
    public sealed class CityEnvironmentFinalArtPreparationTests
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/NeonGrid_Main.asset";
        private const string SourceHash =
            "DCA0BAC7F9FE180DFDE6F60A94E2A516A9344AD44D08FC7ADF48E4A9F75D1648";
        private static readonly string[] ChapterIds =
        {
            CityEnvironmentArtDefinition.PowerStationId,
            CityEnvironmentArtDefinition.SubstationId,
            CityEnvironmentArtDefinition.ControlCenterId,
            CityEnvironmentArtDefinition.AutomationPlantId,
            CityEnvironmentArtDefinition.CentralGridId
        };
        private static readonly string[] RuntimeLayerPaths =
        {
            M15CityEnvironmentFinalArtPreparation.BasePath,
            M15CityEnvironmentFinalArtPreparation.PowerStationPath,
            M15CityEnvironmentFinalArtPreparation.SubstationPath,
            M15CityEnvironmentFinalArtPreparation.ControlCenterPath,
            M15CityEnvironmentFinalArtPreparation.AutomationPlantPath,
            M15CityEnvironmentFinalArtPreparation.CentralGridPath,
            M15CityEnvironmentFinalArtPreparation.FinalAccentPath
        };

        private readonly List<GameObject> cleanup = new List<GameObject>();
        private CampaignDefinition campaign;
        private CityEnvironmentArtDefinition definition;

        [SetUp]
        public void SetUp()
        {
            campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            definition = AssetDatabase.LoadAssetAtPath<CityEnvironmentArtDefinition>(
                M15CityEnvironmentFinalArtPreparation.DefinitionPath);
            Assert.That(campaign, Is.Not.Null);
            Assert.That(definition, Is.Not.Null,
                "Run Neon Grid/M15/Prepare Final City Environment Art.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject root in cleanup)
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            cleanup.Clear();
        }

        [Test]
        public void ApprovedSourceAndPreparedLayersRemainByteDeterministic()
        {
            var expected = new Dictionary<string, string>
            {
                [M15CityEnvironmentFinalArtPreparation.SourcePath] = SourceHash,
                [M15CityEnvironmentFinalArtPreparation.BasePath] =
                    "E7B3860AA4174BB5B1182ABB86E8E9583F399C5E913F167668F8683DC35B38C4",
                [M15CityEnvironmentFinalArtPreparation.PowerStationPath] =
                    "283AE2CDEB8564EFCFA1FD19C5A058748BD516A945B147929466F6F12E1540C9",
                [M15CityEnvironmentFinalArtPreparation.SubstationPath] =
                    "F6FB458DBF5965F933E33250D7AFAC204240421B5405F4198523288A4AC85FFE",
                [M15CityEnvironmentFinalArtPreparation.ControlCenterPath] =
                    "CD1E39B1E2C68E6A64ED64D1271E3623990E490465F397E614427E0395619763",
                [M15CityEnvironmentFinalArtPreparation.AutomationPlantPath] =
                    "A4D3945D0C105780AD10F7DC04E87F3828C9D28945B7A7991D9ABAA4BCBE1778",
                [M15CityEnvironmentFinalArtPreparation.CentralGridPath] =
                    "EC79C4A532C734DF2E8411C55DDE5FF6BBDFCDD013662C03030C670C5F4FA257",
                [M15CityEnvironmentFinalArtPreparation.FinalAccentPath] =
                    "6522414ED8596161207E854B958E8A12C011DF72275C9C3E040A152CE47FE64C"
            };
            foreach (KeyValuePair<string, string> item in expected)
            {
                Assert.That(File.Exists(item.Key), Is.True, item.Key);
                Assert.That(Hash(item.Key), Is.EqualTo(item.Value), item.Key);
            }
        }

        [Test]
        public void PreparedLayersShareExactSourceCanvasAlphaRegistration()
        {
            Texture2D source = LoadPng(M15CityEnvironmentFinalArtPreparation.SourcePath);
            Texture2D cityBase = LoadPng(M15CityEnvironmentFinalArtPreparation.BasePath);
            try
            {
                Assert.That(source.width, Is.EqualTo(1024));
                Assert.That(source.height, Is.EqualTo(1536));
                foreach (string path in RuntimeLayerPaths)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(1024f, 1536f)), path);
                    Assert.That(sprite.pivot, Is.EqualTo(new Vector2(512f, 768f)), path);
                }
                Assert.That(cityBase.GetPixels32().Select(pixel => pixel.a),
                    Is.EqualTo(source.GetPixels32().Select(pixel => pixel.a)),
                    "Base must preserve source silhouette/registration alpha exactly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(cityBase);
            }
        }

        [TestCaseSource(nameof(RuntimeLayerPaths))]
        public void RuntimeLayerImporterMatchesFinalArtContract(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            Assert.That(importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(settings.spriteAlignment, Is.EqualTo((int)SpriteAlignment.Center));
            Assert.That(settings.spriteGenerateFallbackPhysicsShape, Is.False);
            TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
            Assert.That(platform.format, Is.EqualTo(TextureImporterFormat.RGBA32));
            Assert.That(platform.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [Test]
        public void FinalDefinitionValidatesWithExactUniqueStableIds()
        {
            Assert.That(definition.IsConfigured, Is.True);
            Assert.That(definition.BaseCity, Is.Not.Null);
            Assert.That(definition.FinalAccent, Is.Not.Null);
            Assert.That(definition.Districts.Select(layer => layer.ChapterId),
                Is.EqualTo(ChapterIds));
            Assert.That(definition.Districts.Select(layer => layer.ChapterId).Distinct().Count(),
                Is.EqualTo(5));
        }

        [Test]
        public void IsolatedSceneBindsFinalDefinitionAndRemainsExcludedFromBuild()
        {
            string[] dependencies = AssetDatabase.GetDependencies(
                M15CityEnvironmentArtPrototypeBuilder.ScenePath, true);
            Assert.That(dependencies, Does.Contain(
                M15CityEnvironmentFinalArtPreparation.DefinitionPath));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Has.None.EqualTo(
                M15CityEnvironmentArtPrototypeBuilder.ScenePath));
        }

        [Test]
        public void BaseIsAlwaysPresentAndEnvironmentNeverInterceptsInput()
        {
            CityEnvironmentArtView environment = BuildEnvironment(
                new CampaignProgressService(campaign), out _);
            Image baseImage = environment.ArtRoot.Find("Base City").GetComponent<Image>();
            Assert.That(baseImage.enabled, Is.True);
            Assert.That(environment.ArtRoot.GetComponentsInChildren<Image>(true),
                Has.All.Matches<Image>(image => !image.raycastTarget));
            environment.PresentRestoredDistricts(ChapterIds);
            Assert.That(baseImage.enabled, Is.True);
            environment.SetVisible(false);
            Assert.That(environment.IsVisible, Is.False);
            environment.SetVisible(true);
            Assert.That(environment.IsVisible, Is.True);
            Assert.That(baseImage.enabled, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RestoredStateCombinationsEnableExactFinalDistrictLayers(int restoredCount)
        {
            CampaignProgressService progress = RestoredChapters(restoredCount, 2);
            CityEnvironmentArtView environment = BuildEnvironment(progress, out _);
            Assert.That(environment.ActiveDistrictCount, Is.EqualTo(restoredCount));
            for (int index = 0; index < ChapterIds.Length; index++)
                Assert.That(environment.IsDistrictActive(ChapterIds[index]),
                    Is.EqualTo(index < restoredCount), ChapterIds[index]);
            Assert.That(environment.ArtRoot.Find("Final City Accent").GetComponent<Image>().enabled,
                Is.EqualTo(restoredCount == 5));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void PartialProgressDoesNotActivateCurrentDistrict(int chapterIndex)
        {
            CampaignProgressService progress = RestoredChapters(chapterIndex, 3);
            Complete(progress, campaign.Chapters[chapterIndex], 9, 3);
            CityEnvironmentArtView environment = BuildEnvironment(progress, out _);
            Assert.That(environment.IsDistrictActive(ChapterIds[chapterIndex]), Is.False);
        }

        [Test]
        public void StarsDoNotAffectFinalEnvironmentActivation()
        {
            CityEnvironmentArtView oneStar = BuildEnvironment(RestoredChapters(3, 1), out _);
            CityEnvironmentArtView threeStars = BuildEnvironment(RestoredChapters(3, 3), out _);
            Assert.That(oneStar.ActiveDistrictCount, Is.EqualTo(3));
            Assert.That(threeStars.ActiveDistrictCount, Is.EqualTo(3));
            Assert.That(ChapterIds.Select(oneStar.IsDistrictActive),
                Is.EqualTo(ChapterIds.Select(threeStars.IsDistrictActive)));
        }

        [Test]
        public void FinalOverlaysAreLocalizedAndAccentIsSubordinate()
        {
            int finalMaximum = MaximumAlpha(M15CityEnvironmentFinalArtPreparation.FinalAccentPath);
            foreach (string path in RuntimeLayerPaths.Skip(1).Take(5))
            {
                AlphaBounds bounds = BoundsOfAlpha(path);
                Assert.That(bounds.PixelCount, Is.GreaterThan(200), path);
                Assert.That(bounds.PixelCount, Is.LessThan(1024 * 1536 / 8), path);
                Assert.That(MaximumAlpha(path), Is.GreaterThan(finalMaximum), path);
            }
            AlphaBounds control = BoundsOfAlpha(
                M15CityEnvironmentFinalArtPreparation.ControlCenterPath);
            AlphaBounds central = BoundsOfAlpha(
                M15CityEnvironmentFinalArtPreparation.CentralGridPath);
            Assert.That(PixelToMapY(control.MaxY), Is.LessThanOrEqualTo(525f));
            Assert.That(PixelToMapY(central.MinY), Is.GreaterThanOrEqualTo(-405f));
        }

        [Test]
        public void DerivedLayersNeverIntroduceOpaqueBlackQuietZones()
        {
            Texture2D source = LoadPng(M15CityEnvironmentFinalArtPreparation.SourcePath);
            Texture2D cityBase = LoadPng(M15CityEnvironmentFinalArtPreparation.BasePath);
            try
            {
                Color32[] sourcePixels = source.GetPixels32();
                Color32[] basePixels = cityBase.GetPixels32();
                int introducedBlackPixels = 0;
                for (int index = 0; index < sourcePixels.Length; index++)
                    if (basePixels[index].a > 0 && MaximumRgb(basePixels[index]) <= 8 &&
                        MaximumRgb(sourcePixels[index]) > 8)
                        introducedBlackPixels++;
                Assert.That(introducedBlackPixels, Is.Zero,
                    "The inactive derivation must preserve visible source structure.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(cityBase);
            }

            foreach (string path in RuntimeLayerPaths.Skip(1))
            {
                Texture2D layer = LoadPng(path);
                try
                {
                    Assert.That(layer.GetPixels32().Count(pixel =>
                            pixel.a > 0 && MaximumRgb(pixel) <= 8),
                        Is.Zero, path + " must not contain visible black mask pixels.");
                }
                finally { UnityEngine.Object.DestroyImmediate(layer); }
            }
        }

        [Test]
        public void IsolatedPrototypeSuppressesOnlyRedundantLegacyBackdropGeometry()
        {
            GameObject root = new GameObject("E3B.1 Prototype Composition Test");
            cleanup.Add(root);
            CityEnvironmentArtPrototypeController controller =
                root.AddComponent<CityEnvironmentArtPrototypeController>();
            controller.SetData(campaign, definition);
            controller.Initialize();

            RectTransform composition = controller.MapView
                .GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "City Composition");
            Image[] legacyBlocks = composition.Cast<Transform>()
                .Where(child => child.name.StartsWith("City Block ",
                    StringComparison.Ordinal))
                .Select(child => child.GetComponent<Image>())
                .ToArray();
            Assert.That(legacyBlocks, Has.Length.EqualTo(13));
            Assert.That(legacyBlocks, Has.All.Matches<Image>(image => !image.enabled));
            Assert.That(composition.Find("Road Horizontal").GetComponent<Image>().enabled,
                Is.False);
            Assert.That(composition.Find("Road Vertical").GetComponent<Image>().enabled,
                Is.False);
            Assert.That(composition.Find("Road Diagonal").GetComponent<Image>().enabled,
                Is.False);

            CityEnergyPathView[] energyPaths = composition
                .GetComponentsInChildren<CityEnergyPathView>(true);
            Assert.That(energyPaths, Has.Length.EqualTo(4));
            Assert.That(energyPaths, Has.All.Matches<CityEnergyPathView>(path =>
                path.gameObject.activeInHierarchy && path.GetComponentsInChildren<Image>(true)
                    .All(image => image.enabled)));

            CityChapterNodeView[] nodes = composition
                .GetComponentsInChildren<CityChapterNodeView>(true);
            Assert.That(nodes, Has.Length.EqualTo(5));
            Assert.That(nodes, Has.All.Matches<CityChapterNodeView>(node =>
                node.gameObject.activeInHierarchy && node.Label.enabled &&
                node.BuildingArtView != null && node.BuildingArtView.UsesArt));
            Assert.That(controller.EnvironmentView.ArtRoot.GetSiblingIndex(), Is.EqualTo(1));

            GameObject productionRoot = new GameObject("E3B.2 Production Isolation Test");
            cleanup.Add(productionRoot);
            CampaignRuntimeView production = productionRoot.AddComponent<CampaignRuntimeView>();
            production.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { },
                () => { }, campaign.CampaignUiTheme);
            production.ShowMap();
            RectTransform productionComposition = production
                .GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "City Composition");
            Assert.That(productionComposition.Find("Road Horizontal").GetComponent<Image>().enabled,
                Is.True);
            Assert.That(productionComposition.Find("Road Vertical").GetComponent<Image>().enabled,
                Is.True);
            Assert.That(productionComposition.Find("Road Diagonal").GetComponent<Image>().enabled,
                Is.True);
            Assert.That(productionComposition.Cast<Transform>()
                .Where(child => child.name.StartsWith("City Block ", StringComparison.Ordinal))
                .Select(child => child.GetComponent<Image>()),
                Has.All.Matches<Image>(image => image.enabled));
        }

        [Test]
        public void LifecycleAndSiblingOrderRemainStableAcrossSelectorCycles()
        {
            CityEnvironmentArtView environment = BuildEnvironment(
                new CampaignProgressService(campaign), out CampaignRuntimeView map);
            Transform host = environment.ArtRoot.parent;
            int rootId = environment.ArtRoot.GetInstanceID();
            Assert.That(environment.ArtRoot.GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(host.Find("Road Horizontal").GetSiblingIndex(),
                Is.GreaterThan(environment.ArtRoot.GetSiblingIndex()));
            foreach (CityChapterNodeView node in map.GetComponentsInChildren<CityChapterNodeView>(true))
                Assert.That(node.transform.GetSiblingIndex(),
                    Is.GreaterThan(environment.ArtRoot.GetSiblingIndex()), node.ChapterId);
            for (int cycle = 0; cycle < 10; cycle++)
            {
                map.ShowChapter(campaign.Chapters[0]);
                map.ShowMap();
                Assert.That(host.Cast<Transform>().Count(child =>
                    child.name == "City Environment Art"), Is.EqualTo(1));
                Assert.That(environment.ArtRoot.GetInstanceID(), Is.EqualTo(rootId));
                Assert.That(environment.ArtRoot.childCount, Is.EqualTo(7));
            }
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(720f, 1280f)]
        public void FinalEnvironmentPreservesPortraitGeometry(float width, float height)
        {
            CityEnvironmentArtView environment = BuildEnvironment(
                new CampaignProgressService(campaign), out _);
            Assert.That(environment.ArtRoot.sizeDelta, Is.EqualTo(new Vector2(1020f, 1500f)));
            foreach (CityMapLayoutEntry entry in CityMapLayoutCatalog.Production.Entries)
            {
                Rect rect = CityMapLayoutCatalog.Production.CalculateHitRect(entry, width, height);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0f), entry.ChapterId);
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(width), entry.ChapterId);
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0f), entry.ChapterId);
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(height), entry.ChapterId);
            }
        }

        [Test]
        public void ProductionCampaignAndRuntimeHaveNoFinalEnvironmentBinding()
        {
            string[] campaignDependencies = AssetDatabase.GetDependencies(CampaignPath, true);
            Assert.That(campaignDependencies, Has.None.EqualTo(
                M15CityEnvironmentFinalArtPreparation.DefinitionPath));
            Assert.That(typeof(CampaignDefinition).GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(CityEnvironmentArtDefinition)), Is.False);
            GameObject root = new GameObject("E3B Production Isolation");
            cleanup.Add(root);
            CampaignRuntimeView map = root.AddComponent<CampaignRuntimeView>();
            map.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { },
                () => { }, campaign.CampaignUiTheme);
            map.ShowMap();
            Assert.That(root.GetComponentsInChildren<CityEnvironmentArtView>(true), Is.Empty);
        }

        [Test]
        public void AcceptedBuildingFinalArtHashesRemainUnchanged()
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
            GameObject root = new GameObject("E3B Final Environment Test");
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

        private CampaignProgressService RestoredChapters(int count, int stars)
        {
            var progress = new CampaignProgressService(campaign);
            for (int index = 0; index < count; index++)
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

        private static Texture2D LoadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path), false), Is.True, path);
            return texture;
        }

        private static int MaximumAlpha(string path)
        {
            Texture2D texture = LoadPng(path);
            try { return texture.GetPixels32().Max(pixel => (int)pixel.a); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static AlphaBounds BoundsOfAlpha(string path)
        {
            Texture2D texture = LoadPng(path);
            try
            {
                Color32[] pixels = texture.GetPixels32();
                int minX = texture.width;
                int minY = texture.height;
                int maxX = -1;
                int maxY = -1;
                int count = 0;
                for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 0)
                    {
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                        count++;
                    }
                return new AlphaBounds(minX, minY, maxX, maxY, count);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static float PixelToMapY(int pixelY) => pixelY / 1535f * 1500f - 750f;

        private static int MaximumRgb(Color32 pixel) =>
            Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty);
        }

        private readonly struct AlphaBounds
        {
            public AlphaBounds(int minX, int minY, int maxX, int maxY, int pixelCount)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
                PixelCount = pixelCount;
            }
            public int MinX { get; }
            public int MinY { get; }
            public int MaxX { get; }
            public int MaxY { get; }
            public int PixelCount { get; }
        }
    }
}

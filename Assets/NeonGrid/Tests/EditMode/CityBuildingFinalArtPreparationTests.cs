using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Editor;
using NeonGrid.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class CityBuildingFinalArtPreparationTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private CityBuildingArtDefinition power;
        private CityBuildingArtDefinition central;
        private CityBuildingArtDefinition prototypePower;
        private CityBuildingArtDefinition prototypeCentral;
        private CampaignDefinition campaign;

        [SetUp]
        public void SetUp()
        {
            power = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15CityBuildingFinalArtPreparation.PowerDefinitionPath);
            central = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15CityBuildingFinalArtPreparation.CentralDefinitionPath);
            prototypePower = Prototype("PowerStation");
            prototypeCentral = Prototype("CentralGrid");
            campaign = Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void FinalDefinitions_AreSeparatePreparedAssetsWithStableChapterIds()
        {
            Assert.That(power, Is.Not.Null);
            Assert.That(central, Is.Not.Null);
            Assert.That(power, Is.Not.SameAs(prototypePower));
            Assert.That(central, Is.Not.SameAs(prototypeCentral));
            Assert.That(power.ChapterId, Is.EqualTo("power_station"));
            Assert.That(central.ChapterId, Is.EqualTo("central_grid"));
            Assert.That(campaign.Chapters.Any(chapter => chapter.ChapterId == power.ChapterId), Is.True);
            Assert.That(campaign.Chapters.Any(chapter => chapter.ChapterId == central.ChapterId), Is.True);
        }

        [Test]
        public void MissingAuthoredPngs_AreExplicitAndNeverReplacedByPrototypeSprites()
        {
            string[] expected = M15CityBuildingFinalArtPreparation.PowerLayerPaths
                .Concat(M15CityBuildingFinalArtPreparation.CentralLayerPaths).ToArray();
            IReadOnlyList<string> missing = M15CityBuildingFinalArtPreparation.MissingFinalArtFiles();
            Assert.That(missing, Is.EquivalentTo(expected.Where(path => !File.Exists(path))));
            CityBuildingArtDefinition[] definitions = { power, central };
            string[][] paths =
            {
                M15CityBuildingFinalArtPreparation.PowerLayerPaths,
                M15CityBuildingFinalArtPreparation.CentralLayerPaths
            };
            for (int building = 0; building < definitions.Length; building++)
            for (int layer = 0; layer < 4; layer++)
            {
                Sprite sprite = definitions[building].GetSprite(layer);
                if (File.Exists(paths[building][layer]))
                    Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(paths[building][layer]));
                else
                    Assert.That(sprite, Is.Null,
                        "Missing final art must never be substituted with prototype art.");
            }
        }

        [TestCase("PowerStation/PowerStation_Base.png")]
        [TestCase("PowerStation/PowerStation_WarmLights.png")]
        [TestCase("PowerStation/PowerStation_Energy.png")]
        [TestCase("PowerStation/PowerStation_Core.png")]
        [TestCase("CentralGrid/CentralGrid_Base.png")]
        [TestCase("CentralGrid/CentralGrid_WarmLights.png")]
        [TestCase("CentralGrid/CentralGrid_Energy.png")]
        [TestCase("CentralGrid/CentralGrid_Core.png")]
        public void FinalNamingContract_UsesExactProductionArtPath(string suffix)
        {
            Assert.That(M15CityBuildingFinalArtPreparation.PowerLayerPaths
                    .Concat(M15CityBuildingFinalArtPreparation.CentralLayerPaths),
                Has.Member(M15CityBuildingFinalArtPreparation.Root + "/" + suffix));
        }

        [Test]
        public void MissingBaseFailsClearly_OptionalOverlaysRemainOptional()
        {
            CityBuildingArtDefinition missingBase = Object.Instantiate(power);
            cleanup.Add(missingBase);
            missingBase.SetData("power_station", null, power.GetSprite(1), power.GetSprite(2),
                power.GetSprite(3), power.GetTint(0), power.GetTint(1), power.GetTint(2),
                power.GetTint(3), Enumerable.Range(0, 5).Select(index =>
                    power.GetOpacity((ChapterMapVisualState)index)).ToArray(),
                power.FootprintFraction, power.LocalOffset, power.LocalScale);
            Assert.That(CityBuildingArtAssetValidator.Validate(missingBase, "power_station"),
                Has.Member("Base Architecture sprite is required."));
            CityBuildingArtDefinition definition = RenderableCopy(power, power, false);
            Assert.That(definition.IsConfigured, Is.True);
            Assert.That(CityBuildingArtAssetValidator.Validate(definition, "power_station"), Is.Empty);
            Assert.That(definition.GetSprite(1), Is.Null);
            Assert.That(definition.GetSprite(2), Is.Null);
            Assert.That(definition.GetSprite(3), Is.Null);
        }

        [TestCase("PowerStation", "power_station")]
        [TestCase("CentralGrid", "central_grid")]
        public void SuppliedLayers_WhenPresent_MustShareCanvasPivotAndImportContract(
            string name, string chapterId)
        {
            CityBuildingArtDefinition definition = name == "PowerStation" ? power : central;
            string[] paths = name == "PowerStation"
                ? M15CityBuildingFinalArtPreparation.PowerLayerPaths
                : M15CityBuildingFinalArtPreparation.CentralLayerPaths;
            if (File.Exists(paths[0]))
            {
                Assert.That(CityBuildingArtAssetValidator.Validate(definition, chapterId), Is.Empty);
                Assert.That(definition.IsConfigured, Is.True);
            }
            else
            {
                Assert.That(definition.IsConfigured, Is.False);
                Assert.That(CityBuildingArtAssetValidator.Validate(definition, chapterId),
                    Has.Member("Base Architecture sprite is required."));
            }
        }

        [TestCase("power", 0, 1f, 0f, 0f, 0f)]
        [TestCase("power", 1, 1f, 0.42f, 0f, 0f)]
        [TestCase("power", 2, 1f, 0.72f, 0.24f, 0f)]
        [TestCase("power", 3, 1f, 0.88f, 0.63f, 0.12f)]
        [TestCase("power", 4, 1f, 1f, 1f, 0.65f)]
        [TestCase("central", 0, 1f, 0f, 0f, 0f)]
        [TestCase("central", 1, 1f, 0.32f, 0f, 0f)]
        [TestCase("central", 2, 1f, 0.70f, 0.22f, 0f)]
        [TestCase("central", 3, 1f, 0.90f, 0.72f, 0.30f)]
        [TestCase("central", 4, 1f, 1f, 1f, 1f)]
        public void PreparedFiveStateProfiles_HaveBuildingSpecificCumulativeLighting(string building,
            int state, float architecture, float warm, float energy, float core)
        {
            CityBuildingArtDefinition definition = building == "power" ? power : central;
            Assert.That(definition.GetOpacity((ChapterMapVisualState)state),
                Is.EqualTo(new Vector4(architecture, warm, energy, core)));
            if (state > 0)
            {
                Vector4 previous = definition.GetOpacity((ChapterMapVisualState)(state - 1));
                Vector4 current = definition.GetOpacity((ChapterMapVisualState)state);
                for (int layer = 0; layer < 4; layer++)
                    Assert.That(current[layer], Is.GreaterThanOrEqualTo(previous[layer]));
            }
        }

        [Test]
        public void FinalProfiles_RenderAllFiveStatesWithoutChangingSilhouetteOrHierarchy()
        {
            CityBuildingArtDefinition definition = RenderableCopy(central, prototypeCentral, true);
            CityBuildingArtView view = Standalone(definition, new Vector2(260f, 205f));
            Sprite architecture = definition.GetSprite(0);
            for (int iteration = 0; iteration < 20; iteration++)
            {
                ChapterMapVisualState state = (ChapterMapVisualState)(iteration % 5);
                view.Present(state);
                Assert.That(view.ArtRoot.childCount, Is.EqualTo(4));
                Assert.That(view.ArtRoot.GetComponentsInChildren<Image>(true)[0].sprite,
                    Is.SameAs(architecture));
            }
            Assert.That(typeof(CityBuildingArtView).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        }

        [Test]
        public void DefinitionSwitchAndRepeatedEmphasis_ReturnExactBaseTransformWithoutDrift()
        {
            CityBuildingArtDefinition first = RenderableCopy(power, prototypePower, true);
            CityBuildingArtDefinition second = RenderableCopy(power, prototypePower, false);
            CityBuildingArtView view = Standalone(first, new Vector2(210f, 150f));
            Assert.That(view.TrySetDefinition(second), Is.True);
            Vector3 position = view.ArtRoot.localPosition;
            Vector3 scale = view.ArtRoot.localScale;
            Quaternion rotation = view.ArtRoot.localRotation;
            for (int iteration = 0; iteration < 25; iteration++)
            {
                view.Present((ChapterMapVisualState)(iteration % 5));
                view.ApplyEmphasis(1f);
                view.ResetEmphasis();
                Assert.That(view.ArtRoot.localPosition, Is.EqualTo(position));
                Assert.That(view.ArtRoot.localScale, Is.EqualTo(scale));
                Assert.That(view.ArtRoot.localRotation, Is.EqualTo(rotation));
                Assert.That(view.ArtRoot.childCount, Is.EqualTo(4));
            }
        }

        [Test]
        public void ComparisonScene_ReferencesFinalSlotsAndOnlyEnablesValidFinalMode()
        {
            Scene scene = EditorSceneManager.OpenScene(M15CityBuildingArtPrototypeBuilder.ScenePath,
                OpenSceneMode.Additive);
            try
            {
                CityBuildingArtPrototypeController controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        CityBuildingArtPrototypeController>(true)).Single();
                Assert.That(controller.FinalPowerStation, Is.SameAs(power));
                Assert.That(controller.FinalCentralGrid, Is.SameAs(central));
                bool expectedAvailability = power.IsConfigured || central.IsConfigured;
                Assert.That(controller.FinalArtAvailable, Is.EqualTo(expectedAvailability));
                controller.Initialize();
                Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final),
                    Is.EqualTo(power.IsConfigured));
                Assert.That(controller.PreviewSource, Is.EqualTo(power.IsConfigured
                    ? CityBuildingArtPreviewSource.Final
                    : CityBuildingArtPreviewSource.Prototype));
                Assert.That(controller.GetComponentsInChildren<CityBuildingArtView>(true)
                    .All(view => view.UsesArt), Is.True);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void ProductionMapUsesOnlyAcceptedFinalArtAndBuildExcludesPrototypeScene()
        {
            var root = NewObject("Production map");
            CampaignRuntimeView map = root.AddComponent<CampaignRuntimeView>();
            map.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { },
                () => { }, campaign.CampaignUiTheme);
            map.ShowMap();
            Assert.That(map.GetComponentsInChildren<CityBuildingArtView>(true)
                .Select(view => view.Definition.ChapterId),
                Is.EquivalentTo(new[] { "power_station", "central_grid" }));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo(M15CityBuildingArtPrototypeBuilder.ScenePath));
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                string[] dependencies = AssetDatabase.GetDependencies(scene.path);
                Assert.That(dependencies.Any(path => path.StartsWith(
                    M15CityBuildingArtPrototypeBuilder.DirectoryPath)), Is.False);
            }
        }

        [Test]
        public void FinalGeometryParameters_PreserveMapBoundsSafeAreaAndCentralProminence()
        {
            Assert.That(power.FootprintFraction, Is.EqualTo(new Vector2(0.9f, 0.7f)));
            Assert.That(central.FootprintFraction, Is.EqualTo(new Vector2(0.88f, 0.7f)));
            Assert.That(power.LocalOffset, Is.EqualTo(Vector2.zero));
            Assert.That(central.LocalOffset, Is.EqualTo(Vector2.zero));
            CityMapLayoutEntry ordinary = CityMapLayoutCatalog.Production.Entries
                .Single(entry => entry.ChapterId == "power_station");
            CityMapLayoutEntry hub = CityMapLayoutCatalog.Production.Entries
                .Single(entry => entry.ChapterId == "central_grid");
            float ordinaryWidth = ordinary.VisualSize.x * ordinary.Scale * power.FootprintFraction.x;
            float hubWidth = hub.VisualSize.x * hub.Scale *
                             CampaignUiThemeApplicator.CentralGridSilhouetteScaleMultiplier *
                             central.FootprintFraction.x;
            Assert.That(hub.HitSize, Is.EqualTo(new Vector2(350f, 330f)));
            Assert.That(hub.Position, Is.EqualTo(new Vector2(40f, -300f)));
            Assert.That(hubWidth / ordinaryWidth, Is.InRange(1.2f, 1.31f));
            Assert.That(CampaignUiThemeApplicator.CentralGridSilhouetteCenterY,
                Is.EqualTo(76f));
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2340)]
        [TestCase(720, 1280)]
        public void PreparedFinalGeometry_FitsMapScaleAtSupportedPortraitSizes(int width, int height)
        {
            CityBuildingArtPrototypeController controller = PrototypeController();
            float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
            foreach (Canvas canvas in controller.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(width / scale,
                    height / scale);
            }
            Canvas.ForceUpdateCanvases();
            foreach (CityBuildingArtView view in controller.GetComponentsInChildren<
                         CityBuildingArtView>(true))
            {
                CityChapterNodeView node = view.GetComponent<CityChapterNodeView>();
                view.ApplyEmphasis(1f);
                Bounds art = BoundsIn(view.ArtRoot, node.transform);
                Bounds label = BoundsIn(node.Label.rectTransform, node.transform);
                Assert.That(art.min.y, Is.GreaterThan(label.max.y + 6f));
                Assert.That(art.min.x, Is.GreaterThan(node.HitArea.rect.xMin));
                Assert.That(art.max.x, Is.LessThan(node.HitArea.rect.xMax));
                Assert.That(art.max.y, Is.LessThan(node.HitArea.rect.yMax));
            }
        }

        [Test]
        public void PreviewStateChanges_DoNotMutateCampaignProgress()
        {
            CityBuildingArtPrototypeController controller = PrototypeController();
            controller.ShowSource(CityBuildingArtPreviewSource.Prototype);
            for (int building = 0; building < 2; building++)
            {
                controller.SelectBuilding(building);
                for (int state = 0; state < 5; state++) controller.ShowState(state);
            }
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
                Assert.That(controller.PreviewProgress.GetCompletedLevelCount(chapter.ChapterId),
                    Is.Zero);
        }

        private CityBuildingArtPrototypeController PrototypeController()
        {
            GameObject root = NewObject("E1B Preview");
            CityBuildingArtPrototypeController controller =
                root.AddComponent<CityBuildingArtPrototypeController>();
            controller.SetData(campaign, prototypePower, prototypeCentral);
            controller.SetFinalDefinitions(power, central);
            controller.Initialize();
            return controller;
        }

        private CityBuildingArtView Standalone(CityBuildingArtDefinition definition, Vector2 size)
        {
            GameObject root = NewObject("Visual Region", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = size;
            Image placeholder = new GameObject("Placeholder", typeof(RectTransform),
                typeof(Image)).GetComponent<Image>();
            placeholder.transform.SetParent(root.transform, false);
            CityBuildingArtView view = root.AddComponent<CityBuildingArtView>();
            view.Initialize(definition, definition.ChapterId, (RectTransform)root.transform,
                new[] { placeholder });
            return view;
        }

        private CityBuildingArtDefinition RenderableCopy(CityBuildingArtDefinition prepared,
            CityBuildingArtDefinition source, bool allLayers)
        {
            CityBuildingArtDefinition copy = Object.Instantiate(prepared);
            cleanup.Add(copy);
            copy.SetData(prepared.ChapterId, source.GetSprite(0),
                allLayers ? source.GetSprite(1) : null,
                allLayers ? source.GetSprite(2) : null,
                allLayers ? source.GetSprite(3) : null,
                prepared.GetTint(0), prepared.GetTint(1), prepared.GetTint(2),
                prepared.GetTint(3), Enumerable.Range(0, 5)
                    .Select(index => prepared.GetOpacity((ChapterMapVisualState)index)).ToArray(),
                prepared.FootprintFraction, prepared.LocalOffset, prepared.LocalScale);
            return copy;
        }

        private GameObject NewObject(string name, params System.Type[] components)
        {
            var instance = new GameObject(name, components);
            cleanup.Add(instance);
            return instance;
        }

        private static CityBuildingArtDefinition Prototype(string name) =>
            AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15CityBuildingArtPrototypeBuilder.DirectoryPath + "/" + name + ".asset");

        private static Bounds BoundsIn(RectTransform rect, Transform relativeTo)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bounds = new Bounds(relativeTo.InverseTransformPoint(corners[0]), Vector3.zero);
            foreach (Vector3 corner in corners)
                bounds.Encapsulate(relativeTo.InverseTransformPoint(corner));
            return bounds;
        }
    }
}

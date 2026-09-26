using System.Collections.Generic;
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
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class CityBuildingArtTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private CityBuildingArtDefinition power;
        private CityBuildingArtDefinition central;
        private CampaignDefinition campaign;

        [SetUp]
        public void SetUp()
        {
            power = Load("PowerStation");
            central = Load("CentralGrid");
            campaign = Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
        }

        [TestCase("PowerStation", "power_station")]
        [TestCase("CentralGrid", "central_grid")]
        public void AuthoredDefinitions_ValidateAndContainRegisteredLayers(string name, string id)
        {
            CityBuildingArtDefinition definition = Load(name);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.IsConfigured, Is.True);
            Assert.That(definition.ChapterId, Is.EqualTo(id));
            for (int layer = 0; layer < 4; layer++)
            {
                Sprite sprite = definition.GetSprite(layer);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.rect, Is.EqualTo(definition.GetSprite(0).rect));
                Assert.That(sprite.pivot, Is.EqualTo(sprite.rect.size * 0.5f));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.alphaIsTransparency && importer.sRGBTexture, Is.True);
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            }
        }

        [TestCase("chapterId")]
        [TestCase("baseArchitecture")]
        [TestCase("stateOpacity")]
        [TestCase("localScale")]
        public void InvalidRequiredData_IsRejected(string property)
        {
            CityBuildingArtDefinition definition = Clone(power);
            var serialized = new SerializedObject(definition);
            SerializedProperty value = serialized.FindProperty(property);
            if (property == "chapterId") value.stringValue = "";
            else if (property == "baseArchitecture") value.objectReferenceValue = null;
            else if (property == "stateOpacity") value.arraySize = 2;
            else value.floatValue = float.NaN;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(definition.IsConfigured, Is.False);
        }

        [Test]
        public void OptionalLayersMayBeMissing_BaseAndFallbackRemainSafe()
        {
            CityBuildingArtDefinition definition = Clone(power);
            definition.SetData("power_station", power.GetSprite(0), null, null, null, power.FootprintFraction);
            Assert.That(definition.IsConfigured, Is.True);
            CityBuildingArtView view = Standalone(definition, out Image placeholder);
            view.Present(ChapterMapVisualState.Restored);
            Assert.That(view.UsesArt, Is.True);
            Assert.That(placeholder.enabled, Is.False);
            Assert.That(view.ArtRoot.GetComponentsInChildren<Image>().Count(image => image.enabled), Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingDefinitionOrBase_KeepsExistingVisiblePlaceholder(bool missingBase)
        {
            CityBuildingArtDefinition definition = null;
            if (missingBase)
            {
                definition = Clone(power);
                definition.SetData("power_station", null, power.GetSprite(1), null, null, power.FootprintFraction);
            }
            CityBuildingArtView view = Standalone(definition, out Image placeholder);
            view.Present(ChapterMapVisualState.Restored);
            Assert.That(view.UsesArt, Is.False);
            Assert.That(view.ArtRoot, Is.Null);
            Assert.That(placeholder.enabled, Is.True);
        }

        [Test]
        public void WrongChapterAssociation_DoesNotReplacePlaceholder()
        {
            CityBuildingArtView view = Standalone(central, out Image placeholder);
            Assert.That(view.UsesArt, Is.False);
            Assert.That(placeholder.enabled, Is.True);
        }

        [Test]
        public void LostBaseSpriteOrDisabledView_RestoresFallback()
        {
            CityBuildingArtDefinition definition = Clone(power);
            CityBuildingArtView view = Standalone(definition, out Image placeholder);
            view.enabled = false;
            // Non-ExecuteAlways runtime callbacks are not dispatched by EditMode.
            typeof(CityBuildingArtView).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            Assert.That(placeholder.enabled, Is.True);
            view.enabled = true;
            typeof(CityBuildingArtView).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            Assert.That(placeholder.enabled, Is.False);
            definition.SetData("power_station", null, null, null, null, power.FootprintFraction);
            view.Present(ChapterMapVisualState.Restored);
            Assert.That(placeholder.enabled, Is.True);
            Assert.That(view.ArtRoot.gameObject.activeSelf, Is.False);
        }

        [TestCase(CampaignChapterState.Locked, 0, ChapterMapVisualState.Locked)]
        [TestCase(CampaignChapterState.Locked, 10, ChapterMapVisualState.Locked)]
        [TestCase(CampaignChapterState.Available, 0, ChapterMapVisualState.ProgressStage1)]
        [TestCase(CampaignChapterState.Available, 1, ChapterMapVisualState.ProgressStage1)]
        [TestCase(CampaignChapterState.Available, 2, ChapterMapVisualState.ProgressStage1)]
        [TestCase(CampaignChapterState.Available, 3, ChapterMapVisualState.ProgressStage1)]
        [TestCase(CampaignChapterState.Available, 4, ChapterMapVisualState.ProgressStage2)]
        [TestCase(CampaignChapterState.Available, 5, ChapterMapVisualState.ProgressStage2)]
        [TestCase(CampaignChapterState.Available, 6, ChapterMapVisualState.ProgressStage2)]
        [TestCase(CampaignChapterState.Available, 7, ChapterMapVisualState.ProgressStage3)]
        [TestCase(CampaignChapterState.Available, 8, ChapterMapVisualState.ProgressStage3)]
        [TestCase(CampaignChapterState.Available, 9, ChapterMapVisualState.ProgressStage3)]
        [TestCase(CampaignChapterState.Available, 10, ChapterMapVisualState.Restored)]
        [TestCase(CampaignChapterState.Restored, 10, ChapterMapVisualState.Restored)]
        public void ProgressBands_UseAuthoritativeM13Resolver(CampaignChapterState state,
            int completed, ChapterMapVisualState expected)
        {
            CityBuildingArtView view = Standalone(power, out _);
            view.Present(state, completed, 10);
            Assert.That(view.VisualState, Is.EqualTo(expected));
            Assert.That(view.VisualState, Is.EqualTo(CityMapPresentationModel.GetChapterVisualState(state, completed, 10)));
        }

        [TestCase("PowerStation")]
        [TestCase("CentralGrid")]
        public void LightingIsCumulative_ArchitectureStable_WarmPrecedesEnergy(string name)
        {
            CityBuildingArtDefinition definition = Load(name);
            var root = NewObject("State test", typeof(RectTransform));
            var view = root.AddComponent<CityBuildingArtView>();
            ((RectTransform)root.transform).sizeDelta = new Vector2(210, 150);
            view.Initialize(definition, definition.ChapterId, (RectTransform)root.transform, new Image[0]);
            Sprite architecture = definition.GetSprite(0);
            Vector4 previous = Vector4.zero;
            int[] expectedActiveLayers = { 1, 2, 3, 3, 4 };
            for (int state = 0; state < 5; state++)
            {
                view.Present((ChapterMapVisualState)state);
                Vector4 opacity = definition.GetOpacity((ChapterMapVisualState)state);
                for (int layer = 0; layer < 4; layer++) Assert.That(opacity[layer], Is.GreaterThanOrEqualTo(previous[layer]));
                Image[] images = view.ArtRoot.GetComponentsInChildren<Image>();
                Assert.That(images[0].sprite, Is.SameAs(architecture));
                Assert.That(images.Count(image => image.enabled), Is.EqualTo(expectedActiveLayers[state]));
                previous = opacity;
            }
        }

        [Test]
        public void RepeatedStatesAndEmphasis_DoNotAccumulateObjectsTransformsOrAlpha()
        {
            CityBuildingArtView view = Standalone(power, out _);
            Vector3 position = view.ArtRoot.localPosition;
            Vector3 scale = view.ArtRoot.localScale;
            Quaternion rotation = view.ArtRoot.localRotation;
            for (int iteration = 0; iteration < 30; iteration++)
            {
                view.Present((ChapterMapVisualState)(iteration % 5));
                view.ApplyEmphasis(1f);
                view.ApplyEmphasis(1f);
                Assert.That(view.ArtRoot.localScale, Is.EqualTo(scale * CityBuildingArtView.MaximumEmphasis));
                view.ResetEmphasis();
                Assert.That(view.ArtRoot.localPosition, Is.EqualTo(position));
                Assert.That(view.ArtRoot.localScale, Is.EqualTo(scale));
                Assert.That(view.ArtRoot.localRotation, Is.EqualTo(rotation));
                Assert.That(view.ArtRoot.childCount, Is.EqualTo(4));
            }
            view.Present(ChapterMapVisualState.Restored);
            Assert.That(view.ArtRoot.GetComponentInChildren<Image>().color.a, Is.EqualTo(1f));
            Assert.That(typeof(CityBuildingArtView).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance), Is.Null);
        }

        [Test]
        public void PreviewUsesActualMapGeometry_AndPreservesD2CentralTransform()
        {
            CityBuildingArtPrototypeController controller = Prototype();
            foreach (CityChapterNodeView node in controller.MapView.GetComponentsInChildren<CityChapterNodeView>(true))
            {
                CityMapLayoutEntry entry = CityMapLayoutCatalog.Production.Entries.Single(item => item.ChapterId == node.ChapterId);
                Assert.That(node.HitArea.sizeDelta, Is.EqualTo(entry.HitSize));
                Assert.That(node.HitArea.anchoredPosition, Is.EqualTo(entry.Position));
                RectTransform visual = (RectTransform)node.transform.Find("Building Silhouette");
                Assert.That(visual.sizeDelta, Is.EqualTo(entry.VisualSize));
                if (node.ChapterId == "central_grid")
                {
                    Assert.That(node.HitArea.sizeDelta, Is.EqualTo(new Vector2(350, 330)));
                    Assert.That(node.HitArea.anchoredPosition, Is.EqualTo(new Vector2(40, -300)));
                    Assert.That(visual.anchoredPosition.y, Is.EqualTo(76f));
                    Assert.That(visual.localScale.x, Is.EqualTo(1.075f).Within(0.0001f));
                    node.ApplyPowerUp(1f);
                    Assert.That(visual.localScale, Is.EqualTo(node.BaseVisualScale));
                }
            }
            CityBuildingArtView[] views = controller.GetComponentsInChildren<CityBuildingArtView>();
            float ordinaryWidth = BoundsIn(views[0].ArtRoot, views[0].transform).size.x;
            float centralWidth = BoundsIn(views[1].ArtRoot, views[1].transform).size.x;
            Assert.That(centralWidth / ordinaryWidth, Is.InRange(1.2f, 1.31f));
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2340)]
        [TestCase(720, 1280)]
        public void PortraitMapScale_ArtStaysInHitRegionAboveLabels_EvenDuringEmphasis(int width, int height)
        {
            CityBuildingArtPrototypeController controller = Prototype();
            float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
            foreach (Canvas canvas in controller.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(width / scale, height / scale);
            }
            Canvas.ForceUpdateCanvases();
            foreach (CityBuildingArtView view in controller.GetComponentsInChildren<CityBuildingArtView>(true))
            {
                CityChapterNodeView node = view.GetComponent<CityChapterNodeView>();
                view.ApplyEmphasis(1f);
                Bounds artBounds = BoundsIn(view.ArtRoot, node.transform);
                Bounds labelBounds = BoundsIn(node.Label.rectTransform, node.transform);
                Assert.That(artBounds.min.y, Is.GreaterThan(labelBounds.max.y + 6f));
                Assert.That(artBounds.min.x, Is.GreaterThan(node.HitArea.rect.xMin));
                Assert.That(artBounds.max.x, Is.LessThan(node.HitArea.rect.xMax));
                Assert.That(artBounds.max.y, Is.LessThan(node.HitArea.rect.yMax));
                Canvas canvas = view.GetComponentInParent<Canvas>();
                Bounds canvasBounds = BoundsIn(view.ArtRoot, canvas.transform);
                Rect rect = ((RectTransform)canvas.transform).rect;
                Assert.That(rect.Contains(canvasBounds.min), Is.True);
                Assert.That(rect.Contains(canvasBounds.max), Is.True);
            }
            Canvas controls = controller.GetComponentsInChildren<Canvas>(true)
                .Single(canvas => canvas.name == "E1A Developer Controls");
            Rect controlsRect = ((RectTransform)controls.transform).rect;
            foreach (RectTransform child in controls.transform.Cast<Transform>().OfType<RectTransform>())
            {
                Bounds bounds = BoundsIn(child, controls.transform);
                Assert.That(controlsRect.Contains(bounds.min) && controlsRect.Contains(bounds.max), Is.True);
                foreach (CityChapterNodeView node in controller.MapView.GetComponentsInChildren<CityChapterNodeView>(true))
                {
                    Bounds label = BoundsIn(node.Label.rectTransform, controls.transform);
                    Assert.That(bounds.Intersects(label), Is.False, "QA controls must not obscure map labels.");
                }
            }
        }

        [Test]
        public void PreviewStateChanges_DoNotMutateProgress_AndOnlyTwoNodesUseArt()
        {
            CityBuildingArtPrototypeController controller = Prototype();
            for (int building = 0; building < 2; building++)
            {
                controller.SelectBuilding(building);
                for (int state = 0; state < 5; state++) controller.ShowState(state);
            }
            Assert.That(controller.GetComponentsInChildren<CityBuildingArtView>(true), Has.Length.EqualTo(2));
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
                Assert.That(controller.PreviewProgress.GetCompletedLevelCount(chapter.ChapterId), Is.Zero);
            Assert.That(controller.PreviewProgress.GetChapterState("central_grid"), Is.EqualTo(CampaignChapterState.Locked));
            Assert.That(controller.MapView.GetComponentsInChildren<CityEnergyPathView>(true), Has.Length.EqualTo(4));
        }

        [Test]
        public void ProductionMapUsesTwoFinalArtBindings_AndAcceptedThemeBindings()
        {
            var root = NewObject("Production unchanged");
            CampaignRuntimeView view = root.AddComponent<CampaignRuntimeView>();
            view.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { }, () => { }, campaign.CampaignUiTheme);
            view.ShowMap();
            CityBuildingArtView[] productionArt = view.GetComponentsInChildren<
                CityBuildingArtView>(true);
            Assert.That(productionArt, Has.Length.EqualTo(2));
            Assert.That(productionArt.Select(art => art.Definition.ChapterId),
                Is.EquivalentTo(new[] { "power_station", "central_grid" }));
            foreach (CityChapterNodeView node in view.GetComponentsInChildren<CityChapterNodeView>())
            {
                bool hasFinalArt = node.ChapterId == "power_station" || node.ChapterId == "central_grid";
                Assert.That(node.BuildingArtView != null && node.BuildingArtView.UsesArt,
                    Is.EqualTo(hasFinalArt), node.ChapterId);
                Image[] programmerParts = node.transform.Find("Building Silhouette")
                    .GetComponentsInChildren<Image>()
                    .Where(image => !hasFinalArt ||
                                    !image.transform.IsChildOf(node.BuildingArtView.ArtRoot))
                    .ToArray();
                Assert.That(programmerParts.All(image => image.enabled), Is.EqualTo(!hasFinalArt),
                    node.ChapterId);
            }
            Assert.That(campaign.CampaignUiTheme, Is.SameAs(CampaignUiThemeCatalog.LoadTechnicalNeonPrototype()));
            Assert.That(campaign.GameplayVisualTheme, Is.SameAs(CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype()));
        }

        [Test]
        public void PrototypeScene_IsOptInOutsideProductionDependenciesAndBuildSettings()
        {
            string path = M15CityBuildingArtPrototypeBuilder.ScenePath;
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Has.None.EqualTo(path));
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CityBuildingArtPrototypeController>()).Single();
                Assert.That(controller.PowerStation, Is.SameAs(power));
                Assert.That(controller.CentralGrid, Is.SameAs(central));
                Assert.That(controller.Campaign, Is.SameAs(campaign));
                Camera[] cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).ToArray();
                Assert.That(cameras, Has.Length.EqualTo(1));
                Assert.That(cameras[0].enabled && cameras[0].targetDisplay == 0, Is.True);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            foreach (EditorBuildSettingsScene built in EditorBuildSettings.scenes)
                Assert.That(AssetDatabase.GetDependencies(built.path).Any(dependency =>
                    dependency.Contains("CityBuildingPrototype") || dependency.Contains("CityBuildingArtPrototypeController")), Is.False);
        }

        private static Bounds BoundsIn(RectTransform rect, Transform relativeTo)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bounds = new Bounds(relativeTo.InverseTransformPoint(corners[0]), Vector3.zero);
            foreach (Vector3 corner in corners) bounds.Encapsulate(relativeTo.InverseTransformPoint(corner));
            return bounds;
        }

        private CityBuildingArtPrototypeController Prototype()
        {
            var root = NewObject("E1A Test Preview");
            var controller = root.AddComponent<CityBuildingArtPrototypeController>();
            controller.SetData(campaign, power, central);
            controller.Initialize();
            return controller;
        }

        private CityBuildingArtView Standalone(CityBuildingArtDefinition definition, out Image placeholder)
        {
            var root = NewObject("Visual Region", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(210, 150);
            var child = new GameObject("Original Placeholder", typeof(RectTransform), typeof(Image));
            child.transform.SetParent(root.transform, false);
            placeholder = child.GetComponent<Image>();
            var view = root.AddComponent<CityBuildingArtView>();
            view.Initialize(definition, "power_station", (RectTransform)root.transform, new[] { placeholder });
            return view;
        }

        private GameObject NewObject(string name, params System.Type[] components)
        {
            var root = new GameObject(name, components);
            cleanup.Add(root);
            return root;
        }

        private CityBuildingArtDefinition Clone(CityBuildingArtDefinition definition)
        {
            var clone = Object.Instantiate(definition);
            cleanup.Add(clone);
            return clone;
        }

        private static CityBuildingArtDefinition Load(string name) =>
            AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(M15CityBuildingArtPrototypeBuilder.DirectoryPath + "/" + name + ".asset");
    }
}

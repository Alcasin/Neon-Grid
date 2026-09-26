using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Editor;
using NeonGrid.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class CentralGridFinalOverlayTests
    {
        private const string ExpectedBaseHash =
            "C42FA4773C5A0430D7E6DF2A559B895F065B66F6181AF52B51BAAAE1858B3ECB";
        private const string ExpectedPowerBaseHash =
            "C713B39D3E579A13DDC3F4672896D7F49E32D203C918FDCA5D7F7698E05CC343";
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CityBuildingArtDefinition finalPower;
        private CityBuildingArtDefinition finalCentral;
        private CityBuildingArtDefinition prototypePower;
        private CityBuildingArtDefinition prototypeCentral;
        private CampaignDefinition campaign;

        [SetUp]
        public void SetUp()
        {
            finalPower = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15CityBuildingFinalArtPreparation.PowerDefinitionPath);
            finalCentral = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15CityBuildingFinalArtPreparation.CentralDefinitionPath);
            prototypePower = Prototype("PowerStation");
            prototypeCentral = Prototype("CentralGrid");
            campaign = Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void LockedBase_IsExactAuthoritativeSquareRgbaAsset()
        {
            string path = M15CityBuildingFinalArtPreparation.CentralLayerPaths[0];
            Assert.That(File.Exists(path), Is.True);
            Assert.That(Hash(path), Is.EqualTo(ExpectedBaseHash));
            Texture2D texture = LoadSource(path);
            Assert.That(new Vector2Int(texture.width, texture.height),
                Is.EqualTo(new Vector2Int(1278, 1278)));
            Color32[] pixels = texture.GetPixels32();
            Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True);
            Assert.That(pixels.Any(pixel => pixel.a == 255), Is.True);
        }

        [Test]
        public void FourFinalLayers_ShareCanvasPivotImportContractAndStableAssociation()
        {
            Assert.That(finalCentral, Is.Not.Null);
            Assert.That(finalCentral.ChapterId, Is.EqualTo("central_grid"));
            Assert.That(finalCentral.IsConfigured, Is.True);
            Assert.That(CityBuildingArtAssetValidator.Validate(finalCentral, "central_grid"),
                Is.Empty);
            Sprite architecture = finalCentral.GetSprite(0);
            Assert.That(architecture.rect.size, Is.EqualTo(new Vector2(1278f, 1278f)));
            Assert.That(architecture.pivot, Is.EqualTo(new Vector2(639f, 639f)));
            for (int layer = 0; layer < 4; layer++)
            {
                string path = M15CityBuildingFinalArtPreparation.CentralLayerPaths[layer];
                Assert.That(File.Exists(path), Is.True);
                Assert.That(finalCentral.GetTint(layer), Is.EqualTo(Color.white));
                Sprite sprite = finalCentral.GetSprite(layer);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.rect, Is.EqualTo(architecture.rect));
                Assert.That(sprite.pivot, Is.EqualTo(architecture.pivot));
            }
        }

        [TestCase(1, 0.04f)]
        [TestCase(2, 0.09f)]
        [TestCase(3, 0.06f)]
        public void OverlaySource_IsTransparentControlledAndHasNoBlackMatte(int layer,
            float maximumCoverage)
        {
            Texture2D texture = LoadSource(
                M15CityBuildingFinalArtPreparation.CentralLayerPaths[layer]);
            Color32[] pixels = texture.GetPixels32();
            int visible = pixels.Count(pixel => pixel.a > 8);
            Assert.That(visible, Is.GreaterThan(0));
            Assert.That(visible / (float)pixels.Length, Is.LessThan(maximumCoverage));
            Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True);
            Assert.That(pixels.Where(pixel => pixel.a > 0).All(pixel =>
                pixel.r + pixel.g + pixel.b > 0), Is.True);
        }

        [Test]
        public void FiveStates_AreCumulativeAndKeepStableBaseGeometry()
        {
            CityBuildingArtView view = Standalone(finalCentral, "central_grid",
                new Vector2(260f, 205f));
            int[] activeLayers = { 1, 2, 3, 4, 4 };
            Vector4[] expected =
            {
                new Vector4(1f, 0f, 0f, 0f),
                new Vector4(1f, 0.32f, 0f, 0f),
                new Vector4(1f, 0.70f, 0.22f, 0f),
                new Vector4(1f, 0.90f, 0.72f, 0.30f),
                new Vector4(1f, 1f, 1f, 1f)
            };
            for (int state = 0; state < 5; state++)
            {
                view.Present((ChapterMapVisualState)state);
                Image[] images = view.ArtRoot.GetComponentsInChildren<Image>(true);
                Assert.That(finalCentral.GetOpacity((ChapterMapVisualState)state),
                    Is.EqualTo(expected[state]));
                Assert.That(images.Count(image => image.enabled), Is.EqualTo(activeLayers[state]));
                Assert.That(images[0].sprite, Is.SameAs(finalCentral.GetSprite(0)));
                if (state > 0)
                    for (int layer = 0; layer < 4; layer++)
                        Assert.That(expected[state][layer],
                            Is.GreaterThanOrEqualTo(expected[state - 1][layer]));
            }
            Assert.That(view.ArtRoot.sizeDelta.x, Is.EqualTo(228.8f).Within(0.001f));
            Assert.That(view.ArtRoot.sizeDelta.y, Is.EqualTo(143.5f).Within(0.001f));
        }

        [Test]
        public void PrototypeFinalSwitch_ReusesSameFourImagesForBothBuildings()
        {
            CityBuildingArtPrototypeController controller = Controller();
            CityBuildingArtView[] views = controller.GetComponentsInChildren<
                CityBuildingArtView>(true);
            int[] roots = views.Select(view => view.ArtRoot.GetInstanceID()).ToArray();
            int[][] children = views.Select(view => view.ArtRoot.Cast<Transform>()
                .Select(child => child.GetInstanceID()).ToArray()).ToArray();
            for (int cycle = 0; cycle < 10; cycle++)
            {
                Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.True);
                Assert.That(views[0].Definition, Is.SameAs(finalPower));
                Assert.That(views[1].Definition, Is.SameAs(finalCentral));
                Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Prototype), Is.True);
                Assert.That(views[0].Definition, Is.SameAs(prototypePower));
                Assert.That(views[1].Definition, Is.SameAs(prototypeCentral));
            }
            Assert.That(views.Select(view => view.ArtRoot.GetInstanceID()), Is.EqualTo(roots));
            for (int index = 0; index < views.Length; index++)
                Assert.That(views[index].ArtRoot.Cast<Transform>()
                    .Select(child => child.GetInstanceID()), Is.EqualTo(children[index]));
        }

        [Test]
        public void TenEmphasisCycles_ReturnCentralGridToExactRegisteredTransform()
        {
            CityBuildingArtView view = Standalone(finalCentral, "central_grid",
                new Vector2(260f, 205f));
            Vector3 position = view.ArtRoot.localPosition;
            Vector3 scale = view.ArtRoot.localScale;
            Quaternion rotation = view.ArtRoot.localRotation;
            Vector3[] childPositions = view.ArtRoot.Cast<Transform>()
                .Select(child => child.localPosition).ToArray();
            for (int cycle = 0; cycle < 10; cycle++)
            {
                view.Present((ChapterMapVisualState)(cycle % 5));
                view.ApplyEmphasis(1f);
                view.ResetEmphasis();
                Assert.That(view.ArtRoot.localPosition, Is.EqualTo(position));
                Assert.That(view.ArtRoot.localScale, Is.EqualTo(scale));
                Assert.That(view.ArtRoot.localRotation, Is.EqualTo(rotation));
                Assert.That(view.ArtRoot.Cast<Transform>().Select(child => child.localPosition),
                    Is.EqualTo(childPositions));
            }
        }

        [Test]
        public void RealMapScale_KeepsCentralGridTwentyToThirtyPercentWiderThanPowerStation()
        {
            CityBuildingArtPrototypeController controller = Controller();
            Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.True);
            CityBuildingArtView[] views = controller.GetComponentsInChildren<
                CityBuildingArtView>(true);
            CityBuildingArtView power = views.Single(view =>
                view.GetComponent<CityChapterNodeView>().ChapterId == "power_station");
            CityBuildingArtView central = views.Single(view =>
                view.GetComponent<CityChapterNodeView>().ChapterId == "central_grid");
            Bounds powerBounds = BoundsIn(power.ArtRoot, power.GetComponent<CityChapterNodeView>().transform);
            Bounds centralBounds = BoundsIn(central.ArtRoot,
                central.GetComponent<CityChapterNodeView>().transform);
            Assert.That(centralBounds.size.x / powerBounds.size.x, Is.InRange(1.20f, 1.31f));
            Assert.That(centralBounds.size.y, Is.GreaterThan(powerBounds.size.y));
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2340)]
        [TestCase(720, 1280)]
        public void FinalCentralGrid_PreservesD2SafeAreaAtPortraitResolution(int width, int height)
        {
            CityBuildingArtPrototypeController controller = Controller();
            Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.True);
            float canvasScale = Mathf.Sqrt(width / 1080f * height / 1920f);
            foreach (Canvas canvas in controller.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(width / canvasScale,
                    height / canvasScale);
            }
            Canvas.ForceUpdateCanvases();
            CityBuildingArtView view = controller.GetComponentsInChildren<
                CityBuildingArtView>(true).Single(item =>
                item.GetComponent<CityChapterNodeView>().ChapterId == "central_grid");
            CityChapterNodeView node = view.GetComponent<CityChapterNodeView>();
            Assert.That(node.HitArea.sizeDelta, Is.EqualTo(new Vector2(350f, 330f)));
            Assert.That(node.HitArea.anchoredPosition, Is.EqualTo(new Vector2(40f, -300f)));
            RectTransform visual = (RectTransform)node.transform.Find("Building Silhouette");
            Assert.That(visual.anchoredPosition.y, Is.EqualTo(76f));
            Assert.That(visual.localScale.x, Is.EqualTo(1.075f).Within(0.0001f));
            view.Present(ChapterMapVisualState.Restored);
            view.ApplyEmphasis(1f);
            Bounds art = BoundsIn(view.ArtRoot, node.transform);
            Bounds label = BoundsIn(node.Label.rectTransform, node.transform);
            Assert.That(art.min.y, Is.GreaterThan(label.max.y + 6f));
            Assert.That(art.min.x, Is.GreaterThan(node.HitArea.rect.xMin));
            Assert.That(art.max.x, Is.LessThan(node.HitArea.rect.xMax));
            Assert.That(art.max.y, Is.LessThan(node.HitArea.rect.yMax));
        }

        [Test]
        public void PreviewArtifactsRemainDevelopmentOnlyAndProductionUsesAcceptedCentralFinalArt()
        {
            Assert.That(File.Exists(
                "Assets/NeonGrid/Documentation/Previews/CentralGrid_StatePreview.png"), Is.True);
            Assert.That(File.Exists(
                "Assets/NeonGrid/Documentation/Previews/PowerStation_CentralGrid_Comparison.png"),
                Is.True);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo(M15CityBuildingArtPrototypeBuilder.ScenePath));
            var root = NewObject("Production");
            CampaignRuntimeView map = root.AddComponent<CampaignRuntimeView>();
            map.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { },
                () => { }, campaign.CampaignUiTheme);
            map.ShowMap();
            Assert.That(map.GetComponentsInChildren<CityBuildingArtView>(true)
                .Single(view => view.Definition.ChapterId == "central_grid").Definition,
                Is.SameAs(finalCentral));
        }

        [Test]
        public void PowerStationAuthoritativeBaseRemainsUnchanged()
        {
            Assert.That(Hash(M15CityBuildingFinalArtPreparation.PowerLayerPaths[0]),
                Is.EqualTo(ExpectedPowerBaseHash));
        }

        private CityBuildingArtPrototypeController Controller()
        {
            GameObject root = NewObject("E1B.2 Preview");
            CityBuildingArtPrototypeController controller =
                root.AddComponent<CityBuildingArtPrototypeController>();
            controller.SetData(campaign, prototypePower, prototypeCentral);
            controller.SetFinalDefinitions(finalPower, finalCentral);
            controller.Initialize();
            return controller;
        }

        private CityBuildingArtView Standalone(CityBuildingArtDefinition definition,
            string chapterId, Vector2 regionSize)
        {
            GameObject root = NewObject("Art Region", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = regionSize;
            Image placeholder = new GameObject("Placeholder", typeof(RectTransform),
                typeof(Image)).GetComponent<Image>();
            placeholder.transform.SetParent(root.transform, false);
            CityBuildingArtView view = root.AddComponent<CityBuildingArtView>();
            view.Initialize(definition, chapterId, (RectTransform)root.transform,
                new[] { placeholder });
            return view;
        }

        private Texture2D LoadSource(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false), Is.True);
            cleanup.Add(texture);
            return texture;
        }

        private GameObject NewObject(string name, params Type[] components)
        {
            var instance = new GameObject(name, components);
            cleanup.Add(instance);
            return instance;
        }

        private static CityBuildingArtDefinition Prototype(string name) =>
            AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15CityBuildingArtPrototypeBuilder.DirectoryPath + "/" + name + ".asset");

        private static string Hash(string path)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty);
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
    }
}

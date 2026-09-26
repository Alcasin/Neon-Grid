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
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class PowerStationFinalOverlayTests
    {
        private const string ExpectedBaseHash =
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
        public void LockedBase_IsExactApprovedRgbaAssetAndWasNotReencoded()
        {
            string path = M15CityBuildingFinalArtPreparation.PowerLayerPaths[0];
            Assert.That(File.Exists(path), Is.True);
            using (SHA256 hash = SHA256.Create())
                Assert.That(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty), Is.EqualTo(ExpectedBaseHash));
            Texture2D texture = LoadSource(path);
            Assert.That(new Vector2Int(texture.width, texture.height),
                Is.EqualTo(new Vector2Int(1254, 1254)));
            Color32[] pixels = texture.GetPixels32();
            Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True);
            Assert.That(pixels.Any(pixel => pixel.a == 255), Is.True);
        }

        [Test]
        public void AllFourSpritesExistShareExactCanvasPivotAndImportContract()
        {
            Assert.That(finalPower.IsConfigured, Is.True);
            Assert.That(finalPower.ChapterId, Is.EqualTo("power_station"));
            for (int layer = 0; layer < 4; layer++)
                Assert.That(finalPower.GetTint(layer), Is.EqualTo(Color.white),
                    "Authored overlay colors must not be multiplied by a second color tint.");
            Assert.That(CityBuildingArtAssetValidator.Validate(finalPower, "power_station"),
                Is.Empty);
            Sprite baseSprite = finalPower.GetSprite(0);
            Assert.That(baseSprite.rect.size, Is.EqualTo(new Vector2(1254, 1254)));
            Assert.That(baseSprite.pivot, Is.EqualTo(new Vector2(627, 627)));
            for (int layer = 0; layer < 4; layer++)
            {
                string path = M15CityBuildingFinalArtPreparation.PowerLayerPaths[layer];
                Assert.That(File.Exists(path), Is.True);
                Sprite sprite = finalPower.GetSprite(layer);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.rect, Is.EqualTo(baseSprite.rect));
                Assert.That(sprite.pivot, Is.EqualTo(baseSprite.pivot));
            }
        }

        [TestCase(1, 0.008f)]
        [TestCase(2, 0.020f)]
        [TestCase(3, 0.003f)]
        public void OverlaySource_IsTransparentSparseAndContainsNoOpaqueCanvas(int layer,
            float maximumCoverage)
        {
            string path = M15CityBuildingFinalArtPreparation.PowerLayerPaths[layer];
            Texture2D texture = LoadSource(path);
            Color32[] pixels = texture.GetPixels32();
            // Ignore sub-4% Gaussian tail pixels which are intentionally compact local glow.
            int visible = pixels.Count(pixel => pixel.a > 8);
            Assert.That(visible, Is.GreaterThan(0));
            Assert.That(visible / (float)pixels.Length, Is.LessThan(maximumCoverage));
            Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True);
            Assert.That(pixels.Where(pixel => pixel.a > 0).All(pixel =>
                pixel.r + pixel.g + pixel.b > 0), Is.True,
                "Overlay alpha must not carry semi-transparent black matte pixels.");
        }

        [Test]
        public void FiveStatesUseAcceptedPowerProfileAndStableBaseGeometry()
        {
            CityBuildingArtView view = Standalone(finalPower);
            Sprite architecture = finalPower.GetSprite(0);
            int[] activeLayers = { 1, 2, 3, 4, 4 };
            for (int state = 0; state < 5; state++)
            {
                view.Present((ChapterMapVisualState)state);
                Image[] images = view.ArtRoot.GetComponentsInChildren<Image>(true);
                Assert.That(images[0].sprite, Is.SameAs(architecture));
                Assert.That(images.Count(image => image.enabled), Is.EqualTo(activeLayers[state]));
                Assert.That(view.ArtRoot.sizeDelta, Is.EqualTo(new Vector2(189f, 105f)));
            }
            Assert.That(finalPower.GetOpacity(ChapterMapVisualState.Restored),
                Is.EqualTo(new Vector4(1f, 1f, 1f, 0.65f)));
        }

        [Test]
        public void PrototypeFinalSwitch_ReusesHierarchyAndKeepsCentralGridPending()
        {
            CityBuildingArtPrototypeController controller = Controller();
            CityBuildingArtView[] views = controller.GetComponentsInChildren<
                CityBuildingArtView>(true);
            int[] roots = views.Select(view => view.ArtRoot.GetInstanceID()).ToArray();
            for (int cycle = 0; cycle < 10; cycle++)
            {
                Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.True);
                Assert.That(views[0].Definition, Is.SameAs(finalPower));
                Assert.That(views[1].Definition, Is.SameAs(prototypeCentral));
                Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Prototype), Is.True);
                Assert.That(views[0].Definition, Is.SameAs(prototypePower));
                Assert.That(views.Select(view => view.ArtRoot.GetInstanceID()), Is.EqualTo(roots));
                Assert.That(views.All(view => view.ArtRoot.childCount == 4), Is.True);
            }
            controller.ShowSource(CityBuildingArtPreviewSource.Final);
            controller.SelectBuilding(1);
            Assert.That(controller.PreviewSource,
                Is.EqualTo(CityBuildingArtPreviewSource.Prototype));
            Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.False);
            Assert.That(finalCentral.IsConfigured, Is.False);
        }

        [Test]
        public void TenEmphasisCycles_ReturnExactTransformWithoutLayerSeparation()
        {
            CityBuildingArtView view = Standalone(finalPower);
            Vector3 position = view.ArtRoot.localPosition;
            Vector3 scale = view.ArtRoot.localScale;
            Quaternion rotation = view.ArtRoot.localRotation;
            Vector3[] childPositions = view.ArtRoot.Cast<Transform>()
                .Select(child => child.localPosition).ToArray();
            for (int cycle = 0; cycle < 10; cycle++)
            {
                view.ApplyEmphasis(1f);
                view.ResetEmphasis();
                Assert.That(view.ArtRoot.localPosition, Is.EqualTo(position));
                Assert.That(view.ArtRoot.localScale, Is.EqualTo(scale));
                Assert.That(view.ArtRoot.localRotation, Is.EqualTo(rotation));
                Assert.That(view.ArtRoot.Cast<Transform>().Select(child => child.localPosition),
                    Is.EqualTo(childPositions));
            }
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2340)]
        [TestCase(720, 1280)]
        public void FinalPowerStation_FitsRealMapRegionAtPortraitResolution(int width, int height)
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
                item.GetComponent<CityChapterNodeView>().ChapterId == "power_station");
            CityChapterNodeView node = view.GetComponent<CityChapterNodeView>();
            view.Present(ChapterMapVisualState.Restored);
            view.ApplyEmphasis(1f);
            Bounds art = BoundsIn(view.ArtRoot, node.transform);
            Bounds label = BoundsIn(node.Label.rectTransform, node.transform);
            Assert.That(art.min.y, Is.GreaterThan(label.max.y + 6f));
            Assert.That(art.min.x, Is.GreaterThan(node.HitArea.rect.xMin));
            Assert.That(art.max.x, Is.LessThan(node.HitArea.rect.xMax));
            Assert.That(art.max.y, Is.LessThan(node.HitArea.rect.yMax));
            Assert.That(view.ArtRoot.GetComponentsInChildren<Image>(true)
                .Count(image => image.enabled), Is.EqualTo(4));
        }

        [Test]
        public void PreviewArtifactIsDevelopmentOnly_ProductionRemainsPlaceholderBound()
        {
            const string preview =
                "Assets/NeonGrid/Documentation/Previews/PowerStation_StatePreview.png";
            Assert.That(File.Exists(preview), Is.True);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo(M15CityBuildingArtPrototypeBuilder.ScenePath));
            var root = NewObject("Production");
            CampaignRuntimeView map = root.AddComponent<CampaignRuntimeView>();
            map.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { },
                () => { }, campaign.CampaignUiTheme);
            map.ShowMap();
            Assert.That(map.GetComponentsInChildren<CityBuildingArtView>(true), Is.Empty);
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                Assert.That(AssetDatabase.GetDependencies(scene.path),
                    Has.None.StartsWith(M15CityBuildingFinalArtPreparation.PowerDirectory));
        }

        [Test]
        public void CentralGridFinalSpritesRemainAbsent()
        {
            Assert.That(M15CityBuildingFinalArtPreparation.CentralLayerPaths,
                Has.All.Matches<string>(path => !File.Exists(path)));
            Assert.That(finalCentral.IsConfigured, Is.False);
        }

        private CityBuildingArtPrototypeController Controller()
        {
            GameObject root = NewObject("E1B.1 Preview");
            CityBuildingArtPrototypeController controller =
                root.AddComponent<CityBuildingArtPrototypeController>();
            controller.SetData(campaign, prototypePower, prototypeCentral);
            controller.SetFinalDefinitions(finalPower, finalCentral);
            controller.Initialize();
            return controller;
        }

        private CityBuildingArtView Standalone(CityBuildingArtDefinition definition)
        {
            GameObject root = NewObject("Power Region", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(210f, 150f);
            Image placeholder = new GameObject("Placeholder", typeof(RectTransform),
                typeof(Image)).GetComponent<Image>();
            placeholder.transform.SetParent(root.transform, false);
            CityBuildingArtView view = root.AddComponent<CityBuildingArtView>();
            view.Initialize(definition, "power_station", (RectTransform)root.transform,
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

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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class ControlCenterFinalArtPreparationTests
    {
        private const string ExpectedSourceHash =
            "36310C3F1879EE5F52A1566B74AE07B57FF5C44ACA40E7613D3B9AB99B107AE6";
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CityBuildingArtDefinition control;
        private CityBuildingArtDefinition power;
        private CityBuildingArtDefinition central;
        private CityBuildingArtDefinition substation;
        private CityBuildingArtDefinition prototypePower;
        private CityBuildingArtDefinition prototypeCentral;
        private CampaignDefinition campaign;

        [SetUp]
        public void SetUp()
        {
            control = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15ControlCenterFinalArtPreparation.DefinitionPath);
            power = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15CityBuildingFinalArtPreparation.PowerDefinitionPath);
            central = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15CityBuildingFinalArtPreparation.CentralDefinitionPath);
            substation = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15SubstationFinalArtPreparation.DefinitionPath);
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
        public void AuthoritativeSourceIsPreservedAndDerivedCanvasIsSquare()
        {
            Assert.That(Hash(M15ControlCenterFinalArtPreparation.SourcePath),
                Is.EqualTo(ExpectedSourceHash));
            Texture2D source = Load(M15ControlCenterFinalArtPreparation.SourcePath);
            Texture2D derived = Load(M15ControlCenterFinalArtPreparation.LayerPaths[0]);
            Assert.That(new Vector2Int(source.width, source.height),
                Is.EqualTo(new Vector2Int(1484, 1060)));
            Assert.That(new Vector2Int(derived.width, derived.height),
                Is.EqualTo(new Vector2Int(1484, 1484)));
            Assert.That(derived.GetPixels32().Any(pixel => pixel.a == 0), Is.True);
        }

        [Test]
        public void BaseNeutralizesCommandGlowWhilePreservingPixelsOutsideEmissionMasks()
        {
            Texture2D source = Load(M15ControlCenterFinalArtPreparation.SourcePath);
            Texture2D derived = Load(M15ControlCenterFinalArtPreparation.LayerPaths[0]);
            int yOffset = (derived.height - source.height) / 2;
            int sourceBright = CountEmission(source.GetPixels32(), source.width,
                source.height, 0);
            int baseBright = CountEmission(derived.GetPixels32(), derived.width,
                source.height, yOffset);
            Assert.That(sourceBright, Is.GreaterThan(500));
            Assert.That(baseBright, Is.LessThan(sourceBright * 0.40f));
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] basePixels = derived.GetPixels32();
            int compared = 0;
            for (int y = 0; y < source.height; y += 31)
            for (int x = 0; x < source.width; x += 31)
            {
                int topY = source.height - 1 - y;
                if (InEmissionRegion(x, topY)) continue;
                Assert.That(basePixels[(y + yOffset) * derived.width + x],
                    Is.EqualTo(sourcePixels[y * source.width + x]));
                compared++;
            }
            Assert.That(compared, Is.GreaterThan(1000));
        }

        [Test]
        public void DefinitionAndFourLayersMeetStableIdRegistrationAndImportContract()
        {
            Assert.That(control, Is.Not.Null);
            Assert.That(control.ChapterId, Is.EqualTo("control_center"));
            Assert.That(control.IsConfigured, Is.True);
            Assert.That(CityBuildingArtAssetValidator.Validate(control, "control_center"),
                Is.Empty);
            Sprite architecture = control.GetSprite(0);
            Assert.That(architecture.rect.size, Is.EqualTo(new Vector2(1484f, 1484f)));
            Assert.That(architecture.pivot, Is.EqualTo(new Vector2(742f, 742f)));
            for (int layer = 0; layer < 4; layer++)
            {
                Sprite sprite = control.GetSprite(layer);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.rect, Is.EqualTo(architecture.rect));
                Assert.That(sprite.pivot, Is.EqualTo(architecture.pivot));
                Assert.That(control.GetTint(layer), Is.EqualTo(Color.white));
            }
        }

        [TestCase(1, 0.055f)]
        [TestCase(2, 0.018f)]
        [TestCase(3, 0.004f)]
        public void OverlaysAreControlledTransparentAndCarryNoBlackMatte(int layer,
            float maximumCoverage)
        {
            Texture2D texture = Load(M15ControlCenterFinalArtPreparation.LayerPaths[layer]);
            Color32[] pixels = texture.GetPixels32();
            int visible = pixels.Count(pixel => pixel.a > 8);
            Assert.That(visible, Is.GreaterThan(0));
            Assert.That(visible / (float)pixels.Length, Is.LessThan(maximumCoverage));
            Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True);
            Assert.That(pixels.Where(pixel => pixel.a > 0)
                .All(pixel => pixel.r + pixel.g + pixel.b > 0), Is.True);
        }

        [Test]
        public void FiveStateProfileIsExactCumulativeAndWarmFirst()
        {
            Vector4[] expected =
            {
                new Vector4(1f, 0f, 0f, 0f),
                new Vector4(1f, 0.40f, 0f, 0f),
                new Vector4(1f, 0.72f, 0.22f, 0f),
                new Vector4(1f, 0.90f, 0.68f, 0.20f),
                new Vector4(1f, 1f, 1f, 0.76f)
            };
            int[] activeLayers = { 1, 2, 3, 4, 4 };
            CityBuildingArtView view = Standalone(control, "control_center");
            for (int state = 0; state < 5; state++)
            {
                ChapterMapVisualState visualState = (ChapterMapVisualState)state;
                Assert.That(control.GetOpacity(visualState), Is.EqualTo(expected[state]));
                view.Present(visualState);
                Assert.That(view.ArtRoot.GetComponentsInChildren<Image>(true)
                    .Count(image => image.enabled), Is.EqualTo(activeLayers[state]));
                if (state > 0)
                    for (int layer = 0; layer < 4; layer++)
                        Assert.That(expected[state][layer],
                            Is.GreaterThanOrEqualTo(expected[state - 1][layer]));
            }
        }

        [Test]
        public void IsolatedPrototypeOffersControlFinalAndRestoresProgrammerFallback()
        {
            CityBuildingArtPrototypeController controller = Controller(control);
            CityBuildingArtView view = ControlView(controller);
            Assert.That(view.enabled, Is.False);
            Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.True);
            Assert.That(view.enabled, Is.True);
            Assert.That(view.Definition, Is.SameAs(control));
            controller.SelectBuilding(3);
            Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Prototype), Is.True);
            Assert.That(view.enabled, Is.False);
            Assert.That(view.GetComponent<CityChapterNodeView>().transform
                .Find("Building Silhouette").GetComponentsInChildren<Image>()
                .Any(image => image.enabled), Is.True);
        }

        [Test]
        public void InvalidOrMissingControlFinalLeavesFallbackAndDoesNotCreateFourthArtView()
        {
            CityBuildingArtPrototypeController missing = Controller(null);
            Assert.That(missing.GetComponentsInChildren<CityBuildingArtView>(true),
                Has.Length.EqualTo(3));
            CityChapterNodeView node = missing.MapView.GetComponentsInChildren<
                CityChapterNodeView>(true).Single(item => item.ChapterId == "control_center");
            Assert.That(node.transform.Find("Building Silhouette")
                .GetComponentsInChildren<Image>().Any(image => image.enabled), Is.True);
            CityBuildingArtPrototypeController mismatched = Controller(power);
            Assert.That(mismatched.GetComponentsInChildren<CityBuildingArtView>(true),
                Has.Length.EqualTo(3));
        }

        [Test]
        public void RepeatedSourceStateAndEmphasisSwitchingDoesNotGrowOrDrift()
        {
            CityBuildingArtPrototypeController controller = Controller(control);
            CityBuildingArtView view = ControlView(controller);
            int rootId = view.ArtRoot.GetInstanceID();
            int[] childIds = view.ArtRoot.Cast<Transform>()
                .Select(child => child.GetInstanceID()).ToArray();
            Vector3 position = view.ArtRoot.localPosition;
            Vector3 scale = view.ArtRoot.localScale;
            Quaternion rotation = view.ArtRoot.localRotation;
            controller.SelectBuilding(3);
            for (int cycle = 0; cycle < 10; cycle++)
            {
                Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.True);
                controller.ShowState(cycle % 5);
                view.ApplyEmphasis(1f);
                view.ResetEmphasis();
                Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Prototype), Is.True);
            }
            Assert.That(view.ArtRoot.GetInstanceID(), Is.EqualTo(rootId));
            Assert.That(view.ArtRoot.Cast<Transform>().Select(child => child.GetInstanceID()),
                Is.EqualTo(childIds));
            Assert.That(view.ArtRoot.localPosition, Is.EqualTo(position));
            Assert.That(view.ArtRoot.localScale, Is.EqualTo(scale));
            Assert.That(view.ArtRoot.localRotation, Is.EqualTo(rotation));
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2340)]
        [TestCase(720, 1280)]
        public void ControlFinalStaysInsideNodeAndAboveLabelAtPortraitSizes(int width, int height)
        {
            CityBuildingArtPrototypeController controller = Controller(control);
            Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.True);
            float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
            foreach (Canvas canvas in controller.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta =
                    new Vector2(width / scale, height / scale);
            }
            Canvas.ForceUpdateCanvases();
            CityBuildingArtView view = ControlView(controller);
            CityChapterNodeView node = view.GetComponent<CityChapterNodeView>();
            view.Present(ChapterMapVisualState.Restored);
            view.ApplyEmphasis(1f);
            Bounds art = BoundsIn(view.ArtRoot, node.transform);
            Bounds label = BoundsIn(node.Label.rectTransform, node.transform);
            Assert.That(art.min.y, Is.GreaterThan(label.max.y + 6f));
            Assert.That(art.min.x, Is.GreaterThan(node.HitArea.rect.xMin));
            Assert.That(art.max.x, Is.LessThan(node.HitArea.rect.xMax));
            Assert.That(art.max.y, Is.LessThan(node.HitArea.rect.yMax));
            Assert.That(node.HitArea.sizeDelta, Is.EqualTo(new Vector2(280f, 300f)));
            Assert.That(node.HitArea.anchoredPosition, Is.EqualTo(new Vector2(60f, 420f)));
        }

        [Test]
        public void PrototypeSceneBindsControlButProductionCampaignRemainsExactlyE1C()
        {
            Assert.That(File.Exists(M15ControlCenterFinalArtPreparation.PreviewPath), Is.True);
            Scene scene = EditorSceneManager.OpenScene(M15CityBuildingArtPrototypeBuilder.ScenePath,
                OpenSceneMode.Additive);
            try
            {
                CityBuildingArtPrototypeController controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        CityBuildingArtPrototypeController>(true)).Single();
                Assert.That(controller.FinalControlCenter, Is.SameAs(control));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            Assert.That(campaign.CityBuildingArt.Select(binding => binding.ChapterId),
                Is.EqualTo(new[] { "power_station", "central_grid" }));
            Assert.That(campaign.GetCityBuildingArt("control_center"), Is.Null);
            Assert.That(EditorBuildSettings.scenes.Select(item => item.path),
                Has.None.EqualTo(M15CityBuildingArtPrototypeBuilder.ScenePath));
        }

        private CityBuildingArtPrototypeController Controller(CityBuildingArtDefinition finalControl)
        {
            GameObject root = NewObject("Control Center isolated preview");
            CityBuildingArtPrototypeController controller =
                root.AddComponent<CityBuildingArtPrototypeController>();
            controller.SetData(campaign, prototypePower, prototypeCentral);
            controller.SetFinalDefinitions(power, central, substation, finalControl);
            controller.Initialize();
            return controller;
        }

        private static CityBuildingArtView ControlView(
            CityBuildingArtPrototypeController controller) => controller
            .GetComponentsInChildren<CityBuildingArtView>(true)
            .Single(view => view.GetComponent<CityChapterNodeView>().ChapterId == "control_center");

        private CityBuildingArtView Standalone(CityBuildingArtDefinition definition,
            string chapterId)
        {
            GameObject root = NewObject("Control Center art", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(190f, 190f);
            Image placeholder = new GameObject("Fallback", typeof(RectTransform),
                typeof(Image)).GetComponent<Image>();
            placeholder.transform.SetParent(root.transform, false);
            CityBuildingArtView view = root.AddComponent<CityBuildingArtView>();
            view.Initialize(definition, chapterId, (RectTransform)root.transform,
                new[] { placeholder });
            return view;
        }

        private Texture2D Load(string path)
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
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty);
        }

        private static int CountEmission(Color32[] pixels, int width, int sourceHeight, int yOffset)
        {
            int count = 0;
            for (int y = 0; y < sourceHeight; y++)
            for (int x = 0; x < 1484; x++)
            {
                int topY = sourceHeight - 1 - y;
                if (!InEmissionRegion(x, topY)) continue;
                Color32 pixel = pixels[(y + yOffset) * width + x];
                bool warm = pixel.r > 125 && pixel.r > pixel.b * 1.28f &&
                            pixel.g > pixel.b * 1.08f;
                bool monitor = pixel.b > 85 && pixel.g > 75 &&
                               pixel.b > pixel.r * 1.08f;
                bool beacon = pixel.r > 125 && pixel.r > pixel.g * 1.25f;
                if (pixel.a > 0 && (warm || monitor || beacon)) count++;
            }
            return count;
        }

        private static bool InEmissionRegion(int x, int y) =>
            InRect(x, y, 492, 914, 470, 634) || InRect(x, y, 220, 286, 550, 603) ||
            InRect(x, y, 367, 416, 590, 686) || InRect(x, y, 510, 683, 672, 726) ||
            InRect(x, y, 748, 795, 682, 780) || InRect(x, y, 980, 1160, 716, 810) ||
            InRect(x, y, 1242, 1336, 705, 790) || InRect(x, y, 695, 737, 15, 75) ||
            InRect(x, y, 742, 790, 190, 247) || InRect(x, y, 480, 524, 235, 291) ||
            InRect(x, y, 392, 437, 360, 418) || InRect(x, y, 998, 1043, 360, 420) ||
            InRect(x, y, 1285, 1332, 575, 632);

        private static bool InRect(int x, int y, int xMin, int xMax, int yMin, int yMax) =>
            x >= xMin && x <= xMax && y >= yMin && y <= yMax;

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

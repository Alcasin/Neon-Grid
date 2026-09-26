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
    public sealed class SubstationFinalArtPreparationTests
    {
        private const string ExpectedSourceHash =
            "E12B9F811033E3185B28F80A9D147DC7E408E34C0EA0655F8B7895B0F4C4E14A";
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private CityBuildingArtDefinition substation;
        private CityBuildingArtDefinition power;
        private CityBuildingArtDefinition central;
        private CityBuildingArtDefinition prototypePower;
        private CityBuildingArtDefinition prototypeCentral;
        private CampaignDefinition campaign;

        [SetUp]
        public void SetUp()
        {
            substation = AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(
                M15SubstationFinalArtPreparation.DefinitionPath);
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
                if (cleanup[index] != null) UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void AuthoritativeSource_IsPreservedAndDerivedCanvasIsSquare()
        {
            Assert.That(Hash(M15SubstationFinalArtPreparation.SourcePath),
                Is.EqualTo(ExpectedSourceHash));
            Texture2D source = Load(M15SubstationFinalArtPreparation.SourcePath);
            Texture2D derived = Load(M15SubstationFinalArtPreparation.LayerPaths[0]);
            Assert.That(new Vector2Int(source.width, source.height),
                Is.EqualTo(new Vector2Int(1484, 1060)));
            Assert.That(new Vector2Int(derived.width, derived.height),
                Is.EqualTo(new Vector2Int(1484, 1484)));
            Assert.That(derived.GetPixels32().Any(pixel => pixel.a == 0), Is.True);
        }

        [Test]
        public void BaseNeutralizesAuthoredEmissiveWindowsWithoutChangingSourceGeometry()
        {
            Texture2D source = Load(M15SubstationFinalArtPreparation.SourcePath);
            Texture2D derived = Load(M15SubstationFinalArtPreparation.LayerPaths[0]);
            int yOffset = (derived.height - source.height) / 2;
            int sourceBright = CountBrightEmissive(source.GetPixels32(), source.width,
                source.height, 0);
            int baseBright = CountBrightEmissive(derived.GetPixels32(), derived.width,
                source.height, yOffset);
            Assert.That(sourceBright, Is.GreaterThan(100));
            Assert.That(baseBright, Is.LessThan(sourceBright * 0.35f));

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
        public void DefinitionAndFourLayersMeetRegistrationAndImportContract()
        {
            Assert.That(substation, Is.Not.Null);
            Assert.That(substation.ChapterId, Is.EqualTo("substation"));
            Assert.That(substation.IsConfigured, Is.True);
            Assert.That(CityBuildingArtAssetValidator.Validate(substation, "substation"),
                Is.Empty);
            Sprite architecture = substation.GetSprite(0);
            Assert.That(architecture.rect.size, Is.EqualTo(new Vector2(1484f, 1484f)));
            Assert.That(architecture.pivot, Is.EqualTo(new Vector2(742f, 742f)));
            for (int layer = 0; layer < 4; layer++)
            {
                Sprite sprite = substation.GetSprite(layer);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.rect, Is.EqualTo(architecture.rect));
                Assert.That(sprite.pivot, Is.EqualTo(architecture.pivot));
                Assert.That(substation.GetTint(layer), Is.EqualTo(Color.white));
            }
        }

        [TestCase(1, 0.010f)]
        [TestCase(2, 0.018f)]
        [TestCase(3, 0.004f)]
        public void OverlaysAreSparseTransparentAndCarryNoBlackMatte(int layer,
            float maximumCoverage)
        {
            Texture2D texture = Load(M15SubstationFinalArtPreparation.LayerPaths[layer]);
            Color32[] pixels = texture.GetPixels32();
            int visible = pixels.Count(pixel => pixel.a > 8);
            Assert.That(visible, Is.GreaterThan(0));
            Assert.That(visible / (float)pixels.Length, Is.LessThan(maximumCoverage));
            Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True);
            Assert.That(pixels.Where(pixel => pixel.a > 0)
                .All(pixel => pixel.r + pixel.g + pixel.b > 0), Is.True);
        }

        [Test]
        public void FiveStateProfileIsCumulativeWarmFirstAndControlled()
        {
            Vector4[] expected =
            {
                new Vector4(1f, 0f, 0f, 0f),
                new Vector4(1f, 0.40f, 0f, 0f),
                new Vector4(1f, 0.72f, 0.22f, 0f),
                new Vector4(1f, 0.90f, 0.68f, 0.18f),
                new Vector4(1f, 1f, 1f, 0.72f)
            };
            int[] activeLayers = { 1, 2, 3, 4, 4 };
            CityBuildingArtView view = Standalone(substation, "substation");
            for (int state = 0; state < 5; state++)
            {
                ChapterMapVisualState visualState = (ChapterMapVisualState)state;
                Assert.That(substation.GetOpacity(visualState), Is.EqualTo(expected[state]));
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
        public void IsolatedPrototypeOffersSubstationFinalAndRestoresProgrammerFallback()
        {
            CityBuildingArtPrototypeController controller = Controller(substation);
            CityBuildingArtView view = SubstationView(controller);
            Assert.That(view.enabled, Is.False);
            Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.True);
            Assert.That(view.enabled, Is.True);
            Assert.That(view.Definition, Is.SameAs(substation));
            controller.SelectBuilding(2);
            Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Prototype), Is.True);
            Assert.That(view.enabled, Is.False);
            Image[] fallback = view.GetComponent<CityChapterNodeView>().transform
                .Find("Building Silhouette").GetComponentsInChildren<Image>();
            Assert.That(fallback.Any(image => image.enabled), Is.True);
        }

        [Test]
        public void InvalidOrMissingSubstationFinalLeavesVisibleFallbackAndNoThirdArtView()
        {
            CityBuildingArtPrototypeController missing = Controller(null);
            Assert.That(missing.GetComponentsInChildren<CityBuildingArtView>(true),
                Has.Length.EqualTo(2));
            CityChapterNodeView missingNode = missing.MapView.GetComponentsInChildren<
                CityChapterNodeView>(true).Single(node => node.ChapterId == "substation");
            Assert.That(missingNode.transform.Find("Building Silhouette")
                .GetComponentsInChildren<Image>().Any(image => image.enabled), Is.True);

            CityBuildingArtPrototypeController mismatched = Controller(power);
            Assert.That(mismatched.GetComponentsInChildren<CityBuildingArtView>(true),
                Has.Length.EqualTo(2));
        }

        [Test]
        public void RepeatedSourceStateAndEmphasisSwitchingDoesNotGrowOrDrift()
        {
            CityBuildingArtPrototypeController controller = Controller(substation);
            CityBuildingArtView view = SubstationView(controller);
            int rootId = view.ArtRoot.GetInstanceID();
            int[] childIds = view.ArtRoot.Cast<Transform>()
                .Select(child => child.GetInstanceID()).ToArray();
            Vector3 basePosition = view.ArtRoot.localPosition;
            Vector3 baseScale = view.ArtRoot.localScale;
            Quaternion baseRotation = view.ArtRoot.localRotation;
            controller.SelectBuilding(2);
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
            Assert.That(view.ArtRoot.localPosition, Is.EqualTo(basePosition));
            Assert.That(view.ArtRoot.localScale, Is.EqualTo(baseScale));
            Assert.That(view.ArtRoot.localRotation, Is.EqualTo(baseRotation));
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2340)]
        [TestCase(720, 1280)]
        public void SubstationFinalStaysInsideNodeAndAboveLabelAtPortraitSizes(int width,
            int height)
        {
            CityBuildingArtPrototypeController controller = Controller(substation);
            Assert.That(controller.ShowSource(CityBuildingArtPreviewSource.Final), Is.True);
            float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
            foreach (Canvas canvas in controller.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta =
                    new Vector2(width / scale, height / scale);
            }
            Canvas.ForceUpdateCanvases();
            CityBuildingArtView view = SubstationView(controller);
            CityChapterNodeView node = view.GetComponent<CityChapterNodeView>();
            view.Present(ChapterMapVisualState.Restored);
            view.ApplyEmphasis(1f);
            Bounds art = BoundsIn(view.ArtRoot, node.transform);
            Bounds label = BoundsIn(node.Label.rectTransform, node.transform);
            Assert.That(art.min.y, Is.GreaterThan(label.max.y + 6f));
            Assert.That(art.min.x, Is.GreaterThan(node.HitArea.rect.xMin));
            Assert.That(art.max.x, Is.LessThan(node.HitArea.rect.xMax));
            Assert.That(art.max.y, Is.LessThan(node.HitArea.rect.yMax));
            Assert.That(node.HitArea.sizeDelta, Is.EqualTo(new Vector2(280f, 280f)));
            Assert.That(node.HitArea.anchoredPosition, Is.EqualTo(new Vector2(-340f, 0f)));
        }

        [Test]
        public void PrototypeSceneBindsSubstationButProductionCampaignRemainsExactlyE1C()
        {
            Assert.That(File.Exists(M15SubstationFinalArtPreparation.PreviewPath), Is.True);
            Scene scene = EditorSceneManager.OpenScene(M15CityBuildingArtPrototypeBuilder.ScenePath,
                OpenSceneMode.Additive);
            try
            {
                CityBuildingArtPrototypeController controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        CityBuildingArtPrototypeController>(true)).Single();
                Assert.That(controller.FinalSubstation, Is.SameAs(substation));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }

            Assert.That(campaign.CityBuildingArt.Select(binding => binding.ChapterId),
                Is.EqualTo(new[] { "power_station", "central_grid" }));
            Assert.That(campaign.GetCityBuildingArt("substation"), Is.Null);
            Assert.That(EditorBuildSettings.scenes.Select(item => item.path),
                Has.None.EqualTo(M15CityBuildingArtPrototypeBuilder.ScenePath));
        }

        private CityBuildingArtPrototypeController Controller(CityBuildingArtDefinition finalSub)
        {
            GameObject root = NewObject("Substation isolated preview");
            CityBuildingArtPrototypeController controller =
                root.AddComponent<CityBuildingArtPrototypeController>();
            controller.SetData(campaign, prototypePower, prototypeCentral);
            controller.SetFinalDefinitions(power, central, finalSub);
            controller.Initialize();
            return controller;
        }

        private static CityBuildingArtView SubstationView(
            CityBuildingArtPrototypeController controller) => controller
            .GetComponentsInChildren<CityBuildingArtView>(true)
            .Single(view => view.GetComponent<CityChapterNodeView>().ChapterId == "substation");

        private CityBuildingArtView Standalone(CityBuildingArtDefinition definition,
            string chapterId)
        {
            GameObject root = NewObject("Substation art", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(210f, 150f);
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
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false),
                Is.True);
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

        private static int CountBrightEmissive(Color32[] pixels, int width,
            int sourceHeight, int yOffset)
        {
            int count = 0;
            for (int y = 0; y < sourceHeight; y++)
            for (int x = 0; x < 1484; x++)
            {
                int topY = sourceHeight - 1 - y;
                if (!InEmissionRegion(x, topY)) continue;
                Color32 pixel = pixels[(y + yOffset) * width + x];
                int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                if (pixel.a > 0 && (min > 130 || max > 135 && pixel.r > pixel.b * 1.35f &&
                                    pixel.g > pixel.b * 1.12f)) count++;
            }
            return count;
        }

        private static bool InEmissionRegion(int x, int y) =>
            InRect(x, y, 568, 637, 552, 603) || InRect(x, y, 638, 718, 548, 604) ||
            InRect(x, y, 730, 808, 590, 645) || InRect(x, y, 749, 785, 285, 341);

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

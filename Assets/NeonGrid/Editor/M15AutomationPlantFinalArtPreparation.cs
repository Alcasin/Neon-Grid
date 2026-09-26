using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeonGrid.Data;
using NeonGrid.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonGrid.Editor
{
    /// <summary>Deterministic, editor-only Automation Plant final-art preparation.</summary>
    public static class M15AutomationPlantFinalArtPreparation
    {
        public const string Directory = M15CityBuildingFinalArtPreparation.Root + "/AutomationPlant";
        public const string SourcePath = Directory + "/AutomationPlant_Source.png";
        public const string DefinitionPath = Directory + "/AutomationPlant_Final.asset";
        public const string PreviewPath =
            "Assets/NeonGrid/Documentation/Previews/AutomationPlant_StatePreview.png";
        public static readonly string[] LayerPaths =
        {
            Directory + "/AutomationPlant_Base.png",
            Directory + "/AutomationPlant_WarmLights.png",
            Directory + "/AutomationPlant_Energy.png",
            Directory + "/AutomationPlant_Core.png"
        };

        private static readonly Color32 Warm = new Color32(255, 177, 48, 255);
        private static readonly Color32 Energy = new Color32(54, 222, 242, 255);
        private static readonly Color32 Core = new Color32(190, 255, 250, 255);
        private static readonly Vector4[] Profile =
        {
            new Vector4(1f, 0f, 0f, 0f),
            new Vector4(1f, 0.40f, 0f, 0f),
            new Vector4(1f, 0.72f, 0.22f, 0f),
            new Vector4(1f, 0.90f, 0.68f, 0.20f),
            new Vector4(1f, 1f, 1f, 0.76f)
        };

        [MenuItem("Neon Grid/Prepare M15 Automation Plant Final Art")]
        public static void Prepare()
        {
            if (!File.Exists(SourcePath))
                throw new FileNotFoundException("Authoritative Automation Plant source is missing.",
                    SourcePath);
            GenerateLayers();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in LayerPaths) ConfigureImporter(path);

            CityBuildingArtDefinition definition =
                AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CityBuildingArtDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            Sprite[] sprites = LayerPaths.Select(path =>
                AssetDatabase.LoadAssetAtPath<Sprite>(path)).ToArray();
            definition.SetData("automation_plant", sprites[0], sprites[1], sprites[2], sprites[3],
                Color.white, Color.white, Color.white, Color.white, Profile,
                new Vector2(0.92f, 0.74f), new Vector2(0f, 18f), 1f);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            IReadOnlyList<string> issues = CityBuildingArtAssetValidator.Validate(definition,
                "automation_plant");
            if (issues.Count > 0)
                throw new InvalidOperationException("Automation Plant final art validation failed:\n" +
                                                    string.Join("\n", issues));
            BindIsolatedPrototype(definition);
            Debug.Log("Prepared valid isolated Automation Plant final art. Production bindings unchanged.");
        }

        private static void GenerateLayers()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(SourcePath), false))
                throw new InvalidOperationException("Automation Plant source PNG could not be decoded.");
            try
            {
                int size = Mathf.Max(source.width, source.height);
                int xOffset = (size - source.width) / 2;
                int yOffset = (size - source.height) / 2;
                Color32[] sourcePixels = source.GetPixels32();
                Color32[] basePixels = new Color32[size * size];
                Color32[] warmPixels = new Color32[size * size];
                Color32[] energyPixels = new Color32[size * size];
                Color32[] corePixels = new Color32[size * size];
                for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                {
                    Color32 pixel = sourcePixels[y * source.width + x];
                    int topY = source.height - 1 - y;
                    if (IsEmissiveRegion(x, topY) &&
                        (IsEmission(pixel) || IsBrightFixture(x, topY, pixel)))
                        pixel = Neutralize(pixel);
                    basePixels[(y + yOffset) * size + x + xOffset] = pixel;
                }

                // Warm-first activation: facility windows, service strips and safety beacons.
                RectInt[] facilityLights =
                {
                    new RectInt(695, 157, 20, 119), new RectInt(787, 175, 20, 119),
                    new RectInt(697, 286, 17, 72), new RectInt(816, 296, 16, 68),
                    new RectInt(225, 533, 65, 19), new RectInt(487, 526, 57, 23),
                    new RectInt(631, 544, 64, 20), new RectInt(822, 580, 60, 20),
                    new RectInt(379, 569, 19, 88), new RectInt(985, 632, 19, 105),
                    new RectInt(598, 574, 139, 137), new RectInt(780, 613, 145, 141)
                };
                foreach (RectInt light in facilityLights)
                    GlowRectangle(warmPixels, size, xOffset, yOffset, source.height,
                        light, Warm, light.width > 100 ? 14f : 9f,
                        light.width > 100 ? 0.44f : 0.62f);
                Vector2[] beacons =
                {
                    new Vector2(771, 57), new Vector2(969, 232),
                    new Vector2(327, 400), new Vector2(1368, 402)
                };
                foreach (Vector2 beacon in beacons)
                    GlowDisc(warmPixels, size, xOffset + Mathf.RoundToInt(beacon.x),
                        yOffset + source.height - 1 - Mathf.RoundToInt(beacon.y),
                        7f, 18f, Warm, 0.70f);

                // Sparse automation/routing activation follows authored conveyors and conduits.
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(760, 106), new Vector2(760, 246),
                        new Vector2(760, 376) }, Energy, 2.2f, 8f, 0.62f);
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(480, 713), new Vector2(589, 747),
                        new Vector2(700, 784), new Vector2(789, 820) },
                    Energy, 2.4f, 8f, 0.58f);
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(743, 716), new Vector2(839, 753),
                        new Vector2(930, 792), new Vector2(1023, 834) },
                    Energy, 2.4f, 8f, 0.58f);
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(1018, 464), new Vector2(1105, 486),
                        new Vector2(1206, 506) }, Energy, 2f, 7f, 0.52f);
                GlowRectangle(energyPixels, size, xOffset, yOffset, source.height,
                    new RectInt(654, 635, 43, 26), Energy, 7f, 0.54f);
                GlowRectangle(energyPixels, size, xOffset, yOffset, source.height,
                    new RectInt(846, 681, 43, 27), Energy, 7f, 0.54f);

                // Compact restored machine-bay and tower instrumentation highlights.
                GlowDisc(corePixels, size, xOffset + 675,
                    yOffset + source.height - 1 - 654, 5f, 14f, Core, 0.72f);
                GlowDisc(corePixels, size, xOffset + 865,
                    yOffset + source.height - 1 - 704, 5f, 14f, Core, 0.72f);
                GlowRectangle(corePixels, size, xOffset, yOffset, source.height,
                    new RectInt(744, 357, 31, 13), Core, 6f, 0.64f);

                WritePng(LayerPaths[0], size, size, basePixels);
                WritePng(LayerPaths[1], size, size, warmPixels);
                WritePng(LayerPaths[2], size, size, energyPixels);
                WritePng(LayerPaths[3], size, size, corePixels);
                WriteStatePreview(size, basePixels, warmPixels, energyPixels, corePixels);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        private static bool IsEmissiveRegion(int x, int y) =>
            InRect(x, y, 680, 730, 140, 375) || InRect(x, y, 775, 845, 155, 380) ||
            InRect(x, y, 205, 306, 515, 570) || InRect(x, y, 360, 415, 550, 675) ||
            InRect(x, y, 470, 560, 510, 570) || InRect(x, y, 615, 710, 525, 585) ||
            InRect(x, y, 805, 900, 560, 620) || InRect(x, y, 965, 1025, 610, 750) ||
            InRect(x, y, 580, 755, 555, 730) || InRect(x, y, 760, 945, 595, 775) ||
            InRect(x, y, 744, 794, 30, 85) || InRect(x, y, 942, 994, 205, 260) ||
            InRect(x, y, 300, 352, 370, 430) || InRect(x, y, 1340, 1394, 370, 435);

        private static bool InRect(int x, int y, int xMin, int xMax, int yMin, int yMax) =>
            x >= xMin && x <= xMax && y >= yMin && y <= yMax;

        private static bool IsEmission(Color32 pixel)
        {
            if (pixel.a == 0) return false;
            bool warm = pixel.r > 125 && pixel.r > pixel.b * 1.28f &&
                        pixel.g > pixel.b * 1.08f;
            bool beacon = pixel.r > 125 && pixel.r > pixel.g * 1.25f;
            return warm || beacon;
        }

        private static bool IsBrightFixture(int x, int y, Color32 pixel)
        {
            if (pixel.a == 0 || pixel.r + pixel.g + pixel.b < 500) return false;
            return InRect(x, y, 690, 720, 150, 285) ||
                   InRect(x, y, 782, 812, 168, 302) ||
                   InRect(x, y, 690, 720, 280, 370) ||
                   InRect(x, y, 810, 838, 290, 372) ||
                   InRect(x, y, 215, 298, 525, 560) ||
                   InRect(x, y, 372, 405, 560, 666) ||
                   InRect(x, y, 478, 553, 518, 558) ||
                   InRect(x, y, 622, 704, 536, 592) ||
                   InRect(x, y, 812, 890, 572, 610) ||
                   InRect(x, y, 975, 1013, 620, 745) ||
                   InRect(x, y, 748, 792, 30, 86) ||
                   InRect(x, y, 944, 994, 205, 260) ||
                   InRect(x, y, 302, 352, 370, 430) ||
                   InRect(x, y, 1340, 1394, 370, 435);
        }

        private static Color32 Neutralize(Color32 pixel)
        {
            byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(
                (pixel.r * 0.18f + pixel.g * 0.35f + pixel.b * 0.15f) * 0.58f), 24, 104);
            return new Color32((byte)(value * 0.86f), (byte)(value * 0.93f), value, pixel.a);
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing importer: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void BindIsolatedPrototype(CityBuildingArtDefinition automationPlant)
        {
            Scene scene = EditorSceneManager.OpenScene(M15CityBuildingArtPrototypeBuilder.ScenePath,
                OpenSceneMode.Single);
            CityBuildingArtPrototypeController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<
                    CityBuildingArtPrototypeController>(true)).Single();
            controller.SetFinalDefinitions(controller.FinalPowerStation,
                controller.FinalCentralGrid, controller.FinalSubstation,
                controller.FinalControlCenter, automationPlant);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WriteStatePreview(int sourceSize, Color32[] basePixels,
            Color32[] warmPixels, Color32[] energyPixels, Color32[] corePixels)
        {
            const int panelSize = 288;
            const int gap = 8;
            int width = panelSize * 5 + gap * 4;
            var preview = Enumerable.Repeat(new Color32(5, 10, 20, 255),
                width * panelSize).ToArray();
            Color32[][] layers = { basePixels, warmPixels, energyPixels, corePixels };
            for (int state = 0; state < 5; state++)
            {
                int xOffset = state * (panelSize + gap);
                for (int y = 0; y < panelSize; y++)
                for (int x = 0; x < panelSize; x++)
                {
                    int sourceIndex = (y * sourceSize / panelSize) * sourceSize +
                                      x * sourceSize / panelSize;
                    Color32 result = preview[y * width + xOffset + x];
                    for (int layer = 0; layer < 4; layer++)
                        result = Composite(result, layers[layer][sourceIndex],
                            Profile[state][layer]);
                    preview[y * width + xOffset + x] = result;
                }
            }
            WritePng(PreviewPath, width, panelSize, preview);
        }

        private static Color32 Composite(Color32 under, Color32 over, float opacity)
        {
            float alpha = over.a / 255f * opacity;
            if (alpha <= 0f) return under;
            return new Color32((byte)Mathf.RoundToInt(Mathf.Lerp(under.r, over.r, alpha)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(under.g, over.g, alpha)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(under.b, over.b, alpha)), 255);
        }

        private static void WritePng(string path, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void GlowRectangle(Color32[] pixels, int size, int xOffset, int yOffset,
            int sourceHeight, RectInt rect, Color32 color, float glow, float strength)
        {
            int left = xOffset + rect.xMin, right = xOffset + rect.xMax;
            int bottom = yOffset + sourceHeight - rect.yMax, top = yOffset + sourceHeight - rect.yMin;
            for (int y = Mathf.FloorToInt(bottom - glow); y <= Mathf.CeilToInt(top + glow); y++)
            for (int x = Mathf.FloorToInt(left - glow); x <= Mathf.CeilToInt(right + glow); x++)
            {
                float dx = Mathf.Max(left - x, x - right);
                float dy = Mathf.Max(bottom - y, y - top);
                float distance = Mathf.Sqrt(Mathf.Max(0f, dx) * Mathf.Max(0f, dx) +
                                            Mathf.Max(0f, dy) * Mathf.Max(0f, dy));
                float alpha = distance <= 0f ? strength :
                    strength * Mathf.Clamp01(1f - distance / glow) * 0.5f;
                Blend(pixels, size, x, y, color, alpha);
            }
        }

        private static void GlowDisc(Color32[] pixels, int size, int centerX, int centerY,
            float coreRadius, float glowRadius, Color32 color, float strength)
        {
            int radius = Mathf.CeilToInt(glowRadius);
            for (int y = centerY - radius; y <= centerY + radius; y++)
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                if (distance > glowRadius) continue;
                float alpha = distance <= coreRadius ? strength :
                    strength * (1f - (distance - coreRadius) /
                                 (glowRadius - coreRadius)) * 0.55f;
                Blend(pixels, size, x, y, color, alpha);
            }
        }

        private static void Polyline(Color32[] pixels, int size, int xOffset, int yOffset,
            int sourceHeight, IReadOnlyList<Vector2> points, Color32 color,
            float coreWidth, float glowWidth, float strength)
        {
            for (int index = 0; index + 1 < points.Count; index++)
            {
                Vector2 a = ToCanvas(points[index], xOffset, yOffset, sourceHeight);
                Vector2 b = ToCanvas(points[index + 1], xOffset, yOffset, sourceHeight);
                for (int y = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - glowWidth);
                     y <= Mathf.CeilToInt(Mathf.Max(a.y, b.y) + glowWidth); y++)
                for (int x = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - glowWidth);
                     x <= Mathf.CeilToInt(Mathf.Max(a.x, b.x) + glowWidth); x++)
                {
                    float distance = DistanceToSegment(new Vector2(x, y), a, b);
                    if (distance > glowWidth) continue;
                    float alpha = distance <= coreWidth ? strength :
                        strength * (1f - (distance - coreWidth) /
                                     (glowWidth - coreWidth)) * 0.42f;
                    Blend(pixels, size, x, y, color, alpha);
                }
            }
        }

        private static Vector2 ToCanvas(Vector2 point, int xOffset, int yOffset, int sourceHeight) =>
            new Vector2(xOffset + point.x, yOffset + sourceHeight - 1 - point.y);

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float squared = delta.sqrMagnitude;
            if (squared <= Mathf.Epsilon) return Vector2.Distance(point, a);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / squared);
            return Vector2.Distance(point, a + delta * t);
        }

        private static void Blend(Color32[] pixels, int size, int x, int y, Color32 color,
            float alpha)
        {
            if (x < 0 || y < 0 || x >= size || y >= size || alpha <= 0f) return;
            int index = y * size + x;
            float existing = pixels[index].a / 255f;
            pixels[index] = new Color32(color.r, color.g, color.b,
                (byte)Mathf.RoundToInt(Mathf.Clamp01(existing + alpha * (1f - existing)) * 255f));
        }
    }
}

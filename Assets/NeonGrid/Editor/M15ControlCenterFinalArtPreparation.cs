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
    /// <summary>Deterministic, editor-only Control Center final-art preparation.</summary>
    public static class M15ControlCenterFinalArtPreparation
    {
        public const string Directory = M15CityBuildingFinalArtPreparation.Root + "/ControlCenter";
        public const string SourcePath = Directory + "/ControlCenter_Source.png";
        public const string DefinitionPath = Directory + "/ControlCenter_Final.asset";
        public const string PreviewPath =
            "Assets/NeonGrid/Documentation/Previews/ControlCenter_StatePreview.png";
        public static readonly string[] LayerPaths =
        {
            Directory + "/ControlCenter_Base.png",
            Directory + "/ControlCenter_WarmLights.png",
            Directory + "/ControlCenter_Energy.png",
            Directory + "/ControlCenter_Core.png"
        };

        private static readonly Color32 Warm = new Color32(255, 178, 58, 255);
        private static readonly Color32 Energy = new Color32(54, 221, 242, 255);
        private static readonly Color32 Core = new Color32(190, 255, 250, 255);
        private static readonly Vector4[] Profile =
        {
            new Vector4(1f, 0f, 0f, 0f),
            new Vector4(1f, 0.40f, 0f, 0f),
            new Vector4(1f, 0.72f, 0.22f, 0f),
            new Vector4(1f, 0.90f, 0.68f, 0.20f),
            new Vector4(1f, 1f, 1f, 0.76f)
        };

        [MenuItem("Neon Grid/Prepare M15 Control Center Final Art")]
        public static void Prepare()
        {
            if (!File.Exists(SourcePath))
                throw new FileNotFoundException("Authoritative Control Center source is missing.",
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
            definition.SetData("control_center", sprites[0], sprites[1], sprites[2], sprites[3],
                Color.white, Color.white, Color.white, Color.white, Profile,
                new Vector2(0.78f, 0.78f), new Vector2(0f, 24f), 1f);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            IReadOnlyList<string> issues = CityBuildingArtAssetValidator.Validate(definition,
                "control_center");
            if (issues.Count > 0)
                throw new InvalidOperationException("Control Center final art validation failed:\n" +
                                                    string.Join("\n", issues));
            BindIsolatedPrototype(definition);
            Debug.Log("Prepared valid isolated Control Center final art. Production bindings unchanged.");
        }

        private static void GenerateLayers()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(SourcePath), false))
                throw new InvalidOperationException("Control Center source PNG could not be decoded.");
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
                    if (IsEmissiveRegion(x, topY) && IsEmission(pixel))
                        pixel = Neutralize(pixel);
                    basePixels[(y + yOffset) * size + x + xOffset] = pixel;
                }

                // Warm-first activation: command room, entry strips, service indicators, beacons.
                GlowRectangle(warmPixels, size, xOffset, yOffset, source.height,
                    new RectInt(503, 486, 58, 104), Warm, 13f, 0.46f);
                GlowRectangle(warmPixels, size, xOffset, yOffset, source.height,
                    new RectInt(568, 492, 120, 91), Warm, 15f, 0.58f);
                GlowRectangle(warmPixels, size, xOffset, yOffset, source.height,
                    new RectInt(695, 500, 119, 92), Warm, 15f, 0.58f);
                GlowRectangle(warmPixels, size, xOffset, yOffset, source.height,
                    new RectInt(821, 514, 84, 108), Warm, 13f, 0.48f);
                RectInt[] serviceLights =
                {
                    new RectInt(237, 568, 37, 15), new RectInt(382, 608, 18, 61),
                    new RectInt(526, 686, 57, 15), new RectInt(610, 696, 58, 15),
                    new RectInt(763, 702, 18, 62), new RectInt(1004, 747, 18, 40),
                    new RectInt(1050, 742, 83, 13), new RectInt(1261, 732, 61, 14)
                };
                foreach (RectInt light in serviceLights)
                    GlowRectangle(warmPixels, size, xOffset, yOffset, source.height,
                        light, Warm, 8f, 0.58f);
                Vector2[] beacons =
                {
                    new Vector2(718, 44), new Vector2(767, 220),
                    new Vector2(502, 263), new Vector2(414, 390),
                    new Vector2(1020, 388), new Vector2(1307, 604)
                };
                foreach (Vector2 beacon in beacons)
                    GlowDisc(warmPixels, size, xOffset + Mathf.RoundToInt(beacon.x),
                        yOffset + source.height - 1 - Mathf.RoundToInt(beacon.y),
                        7f, 16f, Warm, 0.68f);

                // Sparse communication/network activation on authored tower and conduits.
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(718, 92), new Vector2(718, 198),
                        new Vector2(716, 310), new Vector2(715, 405) }, Energy, 2f, 7f, 0.65f);
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(551, 647), new Vector2(690, 624),
                        new Vector2(823, 648), new Vector2(940, 684) }, Energy, 2.3f, 8f, 0.62f);
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(907, 626), new Vector2(981, 665),
                        new Vector2(1082, 690), new Vector2(1188, 706) }, Energy, 2f, 7f, 0.54f);
                GlowRectangle(energyPixels, size, xOffset, yOffset, source.height,
                    new RectInt(592, 527, 58, 34), Energy, 7f, 0.55f);
                GlowRectangle(energyPixels, size, xOffset, yOffset, source.height,
                    new RectInt(703, 539, 58, 34), Energy, 7f, 0.55f);

                // Compact restored command-core and tower status highlights.
                GlowRectangle(corePixels, size, xOffset, yOffset, source.height,
                    new RectInt(641, 522, 34, 18), Core, 7f, 0.68f);
                GlowRectangle(corePixels, size, xOffset, yOffset, source.height,
                    new RectInt(765, 548, 28, 17), Core, 7f, 0.64f);
                GlowDisc(corePixels, size, xOffset + 718,
                    yOffset + source.height - 1 - 44, 4f, 11f, Core, 0.72f);

                WritePng(LayerPaths[0], size, size, basePixels);
                WritePng(LayerPaths[1], size, size, warmPixels);
                WritePng(LayerPaths[2], size, size, energyPixels);
                WritePng(LayerPaths[3], size, size, corePixels);
                WriteStatePreview(size, basePixels, warmPixels, energyPixels, corePixels);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        private static bool IsEmissiveRegion(int x, int y)
        {
            return InRect(x, y, 492, 914, 470, 634) ||
                   InRect(x, y, 220, 286, 550, 603) ||
                   InRect(x, y, 367, 416, 590, 686) ||
                   InRect(x, y, 510, 683, 672, 726) ||
                   InRect(x, y, 748, 795, 682, 780) ||
                   InRect(x, y, 980, 1160, 716, 810) ||
                   InRect(x, y, 1242, 1336, 705, 790) ||
                   InRect(x, y, 695, 737, 15, 75) ||
                   InRect(x, y, 742, 790, 190, 247) ||
                   InRect(x, y, 480, 524, 235, 291) ||
                   InRect(x, y, 392, 437, 360, 418) ||
                   InRect(x, y, 998, 1043, 360, 420) ||
                   InRect(x, y, 1285, 1332, 575, 632);
        }

        private static bool InRect(int x, int y, int xMin, int xMax, int yMin, int yMax) =>
            x >= xMin && x <= xMax && y >= yMin && y <= yMax;

        private static bool IsEmission(Color32 pixel)
        {
            if (pixel.a == 0) return false;
            bool warm = pixel.r > 125 && pixel.r > pixel.b * 1.28f &&
                        pixel.g > pixel.b * 1.08f;
            bool monitor = pixel.b > 85 && pixel.g > 75 &&
                           pixel.b > pixel.r * 1.08f;
            bool beacon = pixel.r > 125 && pixel.r > pixel.g * 1.25f;
            return warm || monitor || beacon;
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

        private static void BindIsolatedPrototype(CityBuildingArtDefinition controlCenter)
        {
            Scene scene = EditorSceneManager.OpenScene(M15CityBuildingArtPrototypeBuilder.ScenePath,
                OpenSceneMode.Single);
            CityBuildingArtPrototypeController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<
                    CityBuildingArtPrototypeController>(true)).Single();
            controller.SetFinalDefinitions(controller.FinalPowerStation,
                controller.FinalCentralGrid, controller.FinalSubstation, controlCenter);
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

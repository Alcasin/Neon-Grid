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
    /// <summary>
    /// Deterministically derives the isolated Substation final-art layers from the
    /// authoritative supplied source. It never changes the source or production campaign data.
    /// </summary>
    public static class M15SubstationFinalArtPreparation
    {
        public const string Directory = M15CityBuildingFinalArtPreparation.Root + "/Substation";
        public const string SourcePath = Directory + "/Substation_Source.png";
        public const string DefinitionPath = Directory + "/Substation_Final.asset";
        public const string PreviewPath =
            "Assets/NeonGrid/Documentation/Previews/Substation_StatePreview.png";
        public static readonly string[] LayerPaths =
        {
            Directory + "/Substation_Base.png",
            Directory + "/Substation_WarmLights.png",
            Directory + "/Substation_Energy.png",
            Directory + "/Substation_Core.png"
        };

        private static readonly Color32 Warm = new Color32(255, 174, 48, 255);
        private static readonly Color32 Energy = new Color32(55, 222, 242, 255);
        private static readonly Color32 Core = new Color32(190, 255, 250, 255);
        private static readonly Vector4[] Profile =
        {
            new Vector4(1f, 0f, 0f, 0f),
            new Vector4(1f, 0.40f, 0f, 0f),
            new Vector4(1f, 0.72f, 0.22f, 0f),
            new Vector4(1f, 0.90f, 0.68f, 0.18f),
            new Vector4(1f, 1f, 1f, 0.72f)
        };

        [MenuItem("Neon Grid/Prepare M15 Substation Final Art")]
        public static void Prepare()
        {
            if (!File.Exists(SourcePath))
                throw new FileNotFoundException("Authoritative Substation source is missing.",
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
            definition.SetData("substation", sprites[0], sprites[1], sprites[2], sprites[3],
                Color.white, Color.white, Color.white, Color.white,
                Profile,
                new Vector2(0.92f, 0.72f), Vector2.zero, 1f);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            IReadOnlyList<string> issues = CityBuildingArtAssetValidator.Validate(definition,
                "substation");
            if (issues.Count > 0)
                throw new InvalidOperationException("Substation final art validation failed:\n" +
                                                    string.Join("\n", issues));

            BindIsolatedPrototype(definition);
            Debug.Log("Prepared valid isolated Substation final art. Production bindings unchanged.");
        }

        private static void GenerateLayers()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(SourcePath), false))
                throw new InvalidOperationException("Substation source PNG could not be decoded.");
            try
            {
                int size = Mathf.Max(source.width, source.height);
                int xOffset = (size - source.width) / 2;
                int yOffset = (size - source.height) / 2;
                Color32[] basePixels = new Color32[size * size];
                Color32[] warmPixels = new Color32[size * size];
                Color32[] energyPixels = new Color32[size * size];
                Color32[] corePixels = new Color32[size * size];
                Color32[] sourcePixels = source.GetPixels32();

                for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                {
                    Color32 pixel = sourcePixels[y * source.width + x];
                    int topY = source.height - 1 - y;
                    if (IsAuthoredEmissiveRegion(x, topY) && IsBrightEmission(pixel))
                        pixel = NeutralizeEmission(pixel);
                    basePixels[(y + yOffset) * size + x + xOffset] = pixel;
                }

                // Warm facility activation: the three control-building windows and warning beacon.
                GlowRectangle(warmPixels, size, xOffset, yOffset, source.height,
                    new RectInt(576, 563, 57, 25), Warm, 13f, 0.62f);
                GlowRectangle(warmPixels, size, xOffset, yOffset, source.height,
                    new RectInt(648, 559, 62, 28), Warm, 13f, 0.72f);
                GlowRectangle(warmPixels, size, xOffset, yOffset, source.height,
                    new RectInt(739, 603, 57, 25), Warm, 13f, 0.58f);
                GlowDisc(warmPixels, size, xOffset + 767,
                    yOffset + source.height - 1 - 318, 9f, 18f, Warm, 0.72f);

                // Restrained cyan distribution routes following existing buswork.
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(211, 181), new Vector2(333, 208),
                        new Vector2(526, 250), new Vector2(650, 354) }, Energy, 2.2f, 7f, 0.62f);
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(180, 279), new Vector2(365, 319),
                        new Vector2(535, 362), new Vector2(650, 390) }, Energy, 2.0f, 6f, 0.52f);
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(943, 423), new Vector2(1054, 382),
                        new Vector2(1234, 421), new Vector2(1395, 452) }, Energy, 2.2f, 7f, 0.62f);
                Polyline(energyPixels, size, xOffset, yOffset, source.height,
                    new[] { new Vector2(468, 620), new Vector2(555, 606),
                        new Vector2(613, 641), new Vector2(690, 646),
                        new Vector2(765, 626) }, Energy, 2.4f, 8f, 0.68f);

                // Compact restored-state instrumentation, not a separate fantasy object.
                GlowRectangle(corePixels, size, xOffset, yOffset, source.height,
                    new RectInt(690, 594, 31, 12), Core, 8f, 0.75f);
                GlowRectangle(corePixels, size, xOffset, yOffset, source.height,
                    new RectInt(758, 479, 20, 10), Core, 7f, 0.62f);
                GlowDisc(corePixels, size, xOffset + 767,
                    yOffset + source.height - 1 - 318, 4f, 11f, Core, 0.70f);

                WritePng(LayerPaths[0], size, basePixels);
                WritePng(LayerPaths[1], size, warmPixels);
                WritePng(LayerPaths[2], size, energyPixels);
                WritePng(LayerPaths[3], size, corePixels);
                WriteStatePreview(size, basePixels, warmPixels, energyPixels, corePixels);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static bool IsAuthoredEmissiveRegion(int x, int topY)
        {
            return InRect(x, topY, 568, 637, 552, 603) ||
                   InRect(x, topY, 638, 718, 548, 604) ||
                   InRect(x, topY, 730, 808, 590, 645) ||
                   InRect(x, topY, 749, 785, 285, 341);
        }

        private static bool InRect(int x, int y, int xMin, int xMax, int yMin, int yMax) =>
            x >= xMin && x <= xMax && y >= yMin && y <= yMax;

        private static bool IsBrightEmission(Color32 pixel)
        {
            if (pixel.a == 0) return false;
            float max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            float min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
            bool nearWhite = min > 130f;
            bool amberOrRed = max > 135f && pixel.r > pixel.b * 1.35f &&
                              pixel.g > pixel.b * 1.12f;
            return nearWhite || amberOrRed;
        }

        private static Color32 NeutralizeEmission(Color32 pixel)
        {
            byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(
                (pixel.r * 0.20f + pixel.g * 0.42f + pixel.b * 0.08f) * 0.62f), 28, 112);
            return new Color32((byte)(value * 0.88f), (byte)(value * 0.94f), value, pixel.a);
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

        private static void BindIsolatedPrototype(CityBuildingArtDefinition substation)
        {
            Scene scene = EditorSceneManager.OpenScene(M15CityBuildingArtPrototypeBuilder.ScenePath,
                OpenSceneMode.Single);
            CityBuildingArtPrototypeController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<
                    CityBuildingArtPrototypeController>(true)).Single();
            controller.SetFinalDefinitions(controller.FinalPowerStation,
                controller.FinalCentralGrid, substation);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WritePng(string path, int size, Color32[] pixels)
        {
            WritePng(path, size, size, pixels);
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
                    int sourceX = x * sourceSize / panelSize;
                    int sourceY = y * sourceSize / panelSize;
                    int sourceIndex = sourceY * sourceSize + sourceX;
                    Color32 result = preview[y * width + xOffset + x];
                    for (int layer = 0; layer < layers.Length; layer++)
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
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(under.r, over.r, alpha)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(under.g, over.g, alpha)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(under.b, over.b, alpha)), 255);
        }

        private static void GlowRectangle(Color32[] pixels, int size, int xOffset, int yOffset,
            int sourceHeight, RectInt topRect, Color32 color, float glow, float strength)
        {
            int xMin = Mathf.FloorToInt(xOffset + topRect.xMin - glow);
            int xMax = Mathf.CeilToInt(xOffset + topRect.xMax + glow);
            int bottom = yOffset + sourceHeight - topRect.yMax;
            int top = yOffset + sourceHeight - topRect.yMin;
            int yMin = Mathf.FloorToInt(bottom - glow);
            int yMax = Mathf.CeilToInt(top + glow);
            for (int y = yMin; y <= yMax; y++)
            for (int x = xMin; x <= xMax; x++)
            {
                float dx = Mathf.Max(xOffset + topRect.xMin - x, x - (xOffset + topRect.xMax));
                float dy = Mathf.Max(bottom - y, y - top);
                float distance = Mathf.Sqrt(Mathf.Max(0f, dx) * Mathf.Max(0f, dx) +
                                            Mathf.Max(0f, dy) * Mathf.Max(0f, dy));
                float alpha = distance <= 0f ? strength : strength * Mathf.Clamp01(1f - distance / glow) * 0.55f;
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
                float distance = Vector2.Distance(new Vector2(x, y),
                    new Vector2(centerX, centerY));
                if (distance > glowRadius) continue;
                float alpha = distance <= coreRadius ? strength :
                    strength * (1f - (distance - coreRadius) /
                                 (glowRadius - coreRadius)) * 0.55f;
                Blend(pixels, size, x, y, color, alpha);
            }
        }

        private static void Polyline(Color32[] pixels, int size, int xOffset, int yOffset,
            int sourceHeight, IReadOnlyList<Vector2> topPoints, Color32 color,
            float coreWidth, float glowWidth, float strength)
        {
            for (int index = 0; index + 1 < topPoints.Count; index++)
            {
                Vector2 a = ToCanvas(topPoints[index], xOffset, yOffset, sourceHeight);
                Vector2 b = ToCanvas(topPoints[index + 1], xOffset, yOffset, sourceHeight);
                int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - glowWidth);
                int maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + glowWidth);
                int minY = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - glowWidth);
                int maxY = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + glowWidth);
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
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

        private static Vector2 ToCanvas(Vector2 point, int xOffset, int yOffset,
            int sourceHeight) => new Vector2(xOffset + point.x,
            yOffset + sourceHeight - 1 - point.y);

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
            float combined = Mathf.Clamp01(existing + alpha * (1f - existing));
            pixels[index] = new Color32(color.r, color.g, color.b,
                (byte)Mathf.RoundToInt(combined * 255f));
        }
    }
}

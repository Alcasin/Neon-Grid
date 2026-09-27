using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class M15CityEnvironmentFinalArtPreparation
    {
        public const string DirectoryPath = "Assets/NeonGrid/Art/CityEnvironment";
        public const string SourcePath = DirectoryPath + "/CityEnvironment_Source.png";
        public const string BasePath = DirectoryPath + "/CityEnvironment_Base.png";
        public const string PowerStationPath = DirectoryPath + "/PowerStation_District.png";
        public const string SubstationPath = DirectoryPath + "/Substation_District.png";
        public const string ControlCenterPath = DirectoryPath + "/ControlCenter_District.png";
        public const string AutomationPlantPath = DirectoryPath + "/AutomationPlant_District.png";
        public const string CentralGridPath = DirectoryPath + "/CentralGrid_District.png";
        public const string FinalAccentPath = DirectoryPath + "/FinalAccent.png";
        public const string DefinitionPath = DirectoryPath + "/CityEnvironment_Final.asset";

        private static readonly DistrictSpec[] Districts =
        {
            new DistrictSpec(CityEnvironmentArtDefinition.PowerStationId, PowerStationPath,
                new Vector2(-310f, -510f), new Vector2(185f, 175f)),
            new DistrictSpec(CityEnvironmentArtDefinition.SubstationId, SubstationPath,
                new Vector2(-340f, 0f), new Vector2(180f, 185f)),
            // Shift the illumination field down and keep it compact to preserve header calm.
            new DistrictSpec(CityEnvironmentArtDefinition.ControlCenterId, ControlCenterPath,
                new Vector2(60f, 380f), new Vector2(185f, 145f)),
            new DistrictSpec(CityEnvironmentArtDefinition.AutomationPlantId, AutomationPlantPath,
                new Vector2(370f, 0f), new Vector2(180f, 185f)),
            // Shift upward so the Central Grid lower label-safe region remains quiet.
            new DistrictSpec(CityEnvironmentArtDefinition.CentralGridId, CentralGridPath,
                new Vector2(40f, -250f), new Vector2(205f, 155f))
        };

        [MenuItem("Neon Grid/M15/Prepare Final City Environment Art")]
        public static void Prepare()
        {
            if (!File.Exists(SourcePath))
                throw new FileNotFoundException("Approved city environment source is missing.",
                    SourcePath);

            string sourceHashBefore = Hash(SourcePath);
            byte[] sourceBytes = File.ReadAllBytes(SourcePath);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!source.LoadImage(sourceBytes, false))
                throw new InvalidOperationException("Approved city environment source is not a valid PNG.");

            int width = source.width;
            int height = source.height;
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] basePixels = new Color32[sourcePixels.Length];
            var overlayPixels = new Color32[Districts.Length][];
            for (int index = 0; index < overlayPixels.Length; index++)
                overlayPixels[index] = new Color32[sourcePixels.Length];
            Color32[] finalPixels = new Color32[sourcePixels.Length];

            SeparateLayers(sourcePixels, basePixels, overlayPixels, finalPixels, width, height);
            WritePng(BasePath, basePixels, width, height);
            for (int index = 0; index < Districts.Length; index++)
                WritePng(Districts[index].Path, overlayPixels[index], width, height);
            WritePng(FinalAccentPath, finalPixels, width, height);
            UnityEngine.Object.DestroyImmediate(source);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureRuntimeLayer(BasePath);
            foreach (DistrictSpec district in Districts) ConfigureRuntimeLayer(district.Path);
            ConfigureRuntimeLayer(FinalAccentPath);

            CityEnvironmentArtDefinition definition = BuildDefinition();
            M15CityEnvironmentArtPrototypeBuilder.BuildSceneForDefinition(definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string sourceHashAfter = Hash(SourcePath);
            if (!string.Equals(sourceHashBefore, sourceHashAfter, StringComparison.Ordinal))
                throw new InvalidOperationException("Approved source changed during preparation.");
            Debug.Log($"M15-E3B final city environment prepared from byte-preserved source " +
                      $"{sourceHashAfter}; production remains unbound.");
        }

        private static void SeparateLayers(Color32[] source, Color32[] cityBase,
            Color32[][] overlays, Color32[] finalAccent, int width, int height)
        {
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = y * width + x;
                Color32 pixel = source[pixelIndex];
                float warmth = Warmth(pixel);
                float activation = ActivationStrength(warmth);
                cityBase[pixelIndex] = InactivePixel(pixel, activation);
                if (activation <= 0f || pixel.a == 0) continue;

                Vector2 mapPosition = new Vector2(
                    x / (float)(width - 1) * 1020f - 510f,
                    y / (float)(height - 1) * 1500f - 750f);
                for (int districtIndex = 0; districtIndex < Districts.Length; districtIndex++)
                {
                    DistrictSpec district = Districts[districtIndex];
                    Vector2 normalized = new Vector2(
                        (mapPosition.x - district.Center.x) / district.Radius.x,
                        (mapPosition.y - district.Center.y) / district.Radius.y);
                    float distance = normalized.magnitude;
                    if (distance >= 1f) continue;
                    float districtMask = 1f - Mathf.SmoothStep(0.38f, 1f, distance);
                    float overlayAlpha = pixel.a / 255f * activation * districtMask * 1.15f;
                    overlays[districtIndex][pixelIndex] = WithAlpha(pixel, overlayAlpha);
                }

                float sourceAlpha = pixel.a / 255f;
                // The all-restored accent is a low, city-wide lift. District overlays remain
                // the readable restoration signal rather than a global emissive wash.
                finalAccent[pixelIndex] = WithAlpha(pixel,
                    sourceAlpha * activation * 0.11f);
            }
        }

        private static float Warmth(Color32 pixel)
        {
            float r = pixel.r / 255f;
            float g = pixel.g / 255f;
            float b = pixel.b / 255f;
            float warmSeparation = Mathf.Clamp01((r - b - 0.035f) / 0.38f);
            float redLead = Mathf.Clamp01((r - g + 0.02f) / 0.22f);
            float visibility = Mathf.Clamp01((Mathf.Max(r, Mathf.Max(g, b)) - 0.12f) / 0.55f);
            return warmSeparation * redLead * visibility;
        }

        private static float ActivationStrength(float warmth)
        {
            // Baked light halos often have low raw chroma. Expanding that low range lets the
            // inactive base suppress the full source-derived halo while preserving geometry.
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(warmth), 6f);
        }

        private static Color32 InactivePixel(Color32 source, float activation)
        {
            if (activation <= 0f) return source;
            float luminance = (0.2126f * source.r + 0.7152f * source.g + 0.0722f * source.b) /
                              255f;
            var inactive = new Color(
                luminance * 0.16f,
                luminance * 0.24f,
                luminance * 0.36f,
                source.a / 255f);
            Color original = source;
            Color result = Color.Lerp(original, inactive, activation);
            return result;
        }

        private static Color32 WithAlpha(Color32 source, float normalizedAlpha) =>
            new Color32(source.r, source.g, source.b,
                (byte)Mathf.Clamp(Mathf.RoundToInt(normalizedAlpha * 255f), 0, 255));

        private static void WritePng(string path, Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void ConfigureRuntimeLayer(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing importer: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.spritePixelsPerUnit = 100f;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
            platform.overridden = true;
            platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static CityEnvironmentArtDefinition BuildDefinition()
        {
            CityEnvironmentArtDefinition definition =
                AssetDatabase.LoadAssetAtPath<CityEnvironmentArtDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CityEnvironmentArtDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var layers = new List<CityEnvironmentDistrictLayer>();
            foreach (DistrictSpec district in Districts)
                layers.Add(new CityEnvironmentDistrictLayer(district.ChapterId,
                    LoadSprite(district.Path)));
            definition.SetData(LoadSprite(BasePath), layers, LoadSprite(FinalAccentPath),
                Vector2.one, Vector2.zero);
            EditorUtility.SetDirty(definition);
            if (!definition.IsConfigured)
                throw new InvalidOperationException("Prepared final environment definition is invalid.");
            return definition;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Missing prepared sprite: " + path);
            return sprite;
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty);
        }

        private readonly struct DistrictSpec
        {
            public DistrictSpec(string chapterId, string path, Vector2 center, Vector2 radius)
            {
                ChapterId = chapterId;
                Path = path;
                Center = center;
                Radius = radius;
            }

            public string ChapterId { get; }
            public string Path { get; }
            public Vector2 Center { get; }
            public Vector2 Radius { get; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonGrid.Editor
{
    public static class M15CityEnvironmentArtPrototypeBuilder
    {
        public const string DirectoryPath = "Assets/NeonGrid/Art/CityEnvironmentPrototype";
        public const string DefinitionPath = DirectoryPath + "/CityEnvironmentPrototype.asset";
        public const string ScenePath = "Assets/NeonGrid/Scenes/M15_CityEnvironmentArtPrototype.unity";
        public const string CampaignResourcePath = "Campaigns/NeonGrid_Main";

        private const int TextureWidth = 512;
        private const int TextureHeight = 768;

        private static readonly LayerSpec[] DistrictLayers =
        {
            new LayerSpec(CityEnvironmentArtDefinition.PowerStationId, "District_PowerStation.png", new Vector2(-310f, -510f)),
            new LayerSpec(CityEnvironmentArtDefinition.SubstationId, "District_Substation.png", new Vector2(-340f, 0f)),
            new LayerSpec(CityEnvironmentArtDefinition.ControlCenterId, "District_ControlCenter.png", new Vector2(60f, 420f)),
            new LayerSpec(CityEnvironmentArtDefinition.AutomationPlantId, "District_AutomationPlant.png", new Vector2(370f, 0f)),
            new LayerSpec(CityEnvironmentArtDefinition.CentralGridId, "District_CentralGrid.png", new Vector2(40f, -300f))
        };

        [MenuItem("Neon Grid/M15/Build City Environment Art Prototype")]
        public static void Build()
        {
            EnsureFolder(DirectoryPath);
            GenerateArt();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImports();

            CityEnvironmentArtDefinition definition = BuildDefinition();
            BuildSceneForDefinition(definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"M15-E3A city environment prototype generated at {ScenePath}.");
        }

        private static void GenerateArt()
        {
            WriteLayer("BaseCity.png", DrawBaseCity);
            foreach (LayerSpec layer in DistrictLayers)
            {
                WriteLayer(layer.FileName,
                    canvas => DrawDistrict(canvas, layer.MapPosition, layer.ChapterId));
            }

            WriteLayer("FinalAccent.png", DrawFinalAccent);
        }

        private static void WriteLayer(string fileName, Action<PrototypeCanvas> draw)
        {
            PrototypeCanvas canvas = new PrototypeCanvas(TextureWidth, TextureHeight);
            draw(canvas);
            File.WriteAllBytes(Path.GetFullPath($"{DirectoryPath}/{fileName}"), canvas.Encode());
        }

        private static void DrawBaseCity(PrototypeCanvas canvas)
        {
            Color32 road = new Color32(12, 25, 34, 78);
            canvas.LineMap(new Vector2(-480f, -105f), new Vector2(480f, -105f), 12f, road);
            canvas.LineMap(new Vector2(80f, -680f), new Vector2(80f, 660f), 10f, road);
            canvas.LineMap(new Vector2(-480f, -405f), new Vector2(480f, 255f), 6f, road);

            Vector2[] blocks =
            {
                new Vector2(-430f, 540f), new Vector2(-230f, 560f), new Vector2(300f, 560f), new Vector2(450f, 470f),
                new Vector2(-440f, 280f), new Vector2(-190f, 260f), new Vector2(300f, 260f), new Vector2(470f, 170f),
                new Vector2(-440f, -240f), new Vector2(-190f, -300f), new Vector2(290f, -300f), new Vector2(450f, -310f),
                new Vector2(-430f, -650f), new Vector2(-150f, -610f), new Vector2(300f, -630f), new Vector2(470f, -590f)
            };

            for (int i = 0; i < blocks.Length; i++)
            {
                Vector2 size = i % 3 == 0
                    ? new Vector2(78f, 54f)
                    : i % 3 == 1 ? new Vector2(64f, 44f) : new Vector2(88f, 50f);
                canvas.RectMap(blocks[i], size, new Color32(13, 27, 40, 125));
                canvas.RectMap(blocks[i] + new Vector2(0f, 3f),
                    size - new Vector2(10f, 10f), new Color32(24, 44, 56, 68));
            }
        }

        private static void DrawDistrict(PrototypeCanvas canvas, Vector2 center,
            string chapterId)
        {
            float verticalOffset = chapterId == CityEnvironmentArtDefinition.ControlCenterId
                ? 12f
                : 58f;
            Color32 cyan = new Color32(53, 163, 174, 68);
            Color32 warm = new Color32(246, 166, 75, 138);
            Color32 warmDim = new Color32(210, 132, 58, 88);

            // Two quiet rooftop/service-light clusters preserve a dark center and label zones.
            DrawServiceCluster(canvas, center + new Vector2(-108f, verticalOffset),
                warm, warmDim, cyan);
            DrawServiceCluster(canvas, center + new Vector2(70f, verticalOffset + 8f),
                warm, warmDim, cyan);
        }

        private static void DrawFinalAccent(PrototypeCanvas canvas)
        {
            Color32 cyan = new Color32(57, 172, 183, 50);
            Color32 warm = new Color32(238, 160, 72, 100);
            Color32 warmDim = new Color32(200, 125, 54, 65);
            DrawServiceCluster(canvas, new Vector2(-455f, 610f), warm, warmDim, cyan);
            DrawServiceCluster(canvas, new Vector2(250f, 620f), warm, warmDim, cyan);
            DrawServiceCluster(canvas, new Vector2(-455f, -620f), warm, warmDim, cyan);
            DrawServiceCluster(canvas, new Vector2(275f, -610f), warm, warmDim, cyan);
        }

        private static void DrawServiceCluster(PrototypeCanvas canvas, Vector2 origin,
            Color32 warm, Color32 warmDim, Color32 cyan)
        {
            for (int column = 0; column < 3; column++)
            {
                canvas.RectMap(origin + new Vector2(column * 14f, 8f),
                    new Vector2(7f, 4f), column == 1 ? warmDim : warm);
                canvas.RectMap(origin + new Vector2(column * 14f, -2f),
                    new Vector2(7f, 4f), column == 1 ? warm : warmDim);
            }

            canvas.RectMap(origin + new Vector2(14f, -12f), new Vector2(26f, 3f), cyan);
        }

        private static void ConfigureTextureImports()
        {
            ConfigureTexture("BaseCity.png");
            foreach (LayerSpec layer in DistrictLayers)
            {
                ConfigureTexture(layer.FileName);
            }

            ConfigureTexture("FinalAccent.png");
        }

        private static void ConfigureTexture(string fileName)
        {
            string path = $"{DirectoryPath}/{fileName}";
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
            settings.overridden = true;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
            textureSettings.spritePivot = new Vector2(0.5f, 0.5f);
            textureSettings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
        }

        private static CityEnvironmentArtDefinition BuildDefinition()
        {
            CityEnvironmentArtDefinition definition = AssetDatabase.LoadAssetAtPath<CityEnvironmentArtDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CityEnvironmentArtDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            List<CityEnvironmentDistrictLayer> districts = new List<CityEnvironmentDistrictLayer>();
            foreach (LayerSpec layer in DistrictLayers)
            {
                districts.Add(new CityEnvironmentDistrictLayer(layer.ChapterId, LoadSprite(layer.FileName)));
            }

            definition.SetData(LoadSprite("BaseCity.png"), districts, LoadSprite("FinalAccent.png"), Vector2.one, Vector2.zero);
            EditorUtility.SetDirty(definition);
            if (!definition.IsConfigured)
            {
                throw new InvalidOperationException("Generated city environment definition is invalid.");
            }

            return definition;
        }

        public static void BuildSceneForDefinition(CityEnvironmentArtDefinition definition)
        {
            if (definition == null || !definition.IsConfigured)
            {
                throw new ArgumentException("A configured city environment definition is required.",
                    nameof(definition));
            }

            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(CampaignResourcePath);
            if (campaign == null)
            {
                throw new InvalidOperationException($"Missing campaign Resources/{CampaignResourcePath}.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("Prototype Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(5, 13, 22, 255);
            camera.cullingMask = 0;

            GameObject root = new GameObject("M15 City Environment Prototype", typeof(CityEnvironmentArtPrototypeController));
            root.GetComponent<CityEnvironmentArtPrototypeController>().SetData(campaign, definition);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Sprite LoadSprite(string fileName)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{DirectoryPath}/{fileName}");
            if (sprite == null)
            {
                throw new InvalidOperationException($"Missing generated sprite {fileName}.");
            }

            return sprite;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private readonly struct LayerSpec
        {
            public LayerSpec(string chapterId, string fileName, Vector2 mapPosition)
            {
                ChapterId = chapterId;
                FileName = fileName;
                MapPosition = mapPosition;
            }

            public string ChapterId { get; }
            public string FileName { get; }
            public Vector2 MapPosition { get; }
        }

        private sealed class PrototypeCanvas
        {
            private readonly int width;
            private readonly int height;
            private readonly Texture2D texture;
            private readonly Color32[] pixels;

            public PrototypeCanvas(int width, int height)
            {
                this.width = width;
                this.height = height;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                pixels = new Color32[width * height];
            }

            public void RectMap(Vector2 center, Vector2 size, Color32 color)
            {
                Vector2 min = ToPixel(center - size * 0.5f);
                Vector2 max = ToPixel(center + size * 0.5f);
                int xMin = Mathf.Clamp(Mathf.FloorToInt(min.x), 0, width - 1);
                int xMax = Mathf.Clamp(Mathf.CeilToInt(max.x), 0, width - 1);
                int yMin = Mathf.Clamp(Mathf.FloorToInt(min.y), 0, height - 1);
                int yMax = Mathf.Clamp(Mathf.CeilToInt(max.y), 0, height - 1);
                for (int y = yMin; y <= yMax; y++)
                {
                    for (int x = xMin; x <= xMax; x++)
                    {
                        Blend(x, y, color);
                    }
                }
            }

            public void DiscMap(Vector2 center, float radius, Color32 color)
            {
                Vector2 pixelCenter = ToPixel(center);
                float radiusPixels = radius * width / 1020f;
                int extent = Mathf.CeilToInt(radiusPixels);
                for (int y = -extent; y <= extent; y++)
                {
                    for (int x = -extent; x <= extent; x++)
                    {
                        if (x * x + y * y <= radiusPixels * radiusPixels)
                        {
                            Blend(Mathf.RoundToInt(pixelCenter.x) + x, Mathf.RoundToInt(pixelCenter.y) + y, color);
                        }
                    }
                }
            }

            public void LineMap(Vector2 from, Vector2 to, float thickness, Color32 color)
            {
                Vector2 a = ToPixel(from);
                Vector2 b = ToPixel(to);
                float radius = Mathf.Max(1f, thickness * width / 2040f);
                int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius), 0, width - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius), 0, width - 1);
                int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius), 0, height - 1);
                int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius), 0, height - 1);
                Vector2 segment = b - a;
                float lengthSquared = segment.sqrMagnitude;
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 point = new Vector2(x, y);
                        float t = lengthSquared <= 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
                        if ((point - (a + segment * t)).sqrMagnitude <= radius * radius)
                        {
                            Blend(x, y, color);
                        }
                    }
                }
            }

            public byte[] Encode()
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                byte[] bytes = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                return bytes;
            }

            private Vector2 ToPixel(Vector2 map)
            {
                return new Vector2((map.x + 510f) / 1020f * (width - 1), (map.y + 750f) / 1500f * (height - 1));
            }

            private void Blend(int x, int y, Color32 source)
            {
                if (x < 0 || x >= width || y < 0 || y >= height || source.a == 0)
                {
                    return;
                }

                int index = y * width + x;
                Color32 destination = pixels[index];
                float sourceAlpha = source.a / 255f;
                float destinationAlpha = destination.a / 255f;
                float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
                if (outputAlpha <= 0f)
                {
                    return;
                }

                pixels[index] = new Color32(
                    (byte)Mathf.RoundToInt((source.r * sourceAlpha + destination.r * destinationAlpha * (1f - sourceAlpha)) / outputAlpha),
                    (byte)Mathf.RoundToInt((source.g * sourceAlpha + destination.g * destinationAlpha * (1f - sourceAlpha)) / outputAlpha),
                    (byte)Mathf.RoundToInt((source.b * sourceAlpha + destination.b * destinationAlpha * (1f - sourceAlpha)) / outputAlpha),
                    (byte)Mathf.RoundToInt(outputAlpha * 255f));
            }
        }
    }
}

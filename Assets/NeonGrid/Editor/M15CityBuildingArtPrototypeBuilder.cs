using System.IO;
using NeonGrid.Data;
using NeonGrid.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeonGrid.Editor
{
    // Editor-time geometry swatches only, not final illustration or a runtime art generator.
    public static class M15CityBuildingArtPrototypeBuilder
    {
        public const string DirectoryPath = "Assets/NeonGrid/Art/CityBuildingPrototype";
        public const string ScenePath = "Assets/NeonGrid/Scenes/M15_CityBuildingArtPrototype.unity";

        [MenuItem("Neon Grid/Build M15 City Building Art Prototype")]
        public static void Build()
        {
            Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh();
            CityBuildingArtDefinition power = CreateDefinition("PowerStation", "power_station", false);
            CityBuildingArtDefinition central = CreateDefinition("CentralGrid", "central_grid", true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Prototype Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            camera.targetDisplay = 0;
            var root = new GameObject("M15 E1A City Building Art Prototype");
            root.AddComponent<CityBuildingArtPrototypeController>().SetData(
                Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main"), power, central);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created E1A prototype only; production assignments and build settings untouched.");
        }

        private static CityBuildingArtDefinition CreateDefinition(string name, string chapter, bool central)
        {
            string path = DirectoryPath + "/" + name + ".asset";
            // Guard authored replacements against accidental regeneration from this QA builder.
            if (File.Exists(path)) throw new IOException("Prototype already exists; do not overwrite authored art: " + path);
            var sprites = new Sprite[4];
            string[] names = { "Base", "Warm", "Energy", "Core" };
            for (int layer = 0; layer < sprites.Length; layer++)
            {
                string spritePath = DirectoryPath + "/" + name + "_" + names[layer] + ".png";
                var drawing = new PrototypeCanvas(central ? 1232 : 1008, central ? 770 : 560);
                if (central) DrawCentral(drawing, layer);
                else DrawPower(drawing, layer);
                drawing.Save(spritePath);
                AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                sprites[layer] = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }
            var definition = ScriptableObject.CreateInstance<CityBuildingArtDefinition>();
            definition.SetData(chapter, sprites[0], sprites[1], sprites[2], sprites[3],
                new Vector2(central ? 0.88f : 0.9f, 0.7f));
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static void DrawPower(PrototypeCanvas d, int layer)
        {
            if (layer == 0)
            {
                d.Box(50, 13, 43, 10, 6);
                d.Box(50, 28, 37, 16, 22);
                d.Box(32, 50, 8, 5, 34);
                d.Box(65, 51, 7, 5, 28);
            }
            else if (layer == 1)
            {
                d.Line(21, 30, 43, 22, 2, Color.white);
                d.Line(53, 22, 78, 32, 2, Color.white);
            }
            else if (layer == 2)
            {
                d.Line(15, 19, 50, 8, 2, Color.white);
                d.Line(50, 8, 86, 20, 2, Color.white);
                d.Line(32, 58, 32, 77, 2, Color.white);
                d.Line(65, 58, 65, 72, 2, Color.white);
            }
            else
            {
                d.Polygon(Color.white, 24,84, 32,89, 40,84, 32,79);
                d.Polygon(Color.white, 58,79, 65,83, 72,79, 65,75);
            }
        }

        private static void DrawCentral(PrototypeCanvas d, int layer)
        {
            if (layer == 0)
            {
                d.Box(50, 34, 16, 9, 20);
                d.Box(25, 27, 17, 7, 10);
                d.Box(75, 27, 17, 7, 10);
                d.Box(50, 12, 15, 7, 12);
                d.Box(50, 32, 12, 9, 49);
            }
            else if (layer == 1)
            {
                d.Line(13, 28, 25, 23, 2, Color.white);
                d.Line(75, 23, 87, 28, 2, Color.white);
                d.Line(41, 14, 50, 10, 2, Color.white);
                d.Line(50, 10, 59, 14, 2, Color.white);
            }
            else if (layer == 2)
            {
                d.Line(22, 35, 40, 41, 2, Color.white);
                d.Line(60, 41, 78, 35, 2, Color.white);
                d.Line(50, 19, 50, 40, 2, Color.white);
                d.Line(43, 47, 43, 74, 2, Color.white);
                d.Line(57, 47, 57, 74, 2, Color.white);
            }
            else
            {
                d.Polygon(Color.white, 40,81, 50,88, 60,81, 50,74);
                d.Line(50, 76, 50, 48, 3, Color.white);
            }
        }

        // A few filled polygons baked once in the Editor; four imported sprites per building at runtime.
        private sealed class PrototypeCanvas
        {
            private readonly int width;
            private readonly int height;
            private readonly Color32[] pixels;
            public PrototypeCanvas(int w, int h) { width = w; height = h; pixels = new Color32[w * h]; }

            public void Box(float x, float y, float halfWidth, float depth, float heightUnits)
            {
                Polygon(new Color(0.17f, 0.23f, 0.31f), x-halfWidth,y, x,y-depth,
                    x,y-depth+heightUnits, x-halfWidth,y+heightUnits);
                Polygon(new Color(0.24f, 0.32f, 0.41f), x,y-depth, x+halfWidth,y,
                    x+halfWidth,y+heightUnits, x,y-depth+heightUnits);
                Polygon(new Color(0.36f, 0.44f, 0.53f), x-halfWidth,y+heightUnits,
                    x,y+depth+heightUnits, x+halfWidth,y+heightUnits, x,y-depth+heightUnits);
            }

            public void Line(float x1, float y1, float x2, float y2, float thickness, Color color)
            {
                Vector2 normal = new Vector2(-(y2-y1), x2-x1).normalized * thickness * 0.5f;
                Polygon(color, x1+normal.x,y1+normal.y, x2+normal.x,y2+normal.y,
                    x2-normal.x,y2-normal.y, x1-normal.x,y1-normal.y);
            }

            public void Polygon(Color color, params float[] xy)
            {
                var vertices = new Vector2[xy.Length / 2];
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = new Vector2(xy[i*2] * width / 100f, xy[i*2+1] * height / 100f);
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bool inside = false;
                    for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                    {
                        Vector2 a = vertices[i], b = vertices[j];
                        if ((a.y > y + 0.5f) != (b.y > y + 0.5f) && x + 0.5f <
                            (b.x-a.x) * (y+0.5f-a.y) / (b.y-a.y) + a.x) inside = !inside;
                    }
                    if (inside) pixels[y * width + x] = color;
                }
            }

            public void Save(string path)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
        }
    }
}

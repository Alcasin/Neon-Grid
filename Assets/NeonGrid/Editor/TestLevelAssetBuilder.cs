using System.Collections.Generic;
using System.IO;
using NeonGrid.Data;
using NeonGrid.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class TestLevelAssetBuilder
    {
        private const string LevelDirectory = "Assets/NeonGrid/Resources/Levels";
        private const string SceneDirectory = "Assets/NeonGrid/Scenes";

        [MenuItem("Neon Grid/Rebuild Milestone Test Levels")]
        public static void CreateOrUpdateTestLevel()
        {
            Directory.CreateDirectory(LevelDirectory);
            Directory.CreateDirectory(SceneDirectory);

            LevelDefinition milestoneZero = CreateLevel("TestLevel4x4", 4, 4, tiles =>
            {
                Set(tiles, 4, 0, 1, TileType.PowerSource, 0, false);
                Set(tiles, 4, 1, 1, TileType.StraightWire, 0, true);
                Set(tiles, 4, 2, 1, TileType.CornerWire, 0, true);
                Set(tiles, 4, 2, 2, TileType.OutputLamp, 3, false);
            });

            LevelDefinition test01 = CreateLevel("M1_Test_01", 4, 3, tiles =>
            {
                Set(tiles, 4, 0, 1, TileType.PowerSource, 0, false);
                Set(tiles, 4, 1, 1, TileType.CrossJunction, 0, false);
                Set(tiles, 4, 2, 1, TileType.TJunction, 1, true);
                Set(tiles, 4, 3, 1, TileType.OutputLamp, 0, false);
                Set(tiles, 4, 2, 2, TileType.OutputLamp, 3, false);
            });

            LevelDefinition test02 = CreateLevel("M1_Test_02", 3, 3, tiles =>
            {
                Set(tiles, 3, 0, 0, TileType.PowerSource, 0, false);
                Set(tiles, 3, 1, 0, TileType.CornerWire, 0, true);
                Set(tiles, 3, 1, 1, TileType.StraightWire, 0, false);
                Set(tiles, 3, 1, 2, TileType.CornerWire, 1, false);
                Set(tiles, 3, 2, 2, TileType.OutputLamp, 0, false);
            });

            LevelDefinition test03 = CreateLevel("M1_Test_03", 3, 3, tiles =>
            {
                Set(tiles, 3, 0, 1, TileType.OutputLamp, 2, false);
                Set(tiles, 3, 1, 1, TileType.Diode, 0, true);
                Set(tiles, 3, 2, 1, TileType.PowerSource, 2, false);
            });

            LevelDefinition m2Test01 = CreateLevel("M2_Test_01", 4, 1, tiles =>
            {
                Set(tiles, 4, 0, 0, TileType.PowerSource, 0, false);
                Set(tiles, 4, 1, 0, TileType.Switch, 0, false, false);
                Set(tiles, 4, 2, 0, TileType.StraightWire, 1, false);
                Set(tiles, 4, 3, 0, TileType.OutputLamp, 0, false);
            });

            LevelDefinition m2Test02 = CreateLevel("M2_Test_02", 5, 2, tiles =>
            {
                Set(tiles, 5, 0, 0, TileType.PowerSource, 0, false);
                Set(tiles, 5, 1, 0, TileType.Switch, 0, false, false);
                Set(tiles, 5, 2, 0, TileType.AndGate, 0, false);
                Set(tiles, 5, 3, 0, TileType.Switch, 0, false, false);
                Set(tiles, 5, 4, 0, TileType.PowerSource, 2, false);
                Set(tiles, 5, 2, 1, TileType.OutputLamp, 3, false);
            });

            LevelDefinition m2Test03 = CreateLevel("M2_Test_03", 5, 2, tiles =>
            {
                Set(tiles, 5, 0, 0, TileType.PowerSource, 0, false);
                Set(tiles, 5, 1, 0, TileType.Switch, 0, false, false);
                Set(tiles, 5, 2, 0, TileType.OrGate, 0, false);
                Set(tiles, 5, 3, 0, TileType.Switch, 0, false, false);
                Set(tiles, 5, 4, 0, TileType.PowerSource, 2, false);
                Set(tiles, 5, 2, 1, TileType.OutputLamp, 3, false);
            });

            AssetDatabase.SaveAssets();

            string[] scenePaths =
            {
                CreateScene("Prototype", milestoneZero),
                CreateScene("M1_Test_01", test01),
                CreateScene("M1_Test_02", test02),
                CreateScene("M1_Test_03", test03),
                CreateScene("M2_Test_01", m2Test01),
                CreateScene("M2_Test_02", m2Test02),
                CreateScene("M2_Test_03", m2Test03)
            };
            var buildScenes = new EditorBuildSettingsScene[scenePaths.Length];
            for (int i = 0; i < scenePaths.Length; i++)
                buildScenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            EditorBuildSettings.scenes = buildScenes;
            Debug.Log("Created Neon Grid Milestone 0, 1, and 2 test levels and scenes.");
        }

        private static LevelDefinition CreateLevel(string assetName, int width, int height,
            System.Action<List<TileDefinition>> configure)
        {
            string path = $"{LevelDirectory}/{assetName}.asset";
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(level, path);
            }

            var tiles = new List<TileDefinition>();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                tiles.Add(new TileDefinition(new GridPosition(x, y), TileType.Empty, 0, false));
            configure(tiles);
            level.SetData(width, height, tiles);
            EditorUtility.SetDirty(level);
            return level;
        }

        private static string CreateScene(string sceneName, LevelDefinition level)
        {
            string path = $"{SceneDirectory}/{sceneName}.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Neon Grid Bootstrap");
            root.AddComponent<Presentation.NeonGridBootstrap>().SetLevel(level);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static void Set(List<TileDefinition> tiles, int width, int x, int y,
            TileType type, int rotation, bool rotatable, bool startingSwitchOn = false)
        {
            tiles[y * width + x] = new TileDefinition(
                new GridPosition(x, y), type, rotation, rotatable, startingSwitchOn);
        }
    }
}

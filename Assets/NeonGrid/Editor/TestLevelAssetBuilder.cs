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
        private const string LevelPath = LevelDirectory + "/TestLevel4x4.asset";
        private const string SceneDirectory = "Assets/NeonGrid/Scenes";
        private const string ScenePath = SceneDirectory + "/Prototype.unity";

        [MenuItem("Neon Grid/Rebuild 4x4 Test Level")]
        public static void CreateOrUpdateTestLevel()
        {
            Directory.CreateDirectory(LevelDirectory);
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(level, LevelPath);
            }

            var tiles = new List<TileDefinition>();
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                tiles.Add(new TileDefinition(new GridPosition(x, y), TileType.Empty, 0, false));

            Set(tiles, 0, 1, TileType.PowerSource, 0, false); // Exposes Right.
            Set(tiles, 1, 1, TileType.StraightWire, 0, true); // Starts vertical; one tap makes it horizontal.
            Set(tiles, 2, 1, TileType.CornerWire, 0, true);   // Three taps expose Left + Up.
            Set(tiles, 2, 2, TileType.OutputLamp, 3, false);  // Exposes Down.

            level.SetData(4, 4, tiles);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(SceneDirectory);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"Created Neon Grid prototype level at {LevelPath} and scene at {ScenePath}.");
        }

        private static void Set(List<TileDefinition> tiles, int x, int y, TileType type, int rotation, bool rotatable)
        {
            tiles[y * 4 + x] = new TileDefinition(new GridPosition(x, y), type, rotation, rotatable);
        }
    }
}

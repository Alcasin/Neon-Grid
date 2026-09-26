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
    public static class M15CityBuildingFinalArtPreparation
    {
        public const string Root = "Assets/NeonGrid/Art/CityBuildings";
        public const string PowerDirectory = Root + "/PowerStation";
        public const string CentralDirectory = Root + "/CentralGrid";
        public const string PowerDefinitionPath = PowerDirectory + "/PowerStation_Final.asset";
        public const string CentralDefinitionPath = CentralDirectory + "/CentralGrid_Final.asset";
        public static readonly string[] PowerLayerPaths =
        {
            PowerDirectory + "/PowerStation_Base.png",
            PowerDirectory + "/PowerStation_WarmLights.png",
            PowerDirectory + "/PowerStation_Energy.png",
            PowerDirectory + "/PowerStation_Core.png"
        };
        public static readonly string[] CentralLayerPaths =
        {
            CentralDirectory + "/CentralGrid_Base.png",
            CentralDirectory + "/CentralGrid_WarmLights.png",
            CentralDirectory + "/CentralGrid_Energy.png",
            CentralDirectory + "/CentralGrid_Core.png"
        };

        [MenuItem("Neon Grid/Prepare M15 Final City Building Art Bindings")]
        public static void Prepare()
        {
            EnsureFolder(Root);
            EnsureFolder(PowerDirectory);
            EnsureFolder(CentralDirectory);
            CityBuildingArtDefinition power = PrepareDefinition(PowerDefinitionPath,
                "power_station", PowerLayerPaths, new Vector2(0.9f, 0.7f),
                new[]
                {
                    new Vector4(1f, 0f, 0f, 0f),
                    new Vector4(1f, 0.42f, 0f, 0f),
                    new Vector4(1f, 0.72f, 0.24f, 0f),
                    new Vector4(1f, 0.88f, 0.63f, 0.12f),
                    new Vector4(1f, 1f, 1f, 0.65f)
                });
            CityBuildingArtDefinition central = PrepareDefinition(CentralDefinitionPath,
                "central_grid", CentralLayerPaths, new Vector2(0.88f, 0.7f),
                new[]
                {
                    new Vector4(1f, 0f, 0f, 0f),
                    new Vector4(1f, 0.32f, 0f, 0f),
                    new Vector4(1f, 0.70f, 0.22f, 0f),
                    new Vector4(1f, 0.90f, 0.72f, 0.30f),
                    new Vector4(1f, 1f, 1f, 1f)
                });
            AssetDatabase.SaveAssets();
            BindPrototypeScene(power, central);
            Report(power, "power_station", PowerLayerPaths);
            Report(central, "central_grid", CentralLayerPaths);
        }

        public static IReadOnlyList<string> MissingFinalArtFiles()
        {
            return PowerLayerPaths.Concat(CentralLayerPaths)
                .Where(path => !File.Exists(path)).ToArray();
        }

        private static CityBuildingArtDefinition PrepareDefinition(string assetPath,
            string chapterId, IReadOnlyList<string> layerPaths, Vector2 footprint,
            Vector4[] profile)
        {
            CityBuildingArtDefinition definition =
                AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CityBuildingArtDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }
            // Deliberately does not alter importers. Invalid supplied files remain visible
            // to validation rather than being silently repaired.
            Sprite[] sprites = layerPaths.Select(path =>
                AssetDatabase.LoadAssetAtPath<Sprite>(path)).ToArray();
            // Final overlays carry authored color. Neutral tints avoid multiplying their
            // amber/cyan values a second time; opacity profiles remain data-driven here.
            definition.SetData(chapterId, sprites[0], sprites[1], sprites[2], sprites[3],
                Color.white, Color.white, Color.white, Color.white,
                profile, footprint, Vector2.zero, 1f);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void BindPrototypeScene(CityBuildingArtDefinition power,
            CityBuildingArtDefinition central)
        {
            Scene scene = EditorSceneManager.OpenScene(M15CityBuildingArtPrototypeBuilder.ScenePath,
                OpenSceneMode.Single);
            CityBuildingArtPrototypeController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<
                    CityBuildingArtPrototypeController>(true)).Single();
            controller.SetFinalDefinitions(power, central, controller.FinalSubstation,
                controller.FinalControlCenter, controller.FinalAutomationPlant);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(scene);
        }

        private static void Report(CityBuildingArtDefinition definition, string chapterId,
            IEnumerable<string> expectedPaths)
        {
            string[] missing = expectedPaths.Where(path => !File.Exists(path)).ToArray();
            IReadOnlyList<string> issues = CityBuildingArtAssetValidator.Validate(definition,
                chapterId);
            if (missing.Length > 0)
                Debug.LogWarning($"M15-E1B {chapterId}: final art files missing:\n" +
                                 string.Join("\n", missing));
            else if (issues.Count > 0)
                Debug.LogError($"M15-E1B {chapterId}: final art validation failed:\n" +
                               string.Join("\n", issues));
            else
                Debug.Log($"M15-E1B {chapterId}: final art definition is valid.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

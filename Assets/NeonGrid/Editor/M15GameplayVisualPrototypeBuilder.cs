using System.IO;
using NeonGrid.Data;
using NeonGrid.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonGrid.Editor
{
    public static class M15GameplayVisualPrototypeBuilder
    {
        private const string ThemeDirectory = "Assets/NeonGrid/Resources/VisualThemes";
        private const string ThemePath = ThemeDirectory + "/TechnicalNeonPrototype.asset";
        private const string ProductionThemePath =
            ThemeDirectory + "/TechnicalNeonProductionPrototype.asset";
        private const string PrototypeDirectory = "Assets/NeonGrid/Resources/VisualPrototypes";
        private const string PrototypePath =
            PrototypeDirectory + "/M15_GameplayVisualPrototype.asset";
        private const string ScenePath = "Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity";

        [MenuItem("Neon Grid/Rebuild M15 Gameplay Visual Prototype")]
        public static void Build()
        {
            EnsureDirectory(ThemeDirectory);
            EnsureDirectory(PrototypeDirectory);

            CircuitVisualThemeDefinition theme =
                AssetDatabase.LoadAssetAtPath<CircuitVisualThemeDefinition>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<CircuitVisualThemeDefinition>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.SetIdentity("technical_neon_prototype", "Technical Neon Infrastructure",
                CircuitVisualStyle.TechnicalPrototype);
            EditorUtility.SetDirty(theme);

            CircuitVisualThemeDefinition productionTheme =
                AssetDatabase.LoadAssetAtPath<CircuitVisualThemeDefinition>(ProductionThemePath);
            if (productionTheme == null)
            {
                productionTheme = ScriptableObject.CreateInstance<CircuitVisualThemeDefinition>();
                AssetDatabase.CreateAsset(productionTheme, ProductionThemePath);
            }
            productionTheme.SetIdentity("technical_neon_production_prototype",
                "Technical Neon Production Prototype", CircuitVisualStyle.ProductionPrototype);
            EditorUtility.SetDirty(productionTheme);

            LevelDefinition ps01 = Resources.Load<LevelDefinition>("Levels/PowerStation/PS_01");
            LevelDefinition cg10 = Resources.Load<LevelDefinition>("Levels/CentralGrid/CG_10");
            if (ps01 == null || cg10 == null)
                throw new FileNotFoundException("PS_01 or CG_10 production level is missing.");

            GameplayVisualPrototypeDefinition prototype =
                AssetDatabase.LoadAssetAtPath<GameplayVisualPrototypeDefinition>(PrototypePath);
            if (prototype == null)
            {
                prototype = ScriptableObject.CreateInstance<GameplayVisualPrototypeDefinition>();
                AssetDatabase.CreateAsset(prototype, PrototypePath);
            }
            prototype.SetData(theme, productionTheme, ps01, cg10);
            EditorUtility.SetDirty(prototype);
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = productionTheme.Background;
            var root = new GameObject("M15 Gameplay Visual Prototype");
            root.AddComponent<GameplayVisualPrototypeController>().SetDefinition(prototype);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt isolated M15 gameplay visual prototype: {ScenePath}");
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

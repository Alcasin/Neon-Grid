using System.IO;
using NeonGrid.Data;
using NeonGrid.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonGrid.Editor
{
    public static class M15CampaignUiVisualPrototypeBuilder
    {
        private const string ThemeDirectory = "Assets/NeonGrid/Resources/VisualThemes";
        private const string ThemePath =
            ThemeDirectory + "/TechnicalNeonCampaignUiPrototype.asset";
        private const string PrototypeDirectory =
            "Assets/NeonGrid/Resources/VisualPrototypes";
        private const string PrototypePath =
            PrototypeDirectory + "/M15_CampaignUiVisualPrototype.asset";
        private const string ScenePath =
            "Assets/NeonGrid/Scenes/M15_CampaignUiVisualPrototype.unity";

        [MenuItem("Neon Grid/Rebuild M15 Campaign UI Visual Prototype")]
        public static void Build()
        {
            EnsureDirectory(ThemeDirectory);
            EnsureDirectory(PrototypeDirectory);

            CampaignUiThemeDefinition theme =
                AssetDatabase.LoadAssetAtPath<CampaignUiThemeDefinition>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<CampaignUiThemeDefinition>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.SetData(
                "technical_neon_campaign_ui_prototype",
                "Technical Neon Campaign Interface",
                Html("#050810"), Html("#0B1220"), Html("#101A2B"),
                Html("#08111E"), Html("#22BFD694"), Html("#27D8E8"),
                Html("#7895AA"), Html("#32D6A0"), Html("#D2A24C"),
                Html("#EAF8FF"), Html("#D6E8F2"), Html("#819AAC"),
                Html("#133047"), Html("#17465D"), Html("#0E2638"),
                Html("#1A2330"), Html("#1A2230"), Html("#12394B"),
                Html("#123D35"), 2f, 12f, 0.32f);
            EditorUtility.SetDirty(theme);

            CampaignDefinition campaign =
                Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
            if (campaign == null)
                throw new FileNotFoundException("Production campaign is missing.");
            CampaignNarrativeDefinition narrative =
                CampaignNarrativeCatalog.LoadForCampaign(campaign.CampaignId);
            if (narrative == null)
                throw new FileNotFoundException("Production campaign narrative is missing.");

            CampaignUiVisualPrototypeDefinition prototype =
                AssetDatabase.LoadAssetAtPath<CampaignUiVisualPrototypeDefinition>(
                    PrototypePath);
            if (prototype == null)
            {
                prototype = ScriptableObject.CreateInstance<
                    CampaignUiVisualPrototypeDefinition>();
                AssetDatabase.CreateAsset(prototype, PrototypePath);
            }
            prototype.SetData(theme, campaign, narrative);
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
            camera.backgroundColor = theme.Background;
            camera.targetDisplay = 0;

            var root = new GameObject("M15 Campaign UI Visual Prototype");
            root.AddComponent<CampaignUiVisualPrototypeController>()
                .SetDefinition(prototype);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt isolated M15 campaign UI visual prototype: {ScenePath}");
        }

        private static Color Html(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color color))
                throw new System.ArgumentException($"Invalid HTML color: {value}");
            return color;
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

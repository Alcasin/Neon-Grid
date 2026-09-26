using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonGrid.Editor
{
    public static class MainCampaignBuilder
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/NeonGrid_Main.asset";
        private const string ScenePath =
            "Assets/NeonGrid/Scenes/M12_NeonGrid_Main.unity";
        private const string ProductionThemePath =
            "Assets/NeonGrid/Resources/VisualThemes/TechnicalNeonProductionPrototype.asset";
        private const string CampaignUiThemePath =
            "Assets/NeonGrid/Resources/VisualThemes/TechnicalNeonCampaignUiPrototype.asset";
        internal const string PowerStationArtPath =
            "Assets/NeonGrid/Art/CityBuildings/PowerStation/PowerStation_Final.asset";
        internal const string CentralGridArtPath =
            "Assets/NeonGrid/Art/CityBuildings/CentralGrid/CentralGrid_Final.asset";

        private static readonly string[] SourceCampaignPaths =
        {
            "Assets/NeonGrid/Resources/Campaigns/PowerStation_VerticalSlice.asset",
            "Assets/NeonGrid/Resources/Campaigns/Substation_VerticalSlice.asset",
            "Assets/NeonGrid/Resources/Campaigns/ControlCenter_VerticalSlice.asset",
            "Assets/NeonGrid/Resources/Campaigns/AutomationPlant_VerticalSlice.asset",
            "Assets/NeonGrid/Resources/Campaigns/CentralGrid_VerticalSlice.asset"
        };

        [MenuItem("Neon Grid/Rebuild Main Production Campaign")]
        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var chapters = new List<CampaignChapterDefinition>(SourceCampaignPaths.Length);
            foreach (string sourcePath in SourceCampaignPaths)
            {
                CampaignDefinition source =
                    AssetDatabase.LoadAssetAtPath<CampaignDefinition>(sourcePath);
                if (source == null)
                    throw new InvalidOperationException($"Missing source campaign: {sourcePath}");
                CampaignValidationReport sourceValidation = new CampaignValidator().Validate(source);
                if (!sourceValidation.IsValid || source.Chapters.Count != 1)
                    throw new InvalidOperationException($"Invalid source campaign: {sourcePath}");
                chapters.Add(CloneChapter(source.Chapters[0]));
            }

            CampaignDefinition campaign =
                AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            if (campaign == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CampaignPath));
                campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
                AssetDatabase.CreateAsset(campaign, CampaignPath);
            }

            CircuitVisualThemeDefinition productionTheme =
                AssetDatabase.LoadAssetAtPath<CircuitVisualThemeDefinition>(ProductionThemePath);
            if (productionTheme == null || !productionTheme.IsConfigured ||
                !productionTheme.UsesProductionTreatment)
                throw new InvalidOperationException(
                    $"Missing or invalid production gameplay theme: {ProductionThemePath}");

            CampaignUiThemeDefinition campaignUiTheme =
                AssetDatabase.LoadAssetAtPath<CampaignUiThemeDefinition>(CampaignUiThemePath);
            if (campaignUiTheme == null || !campaignUiTheme.IsConfigured)
                throw new InvalidOperationException(
                    $"Missing or invalid production campaign UI theme: {CampaignUiThemePath}");

            campaign.SetData("neon_grid_main", chapters, "Neon Grid", productionTheme,
                campaignUiTheme);
            CityBuildingArtDefinition powerStationArt = LoadFinalArt(PowerStationArtPath,
                "power_station");
            CityBuildingArtDefinition centralGridArt = LoadFinalArt(CentralGridArtPath,
                "central_grid");
            campaign.SetCityBuildingArt(new[]
            {
                new CampaignCityBuildingArtBinding("power_station", powerStationArt),
                new CampaignCityBuildingArtBinding("central_grid", centralGridArt)
            });
            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            if (!validation.IsValid)
                throw new InvalidOperationException("Main production campaign is invalid.");

            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssetIfDirty(campaign);
            EnsureRuntimeScene(campaign);
            EnsureSceneInBuildSettings();
            Debug.Log($"Built Neon Grid main campaign from accepted source campaigns. Scene: {ScenePath}");
        }

        private static CityBuildingArtDefinition LoadFinalArt(string path, string chapterId)
        {
            CityBuildingArtDefinition definition =
                AssetDatabase.LoadAssetAtPath<CityBuildingArtDefinition>(path);
            if (definition == null || definition.ChapterId != chapterId ||
                !definition.IsConfigured)
                throw new InvalidOperationException(
                    $"Missing or invalid final city building art for '{chapterId}': {path}");
            IReadOnlyList<string> issues = CityBuildingArtAssetValidator.Validate(definition,
                chapterId);
            if (issues.Count > 0)
                throw new InvalidOperationException($"Invalid final city building art for " +
                    $"'{chapterId}': {string.Join("; ", issues)}");
            return definition;
        }

        private static CampaignChapterDefinition CloneChapter(CampaignChapterDefinition source)
        {
            var levels = new CampaignLevelEntry[source.Levels.Count];
            for (int index = 0; index < levels.Length; index++)
            {
                CampaignLevelEntry entry = source.Levels[index];
                if (!entry.AuthoredOptimalMoves.HasValue)
                    throw new InvalidOperationException(
                        $"Source level '{entry.LevelId}' has no authored optimal baseline.");
                LevelTutorialDefinition tutorial = entry.Tutorial == null
                    ? null
                    : new LevelTutorialDefinition(entry.Tutorial.Steps.Select(step =>
                        new TutorialStepDefinition(step.Message, step.TargetPosition,
                            step.CompletionCondition)));
                levels[index] = new CampaignLevelEntry(entry.LevelId, entry.DisplayName,
                    entry.LevelDefinition, entry.AuthoredOptimalMoves.Value, tutorial);
            }

            return new CampaignChapterDefinition(source.ChapterId, source.DisplayName, levels);
        }

        private static void EnsureRuntimeScene(CampaignDefinition campaign)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Neon Grid Main Campaign");
            root.AddComponent<Presentation.CampaignRuntimeController>().SetCampaign(campaign);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene scene in scenes)
                if (string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
                    return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

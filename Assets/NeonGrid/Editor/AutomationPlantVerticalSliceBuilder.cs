using System;
using System.Collections.Generic;
using System.IO;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonGrid.Editor
{
    public static class AutomationPlantVerticalSliceBuilder
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/AutomationPlant_VerticalSlice.asset";
        private const string ScenePath =
            "Assets/NeonGrid/Scenes/M10_AutomationPlant_VerticalSlice.unity";
        private const string LevelDirectory =
            "Assets/NeonGrid/Resources/Levels/Automation Plant";

        private static readonly int[] OptimalMoves = { 5, 6, 8, 5, 6, 7, 7, 6, 6, 9 };

        [MenuItem("Neon Grid/Rebuild Automation Plant Vertical Slice")]
        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var levels = new LevelDefinition[10];
            for (int index = 0; index < levels.Length; index++)
                levels[index] = LoadProductionLevel($"AP_{index + 1:D2}");

            CampaignDefinition campaign =
                AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            if (campaign == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CampaignPath));
                campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
                AssetDatabase.CreateAsset(campaign, CampaignPath);
            }

            var entries = new CampaignLevelEntry[levels.Length];
            for (int index = 0; index < entries.Length; index++)
                entries[index] = new CampaignLevelEntry($"automation_plant_{index + 1:D2}",
                    $"Automation Plant Circuit {index + 1}", levels[index], OptimalMoves[index]);

            campaign.SetData("automation_plant_vertical_slice", new[]
            {
                new CampaignChapterDefinition("automation_plant", "Automation Plant", entries)
            });
            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            if (!validation.IsValid)
                throw new InvalidOperationException("Automation Plant vertical-slice campaign is invalid.");

            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssetIfDirty(campaign);
            EnsureRuntimeScene(campaign);
            EnsureSceneInBuildSettings();
            Debug.Log($"Built Automation Plant vertical slice without modifying production levels. Scene: {ScenePath}");
        }

        private static LevelDefinition LoadProductionLevel(string assetName)
        {
            string path = $"{LevelDirectory}/{assetName}.asset";
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level == null)
                throw new InvalidOperationException($"Required production level is missing: {path}");
            return level;
        }

        private static void EnsureRuntimeScene(CampaignDefinition campaign)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Automation Plant Vertical Slice");
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

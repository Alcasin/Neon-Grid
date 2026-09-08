using System;
using System.Collections.Generic;
using System.IO;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonGrid.Editor
{
    public static class SubstationVerticalSliceBuilder
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/Substation_VerticalSlice.asset";
        private const string ScenePath =
            "Assets/NeonGrid/Scenes/M8_Substation_VerticalSlice.unity";
        private const string LevelDirectory =
            "Assets/NeonGrid/Resources/Levels/Substation";

        [MenuItem("Neon Grid/Rebuild Substation Vertical Slice")]
        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            LevelDefinition s01 = LoadProductionLevel("S_01");
            LevelDefinition s02 = LoadProductionLevel("S_02");
            LevelDefinition s03 = LoadProductionLevel("S_03");

            CampaignDefinition campaign =
                AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            if (campaign == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CampaignPath));
                campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
                AssetDatabase.CreateAsset(campaign, CampaignPath);
            }

            campaign.SetData("substation_vertical_slice", new[]
            {
                new CampaignChapterDefinition("substation", "Substation", new[]
                {
                    new CampaignLevelEntry("substation_01", "Substation Circuit 1", s01),
                    new CampaignLevelEntry("substation_02", "Substation Circuit 2", s02),
                    new CampaignLevelEntry("substation_03", "Substation Circuit 3", s03,
                        SwitchTutorial())
                })
            });

            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            if (!validation.IsValid)
                throw new InvalidOperationException(
                    "Substation vertical-slice campaign is invalid.");

            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssets();
            CreateRuntimeScene(campaign);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Built Substation vertical slice without modifying production levels. Scene: {ScenePath}");
        }

        private static LevelDefinition LoadProductionLevel(string assetName)
        {
            string path = $"{LevelDirectory}/{assetName}.asset";
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level == null)
                throw new InvalidOperationException($"Required production level is missing: {path}");
            return level;
        }

        private static LevelTutorialDefinition SwitchTutorial()
        {
            return new LevelTutorialDefinition(new[]
            {
                new TutorialStepDefinition("Switches can open or close a circuit.",
                    new GridPosition(2, 1), TutorialCompletionCondition.ToggleSwitch)
            });
        }

        private static void CreateRuntimeScene(CampaignDefinition campaign)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Substation Vertical Slice");
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

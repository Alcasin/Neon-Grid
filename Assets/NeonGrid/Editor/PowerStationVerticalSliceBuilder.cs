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
    public static class PowerStationVerticalSliceBuilder
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/PowerStation_VerticalSlice.asset";
        private const string ScenePath =
            "Assets/NeonGrid/Scenes/M7_PowerStation_VerticalSlice.unity";
        private const string LevelDirectory =
            "Assets/NeonGrid/Resources/Levels/PowerStation";

        [MenuItem("Neon Grid/Rebuild Power Station Vertical Slice")]
        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            LevelDefinition ps01 = LoadProductionLevel("PS_01");
            LevelDefinition ps02 = LoadProductionLevel("PS_02");
            LevelDefinition ps03 = LoadProductionLevel("PS_03");
            LevelDefinition ps04 = LoadProductionLevel("PS_04");
            LevelDefinition ps05 = LoadProductionLevel("PS_05");
            LevelDefinition ps06 = LoadProductionLevel("PS_06");
            LevelDefinition ps07 = LoadProductionLevel("PS_07");
            LevelDefinition ps08 = LoadProductionLevel("PS_08");
            LevelDefinition ps09 = LoadProductionLevel("PS_09");
            LevelDefinition ps10 = LoadProductionLevel("PS_10");

            CampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            if (campaign == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CampaignPath));
                campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
                AssetDatabase.CreateAsset(campaign, CampaignPath);
            }

            campaign.SetData("power_station_vertical_slice", new[]
            {
                new CampaignChapterDefinition("power_station", "Power Station", new[]
                {
                    new CampaignLevelEntry("power_01", "Power Circuit 01", ps01,
                        Tutorial("Tap a wire to rotate it.", new GridPosition(1, 0))),
                    new CampaignLevelEntry("power_02", "Power Circuit 02", ps02,
                        Tutorial("Corner wires redirect the current.", new GridPosition(1, 0))),
                    new CampaignLevelEntry("power_03", "Power Circuit 03", ps03),
                    new CampaignLevelEntry("power_04", "Power Circuit 04", ps04),
                    new CampaignLevelEntry("power_05", "Power Circuit 05", ps05),
                    new CampaignLevelEntry("power_06", "Power Circuit 06", ps06,
                        Tutorial("T-junctions split power into multiple paths.",
                            new GridPosition(2, 2))),
                    new CampaignLevelEntry("power_07", "Power Circuit 07", ps07),
                    new CampaignLevelEntry("power_08", "Power Circuit 08", ps08),
                    new CampaignLevelEntry("power_09", "Power Circuit 09", ps09,
                        Tutorial("Diodes only allow power in one direction.",
                            new GridPosition(2, 2))),
                    new CampaignLevelEntry("power_10", "Power Circuit 10", ps10)
                })
            });

            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            if (!validation.IsValid)
                throw new InvalidOperationException("Power Station vertical-slice campaign is invalid.");

            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssets();
            CreateRuntimeScene(campaign);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"Built Power Station vertical slice without modifying production levels. Scene: {ScenePath}");
        }

        private static LevelDefinition LoadProductionLevel(string assetName)
        {
            string path = $"{LevelDirectory}/{assetName}.asset";
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level == null)
                throw new InvalidOperationException($"Required production level is missing: {path}");
            return level;
        }

        private static LevelTutorialDefinition Tutorial(string message, GridPosition target)
        {
            return new LevelTutorialDefinition(new[]
            {
                new TutorialStepDefinition(message, target,
                    TutorialCompletionCondition.RotateClockwise)
            });
        }

        private static void CreateRuntimeScene(CampaignDefinition campaign)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Power Station Vertical Slice");
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

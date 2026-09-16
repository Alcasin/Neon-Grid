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
    public static class ControlCenterVerticalSliceBuilder
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/ControlCenter_VerticalSlice.asset";
        private const string ScenePath =
            "Assets/NeonGrid/Scenes/M9_ControlCenter_VerticalSlice.unity";
        private const string LevelDirectory =
            "Assets/NeonGrid/Resources/Levels/ControlCenter";

        private static readonly int[] OptimalMoves = { 4, 6, 1, 3, 4, 3, 4, 7, 6, 8 };

        [MenuItem("Neon Grid/Rebuild Control Center Vertical Slice")]
        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var levels = new LevelDefinition[10];
            for (int index = 0; index < levels.Length; index++)
                levels[index] = LoadProductionLevel($"CC_{index + 1:D2}");

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
            {
                LevelTutorialDefinition tutorial = index == 0
                    ? FixedAnchorTutorial("AND gates require all inputs to be powered.",
                        new GridPosition(2, 2))
                    : index == 2
                        ? FixedAnchorTutorial("OR gates activate when any input is powered.",
                            new GridPosition(2, 2))
                        : null;
                entries[index] = new CampaignLevelEntry($"control_center_{index + 1:D2}",
                    $"Control Center Circuit {index + 1}", levels[index], OptimalMoves[index], tutorial);
            }

            campaign.SetData("control_center_vertical_slice", new[]
            {
                new CampaignChapterDefinition("control_center", "Control Center", entries)
            });
            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            if (!validation.IsValid)
                throw new InvalidOperationException("Control Center vertical-slice campaign is invalid.");

            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssetIfDirty(campaign);
            EnsureRuntimeScene(campaign);
            EnsureSceneInBuildSettings();
            Debug.Log($"Built Control Center vertical slice without modifying production levels. Scene: {ScenePath}");
        }

        private static LevelDefinition LoadProductionLevel(string assetName)
        {
            string path = $"{LevelDirectory}/{assetName}.asset";
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level == null)
                throw new InvalidOperationException($"Required production level is missing: {path}");
            return level;
        }

        private static LevelTutorialDefinition FixedAnchorTutorial(string message,
            GridPosition anchor)
        {
            return new LevelTutorialDefinition(new[]
            {
                new TutorialStepDefinition(message, anchor,
                    TutorialCompletionCondition.AnyAcceptedAction)
            });
        }

        private static void EnsureRuntimeScene(CampaignDefinition campaign)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Control Center Vertical Slice");
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

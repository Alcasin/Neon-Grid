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
    public static class CentralGridVerticalSliceBuilder
    {
        private const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/CentralGrid_VerticalSlice.asset";
        private const string ScenePath =
            "Assets/NeonGrid/Scenes/M11_CentralGrid_VerticalSlice.unity";
        private const string LevelDirectory =
            "Assets/NeonGrid/Resources/Levels/CentralGrid";

        private static readonly int[] OptimalMoves = { 5, 6, 6, 6, 5, 7, 7, 9, 9, 10 };

        [MenuItem("Neon Grid/Rebuild Central Grid Vertical Slice")]
        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var levels = new LevelDefinition[10];
            for (int index = 0; index < levels.Length; index++)
                levels[index] = LoadProductionLevel($"CG_{index + 1:D2}");

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
                entries[index] = new CampaignLevelEntry($"central_grid_{index + 1:D2}",
                    $"Central Grid Circuit {index + 1}", levels[index], OptimalMoves[index]);

            campaign.SetData("central_grid_vertical_slice", new[]
            {
                new CampaignChapterDefinition("central_grid", "Central Grid", entries)
            });
            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            if (!validation.IsValid)
                throw new InvalidOperationException("Central Grid vertical-slice campaign is invalid.");

            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssetIfDirty(campaign);
            EnsureRuntimeScene(campaign);
            EnsureSceneInBuildSettings();
            Debug.Log($"Built Central Grid vertical slice without modifying production levels. Scene: {ScenePath}");
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
            var root = new GameObject("Central Grid Vertical Slice");
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

using System;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    /// <summary>Read-only snapshot of the five accepted vertical-slice campaigns.</summary>
    public static class MainCampaignSourceSnapshot
    {
        private static readonly string[] Paths =
        {
            "Assets/NeonGrid/Resources/Campaigns/PowerStation_VerticalSlice.asset",
            "Assets/NeonGrid/Resources/Campaigns/Substation_VerticalSlice.asset",
            "Assets/NeonGrid/Resources/Campaigns/ControlCenter_VerticalSlice.asset",
            "Assets/NeonGrid/Resources/Campaigns/AutomationPlant_VerticalSlice.asset",
            "Assets/NeonGrid/Resources/Campaigns/CentralGrid_VerticalSlice.asset"
        };

        public static void Run()
        {
            foreach (string path in Paths)
            {
                CampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(path);
                if (campaign == null) throw new InvalidOperationException($"Missing source campaign: {path}");
                CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
                if (!validation.IsValid)
                    throw new InvalidOperationException($"Invalid source campaign: {path}");
                if (campaign.Chapters.Count != 1)
                    throw new InvalidOperationException($"Source campaign must contain one chapter: {path}");

                CampaignChapterDefinition chapter = campaign.Chapters[0];
                int maximumStars = new CampaignProgressService(campaign).MaximumCampaignStars;
                Debug.Log($"M12 SOURCE path={path} campaign={campaign.CampaignId} " +
                    $"chapter={chapter.ChapterId} display={chapter.DisplayName} " +
                    $"levels={chapter.Levels.Count} maxStars={maximumStars}");
                for (int index = 0; index < chapter.Levels.Count; index++)
                {
                    CampaignLevelEntry entry = chapter.Levels[index];
                    string tutorials = entry.Tutorial == null
                        ? "none"
                        : string.Join(" || ", entry.Tutorial.Steps.Select(step =>
                            $"{step.Message}|{step.TargetPosition}|{step.CompletionCondition}"));
                    Debug.Log($"M12 SOURCE ENTRY chapter={chapter.ChapterId} order={index + 1} " +
                        $"id={entry.LevelId} display={entry.DisplayName} " +
                        $"asset={AssetDatabase.GetAssetPath(entry.LevelDefinition)} " +
                        $"optimal={entry.AuthoredOptimalMoves?.ToString() ?? "missing"} " +
                        $"tutorial={tutorials}");
                }
            }

            Debug.Log("M12 SOURCE SNAPSHOT PASSED");
        }
    }
}

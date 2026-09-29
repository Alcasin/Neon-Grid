using System;
using System.IO;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class M16ProductionGameplayFeedbackBuilder
    {
        public const string DefinitionPath =
            "Assets/NeonGrid/Resources/GameplayFeedback/M16_ProductionGameplayFeedback.asset";
        public const string CampaignPath =
            "Assets/NeonGrid/Resources/Campaigns/NeonGrid_Main.asset";

        [MenuItem("Neon Grid/M16/Bind Production Gameplay Feedback")]
        public static void Build()
        {
            EnsureDirectory(Path.GetDirectoryName(DefinitionPath)?.Replace('\\', '/'));
            ProductionGameplayFeedbackDefinition definition =
                AssetDatabase.LoadAssetAtPath<ProductionGameplayFeedbackDefinition>(
                    DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<
                    ProductionGameplayFeedbackDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            CircuitJuiceDefinition juice =
                AssetDatabase.LoadAssetAtPath<CircuitJuiceDefinition>(
                    M16CircuitJuicePrototypeBuilder.JuicePath);
            NeonGridAudioDefinition audio =
                AssetDatabase.LoadAssetAtPath<NeonGridAudioDefinition>(
                    M16AudioPrototypeBuilder.DefinitionPath);
            NeonGridHapticsDefinition haptics =
                AssetDatabase.LoadAssetAtPath<NeonGridHapticsDefinition>(
                    M16HapticsPrototypeBuilder.DefinitionPath);
            definition.SetData(juice, audio, haptics);
            if (!definition.IsConfigured)
                throw new InvalidOperationException(
                    "Accepted M16 feedback definitions are missing or invalid.");

            CampaignDefinition campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(
                CampaignPath);
            if (campaign == null)
                throw new FileNotFoundException("Production campaign is missing.", CampaignPath);
            campaign.SetGameplayFeedback(definition);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Bound accepted M16 feedback definitions to production gameplay.");
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

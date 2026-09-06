using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class CampaignProgressMenu
    {
        [MenuItem("Neon Grid/Reset Campaign Progress")]
        public static void ResetCampaignProgress()
        {
            CampaignDefinition campaign = Selection.activeObject as CampaignDefinition;
            if (campaign == null) return;
            string path = CampaignSaveStore.GetDefaultSavePath(campaign.CampaignId);
            CampaignSaveResult result = new CampaignSaveStore(path).Delete();
            if (result.Succeeded)
                Debug.Log($"Neon Grid campaign '{campaign.CampaignId}' progress reset. Save path: {path}");
            else
                Debug.LogError(result.Message);
        }

        [MenuItem("Neon Grid/Reset Campaign Progress", true)]
        private static bool CanResetCampaignProgress()
        {
            return Selection.activeObject is CampaignDefinition;
        }

        [MenuItem("Neon Grid/Show Campaign Progress Path")]
        public static void ShowCampaignProgressPath()
        {
            CampaignDefinition campaign = Selection.activeObject as CampaignDefinition;
            if (campaign == null) return;
            Debug.Log($"Neon Grid campaign '{campaign.CampaignId}' save path: " +
                      CampaignSaveStore.GetDefaultSavePath(campaign.CampaignId));
        }

        [MenuItem("Neon Grid/Show Campaign Progress Path", true)]
        private static bool CanShowCampaignProgressPath()
        {
            return Selection.activeObject is CampaignDefinition;
        }
    }
}

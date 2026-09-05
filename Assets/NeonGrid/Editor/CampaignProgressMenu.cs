using NeonGrid.Campaign;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class CampaignProgressMenu
    {
        [MenuItem("Neon Grid/Reset Campaign Progress")]
        public static void ResetCampaignProgress()
        {
            string path = CampaignSaveStore.GetDefaultSavePath();
            CampaignSaveResult result = new CampaignSaveStore(path).Delete();
            if (result.Succeeded)
                Debug.Log($"Neon Grid campaign progress reset. Save path: {path}");
            else
                Debug.LogError(result.Message);
        }

        [MenuItem("Neon Grid/Show Campaign Progress Path")]
        public static void ShowCampaignProgressPath()
        {
            Debug.Log($"Neon Grid campaign save path: {CampaignSaveStore.GetDefaultSavePath()}");
        }
    }
}

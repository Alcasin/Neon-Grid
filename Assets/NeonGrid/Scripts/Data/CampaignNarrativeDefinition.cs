using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonGrid.Data
{
    [Serializable]
    public sealed class CampaignIntroPage
    {
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 5)] private string body;
        [SerializeField] private string primaryAction;

        public string Title => title;
        public string Body => body;
        public string PrimaryAction => primaryAction;

        public CampaignIntroPage(string title, string body, string primaryAction)
        {
            this.title = title;
            this.body = body;
            this.primaryAction = primaryAction;
        }
    }

    [CreateAssetMenu(fileName = "CampaignNarrative", menuName = "Neon Grid/Campaign Narrative")]
    public sealed class CampaignNarrativeDefinition : ScriptableObject
    {
        [SerializeField] private string campaignId;
        [SerializeField] private List<CampaignIntroPage> introPages =
            new List<CampaignIntroPage>();

        public string CampaignId => campaignId;
        public IReadOnlyList<CampaignIntroPage> IntroPages => introPages;

        public bool AppliesTo(string candidateCampaignId)
        {
            return !string.IsNullOrWhiteSpace(candidateCampaignId) &&
                   string.Equals(campaignId, candidateCampaignId, StringComparison.Ordinal);
        }

#if UNITY_EDITOR
        public void SetData(string id, IEnumerable<CampaignIntroPage> pages)
        {
            campaignId = id;
            introPages = pages == null
                ? new List<CampaignIntroPage>()
                : new List<CampaignIntroPage>(pages);
        }
#endif
    }

    public static class CampaignNarrativeCatalog
    {
        private const string ResourcePath = "Narratives";

        public static CampaignNarrativeDefinition LoadForCampaign(string campaignId)
        {
            if (string.IsNullOrWhiteSpace(campaignId)) return null;

            CampaignNarrativeDefinition[] definitions =
                Resources.LoadAll<CampaignNarrativeDefinition>(ResourcePath);
            foreach (CampaignNarrativeDefinition definition in definitions)
                if (definition != null && definition.AppliesTo(campaignId))
                    return definition;
            return null;
        }
    }
}

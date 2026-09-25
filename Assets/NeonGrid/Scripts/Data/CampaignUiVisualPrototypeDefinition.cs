using UnityEngine;

namespace NeonGrid.Data
{
    public sealed class CampaignUiVisualPrototypeDefinition : ScriptableObject
    {
        [SerializeField] private CampaignUiThemeDefinition theme;
        [SerializeField] private CampaignDefinition campaign;
        [SerializeField] private CampaignNarrativeDefinition narrative;

        public CampaignUiThemeDefinition Theme => theme;
        public CampaignDefinition Campaign => campaign;
        public CampaignNarrativeDefinition Narrative => narrative;
        public bool IsConfigured => theme != null && theme.IsConfigured && campaign != null &&
                                    narrative != null && narrative.AppliesTo(campaign.CampaignId) &&
                                    narrative.IntroPages.Count > 0 &&
                                    narrative.EndingNarrative != null &&
                                    narrative.EndingNarrative.IsConfigured;

#if UNITY_EDITOR
        public void SetData(CampaignUiThemeDefinition uiTheme,
            CampaignDefinition campaignDefinition,
            CampaignNarrativeDefinition narrativeDefinition)
        {
            theme = uiTheme;
            campaign = campaignDefinition;
            narrative = narrativeDefinition;
        }
#endif
    }

    public static class CampaignUiVisualPrototypeCatalog
    {
        public const string ResourcePath =
            "VisualPrototypes/M15_CampaignUiVisualPrototype";

        public static CampaignUiVisualPrototypeDefinition Load()
        {
            return Resources.Load<CampaignUiVisualPrototypeDefinition>(ResourcePath);
        }
    }
}

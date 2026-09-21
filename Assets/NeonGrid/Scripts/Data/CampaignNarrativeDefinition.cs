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

    [Serializable]
    public sealed class CampaignChapterNarrativeEntry
    {
        [SerializeField] private string chapterId;
        [SerializeField] private string briefingTitle;
        [SerializeField, TextArea(2, 4)] private string briefingBody;
        [SerializeField] private string restoredTitle;
        [SerializeField, TextArea(2, 4)] private string restoredBody;

        public string ChapterId => chapterId;
        public string BriefingTitle => briefingTitle;
        public string BriefingBody => briefingBody;
        public string RestoredTitle => restoredTitle;
        public string RestoredBody => restoredBody;
        public bool HasBriefing => !string.IsNullOrWhiteSpace(briefingTitle) &&
                                   !string.IsNullOrWhiteSpace(briefingBody);
        public bool HasRestorationStatus => !string.IsNullOrWhiteSpace(restoredTitle) &&
                                            !string.IsNullOrWhiteSpace(restoredBody);

        public CampaignChapterNarrativeEntry(string chapterId, string briefingTitle,
            string briefingBody, string restoredTitle = null, string restoredBody = null)
        {
            this.chapterId = chapterId;
            this.briefingTitle = briefingTitle;
            this.briefingBody = briefingBody;
            this.restoredTitle = restoredTitle;
            this.restoredBody = restoredBody;
        }
    }

    [Serializable]
    public sealed class CampaignEndingNarrative
    {
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 4)] private string body;
        [SerializeField] private string statusTitle;
        [SerializeField, TextArea(2, 4)] private string statusBody;
        [SerializeField] private string returnButtonLabel;

        public string Title => title;
        public string Body => body;
        public string StatusTitle => statusTitle;
        public string StatusBody => statusBody;
        public string ReturnButtonLabel => returnButtonLabel;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(title) &&
                                    !string.IsNullOrWhiteSpace(body) &&
                                    !string.IsNullOrWhiteSpace(statusTitle) &&
                                    !string.IsNullOrWhiteSpace(statusBody) &&
                                    !string.IsNullOrWhiteSpace(returnButtonLabel);

        public CampaignEndingNarrative(string title, string body, string statusTitle,
            string statusBody, string returnButtonLabel)
        {
            this.title = title;
            this.body = body;
            this.statusTitle = statusTitle;
            this.statusBody = statusBody;
            this.returnButtonLabel = returnButtonLabel;
        }
    }

    [CreateAssetMenu(fileName = "CampaignNarrative", menuName = "Neon Grid/Campaign Narrative")]
    public sealed class CampaignNarrativeDefinition : ScriptableObject
    {
        [SerializeField] private string campaignId;
        [SerializeField] private List<CampaignIntroPage> introPages =
            new List<CampaignIntroPage>();
        [SerializeField] private List<CampaignChapterNarrativeEntry> chapterNarratives =
            new List<CampaignChapterNarrativeEntry>();
        [SerializeField] private CampaignEndingNarrative endingNarrative;

        public string CampaignId => campaignId;
        public IReadOnlyList<CampaignIntroPage> IntroPages => introPages;
        public IReadOnlyList<CampaignChapterNarrativeEntry> ChapterNarratives =>
            chapterNarratives;
        public CampaignEndingNarrative EndingNarrative => endingNarrative;

        public bool AppliesTo(string candidateCampaignId)
        {
            return !string.IsNullOrWhiteSpace(candidateCampaignId) &&
                   string.Equals(campaignId, candidateCampaignId, StringComparison.Ordinal);
        }

        public bool TryGetChapterNarrative(string chapterId,
            out CampaignChapterNarrativeEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(chapterId))
                foreach (CampaignChapterNarrativeEntry candidate in chapterNarratives)
                    if (candidate != null && string.Equals(candidate.ChapterId, chapterId,
                            StringComparison.Ordinal))
                    {
                        entry = candidate;
                        return true;
                    }
            entry = null;
            return false;
        }

        public bool HasUniqueChapterIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CampaignChapterNarrativeEntry entry in chapterNarratives)
                if (entry == null || string.IsNullOrWhiteSpace(entry.ChapterId) ||
                    !ids.Add(entry.ChapterId))
                    return false;
            return true;
        }

#if UNITY_EDITOR
        public void SetData(string id, IEnumerable<CampaignIntroPage> pages)
        {
            campaignId = id;
            introPages = pages == null
                ? new List<CampaignIntroPage>()
                : new List<CampaignIntroPage>(pages);
        }

        public void SetChapterNarratives(IEnumerable<CampaignChapterNarrativeEntry> entries)
        {
            chapterNarratives = entries == null
                ? new List<CampaignChapterNarrativeEntry>()
                : new List<CampaignChapterNarrativeEntry>(entries);
        }

        public void SetEndingNarrative(CampaignEndingNarrative ending)
        {
            endingNarrative = ending;
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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonGrid.Data
{
    [CreateAssetMenu(fileName = "CampaignDefinition", menuName = "Neon Grid/Campaign Definition")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        [SerializeField] private string campaignId = "campaign";
        [SerializeField] private List<CampaignChapterDefinition> chapters =
            new List<CampaignChapterDefinition>();

        public string CampaignId => campaignId;
        public IReadOnlyList<CampaignChapterDefinition> Chapters => chapters;

#if UNITY_EDITOR
        public void SetData(string newCampaignId, IEnumerable<CampaignChapterDefinition> newChapters)
        {
            campaignId = newCampaignId;
            chapters = newChapters == null
                ? null
                : new List<CampaignChapterDefinition>(newChapters);
        }
#endif
    }

    [Serializable]
    public sealed class CampaignChapterDefinition
    {
        [SerializeField] private string chapterId;
        [SerializeField] private string displayName;
        [SerializeField] private List<CampaignLevelEntry> levels = new List<CampaignLevelEntry>();

        public string ChapterId => chapterId;
        public string DisplayName => displayName;
        public IReadOnlyList<CampaignLevelEntry> Levels => levels;

        public CampaignChapterDefinition(string chapterId, string displayName,
            IEnumerable<CampaignLevelEntry> levels)
        {
            this.chapterId = chapterId;
            this.displayName = displayName;
            this.levels = levels == null ? null : new List<CampaignLevelEntry>(levels);
        }
    }

    [Serializable]
    public sealed class CampaignLevelEntry
    {
        [SerializeField] private string levelId;
        [SerializeField] private string displayName;
        [SerializeField] private LevelDefinition levelDefinition;
        [SerializeField] private LevelTutorialDefinition tutorial = new LevelTutorialDefinition();
        [SerializeField] private int authoredOptimalMoves = -1;

        public string LevelId => levelId;
        public string DisplayName => displayName;
        public LevelDefinition LevelDefinition => levelDefinition;
        public int? AuthoredOptimalMoves => authoredOptimalMoves >= 0
            ? authoredOptimalMoves
            : (int?)null;
        public LevelTutorialDefinition Tutorial => tutorial?.Steps != null && tutorial.Steps.Count > 0
            ? tutorial
            : null;

        public CampaignLevelEntry(string levelId, string displayName, LevelDefinition levelDefinition,
            LevelTutorialDefinition tutorial = null)
        {
            this.levelId = levelId;
            this.displayName = displayName;
            this.levelDefinition = levelDefinition;
            if (tutorial != null)
                this.tutorial = tutorial;
        }

        public CampaignLevelEntry(string levelId, string displayName, LevelDefinition levelDefinition,
            int authoredOptimalMoves, LevelTutorialDefinition tutorial = null)
            : this(levelId, displayName, levelDefinition, tutorial)
        {
            if (authoredOptimalMoves < 0)
                throw new ArgumentOutOfRangeException(nameof(authoredOptimalMoves));
            this.authoredOptimalMoves = authoredOptimalMoves;
        }
    }
}

using System;
using System.Collections.Generic;

namespace NeonGrid.Campaign
{
    [Serializable]
    public sealed class CampaignSaveData
    {
        public int version = CampaignSaveStore.CurrentVersion;
        public string campaignId;
        public List<LevelProgressSaveEntry> levelProgressEntries = new List<LevelProgressSaveEntry>();
    }

    [Serializable]
    public sealed class LevelProgressSaveEntry
    {
        public string levelId;
        public bool completed;
        public int bestStars;
        public int bestMoves = -1;
        public float bestTimeSeconds = -1f;

        public LevelProgressSaveEntry()
        {
        }

        public LevelProgressSaveEntry(string levelId, bool completed, int bestStars, int bestMoves,
            float bestTimeSeconds)
        {
            this.levelId = levelId;
            this.completed = completed;
            this.bestStars = bestStars;
            this.bestMoves = bestMoves;
            this.bestTimeSeconds = bestTimeSeconds;
        }
    }
}

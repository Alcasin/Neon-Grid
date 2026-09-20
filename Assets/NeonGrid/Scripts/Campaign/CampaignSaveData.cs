using System;
using System.Collections.Generic;

namespace NeonGrid.Campaign
{
    [Serializable]
    public sealed class CampaignSaveData
    {
        public int version = CampaignSaveStore.CurrentVersion;
        public string campaignId;
        public bool hasIntroCompletionState;
        public bool introCompleted;
        public List<LevelProgressSaveEntry> levelProgressEntries = new List<LevelProgressSaveEntry>();
        public List<ChapterRestorationSaveEntry> pendingRestorationEvents =
            new List<ChapterRestorationSaveEntry>();
    }

    [Serializable]
    public sealed class ChapterRestorationSaveEntry
    {
        public string restoredChapterId;
        public int restoredChapterIndex = -1;
        public string nextChapterId;
        public bool campaignComplete;

        public ChapterRestorationSaveEntry()
        {
        }

        public ChapterRestorationSaveEntry(string restoredChapterId, int restoredChapterIndex,
            string nextChapterId, bool campaignComplete)
        {
            this.restoredChapterId = restoredChapterId;
            this.restoredChapterIndex = restoredChapterIndex;
            this.nextChapterId = nextChapterId;
            this.campaignComplete = campaignComplete;
        }
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

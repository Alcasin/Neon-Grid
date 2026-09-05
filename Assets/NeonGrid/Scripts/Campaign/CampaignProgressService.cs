using System;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Session;

namespace NeonGrid.Campaign
{
    public enum CampaignChapterState
    {
        Locked,
        Available,
        Restored
    }

    public sealed class LevelProgress
    {
        public bool Completed { get; internal set; }
        public int BestStars { get; internal set; }
        public int BestMoves { get; internal set; } = -1;
        public float BestTimeSeconds { get; internal set; } = -1f;

        public bool HasKnownStars => BestStars > 0;
        public bool HasKnownMoves => BestMoves >= 0;
        public bool HasKnownTime => BestTimeSeconds >= 0f;
    }

    public sealed class CampaignProgressUpdate
    {
        public bool Accepted { get; }
        public string RejectionReason { get; }
        public string LevelId { get; }
        public bool LevelFirstCompleted { get; }
        public bool LevelImproved { get; }
        public bool ChapterJustRestored { get; }
        public string RestoredChapterId { get; }
        public bool NextChapterUnlocked { get; }

        private CampaignProgressUpdate(bool accepted, string rejectionReason, string levelId,
            bool levelFirstCompleted, bool levelImproved, bool chapterJustRestored,
            string restoredChapterId, bool nextChapterUnlocked)
        {
            Accepted = accepted;
            RejectionReason = rejectionReason;
            LevelId = levelId;
            LevelFirstCompleted = levelFirstCompleted;
            LevelImproved = levelImproved;
            ChapterJustRestored = chapterJustRestored;
            RestoredChapterId = restoredChapterId;
            NextChapterUnlocked = nextChapterUnlocked;
        }

        internal static CampaignProgressUpdate Rejected(string reason)
        {
            return new CampaignProgressUpdate(false, reason, null, false, false, false, null, false);
        }

        internal static CampaignProgressUpdate Recorded(string levelId, bool firstCompleted,
            bool improved, bool chapterJustRestored, string restoredChapterId, bool nextChapterUnlocked)
        {
            return new CampaignProgressUpdate(true, null, levelId, firstCompleted, improved,
                chapterJustRestored, restoredChapterId, nextChapterUnlocked);
        }
    }

    public sealed class CampaignProgressService
    {
        private readonly Dictionary<string, LevelProgress> progressByLevelId =
            new Dictionary<string, LevelProgress>(StringComparer.Ordinal);
        private readonly Dictionary<string, LevelLocation> levelLocations =
            new Dictionary<string, LevelLocation>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> chapterIndexes =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public CampaignDefinition Campaign { get; }
        public int MaximumCampaignStars { get; }
        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (LevelProgress progress in progressByLevelId.Values)
                    total += progress.BestStars;
                return total;
            }
        }

        public CampaignProgressService(CampaignDefinition campaign)
        {
            CampaignValidationReport validation = new CampaignValidator().Validate(campaign);
            if (!validation.IsValid)
                throw new ArgumentException("CampaignDefinition must pass validation before use.", nameof(campaign));

            Campaign = campaign;
            int levelCount = 0;
            for (int chapterIndex = 0; chapterIndex < campaign.Chapters.Count; chapterIndex++)
            {
                CampaignChapterDefinition chapter = campaign.Chapters[chapterIndex];
                chapterIndexes.Add(chapter.ChapterId, chapterIndex);
                for (int levelIndex = 0; levelIndex < chapter.Levels.Count; levelIndex++)
                {
                    CampaignLevelEntry entry = chapter.Levels[levelIndex];
                    levelLocations.Add(entry.LevelId,
                        new LevelLocation(chapterIndex, levelIndex, chapter, entry));
                    progressByLevelId.Add(entry.LevelId, new LevelProgress());
                    levelCount++;
                }
            }

            MaximumCampaignStars = levelCount * 3;
        }

        public LevelProgress GetLevelProgress(string levelId)
        {
            if (levelId == null || !progressByLevelId.TryGetValue(levelId, out LevelProgress progress))
                return null;
            return progress;
        }

        public bool IsChapterUnlocked(string chapterId)
        {
            if (chapterId == null || !chapterIndexes.TryGetValue(chapterId, out int chapterIndex))
                return false;
            if (chapterIndex == 0) return true;

            CampaignChapterDefinition chapter = Campaign.Chapters[chapterIndex];
            foreach (CampaignLevelEntry level in chapter.Levels)
                if (progressByLevelId[level.LevelId].Completed)
                    return true;

            return IsChapterRestored(Campaign.Chapters[chapterIndex - 1].ChapterId);
        }

        public CampaignChapterState GetChapterState(string chapterId)
        {
            if (chapterId == null || !chapterIndexes.ContainsKey(chapterId))
                return CampaignChapterState.Locked;
            if (IsChapterRestored(chapterId)) return CampaignChapterState.Restored;
            return IsChapterUnlocked(chapterId)
                ? CampaignChapterState.Available
                : CampaignChapterState.Locked;
        }

        public bool IsChapterRestored(string chapterId)
        {
            if (chapterId == null || !chapterIndexes.TryGetValue(chapterId, out int chapterIndex))
                return false;

            foreach (CampaignLevelEntry level in Campaign.Chapters[chapterIndex].Levels)
                if (!progressByLevelId[level.LevelId].Completed)
                    return false;
            return true;
        }

        public int GetCompletedLevelCount(string chapterId)
        {
            if (chapterId == null || !chapterIndexes.TryGetValue(chapterId, out int chapterIndex))
                return 0;
            int count = 0;
            foreach (CampaignLevelEntry level in Campaign.Chapters[chapterIndex].Levels)
                if (progressByLevelId[level.LevelId].Completed)
                    count++;
            return count;
        }

        public bool IsLevelUnlocked(string levelId)
        {
            if (levelId == null || !levelLocations.TryGetValue(levelId, out LevelLocation location))
                return false;
            if (progressByLevelId[levelId].Completed) return true;
            if (!IsChapterUnlocked(location.Chapter.ChapterId)) return false;
            if (location.LevelIndex == 0) return true;

            string previousId = location.Chapter.Levels[location.LevelIndex - 1].LevelId;
            return progressByLevelId[previousId].Completed;
        }

        public CampaignProgressUpdate RecordCompletion(string levelId, SessionCompletionResult result)
        {
            if (result == null)
                return CampaignProgressUpdate.Rejected("A completed SessionCompletionResult is required.");
            if (!levelLocations.TryGetValue(levelId ?? string.Empty, out LevelLocation location))
                return CampaignProgressUpdate.Rejected($"Unknown campaign LevelId '{levelId}'.");
            if (!IsLevelUnlocked(levelId))
                return CampaignProgressUpdate.Rejected($"Campaign level '{levelId}' is locked.");
            if (result.CompletedLevel != location.Entry.LevelDefinition)
                return CampaignProgressUpdate.Rejected(
                    $"Completion result does not belong to campaign level '{levelId}'.");
            if (result.TotalMoves < 0 || result.ElapsedSeconds < 0f ||
                float.IsNaN(result.ElapsedSeconds) || float.IsInfinity(result.ElapsedSeconds))
                return CampaignProgressUpdate.Rejected("Completion result contains invalid moves or time.");

            bool chapterWasRestored = IsChapterRestored(location.Chapter.ChapterId);
            bool nextChapterWasUnlocked = location.ChapterIndex + 1 < Campaign.Chapters.Count &&
                                          IsChapterUnlocked(Campaign.Chapters[location.ChapterIndex + 1].ChapterId);
            LevelProgress progress = progressByLevelId[levelId];
            bool firstCompletion = !progress.Completed;
            bool improved = false;
            progress.Completed = true;

            if (result.StarRating.Status == StarEvaluationStatus.Rated &&
                result.StarRating.Stars >= 1 && result.StarRating.Stars <= 3 &&
                result.StarRating.Stars > progress.BestStars)
            {
                progress.BestStars = result.StarRating.Stars;
                improved = true;
            }

            if (!progress.HasKnownMoves || result.TotalMoves < progress.BestMoves)
            {
                progress.BestMoves = result.TotalMoves;
                improved = true;
            }

            if (!progress.HasKnownTime || result.ElapsedSeconds < progress.BestTimeSeconds)
            {
                progress.BestTimeSeconds = result.ElapsedSeconds;
                improved = true;
            }

            bool chapterRestored = IsChapterRestored(location.Chapter.ChapterId);
            bool chapterJustRestored = !chapterWasRestored && chapterRestored;
            bool nextChapterUnlocked = false;
            if (location.ChapterIndex + 1 < Campaign.Chapters.Count)
            {
                string nextChapterId = Campaign.Chapters[location.ChapterIndex + 1].ChapterId;
                nextChapterUnlocked = !nextChapterWasUnlocked && IsChapterUnlocked(nextChapterId);
            }

            return CampaignProgressUpdate.Recorded(levelId, firstCompletion, improved,
                chapterJustRestored, chapterJustRestored ? location.Chapter.ChapterId : null,
                nextChapterUnlocked);
        }

        public CampaignLevelEntry FindLevel(string levelId)
        {
            return levelId != null && levelLocations.TryGetValue(levelId, out LevelLocation location)
                ? location.Entry
                : null;
        }

        public CampaignChapterDefinition FindChapter(string chapterId)
        {
            return chapterId != null && chapterIndexes.TryGetValue(chapterId, out int index)
                ? Campaign.Chapters[index]
                : null;
        }

        internal IReadOnlyList<LevelProgressSaveEntry> ExportProgress()
        {
            var entries = new List<LevelProgressSaveEntry>();
            foreach (CampaignChapterDefinition chapter in Campaign.Chapters)
            foreach (CampaignLevelEntry level in chapter.Levels)
            {
                LevelProgress progress = progressByLevelId[level.LevelId];
                if (!progress.Completed) continue;
                entries.Add(new LevelProgressSaveEntry(level.LevelId, true, progress.BestStars,
                    progress.BestMoves, progress.BestTimeSeconds));
            }

            return entries;
        }

        internal void ImportProgress(IEnumerable<LevelProgressSaveEntry> entries, IList<string> warnings)
        {
            if (entries == null) return;
            foreach (LevelProgressSaveEntry saved in entries)
            {
                if (!progressByLevelId.TryGetValue(saved.levelId, out LevelProgress progress))
                {
                    warnings?.Add($"Ignored progress for unknown LevelId '{saved.levelId}'.");
                    continue;
                }

                if (!saved.completed) continue;
                progress.Completed = true;
                progress.BestStars = saved.bestStars;
                progress.BestMoves = saved.bestMoves;
                progress.BestTimeSeconds = saved.bestTimeSeconds;
            }
        }

        private sealed class LevelLocation
        {
            public int ChapterIndex { get; }
            public int LevelIndex { get; }
            public CampaignChapterDefinition Chapter { get; }
            public CampaignLevelEntry Entry { get; }

            public LevelLocation(int chapterIndex, int levelIndex, CampaignChapterDefinition chapter,
                CampaignLevelEntry entry)
            {
                ChapterIndex = chapterIndex;
                LevelIndex = levelIndex;
                Chapter = chapter;
                Entry = entry;
            }
        }
    }
}

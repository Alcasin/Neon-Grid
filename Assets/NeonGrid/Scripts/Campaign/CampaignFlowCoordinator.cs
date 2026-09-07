using System;
using NeonGrid.Data;
using NeonGrid.Session;

namespace NeonGrid.Campaign
{
    public enum CampaignFlowScreen
    {
        Map,
        LevelSelection,
        Gameplay
    }

    public readonly struct CampaignResultNavigationState
    {
        public bool ShowRetry { get; }
        public bool ShowLevels { get; }
        public bool ShowMap { get; }
        public bool ShowNext { get; }

        private CampaignResultNavigationState(bool showRetry, bool showLevels, bool showMap,
            bool showNext)
        {
            ShowRetry = showRetry;
            ShowLevels = showLevels;
            ShowMap = showMap;
            ShowNext = showNext;
        }

        internal static CampaignResultNavigationState Normal(bool canStartNext)
        {
            return new CampaignResultNavigationState(true, true, false, canStartNext);
        }

        internal static CampaignResultNavigationState FirstChapterRestoration()
        {
            return new CampaignResultNavigationState(true, false, true, false);
        }
    }

    public sealed class CampaignFlowCoordinator
    {
        private readonly ICampaignProgressStore saveStore;

        public CampaignDefinition Campaign { get; }
        public CampaignProgressService Progress { get; }
        public CampaignFlowScreen CurrentScreen { get; private set; } = CampaignFlowScreen.Map;
        public CampaignChapterDefinition SelectedChapter { get; private set; }
        public CampaignLevelEntry ActiveLevel { get; private set; }
        public GameplaySession ActiveSession { get; private set; }
        public LevelTutorialDefinition ActiveTutorial { get; private set; }
        public CampaignProgressUpdate LastProgressUpdate { get; private set; }
        public CampaignSaveResult LastSaveResult { get; private set; }
        public string PendingRestorationChapterId { get; private set; }
        public CampaignResultNavigationState ResultNavigation { get; private set; }
        public string SavePath => saveStore.SavePath;

        public event Action<CampaignProgressUpdate> ProgressRecorded;

        public CampaignFlowCoordinator(CampaignDefinition campaign, CampaignProgressService progress,
            ICampaignProgressStore saveStore)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.saveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
            if (progress.Campaign != campaign)
                throw new ArgumentException("Progress belongs to a different CampaignDefinition.",
                    nameof(progress));
        }

        public bool OpenChapter(string chapterId)
        {
            CampaignChapterDefinition chapter = Progress.FindChapter(chapterId);
            if (chapter == null || !Progress.IsChapterUnlocked(chapterId)) return false;

            DetachSession();
            SelectedChapter = chapter;
            ActiveLevel = null;
            LastProgressUpdate = null;
            ResultNavigation = default;
            CurrentScreen = CampaignFlowScreen.LevelSelection;
            return true;
        }

        public bool StartLevel(string levelId)
        {
            CampaignLevelEntry entry = Progress.FindLevel(levelId);
            if (entry == null || SelectedChapter == null || !Contains(SelectedChapter, levelId) ||
                !Progress.IsLevelUnlocked(levelId))
                return false;

            StartAttempt(entry);
            return true;
        }

        public bool Retry()
        {
            if (ActiveLevel == null) return false;
            StartAttempt(ActiveLevel);
            return true;
        }

        public bool CanStartNextLevel()
        {
            if (ActiveLevel == null || ActiveSession == null || !ActiveSession.IsCompleted ||
                SelectedChapter == null)
                return false;

            int index = IndexOf(SelectedChapter, ActiveLevel.LevelId);
            if (index < 0 || index + 1 >= SelectedChapter.Levels.Count) return false;
            return Progress.IsLevelUnlocked(SelectedChapter.Levels[index + 1].LevelId);
        }

        public bool StartNextLevel()
        {
            if (!CanStartNextLevel()) return false;
            int index = IndexOf(SelectedChapter, ActiveLevel.LevelId);
            StartAttempt(SelectedChapter.Levels[index + 1]);
            return true;
        }

        public bool IsFinalLevelInSelectedChapter()
        {
            return ActiveLevel != null && SelectedChapter != null &&
                   IndexOf(SelectedChapter, ActiveLevel.LevelId) == SelectedChapter.Levels.Count - 1;
        }

        public bool ReturnToLevelSelection()
        {
            if (SelectedChapter == null) return false;

            DetachSession();
            ActiveLevel = null;
            LastProgressUpdate = null;
            ResultNavigation = default;
            CurrentScreen = CampaignFlowScreen.LevelSelection;
            return true;
        }

        public void ReturnToMap()
        {
            DetachSession();
            SelectedChapter = null;
            ActiveLevel = null;
            LastProgressUpdate = null;
            ResultNavigation = default;
            CurrentScreen = CampaignFlowScreen.Map;
        }

        public string ConsumePendingRestoration()
        {
            string result = PendingRestorationChapterId;
            PendingRestorationChapterId = null;
            return result;
        }

        private void StartAttempt(CampaignLevelEntry entry)
        {
            DetachSession();
            ActiveLevel = entry;
            LevelProgress historicalProgress = Progress.GetLevelProgress(entry.LevelId);
            ActiveTutorial = historicalProgress != null && !historicalProgress.Completed
                ? entry.Tutorial
                : null;
            LastProgressUpdate = null;
            ResultNavigation = default;
            ActiveSession = new GameplaySession(entry.LevelDefinition);
            ActiveSession.LevelCompleted += HandleSessionCompleted;
            CurrentScreen = CampaignFlowScreen.Gameplay;
            if (ActiveSession.IsCompleted)
                HandleSessionCompleted(ActiveSession.CompletionResult);
        }

        private void HandleSessionCompleted(SessionCompletionResult result)
        {
            CampaignProgressUpdate update = Progress.RecordCompletion(ActiveLevel.LevelId, result);
            LastProgressUpdate = update;
            if (!update.Accepted) return;

            if (update.ChapterJustRestored)
                PendingRestorationChapterId = update.RestoredChapterId;
            ResultNavigation = update.ChapterJustRestored
                ? CampaignResultNavigationState.FirstChapterRestoration()
                : CampaignResultNavigationState.Normal(CanStartNextLevel());
            LastSaveResult = saveStore.Save(Progress);
            ProgressRecorded?.Invoke(update);
        }

        private void DetachSession()
        {
            if (ActiveSession != null)
                ActiveSession.LevelCompleted -= HandleSessionCompleted;
            ActiveSession = null;
            ActiveTutorial = null;
        }

        private static bool Contains(CampaignChapterDefinition chapter, string levelId)
        {
            return IndexOf(chapter, levelId) >= 0;
        }

        private static int IndexOf(CampaignChapterDefinition chapter, string levelId)
        {
            for (int index = 0; index < chapter.Levels.Count; index++)
                if (string.Equals(chapter.Levels[index].LevelId, levelId, StringComparison.Ordinal))
                    return index;
            return -1;
        }
    }
}

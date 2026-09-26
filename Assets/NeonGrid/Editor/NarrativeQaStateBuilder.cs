using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Session;
using UnityEngine;

namespace NeonGrid.Editor
{
    public enum NarrativeQaPreset
    {
        FreshIntro,
        FreshMap,
        PowerStationStage1,
        PowerStationStage2,
        PowerStationStage3,
        PowerStationRestored,
        CentralGridStage1,
        CentralGridStage2,
        CentralGridStage3,
        FirstRestorationPending,
        FinalRestorationPending,
        EndingPending,
        PostEndingComplete
    }

    public static class NarrativeQaStateBuilder
    {
        public static CampaignProgressService Build(CampaignDefinition campaign,
            NarrativeQaPreset preset)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (campaign.Chapters == null || campaign.Chapters.Count == 0)
                throw new ArgumentException("Narrative QA requires at least one campaign chapter.",
                    nameof(campaign));

            var progress = new CampaignProgressService(campaign);
            switch (preset)
            {
                case NarrativeQaPreset.FreshIntro:
                    break;
                case NarrativeQaPreset.FreshMap:
                    progress.SetIntroCompleted(true);
                    break;
                case NarrativeQaPreset.PowerStationStage1:
                    progress.SetIntroCompleted(true);
                    CompleteLevels(progress, campaign.Chapters[0], 1);
                    break;
                case NarrativeQaPreset.PowerStationStage2:
                    progress.SetIntroCompleted(true);
                    CompleteLevels(progress, campaign.Chapters[0], 4);
                    break;
                case NarrativeQaPreset.PowerStationStage3:
                    progress.SetIntroCompleted(true);
                    CompleteLevels(progress, campaign.Chapters[0], 7);
                    break;
                case NarrativeQaPreset.PowerStationRestored:
                    progress.SetIntroCompleted(true);
                    CompleteChapter(progress, campaign.Chapters[0]);
                    break;
                case NarrativeQaPreset.CentralGridStage1:
                    progress.SetIntroCompleted(true);
                    CompleteThroughChapter(progress, campaign, campaign.Chapters.Count - 1);
                    CompleteLevels(progress, campaign.Chapters[campaign.Chapters.Count - 1], 1);
                    break;
                case NarrativeQaPreset.CentralGridStage2:
                    progress.SetIntroCompleted(true);
                    CompleteThroughChapter(progress, campaign, campaign.Chapters.Count - 1);
                    CompleteLevels(progress, campaign.Chapters[campaign.Chapters.Count - 1], 4);
                    break;
                case NarrativeQaPreset.CentralGridStage3:
                    progress.SetIntroCompleted(true);
                    CompleteThroughChapter(progress, campaign, campaign.Chapters.Count - 1);
                    CompleteLevels(progress, campaign.Chapters[campaign.Chapters.Count - 1], 7);
                    break;
                case NarrativeQaPreset.FirstRestorationPending:
                    progress.SetIntroCompleted(true);
                    CompleteChapter(progress, campaign.Chapters[0]);
                    progress.QueuePendingRestoration(campaign.Chapters[0].ChapterId);
                    break;
                case NarrativeQaPreset.FinalRestorationPending:
                    progress.SetIntroCompleted(true);
                    CompleteCampaign(progress, campaign);
                    CampaignChapterDefinition finalChapter =
                        campaign.Chapters[campaign.Chapters.Count - 1];
                    progress.QueuePendingRestoration(finalChapter.ChapterId);
                    break;
                case NarrativeQaPreset.EndingPending:
                    progress.SetIntroCompleted(true);
                    CompleteCampaign(progress, campaign);
                    break;
                case NarrativeQaPreset.PostEndingComplete:
                    progress.SetIntroCompleted(true);
                    CompleteCampaign(progress, campaign);
                    progress.SetEndingCompleted(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
            return progress;
        }

        public static CampaignSaveResult PrepareAndSave(CampaignDefinition campaign,
            NarrativeQaPreset preset, string savePath)
        {
            CampaignProgressService progress = Build(campaign, preset);
            return new CampaignSaveStore(savePath).Save(progress);
        }

        private static void CompleteCampaign(CampaignProgressService progress,
            CampaignDefinition campaign)
        {
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
                CompleteChapter(progress, chapter);
        }

        private static void CompleteThroughChapter(CampaignProgressService progress,
            CampaignDefinition campaign, int exclusiveChapterIndex)
        {
            for (int index = 0; index < exclusiveChapterIndex; index++)
                CompleteChapter(progress, campaign.Chapters[index]);
        }

        private static void CompleteChapter(CampaignProgressService progress,
            CampaignChapterDefinition chapter)
        {
            CompleteLevels(progress, chapter, chapter.Levels.Count);
        }

        private static void CompleteLevels(CampaignProgressService progress,
            CampaignChapterDefinition chapter, int count)
        {
            for (int index = 0; index < Mathf.Clamp(count, 0, chapter.Levels.Count); index++)
            {
                CampaignLevelEntry level = chapter.Levels[index];
                const int moves = 4;
                const int optimalMoves = 1;
                StarEvaluationResult stars = new StarEvaluator().Evaluate(true, moves,
                    optimalMoves);
                var result = new SessionCompletionResult(level.LevelDefinition, moves, 1f,
                    optimalMoves, false, stars);
                CampaignProgressUpdate update = progress.RecordCompletion(level.LevelId, result);
                if (!update.Accepted)
                    throw new InvalidOperationException(
                        $"Could not prepare narrative QA progress for '{level.LevelId}': " +
                        update.RejectionReason);
            }
        }
    }

    public sealed class NarrativeQaSnapshot
    {
        public CampaignLoadStatus LoadStatus { get; }
        public CampaignProgressService Progress { get; }
        public bool SaveExists { get; }
        public string IntroStatus { get; }
        public string EndingStatus { get; }
        public int CompletedLevels { get; }
        public int TotalLevels { get; }
        public string PendingRestoration { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        private NarrativeQaSnapshot(CampaignLoadStatus loadStatus,
            CampaignProgressService progress, bool saveExists, string introStatus,
            string endingStatus, int completedLevels, int totalLevels,
            string pendingRestoration, IReadOnlyList<string> diagnostics)
        {
            LoadStatus = loadStatus;
            Progress = progress;
            SaveExists = saveExists;
            IntroStatus = introStatus;
            EndingStatus = endingStatus;
            CompletedLevels = completedLevels;
            TotalLevels = totalLevels;
            PendingRestoration = pendingRestoration;
            Diagnostics = diagnostics;
        }

        public static NarrativeQaSnapshot Inspect(CampaignDefinition campaign, string savePath)
        {
            bool exists = File.Exists(savePath);
            CampaignSaveData raw = null;
            if (exists)
            {
                try
                {
                    raw = JsonUtility.FromJson<CampaignSaveData>(File.ReadAllText(savePath));
                }
                catch (Exception)
                {
                    // The normal store reports the actionable corrupt-save diagnostic below.
                }
            }

            CampaignLoadResult load = new CampaignSaveStore(savePath).Load(campaign);
            CampaignProgressService progress = load.Progress;
            int completed = 0;
            int total = 0;
            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
            foreach (CampaignLevelEntry level in chapter.Levels)
            {
                total++;
                if (progress.GetLevelProgress(level.LevelId).Completed) completed++;
            }

            string intro = !exists || raw == null
                ? "Pending"
                : raw.hasIntroCompletionState
                    ? progress.IntroCompleted ? "Complete" : "Pending"
                    : progress.IntroCompleted ? "Legacy — Complete" : "Legacy — Pending";
            string ending;
            if (!progress.IsCampaignComplete)
                ending = "Not applicable";
            else if (raw != null && !raw.hasEndingCompletionState)
                ending = "Legacy — Complete";
            else
                ending = progress.EndingCompleted ? "Complete" : "Pending";

            ChapterRestorationEvent pending = progress.PendingRestoration;
            string pendingLabel = pending == null
                ? "None"
                : FormatChapter(campaign, pending.RestoredChapterId);
            return new NarrativeQaSnapshot(load.Status, progress, exists, intro, ending,
                completed, total, pendingLabel, load.Diagnostics);
        }

        private static string FormatChapter(CampaignDefinition campaign, string chapterId)
        {
            CampaignChapterDefinition chapter = campaign.Chapters.FirstOrDefault(candidate =>
                string.Equals(candidate.ChapterId, chapterId, StringComparison.Ordinal));
            return chapter == null ? chapterId : $"{chapter.DisplayName} / {chapter.ChapterId}";
        }
    }

    public static class NarrativeQaValidator
    {
        public static IReadOnlyList<string> Validate(CampaignDefinition campaign,
            CampaignNarrativeDefinition narrative, NarrativeQaSnapshot snapshot = null)
        {
            var issues = new List<string>();
            if (campaign == null)
            {
                issues.Add("Production campaign is missing.");
                return issues;
            }
            if (narrative == null)
            {
                issues.Add("Production narrative definition is missing.");
                return issues;
            }
            if (!narrative.AppliesTo(campaign.CampaignId))
                issues.Add("Narrative CampaignId does not match the production campaign.");
            if (narrative.IntroPages == null || narrative.IntroPages.Count != 4)
                issues.Add("Production narrative must contain exactly four intro pages.");
            else
                for (int index = 0; index < narrative.IntroPages.Count; index++)
                {
                    CampaignIntroPage page = narrative.IntroPages[index];
                    if (page == null || string.IsNullOrWhiteSpace(page.Title) ||
                        string.IsNullOrWhiteSpace(page.Body) ||
                        string.IsNullOrWhiteSpace(page.PrimaryAction))
                        issues.Add($"Intro page {index + 1} has an empty required field.");
                }

            if (narrative.ChapterNarratives == null ||
                narrative.ChapterNarratives.Count != campaign.Chapters.Count)
                issues.Add("Narrative chapter-entry count does not match the campaign.");
            if (!narrative.HasUniqueChapterIds())
                issues.Add("Narrative chapter IDs must be non-empty and unique.");

            int restorationMessages = 0;
            for (int index = 0; index < campaign.Chapters.Count; index++)
            {
                CampaignChapterDefinition chapter = campaign.Chapters[index];
                if (!narrative.TryGetChapterNarrative(chapter.ChapterId, out var entry))
                {
                    issues.Add($"Missing narrative entry for chapter '{chapter.ChapterId}'.");
                    continue;
                }
                if (!entry.HasBriefing)
                    issues.Add($"Chapter '{chapter.ChapterId}' is missing briefing text.");
                bool final = index == campaign.Chapters.Count - 1;
                if (final && entry.HasRestorationStatus)
                    issues.Add("Final chapter must not define a restoration-status message.");
                if (!final && !entry.HasRestorationStatus)
                    issues.Add($"Chapter '{chapter.ChapterId}' is missing restoration text.");
                if (entry.HasRestorationStatus) restorationMessages++;
            }
            if (restorationMessages != Math.Max(0, campaign.Chapters.Count - 1))
                issues.Add("Restoration-message count does not match non-final chapters.");
            if (narrative.EndingNarrative == null || !narrative.EndingNarrative.IsConfigured)
                issues.Add("Production ending narrative is missing a required field.");

            if (snapshot != null)
            {
                foreach (string diagnostic in snapshot.Diagnostics)
                    issues.Add(diagnostic);
                if (snapshot.Progress.EndingCompleted && !snapshot.Progress.IsCampaignComplete)
                    issues.Add("Ending is complete while the campaign is incomplete.");
            }
            return issues;
        }
    }

    public sealed class NarrativeQaBackupService
    {
        private readonly string savePath;
        private readonly string backupPath;
        private readonly string absentMarkerPath;

        public string BackupPath => backupPath;
        public bool HasBackup => File.Exists(backupPath) || File.Exists(absentMarkerPath);

        public NarrativeQaBackupService(string savePath, string backupDirectory,
            string campaignId)
        {
            this.savePath = Path.GetFullPath(savePath);
            string directory = Path.GetFullPath(backupDirectory);
            backupPath = Path.Combine(directory, $"{campaignId}.campaign_progress.backup.json");
            absentMarkerPath = backupPath + ".no-save";
        }

        public string BackupCurrentSave()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            if (File.Exists(savePath))
            {
                File.Copy(savePath, backupPath, true);
                if (File.Exists(absentMarkerPath)) File.Delete(absentMarkerPath);
                return $"Backed up current save to {backupPath}";
            }

            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.WriteAllText(absentMarkerPath, "No production save existed at backup time.");
            return "Recorded that no production save existed at backup time.";
        }

        public string RestoreLastBackup()
        {
            if (File.Exists(backupPath))
            {
                string directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.Copy(backupPath, savePath, true);
                string temporary = savePath + ".tmp";
                if (File.Exists(temporary)) File.Delete(temporary);
                return $"Restored production save from {backupPath}";
            }
            if (File.Exists(absentMarkerPath))
            {
                CampaignSaveResult result = new CampaignSaveStore(savePath).Delete();
                if (!result.Succeeded) throw new IOException(result.Message);
                return "Restored the backed-up no-save state.";
            }
            throw new InvalidOperationException("No Narrative QA backup is available.");
        }
    }
}

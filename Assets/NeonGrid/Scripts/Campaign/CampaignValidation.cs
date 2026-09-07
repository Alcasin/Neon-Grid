using System;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;

namespace NeonGrid.Campaign
{
    public enum CampaignValidationSeverity
    {
        Warning,
        Error
    }

    public enum CampaignValidationCode
    {
        MissingCampaign,
        EmptyCampaignId,
        InvalidCampaignId,
        MissingChapters,
        NullChapter,
        EmptyChapterId,
        DuplicateChapterId,
        EmptyChapter,
        NullLevelEntry,
        EmptyLevelId,
        DuplicateLevelId,
        MissingLevelDefinition,
        DuplicateLevelDefinitionReference,
        NullTutorialStep,
        EmptyTutorialMessage,
        TutorialTargetOutOfBounds,
        TutorialTargetEmpty,
        TutorialCompletionIncompatible
    }

    public sealed class CampaignValidationIssue
    {
        public CampaignValidationSeverity Severity { get; }
        public CampaignValidationCode Code { get; }
        public string Message { get; }

        internal CampaignValidationIssue(CampaignValidationSeverity severity,
            CampaignValidationCode code, string message)
        {
            Severity = severity;
            Code = code;
            Message = message;
        }
    }

    public sealed class CampaignValidationReport
    {
        private readonly List<CampaignValidationIssue> issues = new List<CampaignValidationIssue>();

        public IReadOnlyList<CampaignValidationIssue> Issues => issues;
        public bool IsValid
        {
            get
            {
                foreach (CampaignValidationIssue issue in issues)
                    if (issue.Severity == CampaignValidationSeverity.Error)
                        return false;
                return true;
            }
        }

        internal void Add(CampaignValidationSeverity severity, CampaignValidationCode code, string message)
        {
            issues.Add(new CampaignValidationIssue(severity, code, message));
        }
    }

    public sealed class CampaignValidator
    {
        public CampaignValidationReport Validate(CampaignDefinition campaign)
        {
            var report = new CampaignValidationReport();
            if (campaign == null)
            {
                report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.MissingCampaign,
                    "CampaignDefinition is required.");
                return report;
            }

            if (string.IsNullOrWhiteSpace(campaign.CampaignId))
                report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.EmptyCampaignId,
                    "CampaignId must be non-empty.");
            else if (!CampaignIdRules.IsSafeStableId(campaign.CampaignId))
                report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.InvalidCampaignId,
                    "CampaignId may contain only lowercase letters, digits, underscores, and hyphens.");

            IReadOnlyList<CampaignChapterDefinition> chapters = campaign.Chapters;
            if (chapters == null || chapters.Count == 0)
            {
                report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.MissingChapters,
                    "Campaign must contain at least one chapter.");
                return report;
            }

            var chapterIds = new HashSet<string>(StringComparer.Ordinal);
            var levelIds = new HashSet<string>(StringComparer.Ordinal);
            var levelAssets = new Dictionary<LevelDefinition, string>();
            for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
            {
                CampaignChapterDefinition chapter = chapters[chapterIndex];
                if (chapter == null)
                {
                    report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.NullChapter,
                        $"Chapter record at index {chapterIndex} is null.");
                    continue;
                }

                string chapterContext = string.IsNullOrWhiteSpace(chapter.ChapterId)
                    ? $"chapter index {chapterIndex}"
                    : $"chapter '{chapter.ChapterId}'";
                if (string.IsNullOrWhiteSpace(chapter.ChapterId))
                    report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.EmptyChapterId,
                        $"ChapterId is empty at index {chapterIndex}.");
                else if (!chapterIds.Add(chapter.ChapterId))
                    report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.DuplicateChapterId,
                        $"ChapterId '{chapter.ChapterId}' is duplicated.");

                IReadOnlyList<CampaignLevelEntry> levels = chapter.Levels;
                if (levels == null || levels.Count == 0)
                {
                    report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.EmptyChapter,
                        $"{chapterContext} must contain at least one level.");
                    continue;
                }

                for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
                {
                    CampaignLevelEntry entry = levels[levelIndex];
                    if (entry == null)
                    {
                        report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.NullLevelEntry,
                            $"Level record at {chapterContext}, index {levelIndex} is null.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entry.LevelId))
                        report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.EmptyLevelId,
                            $"LevelId is empty at {chapterContext}, index {levelIndex}.");
                    else if (!levelIds.Add(entry.LevelId))
                        report.Add(CampaignValidationSeverity.Error, CampaignValidationCode.DuplicateLevelId,
                            $"LevelId '{entry.LevelId}' is duplicated across the campaign.");

                    if (entry.LevelDefinition == null)
                    {
                        report.Add(CampaignValidationSeverity.Error,
                            CampaignValidationCode.MissingLevelDefinition,
                            $"Level '{entry.LevelId}' has no LevelDefinition.");
                    }
                    else if (levelAssets.TryGetValue(entry.LevelDefinition, out string existingId))
                    {
                        report.Add(CampaignValidationSeverity.Warning,
                            CampaignValidationCode.DuplicateLevelDefinitionReference,
                            $"Levels '{existingId}' and '{entry.LevelId}' reference the same LevelDefinition.");
                    }
                    else
                    {
                        levelAssets.Add(entry.LevelDefinition, entry.LevelId);
                    }

                    ValidateTutorial(entry, report);
                }
            }

            return report;
        }

        private static void ValidateTutorial(CampaignLevelEntry entry,
            CampaignValidationReport report)
        {
            if (entry.Tutorial?.Steps == null) return;

            for (int stepIndex = 0; stepIndex < entry.Tutorial.Steps.Count; stepIndex++)
            {
                TutorialStepDefinition step = entry.Tutorial.Steps[stepIndex];
                string context = $"Tutorial step {stepIndex + 1} for level '{entry.LevelId}'";
                if (step == null)
                {
                    report.Add(CampaignValidationSeverity.Error,
                        CampaignValidationCode.NullTutorialStep, $"{context} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(step.Message))
                    report.Add(CampaignValidationSeverity.Error,
                        CampaignValidationCode.EmptyTutorialMessage,
                        $"{context} must have a non-empty message.");

                LevelDefinition level = entry.LevelDefinition;
                if (level == null) continue;
                if (step.TargetPosition.x < 0 || step.TargetPosition.x >= level.Width ||
                    step.TargetPosition.y < 0 || step.TargetPosition.y >= level.Height)
                {
                    report.Add(CampaignValidationSeverity.Error,
                        CampaignValidationCode.TutorialTargetOutOfBounds,
                        $"{context} targets {step.TargetPosition}, outside the referenced level.");
                    continue;
                }

                TileDefinition target = FindTile(level, step.TargetPosition);
                if (target == null || target.tileType == TileType.Empty)
                {
                    report.Add(CampaignValidationSeverity.Error,
                        CampaignValidationCode.TutorialTargetEmpty,
                        $"{context} targets an Empty tile at {step.TargetPosition}.");
                    continue;
                }

                if (!IsCompletionCompatible(step.CompletionCondition, target))
                    report.Add(CampaignValidationSeverity.Error,
                        CampaignValidationCode.TutorialCompletionIncompatible,
                        $"{context} completion '{step.CompletionCondition}' is incompatible with " +
                        $"the {target.tileType} tile at {step.TargetPosition}.");
            }
        }

        private static TileDefinition FindTile(LevelDefinition level, GridPosition position)
        {
            if (level.Tiles == null) return null;
            foreach (TileDefinition tile in level.Tiles)
                if (tile != null && tile.position.Equals(position))
                    return tile;
            return null;
        }

        private static bool IsCompletionCompatible(TutorialCompletionCondition condition,
            TileDefinition target)
        {
            switch (condition)
            {
                case TutorialCompletionCondition.RotateClockwise:
                    return target.isRotatable && target.tileType != TileType.Empty &&
                           target.tileType != TileType.Switch;
                case TutorialCompletionCondition.ToggleSwitch:
                    return target.tileType == TileType.Switch;
                default:
                    return false;
            }
        }
    }

    internal static class CampaignIdRules
    {
        public static bool IsSafeStableId(string campaignId)
        {
            if (string.IsNullOrWhiteSpace(campaignId)) return false;
            for (int index = 0; index < campaignId.Length; index++)
            {
                char character = campaignId[index];
                bool valid = character >= 'a' && character <= 'z' ||
                             character >= '0' && character <= '9' ||
                             character == '_' || character == '-';
                if (!valid) return false;
            }

            return true;
        }
    }
}

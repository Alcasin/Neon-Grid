using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Campaign
{
    public enum CampaignLoadStatus
    {
        NoSaveFound,
        Loaded,
        Corrupt,
        UnsupportedVersion,
        CampaignMismatch
    }

    public enum CampaignSaveStatus
    {
        Saved,
        Failed
    }

    public sealed class CampaignLoadResult
    {
        public CampaignLoadStatus Status { get; }
        public CampaignProgressService Progress { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        internal CampaignLoadResult(CampaignLoadStatus status, CampaignProgressService progress,
            IReadOnlyList<string> diagnostics)
        {
            Status = status;
            Progress = progress;
            Diagnostics = diagnostics;
        }
    }

    public sealed class CampaignSaveResult
    {
        public CampaignSaveStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == CampaignSaveStatus.Saved;

        internal CampaignSaveResult(CampaignSaveStatus status, string message)
        {
            Status = status;
            Message = message;
        }
    }

    public interface ICampaignProgressStore
    {
        string SavePath { get; }
        CampaignLoadResult Load(CampaignDefinition campaign);
        CampaignSaveResult Save(CampaignProgressService progress);
        CampaignSaveResult Delete();
    }

    public sealed class CampaignSaveStore : ICampaignProgressStore
    {
        public const int CurrentVersion = 1;
        public const string SaveFileName = "campaign_progress.json";

        public string SavePath { get; }

        public CampaignSaveStore(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
                throw new ArgumentException("A save file path is required.", nameof(savePath));
            SavePath = Path.GetFullPath(savePath);
        }

        public static string GetDefaultSavePath(string campaignId)
        {
            return BuildSavePath(Application.persistentDataPath, campaignId);
        }

        internal static string BuildSavePath(string persistentDataPath, string campaignId)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("A persistent-data root is required.", nameof(persistentDataPath));
            if (!CampaignIdRules.IsSafeStableId(campaignId))
                throw new ArgumentException(
                    "CampaignId may contain only lowercase letters, digits, underscores, and hyphens.",
                    nameof(campaignId));
            return Path.Combine(Path.GetFullPath(persistentDataPath), "NeonGrid", campaignId,
                SaveFileName);
        }

        public CampaignLoadResult Load(CampaignDefinition campaign)
        {
            var fresh = new CampaignProgressService(campaign);
            if (!File.Exists(SavePath))
                return new CampaignLoadResult(CampaignLoadStatus.NoSaveFound, fresh,
                    Array.Empty<string>());

            CampaignSaveData data;
            try
            {
                string json = File.ReadAllText(SavePath, Encoding.UTF8);
                data = JsonUtility.FromJson<CampaignSaveData>(json);
            }
            catch (Exception exception) when (IsFileOrDataException(exception))
            {
                return FailedLoad(CampaignLoadStatus.Corrupt, fresh,
                    $"Could not read campaign save: {exception.Message}");
            }

            if (data == null)
                return FailedLoad(CampaignLoadStatus.Corrupt, fresh,
                    "Campaign save did not contain a JSON object.");
            if (data.version != CurrentVersion)
                return FailedLoad(CampaignLoadStatus.UnsupportedVersion, fresh,
                    $"Save version {data.version} is unsupported; expected {CurrentVersion}.");
            if (!string.Equals(data.campaignId, campaign.CampaignId, StringComparison.Ordinal))
                return FailedLoad(CampaignLoadStatus.CampaignMismatch, fresh,
                    $"Save CampaignId '{data.campaignId}' does not match '{campaign.CampaignId}'.");
            if (!TryValidateEntries(data.levelProgressEntries, out string validationError))
                return FailedLoad(CampaignLoadStatus.Corrupt, fresh, validationError);

            var diagnostics = new List<string>();
            fresh.ImportProgress(data.levelProgressEntries, diagnostics);
            return new CampaignLoadResult(CampaignLoadStatus.Loaded, fresh,
                diagnostics.AsReadOnly());
        }

        public CampaignSaveResult Save(CampaignProgressService progress)
        {
            if (progress == null)
                return new CampaignSaveResult(CampaignSaveStatus.Failed,
                    "Campaign progress is required.");

            string temporaryPath = SavePath + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var data = new CampaignSaveData
                {
                    version = CurrentVersion,
                    campaignId = progress.Campaign.CampaignId,
                    levelProgressEntries = new List<LevelProgressSaveEntry>(progress.ExportProgress())
                };
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

                if (!File.Exists(SavePath))
                    File.Move(temporaryPath, SavePath);
                else
                    ReplaceExistingFile(temporaryPath);

                return new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }
            catch (Exception exception) when (IsFileOrDataException(exception))
            {
                TryDeleteTemporaryFile(temporaryPath);
                return new CampaignSaveResult(CampaignSaveStatus.Failed,
                    $"Could not save campaign progress: {exception.Message}");
            }
        }

        public CampaignSaveResult Delete()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                string temporaryPath = SavePath + ".tmp";
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                return new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }
            catch (Exception exception) when (IsFileOrDataException(exception))
            {
                return new CampaignSaveResult(CampaignSaveStatus.Failed,
                    $"Could not delete campaign progress: {exception.Message}");
            }
        }

        private static bool TryValidateEntries(IReadOnlyList<LevelProgressSaveEntry> entries,
            out string error)
        {
            if (entries == null)
            {
                error = "Campaign save is missing levelProgressEntries.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                LevelProgressSaveEntry entry = entries[index];
                if (entry == null)
                {
                    error = $"Campaign save progress entry {index} is null.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(entry.levelId) || !ids.Add(entry.levelId))
                {
                    error = $"Campaign save contains an empty or duplicate LevelId at entry {index}.";
                    return false;
                }
                if (entry.bestStars < 0 || entry.bestStars > 3 || entry.bestMoves < -1 ||
                    entry.bestTimeSeconds < -1f || float.IsNaN(entry.bestTimeSeconds) ||
                    float.IsInfinity(entry.bestTimeSeconds))
                {
                    error = $"Campaign save contains invalid best-result values for '{entry.levelId}'.";
                    return false;
                }
                if (entry.completed && (entry.bestMoves < 0 || entry.bestTimeSeconds < 0f))
                {
                    error = $"Completed level '{entry.levelId}' is missing moves or completion time.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static CampaignLoadResult FailedLoad(CampaignLoadStatus status,
            CampaignProgressService fresh, string diagnostic)
        {
            return new CampaignLoadResult(status, fresh,
                Array.AsReadOnly(new[] { diagnostic }));
        }

        private static void TryDeleteTemporaryFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void ReplaceExistingFile(string temporaryPath)
        {
            try
            {
                File.Replace(temporaryPath, SavePath, null);
            }
            catch (PlatformNotSupportedException)
            {
                // Some Unity player filesystems do not expose atomic replace. The fully
                // written temporary file is still copied only after serialization succeeds.
                File.Copy(temporaryPath, SavePath, true);
                File.Delete(temporaryPath);
            }
        }

        private static bool IsFileOrDataException(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is ArgumentException || exception is NotSupportedException ||
                   exception is SecurityException;
        }
    }
}

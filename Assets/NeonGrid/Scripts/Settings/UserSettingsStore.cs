using System;
using System.IO;
using System.Security;
using System.Text;
using UnityEngine;

namespace NeonGrid.Settings
{
    public enum UserSettingsLoadStatus
    {
        NoFileFound,
        Loaded,
        Corrupt,
        UnsupportedVersion
    }

    public enum UserSettingsSaveStatus
    {
        Saved,
        Failed
    }

    public sealed class UserSettingsLoadResult
    {
        public UserSettingsLoadStatus Status { get; }
        public UserSettings Settings { get; }
        public string Diagnostic { get; }

        internal UserSettingsLoadResult(
            UserSettingsLoadStatus status, UserSettings settings, string diagnostic)
        {
            Status = status;
            Settings = settings;
            Diagnostic = diagnostic;
        }
    }

    public sealed class UserSettingsSaveResult
    {
        public UserSettingsSaveStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == UserSettingsSaveStatus.Saved;

        internal UserSettingsSaveResult(UserSettingsSaveStatus status, string message)
        {
            Status = status;
            Message = message;
        }
    }

    public interface IUserSettingsStore
    {
        string SettingsPath { get; }
        UserSettingsLoadResult Load();
        UserSettingsSaveResult Save(UserSettings settings);
        UserSettingsSaveResult Delete();
    }

    /// <summary>
    /// Persists preferences independently from campaign progress. The path-injectable
    /// constructor keeps tests and future tools away from the real player settings file.
    /// </summary>
    public sealed class UserSettingsStore : IUserSettingsStore
    {
        public const int CurrentVersion = 1;
        public const string SettingsFileName = "neon_grid_settings.json";

        public string SettingsPath { get; }

        public UserSettingsStore(string settingsPath)
        {
            if (string.IsNullOrWhiteSpace(settingsPath))
                throw new ArgumentException("A settings file path is required.",
                    nameof(settingsPath));
            SettingsPath = Path.GetFullPath(settingsPath);
        }

        public static UserSettingsStore CreateDefault()
        {
            return new UserSettingsStore(GetDefaultSettingsPath());
        }

        public static string GetDefaultSettingsPath()
        {
            return BuildSettingsPath(Application.persistentDataPath);
        }

        internal static string BuildSettingsPath(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("A persistent-data root is required.",
                    nameof(persistentDataPath));
            return Path.Combine(Path.GetFullPath(persistentDataPath), "NeonGrid", "Settings",
                SettingsFileName);
        }

        public UserSettingsLoadResult Load()
        {
            if (!File.Exists(SettingsPath))
                return new UserSettingsLoadResult(UserSettingsLoadStatus.NoFileFound,
                    UserSettings.CreateDefault(), string.Empty);

            UserSettingsSaveData data;
            try
            {
                string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                data = JsonUtility.FromJson<UserSettingsSaveData>(json);
            }
            catch (Exception exception) when (IsFileOrDataException(exception))
            {
                return FailedLoad(UserSettingsLoadStatus.Corrupt,
                    $"Could not read user settings: {exception.Message}");
            }

            if (data == null)
                return FailedLoad(UserSettingsLoadStatus.Corrupt,
                    "User settings did not contain a JSON object.");
            if (data.version != CurrentVersion)
                return FailedLoad(UserSettingsLoadStatus.UnsupportedVersion,
                    $"Settings version {data.version} is unsupported; expected {CurrentVersion}.");

            var settings = new UserSettings(data.masterVolume, data.sfxVolume,
                data.ambienceVolume, data.hapticsEnabled);
            return new UserSettingsLoadResult(UserSettingsLoadStatus.Loaded, settings,
                string.Empty);
        }

        public UserSettingsSaveResult Save(UserSettings settings)
        {
            if (settings == null)
                return new UserSettingsSaveResult(UserSettingsSaveStatus.Failed,
                    "User settings are required.");

            string temporaryPath = SettingsPath + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                UserSettings sanitized = settings.CopySanitized();
                var data = new UserSettingsSaveData
                {
                    version = CurrentVersion,
                    masterVolume = sanitized.MasterVolume,
                    sfxVolume = sanitized.SfxVolume,
                    ambienceVolume = sanitized.AmbienceVolume,
                    hapticsEnabled = sanitized.HapticsEnabled
                };
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true),
                    new UTF8Encoding(false));

                if (!File.Exists(SettingsPath))
                    File.Move(temporaryPath, SettingsPath);
                else
                    ReplaceExistingFile(temporaryPath);

                return new UserSettingsSaveResult(UserSettingsSaveStatus.Saved, SettingsPath);
            }
            catch (Exception exception) when (IsFileOrDataException(exception))
            {
                TryDeleteTemporaryFile(temporaryPath);
                return new UserSettingsSaveResult(UserSettingsSaveStatus.Failed,
                    $"Could not save user settings: {exception.Message}");
            }
        }

        public UserSettingsSaveResult Delete()
        {
            try
            {
                if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
                string temporaryPath = SettingsPath + ".tmp";
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                return new UserSettingsSaveResult(UserSettingsSaveStatus.Saved, SettingsPath);
            }
            catch (Exception exception) when (IsFileOrDataException(exception))
            {
                return new UserSettingsSaveResult(UserSettingsSaveStatus.Failed,
                    $"Could not delete user settings: {exception.Message}");
            }
        }

        private static UserSettingsLoadResult FailedLoad(
            UserSettingsLoadStatus status, string diagnostic)
        {
            return new UserSettingsLoadResult(status, UserSettings.CreateDefault(), diagnostic);
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
                File.Replace(temporaryPath, SettingsPath, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, SettingsPath, true);
                File.Delete(temporaryPath);
            }
        }

        private static bool IsFileOrDataException(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is ArgumentException || exception is NotSupportedException ||
                   exception is SecurityException;
        }

        [Serializable]
        private sealed class UserSettingsSaveData
        {
            public int version = CurrentVersion;
            public float masterVolume = UserSettings.DefaultMasterVolume;
            public float sfxVolume = UserSettings.DefaultSfxVolume;
            public float ambienceVolume = UserSettings.DefaultAmbienceVolume;
            public bool hapticsEnabled = UserSettings.DefaultHapticsEnabled;
        }
    }
}

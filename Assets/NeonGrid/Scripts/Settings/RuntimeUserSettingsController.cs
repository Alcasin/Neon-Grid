using System;
using NeonGrid.Presentation;

namespace NeonGrid.Settings
{
    /// <summary>
    /// Owns one session's authoritative settings state. Live application is separate from
    /// explicit persistence so future sliders can update continuously without writing per tick.
    /// </summary>
    public sealed class RuntimeUserSettingsController
    {
        private readonly IUserSettingsStore store;
        private UserSettings current;
        private NeonGridAudioService audioService;
        private NeonGridHapticsService hapticsService;

        public UserSettings CurrentSettings => current.CopySanitized();
        public UserSettingsLoadResult LastLoadResult { get; }
        public UserSettingsSaveResult LastSaveResult { get; private set; }
        public bool HasUnpersistedChanges { get; private set; }
        public NeonGridAudioService AudioService => audioService;
        public NeonGridHapticsService HapticsService => hapticsService;

        public RuntimeUserSettingsController(IUserSettingsStore settingsStore)
        {
            store = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            LastLoadResult = store.Load();
            current = (LastLoadResult.Settings ?? UserSettings.CreateDefault()).CopySanitized();
        }

        private RuntimeUserSettingsController()
        {
            current = UserSettings.CreateDefault();
            LastLoadResult = new UserSettingsLoadResult(UserSettingsLoadStatus.NoFileFound,
                current.CopySanitized(), string.Empty);
        }

        internal static RuntimeUserSettingsController CreateDefaultsWithoutPersistence()
        {
            return new RuntimeUserSettingsController();
        }

        public void AttachServices(NeonGridAudioService audio, NeonGridHapticsService haptics)
        {
            audioService = audio;
            hapticsService = haptics;
            ApplyCurrentSettings();
        }

        public bool SetMasterVolume(float value)
        {
            float previous = current.MasterVolume;
            current.MasterVolume = value;
            if (current.MasterVolume.Equals(previous)) return false;
            HasUnpersistedChanges = true;
            ApplyAudioSettings();
            return true;
        }

        public bool SetSfxVolume(float value)
        {
            float previous = current.SfxVolume;
            current.SfxVolume = value;
            if (current.SfxVolume.Equals(previous)) return false;
            HasUnpersistedChanges = true;
            ApplyAudioSettings();
            return true;
        }

        public bool SetAmbienceVolume(float value)
        {
            float previous = current.AmbienceVolume;
            current.AmbienceVolume = value;
            if (current.AmbienceVolume.Equals(previous)) return false;
            HasUnpersistedChanges = true;
            ApplyAudioSettings();
            return true;
        }

        public bool SetHapticsEnabled(bool enabled)
        {
            if (current.HapticsEnabled == enabled) return false;
            current.HapticsEnabled = enabled;
            HasUnpersistedChanges = true;
            hapticsService?.SetHapticsEnabled(enabled);
            return true;
        }

        public UserSettingsSaveResult PersistCurrentSettings()
        {
            if (store == null)
            {
                LastSaveResult = new UserSettingsSaveResult(UserSettingsSaveStatus.Failed,
                    "No user-settings store is attached to this runtime.");
                return LastSaveResult;
            }

            LastSaveResult = store.Save(current.CopySanitized());
            if (LastSaveResult.Succeeded) HasUnpersistedChanges = false;
            return LastSaveResult;
        }

        private void ApplyCurrentSettings()
        {
            ApplyAudioSettings();
            hapticsService?.SetHapticsEnabled(current.HapticsEnabled);
        }

        private void ApplyAudioSettings()
        {
            audioService?.SetUserVolumeMultipliers(current.MasterVolume, current.SfxVolume,
                current.AmbienceVolume);
        }
    }
}

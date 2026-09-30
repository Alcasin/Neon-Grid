using System;

namespace NeonGrid.Settings
{
    /// <summary>
    /// Runtime-neutral user preference state. Values are sanitized at the model boundary so
    /// invalid persisted or caller-provided volumes cannot enter later runtime integrations.
    /// </summary>
    [Serializable]
    public sealed class UserSettings
    {
        public const float DefaultMasterVolume = 1f;
        public const float DefaultSfxVolume = 1f;
        public const float DefaultAmbienceVolume = 1f;
        public const bool DefaultHapticsEnabled = true;

        private float masterVolume;
        private float sfxVolume;
        private float ambienceVolume;

        public float MasterVolume
        {
            get => masterVolume;
            set => masterVolume = SanitizeVolume(value, DefaultMasterVolume);
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = SanitizeVolume(value, DefaultSfxVolume);
        }

        public float AmbienceVolume
        {
            get => ambienceVolume;
            set => ambienceVolume = SanitizeVolume(value, DefaultAmbienceVolume);
        }

        public bool HapticsEnabled { get; set; }

        public UserSettings(
            float masterVolume = DefaultMasterVolume,
            float sfxVolume = DefaultSfxVolume,
            float ambienceVolume = DefaultAmbienceVolume,
            bool hapticsEnabled = DefaultHapticsEnabled)
        {
            MasterVolume = masterVolume;
            SfxVolume = sfxVolume;
            AmbienceVolume = ambienceVolume;
            HapticsEnabled = hapticsEnabled;
        }

        public static UserSettings CreateDefault()
        {
            return new UserSettings();
        }

        internal UserSettings CopySanitized()
        {
            return new UserSettings(MasterVolume, SfxVolume, AmbienceVolume, HapticsEnabled);
        }

        private static float SanitizeVolume(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}

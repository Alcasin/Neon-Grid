using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Settings;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeonGrid.Tests
{
    public sealed class RuntimeUserSettingsTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private ProductionGameplayFeedbackDefinition feedback;

        [SetUp]
        public void SetUp()
        {
            NeonGridHapticsSettings.HapticsEnabled = true;
            feedback = Resources.Load<ProductionGameplayFeedbackDefinition>(
                "GameplayFeedback/M16_ProductionGameplayFeedback");
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.IsConfigured, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            NeonGridHapticsSettings.HapticsEnabled = true;
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void MasterVolume_MultipliesAuthoredSfxGain()
        {
            RuntimeFixture fixture = Build(new UserSettings(.5f, 1f, 1f, true));
            Assert.That(fixture.Audio.TryResolveCue(NeonGridAudioEvent.UIButtonPressed,
                out NeonGridAudioCue cue), Is.True);

            Assert.That(fixture.Audio.TryPlay(NeonGridAudioEvent.UIButtonPressed, 1f), Is.True);

            float expected = cue.Gain * feedback.AudioDefinition.MasterSfxMultiplier *
                             feedback.AudioDefinition.SfxVolume * .5f;
            Assert.That(fixture.Audio.LastPlayback.Value.Gain,
                Is.EqualTo(expected).Within(.0001f));
        }

        [Test]
        public void SfxVolume_AffectsSfxButNotAmbience()
        {
            RuntimeFixture fixture = Build(UserSettings.CreateDefault());
            fixture.Audio.RequestAmbience(NeonGridAmbienceMode.City);
            fixture.Audio.AdvanceAmbience(feedback.AudioDefinition.AmbienceFadeDuration);
            float ambienceBefore = ActiveAmbience(fixture.Audio).volume;

            Assert.That(fixture.Settings.SetSfxVolume(.25f), Is.True);
            Assert.That(fixture.Audio.TryPlay(NeonGridAudioEvent.UIButtonPressed, 1f), Is.True);

            Assert.That(fixture.Audio.LastPlayback.Value.Gain,
                Is.EqualTo(AuthoredSfxGain(NeonGridAudioEvent.UIButtonPressed) * .25f)
                    .Within(.0001f));
            Assert.That(ActiveAmbience(fixture.Audio).volume,
                Is.EqualTo(ambienceBefore).Within(.0001f));
        }

        [Test]
        public void AmbienceVolume_AffectsAmbienceButNotSfx()
        {
            RuntimeFixture fixture = Build(UserSettings.CreateDefault());
            fixture.Audio.RequestAmbience(NeonGridAmbienceMode.City);
            fixture.Audio.AdvanceAmbience(feedback.AudioDefinition.AmbienceFadeDuration);

            Assert.That(fixture.Settings.SetAmbienceVolume(.4f), Is.True);
            Assert.That(fixture.Audio.TryPlay(NeonGridAudioEvent.UIButtonPressed, 1f), Is.True);

            Assert.That(ActiveAmbience(fixture.Audio).volume,
                Is.EqualTo(.18f * feedback.AudioDefinition.AmbienceVolume * .4f)
                    .Within(.0001f));
            Assert.That(fixture.Audio.LastPlayback.Value.Gain,
                Is.EqualTo(AuthoredSfxGain(NeonGridAudioEvent.UIButtonPressed))
                    .Within(.0001f));
        }

        [Test]
        public void MasterVolume_MultipliesAmbienceWithoutReplacingBaseGain()
        {
            RuntimeFixture fixture = Build(new UserSettings(.5f, 1f, .4f, true));
            fixture.Audio.RequestAmbience(NeonGridAmbienceMode.City);
            fixture.Audio.AdvanceAmbience(feedback.AudioDefinition.AmbienceFadeDuration);

            Assert.That(feedback.AudioDefinition.CityAmbienceGain,
                Is.EqualTo(.18f).Within(.0001f));
            Assert.That(feedback.AudioDefinition.GameplayAmbienceGain,
                Is.EqualTo(.153f).Within(.0001f));
            Assert.That(ActiveAmbience(fixture.Audio).volume,
                Is.EqualTo(.18f * .5f * .4f).Within(.0001f));
        }

        [Test]
        public void VolumeInputs_AreSanitizedAndEffectiveOutputsRemainSafe()
        {
            RuntimeFixture fixture = Build(UserSettings.CreateDefault());

            fixture.Settings.SetMasterVolume(-4f);
            fixture.Settings.SetSfxVolume(float.PositiveInfinity);
            fixture.Settings.SetAmbienceVolume(float.NaN);

            Assert.That(fixture.Audio.UserMasterVolume, Is.Zero);
            Assert.That(fixture.Audio.UserSfxVolume, Is.EqualTo(1f));
            Assert.That(fixture.Audio.UserAmbienceVolume, Is.EqualTo(1f));
            Assert.That(fixture.Audio.EffectiveSfxMultiplier, Is.InRange(0f, 1f));
            Assert.That(fixture.Audio.EffectiveAmbienceMultiplier, Is.InRange(0f, 1f));
        }

        [Test]
        public void HapticsSetting_DisablesAndRestoresExistingDispatch()
        {
            var backend = new RecordingHapticsBackend();
            RuntimeFixture fixture = Build(new UserSettings(1f, 1f, 1f, false), backend);

            Assert.That(fixture.Haptics.TryPlay(NeonGridHapticEvent.TileRotate, 1f), Is.False);
            Assert.That(backend.PulseCount, Is.Zero);
            Assert.That(fixture.Settings.SetHapticsEnabled(true), Is.True);
            Assert.That(fixture.Haptics.TryPlay(NeonGridHapticEvent.TileRotate, 2f), Is.True);
            Assert.That(backend.PulseCount, Is.EqualTo(1));
        }

        [Test]
        public void LiveMutations_ReuseServicesAndBoundedSourceHierarchy()
        {
            RuntimeFixture fixture = Build(UserSettings.CreateDefault());
            int audioId = fixture.Audio.GetInstanceID();
            int hapticsId = fixture.Haptics.GetInstanceID();
            int[] sourceIds = fixture.Audio.GetComponentsInChildren<AudioSource>(true)
                .Select(source => source.GetInstanceID()).ToArray();

            fixture.Settings.SetMasterVolume(.5f);
            fixture.Settings.SetSfxVolume(.6f);
            fixture.Settings.SetAmbienceVolume(.7f);
            fixture.Settings.SetHapticsEnabled(false);

            Assert.That(fixture.Settings.AudioService.GetInstanceID(), Is.EqualTo(audioId));
            Assert.That(fixture.Settings.HapticsService.GetInstanceID(), Is.EqualTo(hapticsId));
            Assert.That(fixture.Audio.GetComponentsInChildren<AudioSource>(true)
                .Select(source => source.GetInstanceID()), Is.EquivalentTo(sourceIds));
            Assert.That(fixture.Audio.VoiceCount, Is.EqualTo(6));
            Assert.That(fixture.Audio.AmbienceVoiceCount, Is.EqualTo(2));
        }

        [Test]
        public void SameValueUpdates_AreStableNoOps()
        {
            RuntimeFixture fixture = Build(new UserSettings(.5f, .6f, .7f, false));
            fixture.Audio.RequestAmbience(NeonGridAmbienceMode.City);
            int playbackStarts = fixture.Audio.AmbiencePlaybackStartCount;

            Assert.That(fixture.Settings.SetMasterVolume(.5f), Is.False);
            Assert.That(fixture.Settings.SetSfxVolume(.6f), Is.False);
            Assert.That(fixture.Settings.SetAmbienceVolume(.7f), Is.False);
            Assert.That(fixture.Settings.SetHapticsEnabled(false), Is.False);
            Assert.That(fixture.Audio.AmbiencePlaybackStartCount, Is.EqualTo(playbackStarts));
            Assert.That(fixture.Audio.RequestedAmbienceMode,
                Is.EqualTo(NeonGridAmbienceMode.City));
        }

        [Test]
        public void MasterMute_PreservesActiveAmbienceLoopAndPlaybackPosition()
        {
            RuntimeFixture fixture = Build(UserSettings.CreateDefault());
            fixture.Audio.RequestAmbience(NeonGridAmbienceMode.City);
            fixture.Audio.AdvanceAmbience(feedback.AudioDefinition.AmbienceFadeDuration);
            AudioSource active = ActiveAmbience(fixture.Audio);
            int sourceId = active.GetInstanceID();
            int playbackStarts = fixture.Audio.AmbiencePlaybackStartCount;

            fixture.Settings.SetMasterVolume(0f);
            Assert.That(active.volume, Is.Zero);
            Assert.That(active.clip, Is.EqualTo(feedback.AudioDefinition.CityAmbience));
            Assert.That(fixture.Audio.AmbiencePlaybackStartCount, Is.EqualTo(playbackStarts));
            fixture.Settings.SetMasterVolume(.5f);

            Assert.That(ActiveAmbience(fixture.Audio).GetInstanceID(), Is.EqualTo(sourceId));
            Assert.That(active.volume, Is.EqualTo(.18f * .5f).Within(.0001f));
            Assert.That(fixture.Audio.AmbiencePlaybackStartCount, Is.EqualTo(playbackStarts));
        }

        [Test]
        public void LiveVolumeChange_ComposesWithActiveCrossfadeWithoutRestartingIt()
        {
            RuntimeFixture fixture = Build(UserSettings.CreateDefault());
            float duration = feedback.AudioDefinition.AmbienceFadeDuration;
            fixture.Audio.RequestAmbience(NeonGridAmbienceMode.City);
            fixture.Audio.AdvanceAmbience(duration * .5f);
            AudioSource active = ActiveAmbience(fixture.Audio);
            float halfEnvelopeVolume = active.volume;
            int playbackStarts = fixture.Audio.AmbiencePlaybackStartCount;

            fixture.Settings.SetAmbienceVolume(.5f);

            Assert.That(fixture.Audio.IsAmbienceTransitioning, Is.True);
            Assert.That(active.volume, Is.EqualTo(halfEnvelopeVolume * .5f).Within(.0001f));
            Assert.That(fixture.Audio.AmbiencePlaybackStartCount, Is.EqualTo(playbackStarts));
            fixture.Audio.AdvanceAmbience(duration * .5f);
            Assert.That(fixture.Audio.IsAmbienceTransitioning, Is.False);
            Assert.That(active.volume, Is.EqualTo(.18f * .5f).Within(.0001f));
        }

        [Test]
        public void ExplicitPersistence_RoundTripsCurrentRuntimeState()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NeonGridM17A2Tests",
                Guid.NewGuid().ToString("N"));
            string path = UserSettingsStore.BuildSettingsPath(directory);
            var store = new UserSettingsStore(path);
            try
            {
                var settings = new RuntimeUserSettingsController(store);
                settings.SetMasterVolume(.5f);
                settings.SetSfxVolume(.8f);
                settings.SetAmbienceVolume(.4f);
                settings.SetHapticsEnabled(false);

                Assert.That(settings.PersistCurrentSettings().Succeeded, Is.True);
                UserSettings loaded = store.Load().Settings;
                Assert.That(loaded.MasterVolume, Is.EqualTo(.5f));
                Assert.That(loaded.SfxVolume, Is.EqualTo(.8f));
                Assert.That(loaded.AmbienceVolume, Is.EqualTo(.4f));
                Assert.That(loaded.HapticsEnabled, Is.False);
            }
            finally
            {
                store.Delete();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PersistenceFailure_DoesNotRollBackRuntimeState()
        {
            var store = new MemorySettingsStore(UserSettings.CreateDefault(), failSave: true);
            RuntimeFixture fixture = BuildWithStore(store);
            fixture.Settings.SetMasterVolume(.35f);
            fixture.Settings.SetHapticsEnabled(false);

            UserSettingsSaveResult result = fixture.Settings.PersistCurrentSettings();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(fixture.Settings.CurrentSettings.MasterVolume, Is.EqualTo(.35f));
            Assert.That(fixture.Settings.CurrentSettings.HapticsEnabled, Is.False);
            Assert.That(fixture.Audio.UserMasterVolume, Is.EqualTo(.35f));
            Assert.That(fixture.Haptics.HapticsEnabled, Is.False);
        }

        private RuntimeFixture Build(UserSettings settings,
            INeonGridHapticsBackend backend = null)
        {
            return BuildWithStore(new MemorySettingsStore(settings), backend);
        }

        private RuntimeFixture BuildWithStore(IUserSettingsStore store,
            INeonGridHapticsBackend backend = null)
        {
            var root = new GameObject("M17 A2 Runtime Settings Fixture");
            cleanup.Add(root);
            NeonGridAudioService audio = root.AddComponent<NeonGridAudioService>();
            audio.Initialize(feedback.AudioDefinition);
            NeonGridHapticsService haptics = root.AddComponent<NeonGridHapticsService>();
            haptics.Initialize(feedback.HapticsDefinition, null,
                backend ?? new RecordingHapticsBackend());
            var settings = new RuntimeUserSettingsController(store);
            settings.AttachServices(audio, haptics);
            return new RuntimeFixture(settings, audio, haptics);
        }

        private float AuthoredSfxGain(NeonGridAudioEvent eventType)
        {
            Assert.That(feedback.AudioDefinition.TryGetCue(eventType, out NeonGridAudioCue cue),
                Is.True);
            return cue.Gain * feedback.AudioDefinition.MasterSfxMultiplier *
                   feedback.AudioDefinition.SfxVolume;
        }

        private static AudioSource ActiveAmbience(NeonGridAudioService audio)
        {
            return audio.GetComponentsInChildren<AudioSource>(true)
                .Single(source => source.loop && source.clip != null);
        }

        private readonly struct RuntimeFixture
        {
            public RuntimeUserSettingsController Settings { get; }
            public NeonGridAudioService Audio { get; }
            public NeonGridHapticsService Haptics { get; }

            public RuntimeFixture(RuntimeUserSettingsController settings,
                NeonGridAudioService audio, NeonGridHapticsService haptics)
            {
                Settings = settings;
                Audio = audio;
                Haptics = haptics;
            }
        }

        private sealed class RecordingHapticsBackend : INeonGridHapticsBackend
        {
            public bool IsSupported => true;
            public int PulseCount { get; private set; }

            public bool TryPulse(int durationMilliseconds, float intensity)
            {
                PulseCount++;
                return true;
            }
        }

        private sealed class MemorySettingsStore : IUserSettingsStore
        {
            private UserSettings settings;
            private readonly bool failSave;

            public string SettingsPath => "memory://m17-a2-settings";

            public MemorySettingsStore(UserSettings initial, bool failSave = false)
            {
                settings = initial.CopySanitized();
                this.failSave = failSave;
            }

            public UserSettingsLoadResult Load()
            {
                return new UserSettingsLoadResult(UserSettingsLoadStatus.Loaded,
                    settings.CopySanitized(), string.Empty);
            }

            public UserSettingsSaveResult Save(UserSettings value)
            {
                if (failSave)
                    return new UserSettingsSaveResult(UserSettingsSaveStatus.Failed,
                        "Simulated settings save failure.");
                settings = value.CopySanitized();
                return new UserSettingsSaveResult(UserSettingsSaveStatus.Saved, SettingsPath);
            }

            public UserSettingsSaveResult Delete()
            {
                settings = UserSettings.CreateDefault();
                return new UserSettingsSaveResult(UserSettingsSaveStatus.Saved, SettingsPath);
            }
        }
    }
}

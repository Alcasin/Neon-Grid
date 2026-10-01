using System;
using System.Collections;
using System.IO;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NeonGrid.Tests
{
    public sealed class M17ProductionSettingsPlayModeTests
    {
        [UnityTest]
        public IEnumerator ProductionSettings_PreserveMapAmbienceServicesAndPersistOnce()
        {
            NeonGridHapticsSettings.HapticsEnabled = true;
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            string testRoot = Path.Combine(Application.temporaryCachePath, "NeonGrid", "Tests",
                "M17B1", Guid.NewGuid().ToString("N"));
            var campaignStore = new CampaignSaveStore(Path.Combine(testRoot,
                CampaignSaveStore.SaveFileName));
            var settingsFileStore = new UserSettingsStore(Path.Combine(testRoot,
                UserSettingsStore.SettingsFileName));
            Assert.That(settingsFileStore.Save(new UserSettings(.8f, .7f, .6f, true)).Succeeded,
                Is.True);
            var settingsStore = new CountingSettingsStore(settingsFileStore);
            var progress = new CampaignProgressService(campaign);
            var flow = new CampaignFlowCoordinator(campaign, progress, campaignStore);
            Assert.That(flow.TryCompleteIntro(), Is.True);

            var cameraObject = new GameObject("M17 B1 Runtime Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>().orthographic = true;
            var runtimeObject = new GameObject("M17 B1 Production Runtime");
            LogAssert.Expect(LogType.Error,
                "CampaignRuntimeController requires a CampaignDefinition.");
            CampaignRuntimeController runtime =
                runtimeObject.AddComponent<CampaignRuntimeController>();
            try
            {
                runtime.Initialize(campaign, campaignStore, settingsStore);
                CampaignRuntimeView campaignView = runtime.CampaignView;
                ProductionSettingsView settingsView = campaignView.SettingsView;
                RuntimeUserSettingsController settings = runtime.RuntimeSettings;
                NeonGridAudioService audio = runtime.GameplayFeedback.AudioService;
                NeonGridHapticsService haptics = runtime.GameplayFeedback.HapticsService;
                var hapticsBackend = new RecordingHapticsBackend();
                haptics.Initialize(haptics.Definition, null, hapticsBackend);
                settings.AttachServices(audio, haptics);
                audio.AdvanceAmbience(audio.Definition.AmbienceFadeDuration);
                AudioSource city = ActiveAmbience(audio);
                AudioClip cityClip = city.clip;
                int citySourceId = city.GetInstanceID();
                int[] sourceIds = runtimeObject.GetComponentsInChildren<AudioSource>(true)
                    .Select(source => source.GetInstanceID()).ToArray();
                int audioId = audio.GetInstanceID();
                int hapticsId = haptics.GetInstanceID();
                int starts = audio.AmbiencePlaybackStartCount;
                GameObject modal = Find(runtimeObject.transform, "Settings Modal").gameObject;
                int modalId = modal.GetInstanceID();
                Button entryButton = Find(runtimeObject.transform, "Settings Button")
                    .GetComponent<Button>();
                Button closeButton = Find(runtimeObject.transform, "Close Button")
                    .GetComponent<Button>();
                Slider masterSlider = Find(runtimeObject.transform, "Master Slider")
                    .GetComponent<Slider>();
                Slider sfxSlider = Find(runtimeObject.transform, "SFX Slider")
                    .GetComponent<Slider>();
                Slider ambienceSlider = Find(runtimeObject.transform, "Ambience Slider")
                    .GetComponent<Slider>();
                Toggle hapticsToggle = Find(runtimeObject.transform, "Haptics Toggle")
                    .GetComponent<Toggle>();

                Assert.That(campaignView.IsVisible, Is.True);
                Assert.That(settingsView.IsEntryVisible, Is.True);
                Assert.That(audio.RequestedAmbienceMode, Is.EqualTo(NeonGridAmbienceMode.City));
                entryButton.onClick.Invoke();
                Assert.That(settingsView.IsOpen, Is.True);
                Assert.That(audio.GetInstanceID(), Is.EqualTo(audioId));
                Assert.That(city.GetInstanceID(), Is.EqualTo(citySourceId));
                Assert.That(audio.AmbiencePlaybackStartCount, Is.EqualTo(starts));

                masterSlider.value = 0f;
                Assert.That(city.volume, Is.Zero);
                Assert.That(city.isPlaying, Is.True);
                Assert.That(city.clip, Is.SameAs(cityClip));
                Assert.That(audio.AmbiencePlaybackStartCount, Is.EqualTo(starts));
                masterSlider.value = .5f;
                Assert.That(city.volume, Is.EqualTo(.18f * .5f * .6f).Within(.0001f));

                ambienceSlider.value = .25f;
                float ambienceVolume = city.volume;
                Assert.That(ambienceVolume, Is.EqualTo(.18f * .5f * .25f).Within(.0001f));
                sfxSlider.value = .2f;
                Assert.That(city.volume, Is.EqualTo(ambienceVolume).Within(.0001f));
                Assert.That(audio.AmbiencePlaybackStartCount, Is.EqualTo(starts));

                hapticsToggle.isOn = false;
                Assert.That(haptics.TryPlay(NeonGridHapticEvent.TileRotate, 1f), Is.False);
                Assert.That(hapticsBackend.PulseCount, Is.Zero);
                hapticsToggle.isOn = true;
                Assert.That(haptics.TryPlay(NeonGridHapticEvent.TileRotate, 2f), Is.True);
                Assert.That(hapticsBackend.PulseCount, Is.EqualTo(1));

                closeButton.onClick.Invoke();
                Assert.That(settingsStore.SaveCount, Is.EqualTo(1));
                Assert.That(campaignView.IsVisible, Is.True);
                Assert.That(campaignView.IsMapInteractionEnabled, Is.True);
                Assert.That(audio.AmbiencePlaybackStartCount, Is.EqualTo(starts));
                Assert.That(city.GetInstanceID(), Is.EqualTo(citySourceId));

                entryButton.onClick.Invoke();
                Assert.That(modal.GetInstanceID(), Is.EqualTo(modalId));
                Assert.That(masterSlider.value, Is.EqualTo(.5f));
                Assert.That(sfxSlider.value, Is.EqualTo(.2f));
                Assert.That(ambienceSlider.value, Is.EqualTo(.25f));
                Assert.That(runtimeObject.GetComponents<ProductionSettingsView>(),
                    Has.Length.EqualTo(1));
                Assert.That(runtimeObject.GetComponents<NeonGridAudioService>(),
                    Has.Length.EqualTo(1));
                Assert.That(runtimeObject.GetComponents<NeonGridHapticsService>(),
                    Has.Length.EqualTo(1));
                Assert.That(haptics.GetInstanceID(), Is.EqualTo(hapticsId));
                Assert.That(runtimeObject.GetComponentsInChildren<AudioSource>(true),
                    Has.Length.EqualTo(8));
                Assert.That(runtimeObject.GetComponentsInChildren<AudioSource>(true)
                    .Select(source => source.GetInstanceID()), Is.EquivalentTo(sourceIds));
                Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None), Has.Length.EqualTo(1));
            }
            finally
            {
                NeonGridHapticsSettings.HapticsEnabled = true;
                campaignStore.Delete();
                settingsFileStore.Delete();
                Object.Destroy(runtimeObject);
                Object.Destroy(cameraObject);
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            }
            yield return null;
        }

        private static AudioSource ActiveAmbience(NeonGridAudioService audio)
        {
            return audio.GetComponentsInChildren<AudioSource>(true)
                .Single(source => source.loop && source.clip != null && source.isPlaying);
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Single(candidate => candidate.name == name);
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

        private sealed class CountingSettingsStore : IUserSettingsStore
        {
            private readonly UserSettingsStore inner;

            public string SettingsPath => inner.SettingsPath;
            public int SaveCount { get; private set; }

            public CountingSettingsStore(UserSettingsStore store)
            {
                inner = store;
            }

            public UserSettingsLoadResult Load()
            {
                return inner.Load();
            }

            public UserSettingsSaveResult Save(UserSettings settings)
            {
                SaveCount++;
                return inner.Save(settings);
            }

            public UserSettingsSaveResult Delete()
            {
                return inner.Delete();
            }
        }
    }
}

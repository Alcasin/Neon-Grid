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
using Object = UnityEngine.Object;

namespace NeonGrid.Tests
{
    public sealed class M17RuntimeSettingsPlayModeTests
    {
        [UnityTest]
        public IEnumerator PersistedSettingsApplyBeforeCityPlaybackAndSurviveTransitions()
        {
            NeonGridHapticsSettings.HapticsEnabled = true;
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            string testRoot = Path.Combine(Application.temporaryCachePath, "NeonGrid", "Tests",
                "M17A2", Guid.NewGuid().ToString("N"));
            var campaignStore = new CampaignSaveStore(Path.Combine(testRoot,
                CampaignSaveStore.SaveFileName));
            var settingsStore = new UserSettingsStore(Path.Combine(testRoot,
                UserSettingsStore.SettingsFileName));
            Assert.That(settingsStore.Save(new UserSettings(.5f, .8f, .4f, false)).Succeeded,
                Is.True);
            var progress = new CampaignProgressService(campaign);
            var flow = new CampaignFlowCoordinator(campaign, progress, campaignStore);
            Assert.That(flow.TryCompleteIntro(), Is.True);

            var cameraObject = new GameObject("M17 A2 Runtime Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>().orthographic = true;
            var runtimeObject = new GameObject("M17 A2 Production Runtime");
            LogAssert.Expect(LogType.Error,
                "CampaignRuntimeController requires a CampaignDefinition.");
            CampaignRuntimeController runtime =
                runtimeObject.AddComponent<CampaignRuntimeController>();
            try
            {
                runtime.Initialize(campaign, campaignStore, settingsStore);
                RuntimeUserSettingsController settings = runtime.RuntimeSettings;
                NeonGridAudioService audio = runtime.GameplayFeedback.AudioService;
                NeonGridHapticsService haptics = runtime.GameplayFeedback.HapticsService;
                UserSettings loaded = settings.CurrentSettings;
                AudioSource[] initialSources = runtimeObject
                    .GetComponentsInChildren<AudioSource>(true);
                int[] sourceIds = initialSources.Select(source => source.GetInstanceID()).ToArray();

                Assert.That(loaded.MasterVolume, Is.EqualTo(.5f));
                Assert.That(loaded.SfxVolume, Is.EqualTo(.8f));
                Assert.That(loaded.AmbienceVolume, Is.EqualTo(.4f));
                Assert.That(loaded.HapticsEnabled, Is.False);
                Assert.That(audio.UserMasterVolume, Is.EqualTo(.5f));
                Assert.That(audio.UserSfxVolume, Is.EqualTo(.8f));
                Assert.That(audio.UserAmbienceVolume, Is.EqualTo(.4f));
                Assert.That(haptics.HapticsEnabled, Is.False);
                Assert.That(audio.RequestedAmbienceMode, Is.EqualTo(NeonGridAmbienceMode.City));
                Assert.That(AmbienceSources(audio).Single(source => source.clip != null).volume,
                    Is.Zero, "Persisted multipliers must apply before the first audible frame.");

                audio.AdvanceAmbience(audio.Definition.AmbienceFadeDuration);
                AudioSource city = ActiveAmbience(audio);
                Assert.That(city.volume, Is.EqualTo(.18f * .5f * .4f).Within(.0001f));

                Assert.That(runtime.OpenChapter("power_station"), Is.True);
                Assert.That(runtime.StartLevel("power_01"), Is.True);
                audio.AdvanceAmbience(audio.Definition.AmbienceFadeDuration);
                Assert.That(audio.RequestedAmbienceMode,
                    Is.EqualTo(NeonGridAmbienceMode.Gameplay));
                Assert.That(ActiveAmbience(audio).volume,
                    Is.EqualTo(.153f * .5f * .4f).Within(.0001f));

                runtime.ShowMap();
                audio.AdvanceAmbience(audio.Definition.AmbienceFadeDuration);
                city = ActiveAmbience(audio);
                Assert.That(audio.RequestedAmbienceMode, Is.EqualTo(NeonGridAmbienceMode.City));
                Assert.That(city.volume, Is.EqualTo(.18f * .5f * .4f).Within(.0001f));

                int startsBeforeMute = audio.AmbiencePlaybackStartCount;
                int activeSourceId = city.GetInstanceID();
                AudioClip activeClip = city.clip;
                int timeSamplesBeforeMute = city.timeSamples;
                Assert.That(settings.SetMasterVolume(0f), Is.True);
                Assert.That(city.volume, Is.Zero);
                Assert.That(city.isPlaying, Is.True);
                Assert.That(city.clip, Is.SameAs(activeClip));
                Assert.That(audio.AmbiencePlaybackStartCount, Is.EqualTo(startsBeforeMute));

                Assert.That(settings.SetMasterVolume(.5f), Is.True);
                Assert.That(ActiveAmbience(audio).GetInstanceID(), Is.EqualTo(activeSourceId));
                Assert.That(city.isPlaying, Is.True);
                Assert.That(city.volume, Is.EqualTo(.18f * .5f * .4f).Within(.0001f));
                Assert.That(audio.AmbiencePlaybackStartCount, Is.EqualTo(startsBeforeMute));
                Assert.That(city.timeSamples, Is.GreaterThanOrEqualTo(timeSamplesBeforeMute));

                Assert.That(settings.SetHapticsEnabled(true), Is.True);
                Assert.That(haptics.HapticsEnabled, Is.True);
                Assert.That(settings.SetHapticsEnabled(false), Is.True);
                Assert.That(haptics.HapticsEnabled, Is.False);

                Assert.That(runtimeObject.GetComponents<NeonGridAudioService>(),
                    Has.Length.EqualTo(1));
                Assert.That(runtimeObject.GetComponents<NeonGridHapticsService>(),
                    Has.Length.EqualTo(1));
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
                settingsStore.Delete();
                Object.Destroy(runtimeObject);
                Object.Destroy(cameraObject);
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            }
            yield return null;
        }

        private static AudioSource[] AmbienceSources(NeonGridAudioService audio)
        {
            return audio.GetComponentsInChildren<AudioSource>(true)
                .Where(source => source.loop).ToArray();
        }

        private static AudioSource ActiveAmbience(NeonGridAudioService audio)
        {
            return AmbienceSources(audio).Single(source => source.clip != null && source.isPlaying);
        }
    }
}

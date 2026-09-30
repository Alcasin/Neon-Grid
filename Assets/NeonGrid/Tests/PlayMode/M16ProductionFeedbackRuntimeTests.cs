using System;
using System.Collections;
using System.IO;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NeonGrid.Tests
{
    public sealed class M16ProductionFeedbackRuntimeTests
    {
        [UnityTest]
        public IEnumerator ProductionSessionStartsGameplayFeedbackAndStopsCleanly()
        {
            NeonGridHapticsSettings.HapticsEnabled = true;
            ProductionGameplayFeedbackDefinition definition =
                Resources.Load<ProductionGameplayFeedbackDefinition>(
                    "GameplayFeedback/M16_ProductionGameplayFeedback");
            LevelDefinition level = Resources.Load<LevelDefinition>(
                "Levels/PowerStation/PS_01");
            CircuitVisualThemeDefinition theme =
                CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype();
            var cameraObject = new GameObject("M16 D1 Runtime Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            var feedbackObject = new GameObject("M16 D1 Production Feedback");
            ProductionGameplayFeedbackController feedback =
                feedbackObject.AddComponent<ProductionGameplayFeedbackController>();
            var boardObject = new GameObject("M16 D1 Production Board");
            BoardController board = boardObject.AddComponent<BoardController>();
            try
            {
                Assert.That(definition, Is.Not.Null);
                feedback.Initialize(definition, camera);
                board.Initialize(level, theme, definition.CircuitJuice);
                feedback.EnterGameplay(board);
                yield return null;

                Assert.That(feedback.AudioService.RequestedAmbienceMode,
                    Is.EqualTo(NeonGridAmbienceMode.Gameplay));
                Assert.That(feedback.AudioService.VoiceCount, Is.EqualTo(6));
                Assert.That(feedback.AudioService.AmbienceVoiceCount, Is.EqualTo(2));
                Assert.That(feedback.GetComponentsInChildren<AudioSource>(true),
                    Has.Length.EqualTo(8));
                Assert.That(board.PerformPlayerAction(new GridPosition(1, 0)), Is.True);
                Assert.That(feedback.AudioService.SuccessfulPlaybackCount, Is.GreaterThan(0));

                board.Restart();
                Assert.That(board.Session.MoveCount, Is.Zero);
                feedback.HapticsService.SetHapticsEnabled(false);
                Assert.That(feedback.HapticsService.HapticsEnabled, Is.False);
                feedback.HapticsService.SetHapticsEnabled(true);
                feedback.ExitGameplay();
                Assert.That(feedback.AudioService.RequestedAmbienceMode,
                    Is.EqualTo(NeonGridAmbienceMode.None));
                yield return new WaitForSecondsRealtime(
                    definition.AudioDefinition.AmbienceFadeDuration + .05f);
                Assert.That(feedback.GetComponentsInChildren<AudioSource>(true)
                    .Where(source => source.loop).All(source =>
                        !source.isPlaying && source.clip == null && source.volume == 0f), Is.True);
            }
            finally
            {
                NeonGridHapticsSettings.HapticsEnabled = true;
                Object.Destroy(boardObject);
                Object.Destroy(feedbackObject);
                Object.Destroy(cameraObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedProductionBoardsReuseOneBoundedServiceHierarchy()
        {
            ProductionGameplayFeedbackDefinition definition =
                Resources.Load<ProductionGameplayFeedbackDefinition>(
                    "GameplayFeedback/M16_ProductionGameplayFeedback");
            LevelDefinition level = Resources.Load<LevelDefinition>(
                "Levels/PowerStation/PS_01");
            CircuitVisualThemeDefinition theme =
                CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype();
            var cameraObject = new GameObject("M16 D1 Reentry Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            var feedbackObject = new GameObject("M16 D1 Reentry Feedback");
            ProductionGameplayFeedbackController feedback =
                feedbackObject.AddComponent<ProductionGameplayFeedbackController>();
            try
            {
                feedback.Initialize(definition, camera);
                int hierarchyCount = feedbackObject.GetComponentsInChildren<Transform>(true).Length;
                for (int index = 0; index < 3; index++)
                {
                    var boardObject = new GameObject("M16 D1 Reentry Board " + index);
                    BoardController board = boardObject.AddComponent<BoardController>();
                    board.Initialize(level, theme, definition.CircuitJuice);
                    feedback.EnterGameplay(board);
                    Assert.That(feedback.ActiveBoard, Is.SameAs(board));
                    feedback.ExitGameplay();
                    Object.Destroy(boardObject);
                    yield return null;
                    Assert.That(feedbackObject.GetComponentsInChildren<Transform>(true),
                        Has.Length.EqualTo(hierarchyCount));
                    Assert.That(feedbackObject.GetComponentsInChildren<AudioSource>(true),
                        Has.Length.EqualTo(8));
                    Assert.That(feedbackObject.GetComponents<NeonGridAudioService>(),
                        Has.Length.EqualTo(1));
                    Assert.That(feedbackObject.GetComponents<NeonGridHapticsService>(),
                        Has.Length.EqualTo(1));
                }
            }
            finally
            {
                Object.Destroy(feedbackObject);
                Object.Destroy(cameraObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionMapGameplayMapTransitionsCrossfadeWithoutSourceGrowth()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            var progress = new CampaignProgressService(campaign);
            string savePath = Path.Combine(Application.temporaryCachePath,
                "NeonGrid", "Tests", "M16D2", Guid.NewGuid().ToString("N"),
                CampaignSaveStore.SaveFileName);
            var store = new CampaignSaveStore(savePath);
            var flow = new CampaignFlowCoordinator(campaign, progress, store);
            Assert.That(flow.TryCompleteIntro(), Is.True);
            var cameraObject = new GameObject("M16 D2 Runtime Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>().orthographic = true;
            var runtimeObject = new GameObject("M16 D2 Production Runtime");
            LogAssert.Expect(LogType.Error,
                "CampaignRuntimeController requires a CampaignDefinition.");
            CampaignRuntimeController runtime =
                runtimeObject.AddComponent<CampaignRuntimeController>();
            try
            {
                runtime.Initialize(campaign, store);
                NeonGridAudioService audio = runtime.GameplayFeedback.AudioService;
                AudioSource[] initialSources = runtimeObject
                    .GetComponentsInChildren<AudioSource>(true);
                int[] sourceIds = initialSources.Select(source => source.GetInstanceID()).ToArray();
                float fadeWait = audio.Definition.AmbienceFadeDuration + .05f;

                Assert.That(audio.RequestedAmbienceMode, Is.EqualTo(NeonGridAmbienceMode.City));
                yield return new WaitForSecondsRealtime(fadeWait);
                AssertSingleAmbience(audio, audio.Definition.CityAmbience);

                Assert.That(runtime.OpenChapter("power_station"), Is.True);
                Assert.That(runtime.StartLevel("power_01"), Is.True);
                Assert.That(audio.RequestedAmbienceMode,
                    Is.EqualTo(NeonGridAmbienceMode.Gameplay));
                yield return new WaitForSecondsRealtime(fadeWait);
                AssertSingleAmbience(audio, audio.Definition.GameplayAmbience);
                Assert.That(AmbienceSources(audio).Any(source =>
                    source.clip == audio.Definition.CityAmbience && source.isPlaying), Is.False);

                runtime.ShowMap();
                Assert.That(audio.RequestedAmbienceMode, Is.EqualTo(NeonGridAmbienceMode.City));
                yield return new WaitForSecondsRealtime(fadeWait);
                AssertSingleAmbience(audio, audio.Definition.CityAmbience);
                Assert.That(AmbienceSources(audio).Any(source =>
                    source.clip == audio.Definition.GameplayAmbience && source.isPlaying), Is.False);

                Assert.That(runtime.OpenChapter("power_station"), Is.True);
                Assert.That(runtime.StartLevel("power_01"), Is.True);
                yield return new WaitForSecondsRealtime(fadeWait);
                runtime.ShowMap();
                yield return new WaitForSecondsRealtime(fadeWait);
                AssertSingleAmbience(audio, audio.Definition.CityAmbience);

                Assert.That(runtimeObject.GetComponentsInChildren<AudioSource>(true)
                        .Select(source => source.GetInstanceID()), Is.EquivalentTo(sourceIds));
                Assert.That(runtimeObject.GetComponents<NeonGridAudioService>(),
                    Has.Length.EqualTo(1));
                Assert.That(runtimeObject.GetComponents<NeonGridHapticsService>(),
                    Has.Length.EqualTo(1));
                Assert.That(runtimeObject.GetComponentsInChildren<AudioSource>(true),
                    Has.Length.EqualTo(8));
            }
            finally
            {
                NeonGridHapticsSettings.HapticsEnabled = true;
                store.Delete();
                Object.Destroy(runtimeObject);
                Object.Destroy(cameraObject);
            }
            yield return null;
        }

        private static AudioSource[] AmbienceSources(NeonGridAudioService audio) =>
            audio.GetComponentsInChildren<AudioSource>(true)
                .Where(source => source.loop).ToArray();

        private static void AssertSingleAmbience(NeonGridAudioService audio,
            AudioClip expected)
        {
            AudioSource[] ambience = AmbienceSources(audio);
            Assert.That(ambience, Has.Length.EqualTo(2));
            Assert.That(ambience.Count(source => source.isPlaying), Is.EqualTo(1));
            Assert.That(ambience.Count(source => source.isPlaying && source.clip == expected),
                Is.EqualTo(1));
        }

    }
}

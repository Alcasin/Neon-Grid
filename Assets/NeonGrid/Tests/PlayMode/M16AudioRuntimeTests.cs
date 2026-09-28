using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NeonGrid.Tests
{
    public sealed class M16AudioRuntimeTests
    {
        [UnityTest]
        public IEnumerator RepeatedPlaybackSurvivesClipCompletionAndServiceRemainsUsable()
        {
            NeonGridAudioDefinition definition = Resources.Load<NeonGridAudioDefinition>(
                "AudioPrototypes/M16_AudioPrototype");
            Assert.That(definition, Is.Not.Null);
            GameObject listenerObject = null;
            if (Object.FindFirstObjectByType<AudioListener>() == null)
            {
                listenerObject = new GameObject("M16 B1 Runtime Listener");
                listenerObject.AddComponent<AudioListener>();
            }
            var serviceObject = new GameObject("M16 B1 Runtime Audio Service");
            NeonGridAudioService service = serviceObject.AddComponent<NeonGridAudioService>();
            service.Initialize(definition);
            try
            {
                int initialHierarchySize =
                    service.GetComponentsInChildren<Transform>(true).Length;
                AudioSource[] initialSources =
                    service.GetComponentsInChildren<AudioSource>(true);
                AudioSource[] initialSfxSources =
                    initialSources.Where(source => !source.loop).ToArray();
                AudioSource[] initialAmbienceSources =
                    initialSources.Where(source => source.loop).ToArray();
                Assert.That(service.VoiceCount, Is.EqualTo(6));
                Assert.That(service.AmbienceVoiceCount, Is.EqualTo(2));
                Assert.That(initialSources, Has.Length.EqualTo(8));
                Assert.That(initialSfxSources, Has.Length.EqualTo(6));
                Assert.That(initialAmbienceSources, Has.Length.EqualTo(2));
                Assert.That(initialSfxSources.All(source => source.enabled &&
                    source.gameObject.activeInHierarchy && !source.mute &&
                    source.volume > 0f && !source.loop), Is.True);
                Assert.That(initialAmbienceSources.All(source => source.enabled &&
                    source.gameObject.activeInHierarchy && !source.mute && source.loop), Is.True);

                Assert.That(service.TryPlay(NeonGridAudioEvent.TileRotateAccepted), Is.True);
                int firstCount = service.SuccessfulPlaybackCount;
                yield return new WaitForSecondsRealtime(.15f);
                NeonGridAudioEvent[] rapidSequence =
                {
                    NeonGridAudioEvent.TileRotateAccepted,
                    NeonGridAudioEvent.InteractionRejected,
                    NeonGridAudioEvent.PowerActivated,
                    NeonGridAudioEvent.PowerDeactivated,
                    NeonGridAudioEvent.SwitchChanged,
                    NeonGridAudioEvent.GateActivated,
                    NeonGridAudioEvent.ObjectiveActivated,
                    NeonGridAudioEvent.HintActivated
                };
                var voiceIndices = new List<int>();
                foreach (NeonGridAudioEvent eventType in rapidSequence)
                {
                    Assert.That(service.TryPlay(eventType), Is.True, eventType.ToString());
                    voiceIndices.Add(service.LastPlayback.Value.VoiceIndex);
                }
                Assert.That(voiceIndices, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 0, 1, 2 }));
                Assert.That(service.SuccessfulPlaybackCount,
                    Is.EqualTo(firstCount + rapidSequence.Length));
                Assert.That(service.enabled, Is.True);
                Assert.That(service.gameObject.activeInHierarchy, Is.True);
                AudioSource[] sources = service.GetComponentsInChildren<AudioSource>(true);
                AudioSource[] sfxSources = sources.Where(source => !source.loop).ToArray();
                AudioSource[] ambienceSources = sources.Where(source => source.loop).ToArray();
                Assert.That(sources, Has.Length.EqualTo(8));
                Assert.That(sfxSources, Has.Length.EqualTo(definition.SourcePoolSize));
                Assert.That(ambienceSources, Has.Length.EqualTo(2));
                Assert.That(sfxSources.All(source =>
                    source.enabled && source.gameObject.activeInHierarchy && !source.mute &&
                    source.volume > 0f && !source.loop), Is.True);
                Assert.That(ambienceSources.All(source =>
                    source.enabled && source.gameObject.activeInHierarchy && !source.mute &&
                    source.loop), Is.True);
                yield return new WaitForSecondsRealtime(.30f);
                Assert.That(service.TryPlay(NeonGridAudioEvent.TileRotateAccepted), Is.True);
                Assert.That(service.LastPlayback.Value.VoiceIndex, Is.EqualTo(3));
                Assert.That(service.GetComponentsInChildren<Transform>(true),
                    Has.Length.EqualTo(initialHierarchySize));
                Assert.That(service.GetComponentsInChildren<AudioSource>(true)
                        .Select(source => source.GetInstanceID()),
                    Is.EquivalentTo(initialSources.Select(source => source.GetInstanceID())));
            }
            finally
            {
                Object.Destroy(serviceObject);
                if (listenerObject != null) Object.Destroy(listenerObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AmbienceModesCrossfadeWithoutStackingOrSourceLeakage()
        {
            NeonGridAudioDefinition definition = Resources.Load<NeonGridAudioDefinition>(
                "AudioPrototypes/M16_AudioPrototype");
            Assert.That(definition, Is.Not.Null);
            GameObject listenerObject = null;
            if (Object.FindFirstObjectByType<AudioListener>() == null)
            {
                listenerObject = new GameObject("M16 B2 Runtime Listener");
                listenerObject.AddComponent<AudioListener>();
            }
            var serviceObject = new GameObject("M16 B2 Runtime Ambience Service");
            NeonGridAudioService service = serviceObject.AddComponent<NeonGridAudioService>();
            service.Initialize(definition);
            service.Initialize(definition);
            try
            {
                Assert.That(service.AmbienceVoiceCount, Is.EqualTo(2));
                Assert.That(service.RequestAmbience(NeonGridAmbienceMode.Gameplay), Is.True);
                Assert.That(service.RequestAmbience(NeonGridAmbienceMode.Gameplay), Is.False);
                yield return new WaitForSecondsRealtime(definition.AmbienceFadeDuration + .05f);
                Assert.That(service.RequestAmbience(NeonGridAmbienceMode.City), Is.True);
                yield return new WaitForSecondsRealtime(definition.AmbienceFadeDuration + .05f);
                Assert.That(service.RequestAmbience(NeonGridAmbienceMode.None), Is.True);
                yield return new WaitForSecondsRealtime(definition.AmbienceFadeDuration + .05f);
                AudioSource[] sources = service.GetComponentsInChildren<AudioSource>(true);
                Assert.That(sources, Has.Length.EqualTo(definition.SourcePoolSize + 2));
                Assert.That(sources.Count(source => source.loop), Is.EqualTo(2));
                Assert.That(sources.Where(source => source.loop).All(source =>
                    source.clip == null && !source.isPlaying && source.volume == 0f), Is.True);
                Assert.That(service.AmbiencePlaybackStartCount, Is.EqualTo(2));
            }
            finally
            {
                Object.Destroy(serviceObject);
                if (listenerObject != null) Object.Destroy(listenerObject);
            }
            yield return null;
        }
    }
}

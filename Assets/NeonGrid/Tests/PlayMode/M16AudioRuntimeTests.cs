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
                Assert.That(sources, Has.Length.EqualTo(definition.SourcePoolSize));
                Assert.That(sources.All(source =>
                    source.enabled && source.gameObject.activeInHierarchy && !source.mute &&
                    source.volume > 0f), Is.True);
                yield return new WaitForSecondsRealtime(.30f);
                Assert.That(service.TryPlay(NeonGridAudioEvent.TileRotateAccepted), Is.True);
                Assert.That(service.LastPlayback.Value.VoiceIndex, Is.EqualTo(3));
                Assert.That(service.GetComponentsInChildren<Transform>(true),
                    Has.Length.EqualTo(definition.SourcePoolSize + 1));
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

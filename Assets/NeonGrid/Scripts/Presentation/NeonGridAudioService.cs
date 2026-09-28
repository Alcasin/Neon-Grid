using System;
using System.Collections.Generic;
using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public readonly struct NeonGridAudioPlayback
    {
        public NeonGridAudioEvent EventType { get; }
        public AudioClip Clip { get; }
        public float Gain { get; }
        public float Pitch { get; }
        public int VoiceIndex { get; }

        internal NeonGridAudioPlayback(NeonGridAudioEvent eventType, AudioClip clip,
            float gain, float pitch, int voiceIndex)
        {
            EventType = eventType;
            Clip = clip;
            Gain = gain;
            Pitch = pitch;
            VoiceIndex = voiceIndex;
        }
    }

    public sealed class NeonGridAudioService : MonoBehaviour
    {
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly Dictionary<NeonGridAudioEvent, float> lastPlaybackTimes =
            new Dictionary<NeonGridAudioEvent, float>();
        private readonly Dictionary<NeonGridAudioEvent, int> playCounts =
            new Dictionary<NeonGridAudioEvent, int>();
        private CircuitJuiceCoordinator coordinator;
        private int reuseCursor;

        public NeonGridAudioDefinition Definition { get; private set; }
        public int VoiceCount => voices.Count;
        public int SuccessfulPlaybackCount { get; private set; }
        public NeonGridAudioPlayback? LastPlayback { get; private set; }

        public void Initialize(NeonGridAudioDefinition definition,
            CircuitJuiceCoordinator eventSource = null)
        {
            Definition = definition != null && definition.IsConfigured ? definition : null;
            if (Definition == null) return;
            for (int index = 0; index < Definition.SourcePoolSize; index++)
            {
                var voiceObject = new GameObject($"SFX Voice {index + 1}");
                voiceObject.transform.SetParent(transform, false);
                AudioSource source = voiceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.volume = 1f;
                voices.Add(source);
            }
            Bind(eventSource);
        }

        public void Bind(CircuitJuiceCoordinator eventSource)
        {
            if (coordinator != null)
                coordinator.PresentationEvent -= HandlePresentationEvent;
            coordinator = eventSource;
            if (coordinator != null)
                coordinator.PresentationEvent += HandlePresentationEvent;
        }

        public bool TryResolveCue(NeonGridAudioEvent eventType,
            out NeonGridAudioCue cue)
        {
            if (Definition == null)
            {
                cue = null;
                return false;
            }
            return Definition.TryGetCue(eventType, out cue);
        }

        public bool TryPlay(NeonGridAudioEvent eventType)
        {
            return TryPlay(eventType, Time.unscaledTime);
        }

        public bool TryPlay(NeonGridAudioEvent eventType, float timestamp)
        {
            if (!TryResolveCue(eventType, out NeonGridAudioCue cue) || voices.Count == 0)
                return false;
            if (lastPlaybackTimes.TryGetValue(eventType, out float previous) &&
                timestamp - previous < cue.CooldownSeconds)
                return false;

            int voiceIndex = SelectVoice();
            int playCount = playCounts.TryGetValue(eventType, out int current)
                ? current
                : 0;
            float pitch = DeterministicPitch(cue, playCount);
            float gain = cue.Gain * Definition.MasterSfxMultiplier * Definition.SfxVolume;
            AudioSource voice = voices[voiceIndex];
            voice.pitch = pitch;
            voice.PlayOneShot(cue.Clip, gain);
            lastPlaybackTimes[eventType] = timestamp;
            playCounts[eventType] = playCount + 1;
            SuccessfulPlaybackCount++;
            LastPlayback = new NeonGridAudioPlayback(eventType, cue.Clip, gain, pitch,
                voiceIndex);
            return true;
        }

        public void StopAll()
        {
            foreach (AudioSource voice in voices) voice.Stop();
            lastPlaybackTimes.Clear();
            LastPlayback = null;
        }

        private void OnDestroy()
        {
            if (coordinator != null)
                coordinator.PresentationEvent -= HandlePresentationEvent;
        }

        private void HandlePresentationEvent(CircuitJuiceEventType eventType)
        {
            if (TryMap(eventType, out NeonGridAudioEvent audioEvent))
                TryPlay(audioEvent);
        }

        private int SelectVoice()
        {
            int start = reuseCursor % voices.Count;
            for (int offset = 0; offset < voices.Count; offset++)
            {
                int index = (start + offset) % voices.Count;
                if (voices[index].isPlaying) continue;
                reuseCursor = (index + 1) % voices.Count;
                return index;
            }
            int selected = start;
            reuseCursor = (reuseCursor + 1) % voices.Count;
            voices[selected].Stop();
            return selected;
        }

        private static float DeterministicPitch(NeonGridAudioCue cue, int playCount)
        {
            uint value = unchecked((uint)cue.VariationSeed +
                                   (uint)(playCount + 1) * 0x9E3779B9u);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            float normalized = (value & 0x00FFFFFFu) / 16777215f;
            return Mathf.Lerp(cue.MinimumPitch, cue.MaximumPitch, normalized);
        }

        private static bool TryMap(CircuitJuiceEventType eventType,
            out NeonGridAudioEvent audioEvent)
        {
            switch (eventType)
            {
                case CircuitJuiceEventType.RotationAccepted:
                    audioEvent = NeonGridAudioEvent.TileRotateAccepted; return true;
                case CircuitJuiceEventType.InteractionRejected:
                    audioEvent = NeonGridAudioEvent.InteractionRejected; return true;
                case CircuitJuiceEventType.PowerActivated:
                    audioEvent = NeonGridAudioEvent.PowerActivated; return true;
                case CircuitJuiceEventType.PowerDeactivated:
                    audioEvent = NeonGridAudioEvent.PowerDeactivated; return true;
                case CircuitJuiceEventType.SourcePulse:
                    audioEvent = NeonGridAudioEvent.SourcePulse; return true;
                case CircuitJuiceEventType.SwitchChanged:
                    audioEvent = NeonGridAudioEvent.SwitchChanged; return true;
                case CircuitJuiceEventType.GateActivated:
                    audioEvent = NeonGridAudioEvent.GateActivated; return true;
                case CircuitJuiceEventType.GateDeactivated:
                    audioEvent = NeonGridAudioEvent.GateDeactivated; return true;
                case CircuitJuiceEventType.ObjectiveActivated:
                    audioEvent = NeonGridAudioEvent.ObjectiveActivated; return true;
                case CircuitJuiceEventType.HintTargeted:
                    audioEvent = NeonGridAudioEvent.HintActivated; return true;
                case CircuitJuiceEventType.CompletionTriggered:
                    audioEvent = NeonGridAudioEvent.CompletionTriggered; return true;
                default:
                    audioEvent = default; return false;
            }
        }
    }
}

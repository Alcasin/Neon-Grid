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
        private readonly List<AudioSource> ambienceVoices = new List<AudioSource>();
        private readonly Dictionary<NeonGridAudioEvent, float> lastPlaybackTimes =
            new Dictionary<NeonGridAudioEvent, float>();
        private readonly Dictionary<NeonGridAudioEvent, int> playCounts =
            new Dictionary<NeonGridAudioEvent, int>();
        private CircuitJuiceCoordinator coordinator;
        private int reuseCursor;
        private readonly float[] ambienceStartEnvelopes = new float[2];
        private readonly float[] ambienceCurrentEnvelopes = new float[2];
        private readonly float[] ambienceTargetEnvelopes = new float[2];
        private float ambienceFadeElapsed;
        private bool ambienceTransitioning;
        private int activeAmbienceVoice = -1;
        private float userMasterVolume = 1f;
        private float userSfxVolume = 1f;
        private float userAmbienceVolume = 1f;

        public NeonGridAudioDefinition Definition { get; private set; }
        public int VoiceCount => voices.Count;
        public int AmbienceVoiceCount => ambienceVoices.Count;
        public int SuccessfulPlaybackCount { get; private set; }
        public int AmbiencePlaybackStartCount { get; private set; }
        public NeonGridAudioPlayback? LastPlayback { get; private set; }
        public NeonGridAmbienceMode RequestedAmbienceMode { get; private set; }
        public bool IsAmbienceTransitioning => ambienceTransitioning;
        public float UserMasterVolume => userMasterVolume;
        public float UserSfxVolume => userSfxVolume;
        public float UserAmbienceVolume => userAmbienceVolume;
        public float EffectiveSfxMultiplier => userMasterVolume * userSfxVolume;
        public float EffectiveAmbienceMultiplier => userMasterVolume * userAmbienceVolume;

        public void Initialize(NeonGridAudioDefinition definition,
            CircuitJuiceCoordinator eventSource = null)
        {
            Definition = definition != null && definition.IsConfigured ? definition : null;
            if (Definition == null) return;
            if (voices.Count == 0)
            {
                for (int index = 0; index < Definition.SourcePoolSize; index++)
                {
                    var voiceObject = new GameObject($"SFX Voice {index + 1}");
                    voiceObject.transform.SetParent(transform, false);
                    AudioSource source = voiceObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    source.spatialBlend = 0f;
                    source.volume = EffectiveSfxMultiplier;
                    voices.Add(source);
                }
            }
            if (Definition.IsAmbienceConfigured && ambienceVoices.Count == 0)
            {
                for (int index = 0; index < 2; index++)
                {
                    var ambienceObject = new GameObject($"Ambience Voice {(char)('A' + index)}");
                    ambienceObject.transform.SetParent(transform, false);
                    AudioSource source = ambienceObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = true;
                    source.spatialBlend = 0f;
                    source.volume = 0f;
                    ambienceVoices.Add(source);
                }
            }
            Bind(eventSource);
        }

        public bool SetUserVolumeMultipliers(float masterVolume, float sfxVolume,
            float ambienceVolume)
        {
            float sanitizedMaster = SanitizeVolume(masterVolume);
            float sanitizedSfx = SanitizeVolume(sfxVolume);
            float sanitizedAmbience = SanitizeVolume(ambienceVolume);
            if (userMasterVolume.Equals(sanitizedMaster) &&
                userSfxVolume.Equals(sanitizedSfx) &&
                userAmbienceVolume.Equals(sanitizedAmbience))
                return false;

            userMasterVolume = sanitizedMaster;
            userSfxVolume = sanitizedSfx;
            userAmbienceVolume = sanitizedAmbience;
            foreach (AudioSource voice in voices)
                voice.volume = EffectiveSfxMultiplier;
            ApplyAmbienceOutputVolumes();
            return true;
        }

        private void Update()
        {
            AdvanceAmbience(Time.unscaledDeltaTime);
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
            float authoredGain = cue.Gain * Definition.MasterSfxMultiplier *
                                 Definition.SfxVolume;
            float effectiveGain = authoredGain * EffectiveSfxMultiplier;
            AudioSource voice = voices[voiceIndex];
            voice.pitch = pitch;
            voice.PlayOneShot(cue.Clip, authoredGain);
            lastPlaybackTimes[eventType] = timestamp;
            playCounts[eventType] = playCount + 1;
            SuccessfulPlaybackCount++;
            LastPlayback = new NeonGridAudioPlayback(eventType, cue.Clip, effectiveGain, pitch,
                voiceIndex);
            return true;
        }

        public void StopAll()
        {
            foreach (AudioSource voice in voices) voice.Stop();
            lastPlaybackTimes.Clear();
            LastPlayback = null;
        }

        public bool RequestAmbience(NeonGridAmbienceMode mode)
        {
            if (Definition == null || !Definition.IsAmbienceConfigured ||
                ambienceVoices.Count != 2 || mode == RequestedAmbienceMode)
                return false;

            RequestedAmbienceMode = mode;
            for (int index = 0; index < 2; index++)
                ambienceStartEnvelopes[index] = ambienceCurrentEnvelopes[index];

            if (mode == NeonGridAmbienceMode.None)
            {
                ambienceTargetEnvelopes[0] = 0f;
                ambienceTargetEnvelopes[1] = 0f;
                BeginAmbienceTransition();
                return true;
            }

            AudioClip clip = Definition.GetAmbienceClip(mode);
            int incoming = FindAmbienceVoice(clip);
            if (incoming < 0) incoming = activeAmbienceVoice == 0 ? 1 : 0;
            AudioSource incomingSource = ambienceVoices[incoming];
            if (incomingSource.clip != clip)
            {
                incomingSource.Stop();
                incomingSource.clip = clip;
            }
            if (!incomingSource.isPlaying)
            {
                incomingSource.Play();
                AmbiencePlaybackStartCount++;
            }
            activeAmbienceVoice = incoming;
            float target = Definition.GetAmbienceGain(mode) * Definition.AmbienceVolume;
            for (int index = 0; index < 2; index++)
                ambienceTargetEnvelopes[index] = index == incoming ? target : 0f;
            BeginAmbienceTransition();
            return true;
        }

        public void AdvanceAmbience(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!ambienceTransitioning) return;
            ambienceFadeElapsed += deltaSeconds;
            float duration = Definition.AmbienceFadeDuration;
            float amount = duration <= 0f ? 1f : Mathf.Clamp01(ambienceFadeElapsed / duration);
            for (int index = 0; index < ambienceVoices.Count; index++)
                ambienceCurrentEnvelopes[index] = Mathf.Lerp(
                    ambienceStartEnvelopes[index], ambienceTargetEnvelopes[index], amount);
            ApplyAmbienceOutputVolumes();
            if (amount < 1f) return;

            ambienceTransitioning = false;
            for (int index = 0; index < ambienceVoices.Count; index++)
            {
                if (ambienceTargetEnvelopes[index] > 0f) continue;
                ambienceVoices[index].Stop();
                ambienceVoices[index].clip = null;
                ambienceCurrentEnvelopes[index] = 0f;
            }
            if (RequestedAmbienceMode == NeonGridAmbienceMode.None)
                activeAmbienceVoice = -1;
        }

        public void StopAmbienceImmediately()
        {
            RequestedAmbienceMode = NeonGridAmbienceMode.None;
            ambienceTransitioning = false;
            activeAmbienceVoice = -1;
            for (int index = 0; index < ambienceVoices.Count; index++)
            {
                ambienceVoices[index].Stop();
                ambienceVoices[index].clip = null;
                ambienceVoices[index].volume = 0f;
                ambienceStartEnvelopes[index] = 0f;
                ambienceCurrentEnvelopes[index] = 0f;
                ambienceTargetEnvelopes[index] = 0f;
            }
        }

        private void ApplyAmbienceOutputVolumes()
        {
            for (int index = 0; index < ambienceVoices.Count; index++)
                ambienceVoices[index].volume = ambienceCurrentEnvelopes[index] *
                                                 EffectiveAmbienceMultiplier;
        }

        private static float SanitizeVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Mathf.Clamp01(value);
        }

        private void BeginAmbienceTransition()
        {
            ambienceFadeElapsed = 0f;
            ambienceTransitioning = true;
            if (Definition.AmbienceFadeDuration <= 0f) AdvanceAmbience(0f);
        }

        private int FindAmbienceVoice(AudioClip clip)
        {
            for (int index = 0; index < ambienceVoices.Count; index++)
                if (ambienceVoices[index].clip == clip) return index;
            return -1;
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

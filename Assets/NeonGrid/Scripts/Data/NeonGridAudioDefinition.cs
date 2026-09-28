using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonGrid.Data
{
    public enum NeonGridAudioEvent
    {
        TileRotateAccepted,
        InteractionRejected,
        PowerActivated,
        PowerDeactivated,
        SourcePulse,
        SwitchChanged,
        GateActivated,
        GateDeactivated,
        ObjectiveActivated,
        HintActivated,
        UIButtonPressed,
        CompletionTriggered
    }

    [Serializable]
    public sealed class NeonGridAudioCue
    {
        [SerializeField] private NeonGridAudioEvent eventType;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float gain = 0.7f;
        [SerializeField] private float minimumPitch = 0.98f;
        [SerializeField] private float maximumPitch = 1.02f;
        [SerializeField] private float cooldownSeconds = 0.04f;
        [SerializeField] private int variationSeed = 1;

        public NeonGridAudioEvent EventType => eventType;
        public AudioClip Clip => clip;
        public float Gain => gain;
        public float MinimumPitch => minimumPitch;
        public float MaximumPitch => maximumPitch;
        public float CooldownSeconds => cooldownSeconds;
        public int VariationSeed => variationSeed;
        public bool IsConfigured => clip != null && gain >= 0f && gain <= 1f &&
                                    minimumPitch > 0f && maximumPitch >= minimumPitch &&
                                    cooldownSeconds >= 0f;

#if UNITY_EDITOR
        public NeonGridAudioCue(NeonGridAudioEvent eventValue, AudioClip clipValue,
            float gainValue, float minimumPitchValue, float maximumPitchValue,
            float cooldownValue, int seedValue)
        {
            eventType = eventValue;
            clip = clipValue;
            gain = gainValue;
            minimumPitch = minimumPitchValue;
            maximumPitch = maximumPitchValue;
            cooldownSeconds = cooldownValue;
            variationSeed = seedValue;
        }
#endif
    }

    [CreateAssetMenu(fileName = "NeonGridAudio",
        menuName = "Neon Grid/Audio Definition")]
    public sealed class NeonGridAudioDefinition : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float masterSfxMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 1f;
        [SerializeField, Range(4, 8)] private int sourcePoolSize = 6;
        [SerializeField] private List<NeonGridAudioCue> cues =
            new List<NeonGridAudioCue>();

        public float MasterSfxMultiplier => masterSfxMultiplier;
        public float SfxVolume => sfxVolume;
        public float AmbienceVolume => ambienceVolume;
        public int SourcePoolSize => sourcePoolSize;
        public IReadOnlyList<NeonGridAudioCue> Cues => cues;
        public bool IsConfigured
        {
            get
            {
                if (sourcePoolSize < 4 || sourcePoolSize > 8 || cues == null ||
                    cues.Count == 0) return false;
                var events = new HashSet<NeonGridAudioEvent>();
                foreach (NeonGridAudioCue cue in cues)
                    if (cue == null || !cue.IsConfigured || !events.Add(cue.EventType))
                        return false;
                return true;
            }
        }

        public bool TryGetCue(NeonGridAudioEvent eventType, out NeonGridAudioCue cue)
        {
            foreach (NeonGridAudioCue candidate in cues)
            {
                if (candidate.EventType != eventType) continue;
                cue = candidate;
                return true;
            }
            cue = null;
            return false;
        }

#if UNITY_EDITOR
        public void SetData(IEnumerable<NeonGridAudioCue> values, int poolSize = 6,
            float masterMultiplier = 1f, float volume = 1f)
        {
            cues = values == null ? new List<NeonGridAudioCue>() :
                new List<NeonGridAudioCue>(values);
            sourcePoolSize = Mathf.Clamp(poolSize, 4, 8);
            masterSfxMultiplier = Mathf.Clamp01(masterMultiplier);
            sfxVolume = Mathf.Clamp01(volume);
        }
#endif
    }
}

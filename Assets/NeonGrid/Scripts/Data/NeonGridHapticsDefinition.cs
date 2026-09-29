using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonGrid.Data
{
    public static class NeonGridHapticsSettings
    {
        public static bool HapticsEnabled { get; set; } = true;
    }

    public enum NeonGridHapticEvent
    {
        TileRotate,
        LockedReject,
        SwitchToggle,
        ObjectiveActivate,
        Hint,
        Completion
    }

    [Serializable]
    public sealed class NeonGridHapticCue
    {
        [SerializeField] private NeonGridHapticEvent eventType;
        [SerializeField, Range(1, 100)] private int durationMilliseconds = 16;
        [SerializeField, Range(0f, 1f)] private float intensity = 0.25f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.05f;

        public NeonGridHapticEvent EventType => eventType;
        public int DurationMilliseconds => durationMilliseconds;
        public float Intensity => intensity;
        public float CooldownSeconds => cooldownSeconds;
        public bool IsConfigured => durationMilliseconds > 0 &&
                                    durationMilliseconds <= 100 &&
                                    intensity > 0f && intensity <= 1f &&
                                    cooldownSeconds >= 0f;

#if UNITY_EDITOR
        public NeonGridHapticCue(NeonGridHapticEvent eventValue, int durationValue,
            float intensityValue, float cooldownValue)
        {
            eventType = eventValue;
            durationMilliseconds = durationValue;
            intensity = intensityValue;
            cooldownSeconds = cooldownValue;
        }
#endif
    }

    [CreateAssetMenu(fileName = "NeonGridHaptics",
        menuName = "Neon Grid/Haptics Definition")]
    public sealed class NeonGridHapticsDefinition : ScriptableObject
    {
        [SerializeField] private List<NeonGridHapticCue> cues =
            new List<NeonGridHapticCue>();

        public IReadOnlyList<NeonGridHapticCue> Cues => cues;
        public bool IsConfigured
        {
            get
            {
                if (cues == null || cues.Count !=
                    Enum.GetValues(typeof(NeonGridHapticEvent)).Length)
                    return false;
                var events = new HashSet<NeonGridHapticEvent>();
                foreach (NeonGridHapticCue cue in cues)
                    if (cue == null || !cue.IsConfigured ||
                        !Enum.IsDefined(typeof(NeonGridHapticEvent), cue.EventType) ||
                        !events.Add(cue.EventType))
                        return false;
                return true;
            }
        }

        public bool TryGetCue(NeonGridHapticEvent eventType, out NeonGridHapticCue cue)
        {
            foreach (NeonGridHapticCue candidate in cues)
            {
                if (candidate.EventType != eventType) continue;
                cue = candidate;
                return true;
            }
            cue = null;
            return false;
        }

#if UNITY_EDITOR
        public void SetData(IEnumerable<NeonGridHapticCue> values)
        {
            cues = values == null ? new List<NeonGridHapticCue>() :
                new List<NeonGridHapticCue>(values);
        }
#endif
    }
}

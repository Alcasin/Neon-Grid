using System;
using System.Collections.Generic;
using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public interface INeonGridHapticsBackend
    {
        bool IsSupported { get; }
        bool TryPulse(int durationMilliseconds, float intensity);
    }

    public sealed class NeonGridNoOpHapticsBackend : INeonGridHapticsBackend
    {
        public bool IsSupported => false;

        public bool TryPulse(int durationMilliseconds, float intensity)
        {
            return false;
        }
    }

    public sealed class NeonGridHapticsService : MonoBehaviour
    {
        private readonly Dictionary<NeonGridHapticEvent, float> lastPulseTimes =
            new Dictionary<NeonGridHapticEvent, float>();
        private CircuitJuiceCoordinator coordinator;
        private INeonGridHapticsBackend backend;
        private bool completionIssued;

        public NeonGridHapticsDefinition Definition { get; private set; }
        public bool HapticsEnabled => NeonGridHapticsSettings.HapticsEnabled;
        public bool BackendSupported => backend != null && backend.IsSupported;
        public int SuccessfulPulseCount { get; private set; }
        public NeonGridHapticEvent? LastEvent { get; private set; }

        public void Initialize(NeonGridHapticsDefinition definition,
            CircuitJuiceCoordinator eventSource = null,
            INeonGridHapticsBackend backendOverride = null)
        {
            Unbind();
            if (backend is IDisposable disposable)
                disposable.Dispose();
            lastPulseTimes.Clear();
            completionIssued = false;
            SuccessfulPulseCount = 0;
            LastEvent = null;
            Definition = definition != null && definition.IsConfigured ? definition : null;
            backend = backendOverride ?? NeonGridHapticsBackendFactory.Create();
            if (Definition == null) return;
            Bind(eventSource);
        }

        public void Bind(CircuitJuiceCoordinator eventSource)
        {
            Unbind();
            coordinator = eventSource;
            if (coordinator != null)
                coordinator.PresentationEvent += HandlePresentationEvent;
        }

        public void SetHapticsEnabled(bool enabled)
        {
            NeonGridHapticsSettings.HapticsEnabled = enabled;
        }

        public bool TryPlay(NeonGridHapticEvent eventType)
        {
            return TryPlay(eventType, Time.unscaledTime);
        }

        public bool TryPlay(NeonGridHapticEvent eventType, float timestamp)
        {
            if (!HapticsEnabled || Definition == null || backend == null ||
                !backend.IsSupported || !Definition.TryGetCue(eventType, out NeonGridHapticCue cue))
                return false;
            if (eventType == NeonGridHapticEvent.Completion && completionIssued)
                return false;
            if (lastPulseTimes.TryGetValue(eventType, out float lastTime) &&
                timestamp - lastTime < cue.CooldownSeconds)
                return false;

            bool played;
            try
            {
                played = backend.TryPulse(cue.DurationMilliseconds, cue.Intensity);
            }
            catch
            {
                played = false;
            }
            if (!played) return false;

            lastPulseTimes[eventType] = timestamp;
            if (eventType == NeonGridHapticEvent.Completion)
                completionIssued = true;
            LastEvent = eventType;
            SuccessfulPulseCount++;
            return true;
        }

        public void ResetCompletionLifecycle()
        {
            completionIssued = false;
            lastPulseTimes.Remove(NeonGridHapticEvent.Completion);
        }

        private void HandlePresentationEvent(CircuitJuiceEventType eventType)
        {
            if (TryMap(eventType, out NeonGridHapticEvent hapticEvent))
                TryPlay(hapticEvent);
        }

        private static bool TryMap(CircuitJuiceEventType eventType,
            out NeonGridHapticEvent hapticEvent)
        {
            switch (eventType)
            {
                case CircuitJuiceEventType.RotationAccepted:
                    hapticEvent = NeonGridHapticEvent.TileRotate; return true;
                case CircuitJuiceEventType.InteractionRejected:
                    hapticEvent = NeonGridHapticEvent.LockedReject; return true;
                case CircuitJuiceEventType.SwitchChanged:
                    hapticEvent = NeonGridHapticEvent.SwitchToggle; return true;
                case CircuitJuiceEventType.ObjectiveActivated:
                    hapticEvent = NeonGridHapticEvent.ObjectiveActivate; return true;
                case CircuitJuiceEventType.HintTargeted:
                    hapticEvent = NeonGridHapticEvent.Hint; return true;
                case CircuitJuiceEventType.CompletionTriggered:
                    hapticEvent = NeonGridHapticEvent.Completion; return true;
                default:
                    hapticEvent = default; return false;
            }
        }

        private void Unbind()
        {
            if (coordinator != null)
                coordinator.PresentationEvent -= HandlePresentationEvent;
            coordinator = null;
        }

        private void OnDestroy()
        {
            Unbind();
            if (backend is IDisposable disposable)
                disposable.Dispose();
            backend = null;
        }
    }

    internal static class NeonGridHapticsBackendFactory
    {
        public static INeonGridHapticsBackend Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new NeonGridAndroidHapticsBackend();
#else
            return new NeonGridNoOpHapticsBackend();
#endif
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    internal sealed class NeonGridAndroidHapticsBackend : INeonGridHapticsBackend, IDisposable
    {
        private AndroidJavaObject vibrator;
        private readonly bool supportsAmplitude;

        public bool IsSupported => vibrator != null && supportsAmplitude;

        public NeonGridAndroidHapticsBackend()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>(
                           "currentActivity"))
                using (AndroidJavaObject context = activity?.Call<AndroidJavaObject>(
                           "getApplicationContext"))
                {
                    vibrator = context?.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdk = version.GetStatic<int>("SDK_INT");
                    supportsAmplitude = sdk >= 26 && vibrator != null &&
                                        vibrator.Call<bool>("hasVibrator") &&
                                        vibrator.Call<bool>("hasAmplitudeControl");
                }
                if (!supportsAmplitude)
                {
                    vibrator?.Dispose();
                    vibrator = null;
                }
            }
            catch
            {
                vibrator?.Dispose();
                vibrator = null;
                supportsAmplitude = false;
            }
        }

        public bool TryPulse(int durationMilliseconds, float intensity)
        {
            if (!IsSupported) return false;
            try
            {
                int amplitude = Mathf.Clamp(Mathf.RoundToInt(intensity * 255f), 1, 255);
                using (var vibrationEffect = new AndroidJavaClass(
                           "android.os.VibrationEffect"))
                using (AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                           "createOneShot", (long)durationMilliseconds, amplitude))
                {
                    vibrator.Call("vibrate", effect);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            vibrator?.Dispose();
            vibrator = null;
        }
    }
#endif
}

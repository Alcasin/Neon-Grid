using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class CircuitTileJuiceView : MonoBehaviour
    {
        private CircuitJuiceDefinition definition;
        private CircuitJuiceCoordinator coordinator;
        private Transform visualRoot;
        private TechnicalNeonTileRenderer tileRenderer;

        private float pressElapsed = -1f;
        private float pulseElapsed = -1f;
        private float pulseDuration;
        private float pulseScale = 1f;
        private CircuitJuiceEventType pulseEventType;
        private float rejectionElapsed = -1f;
        private float rotationElapsed = -1f;
        private float rotationFrom;
        private float rotationTarget;
        private float canonicalRotation;
        private float powerElapsed = -1f;
        private float completionElapsed = -1f;
        private bool powerWaiting;

        internal bool IsQueued { get; set; }
        public CircuitJuiceEventType LastEvent { get; private set; }
        public CircuitJuiceDefinition Definition => definition;
        public bool IsAnimating => pressElapsed >= 0f || pulseElapsed >= 0f ||
                                   rejectionElapsed >= 0f || rotationElapsed >= 0f ||
                                   powerElapsed >= 0f || completionElapsed >= 0f;
        public Transform VisualRoot => visualRoot;
        public Vector3 CurrentScale => visualRoot != null ? visualRoot.localScale : Vector3.one;
        public Vector3 CurrentOffset => visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        public float CurrentRotationDegrees { get; private set; }
        public float CanonicalRotationDegrees => canonicalRotation;
        public float PowerPresentationAmount => tileRenderer?.CurrentPowerPresentation ?? 1f;
        public float SuccessPresentationAmount => tileRenderer?.CurrentSuccessPresentation ?? 0f;

        internal void Initialize(CircuitJuiceDefinition value,
            CircuitJuiceCoordinator owner, Transform root,
            TechnicalNeonTileRenderer tileRenderer)
        {
            definition = value;
            coordinator = owner;
            visualRoot = root;
            this.tileRenderer = tileRenderer;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localScale = Vector3.one;
        }

        public void SetInitialRotation(int rotation)
        {
            canonicalRotation = -90f * rotation;
            rotationTarget = canonicalRotation;
            SetRotation(canonicalRotation, true);
        }

        public void PresentPress()
        {
            LastEvent = CircuitJuiceEventType.TilePressed;
            if (!definition.PressFeedback || definition.PressDuration +
                definition.PressSettleDuration <= 0f)
            {
                RestoreTransformIfIdle();
                return;
            }
            pressElapsed = 0f;
            coordinator.Activate(this);
        }

        public void PresentRejected()
        {
            LastEvent = CircuitJuiceEventType.InteractionRejected;
            coordinator.PublishPresentationEvent(CircuitJuiceEventType.InteractionRejected);
            if (!definition.RejectionFeedback || definition.RejectionDuration <= 0f ||
                definition.RejectionNudge <= 0f)
            {
                RestoreTransformIfIdle();
                return;
            }
            rejectionElapsed = 0f;
            coordinator.Activate(this);
        }

        public void PresentPulse(CircuitJuiceEventType eventType, float duration, float scale)
        {
            LastEvent = eventType;
            pulseEventType = eventType;
            if (duration <= 0f || Mathf.Approximately(scale, 1f))
            {
                RestoreTransformIfIdle();
                return;
            }
            pulseElapsed = 0f;
            pulseDuration = duration;
            pulseScale = scale;
            coordinator.Activate(this);
        }

        public void RetargetRotation(int rotation, bool clockwiseAction)
        {
            canonicalRotation = -90f * rotation;
            LastEvent = CircuitJuiceEventType.RotationAccepted;
            coordinator.PublishPresentationEvent(CircuitJuiceEventType.RotationAccepted);
            if (!definition.RotationFeedback || definition.RotationDuration <= 0f)
            {
                rotationElapsed = -1f;
                rotationTarget = canonicalRotation;
                SetRotation(canonicalRotation, true);
                RestoreTransformIfIdle();
                return;
            }

            rotationFrom = CurrentRotationDegrees;
            rotationTarget = clockwiseAction
                ? rotationTarget - 90f
                : CurrentRotationDegrees + Mathf.DeltaAngle(CurrentRotationDegrees,
                    canonicalRotation);
            rotationElapsed = 0f;
            coordinator.Activate(this);
        }

        public void MarkCompletion()
        {
            LastEvent = CircuitJuiceEventType.CompletionTriggered;
        }

        internal void PublishSemanticEvent(CircuitJuiceEventType eventType)
        {
            coordinator.PublishPresentationEvent(eventType);
        }

        internal void PreparePowerActivation(CircuitJuiceEventType eventType)
        {
            LastEvent = eventType;
            powerElapsed = -1f;
            powerWaiting = true;
            tileRenderer?.SetPowerPresentation(0f, 1f, 0f,
                definition.CompletionSuccessColor);
        }

        internal void BeginPowerActivation(CircuitJuiceEventType eventType)
        {
            LastEvent = eventType;
            powerWaiting = false;
            if (!definition.PropagationFeedback ||
                definition.PropagationActivationDuration <= 0f)
            {
                powerElapsed = -1f;
                ApplyPowerPresentation(1f, 1f, 0f);
                return;
            }
            powerElapsed = 0f;
            coordinator.Activate(this);
        }

        internal void PresentPowerDeactivation()
        {
            CircuitJuiceEventType eventType = LastEvent ==
                                               CircuitJuiceEventType.GateDeactivated
                ? CircuitJuiceEventType.GateDeactivated
                : CircuitJuiceEventType.PowerDeactivated;
            ApplyPowerPresentation(1f, 1f, 0f);
            PresentPulse(eventType,
                definition.PropagationDeactivationDuration,
                definition.PowerDeactivationScale);
        }

        internal void BeginCompletionPulse()
        {
            LastEvent = CircuitJuiceEventType.CompletionTriggered;
            if (!definition.CompletionFeedback || definition.CompletionPulseDuration <= 0f)
            {
                completionElapsed = -1f;
                ApplyPowerPresentation(1f, 1f, 0f);
                return;
            }
            completionElapsed = 0f;
            coordinator.Activate(this);
        }

        internal void CancelScheduledChannels()
        {
            if (pulseEventType == CircuitJuiceEventType.SourcePulse)
                pulseElapsed = -1f;
            powerElapsed = -1f;
            completionElapsed = -1f;
            powerWaiting = false;
            ApplyPowerPresentation(1f, 1f, 0f);
        }

        internal bool Advance(float deltaSeconds)
        {
            float scale = 1f;
            if (pressElapsed >= 0f)
            {
                pressElapsed += deltaSeconds;
                float press = definition.PressDuration;
                float settle = definition.PressSettleDuration;
                if (pressElapsed < press && press > 0f)
                    scale *= Mathf.Lerp(1f, definition.PressScale,
                        Mathf.Clamp01(pressElapsed / press));
                else if (pressElapsed < press + settle && settle > 0f)
                    scale *= Mathf.Lerp(definition.PressScale, 1f,
                        Mathf.Clamp01((pressElapsed - press) / settle));
                else
                    pressElapsed = -1f;
            }

            if (pulseElapsed >= 0f)
            {
                pulseElapsed += deltaSeconds;
                if (pulseElapsed < pulseDuration && pulseDuration > 0f)
                    scale *= Mathf.Lerp(1f, pulseScale,
                        Mathf.Sin(Mathf.Clamp01(pulseElapsed / pulseDuration) * Mathf.PI));
                else
                    pulseElapsed = -1f;
            }

            float powerAmount = powerWaiting ? 0f : 1f;
            float propagationHalo = 1f;
            if (powerElapsed >= 0f)
            {
                powerElapsed += deltaSeconds;
                float duration = definition.PropagationActivationDuration;
                float progress = duration <= 0f ? 1f :
                    Mathf.Clamp01(powerElapsed / duration);
                powerAmount = Mathf.SmoothStep(0f, 1f, progress);
                propagationHalo = Mathf.Lerp(definition.PropagationHaloEmphasis, 1f,
                    progress);
                if (progress >= 1f) powerElapsed = -1f;
            }

            float success = 0f;
            float completionHalo = 1f;
            if (completionElapsed >= 0f)
            {
                completionElapsed += deltaSeconds;
                float duration = definition.CompletionPulseDuration;
                float progress = duration <= 0f ? 1f :
                    Mathf.Clamp01(completionElapsed / duration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                success = pulse;
                completionHalo = Mathf.Lerp(1f, definition.CompletionHaloEmphasis, pulse);
                scale *= Mathf.Lerp(1f, definition.CompletionPulseScale, pulse);
                if (progress >= 1f) completionElapsed = -1f;
            }

            ApplyPowerPresentation(powerAmount,
                Mathf.Max(propagationHalo, completionHalo), success);

            visualRoot.localScale = new Vector3(scale, scale, 1f);

            if (rejectionElapsed >= 0f)
            {
                rejectionElapsed += deltaSeconds;
                if (rejectionElapsed < definition.RejectionDuration &&
                    definition.RejectionDuration > 0f)
                {
                    float phase = rejectionElapsed / definition.RejectionDuration;
                    visualRoot.localPosition = new Vector3(
                        Mathf.Sin(phase * Mathf.PI * 2f) * definition.RejectionNudge,
                        0f, 0f);
                }
                else
                {
                    rejectionElapsed = -1f;
                    visualRoot.localPosition = Vector3.zero;
                }
            }

            if (rotationElapsed >= 0f)
            {
                rotationElapsed += deltaSeconds;
                float progress = definition.RotationDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(rotationElapsed / definition.RotationDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                SetRotation(Mathf.LerpUnclamped(rotationFrom, rotationTarget, eased), false);
                if (progress >= 1f)
                {
                    rotationElapsed = -1f;
                    rotationTarget = canonicalRotation;
                    SetRotation(canonicalRotation, true);
                }
            }

            if (IsAnimating) return true;
            RestoreTransformIfIdle();
            return false;
        }

        public void CancelAndSnap(int rotation)
        {
            canonicalRotation = -90f * rotation;
            rotationTarget = canonicalRotation;
            CancelAndRestore();
            SetRotation(canonicalRotation, true);
        }

        internal void CancelAndRestore()
        {
            pressElapsed = -1f;
            pulseElapsed = -1f;
            rejectionElapsed = -1f;
            rotationElapsed = -1f;
            powerElapsed = -1f;
            completionElapsed = -1f;
            powerWaiting = false;
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one;
                visualRoot.localPosition = Vector3.zero;
            }
            if (tileRenderer != null) SetRotation(canonicalRotation, true);
            ApplyPowerPresentation(1f, 1f, 0f);
        }

        private void RestoreTransformIfIdle()
        {
            if (IsAnimating || visualRoot == null) return;
            visualRoot.localScale = Vector3.one;
            visualRoot.localPosition = Vector3.zero;
        }

        private void SetRotation(float degrees, bool canonical)
        {
            CurrentRotationDegrees = degrees;
            tileRenderer?.SetVisualRotationDegrees(degrees, canonical);
        }

        private void ApplyPowerPresentation(float amount, float halo, float success)
        {
            tileRenderer?.SetPowerPresentation(amount, halo, success,
                definition.CompletionSuccessColor);
        }
    }
}

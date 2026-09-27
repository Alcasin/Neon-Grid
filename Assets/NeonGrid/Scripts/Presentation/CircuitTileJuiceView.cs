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
        private float rejectionElapsed = -1f;
        private float rotationElapsed = -1f;
        private float rotationFrom;
        private float rotationTarget;
        private float canonicalRotation;

        internal bool IsQueued { get; set; }
        public CircuitJuiceEventType LastEvent { get; private set; }
        public CircuitJuiceDefinition Definition => definition;
        public bool IsAnimating => pressElapsed >= 0f || pulseElapsed >= 0f ||
                                   rejectionElapsed >= 0f || rotationElapsed >= 0f;
        public Transform VisualRoot => visualRoot;
        public Vector3 CurrentScale => visualRoot != null ? visualRoot.localScale : Vector3.one;
        public Vector3 CurrentOffset => visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        public float CurrentRotationDegrees { get; private set; }
        public float CanonicalRotationDegrees => canonicalRotation;

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
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one;
                visualRoot.localPosition = Vector3.zero;
            }
            if (tileRenderer != null) SetRotation(canonicalRotation, true);
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
    }
}

using UnityEngine;

namespace NeonGrid.Data
{
    [CreateAssetMenu(fileName = "CircuitJuice",
        menuName = "Neon Grid/Circuit Juice Definition")]
    public sealed class CircuitJuiceDefinition : ScriptableObject
    {
        [Header("Global Motion")]
        [SerializeField, Range(0f, 1f)] private float motionScale = 1f;

        [Header("Feature Toggles")]
        [SerializeField] private bool pressFeedback = true;
        [SerializeField] private bool rotationFeedback = true;
        [SerializeField] private bool powerFeedback = true;
        [SerializeField] private bool componentFeedback = true;
        [SerializeField] private bool rejectionFeedback = true;
        [SerializeField] private bool hintFeedback = true;
        [SerializeField] private bool propagationFeedback = true;
        [SerializeField] private bool completionFeedback = true;

        [Header("Timings (seconds)")]
        [SerializeField] private float pressDuration = 0.055f;
        [SerializeField] private float pressSettleDuration = 0.080f;
        [SerializeField] private float rotationDuration = 0.130f;
        [SerializeField] private float powerActivationDuration = 0.150f;
        [SerializeField] private float powerDeactivationDuration = 0.100f;
        [SerializeField] private float objectiveActivationDuration = 0.190f;
        [SerializeField] private float rejectionDuration = 0.090f;
        [SerializeField] private float hintEmphasisDuration = 0.180f;

        [Header("Propagation and Completion (seconds)")]
        [SerializeField] private float propagationDepthDelay = 0.035f;
        [SerializeField] private float maximumPropagationDelay = 0.420f;
        [SerializeField] private float propagationActivationDuration = 0.140f;
        [SerializeField] private float propagationDeactivationDuration = 0.085f;
        [SerializeField] private float completionSettleDelay = 0.025f;
        [SerializeField] private float completionPulseDuration = 0.180f;

        [Header("Amplitudes")]
        [SerializeField] private float pressScale = 0.970f;
        [SerializeField] private float powerActivationScale = 1.025f;
        [SerializeField] private float powerDeactivationScale = 0.985f;
        [SerializeField] private float sourcePulseScale = 1.030f;
        [SerializeField] private float objectivePulseScale = 1.045f;
        [SerializeField] private float componentPulseScale = 1.025f;
        [SerializeField] private float hintPulseScale = 1.020f;
        [SerializeField] private float rejectionNudge = 0.012f;
        [SerializeField] private float propagationHaloEmphasis = 1.35f;
        [SerializeField] private float completionPulseScale = 1.018f;
        [SerializeField] private float completionHaloEmphasis = 1.65f;
        [SerializeField] private Color completionSuccessColor =
            new Color(0.133333f, 0.87451f, 0.647059f, 1f);

        public float MotionScale => motionScale;
        public bool PressFeedback => pressFeedback;
        public bool RotationFeedback => rotationFeedback;
        public bool PowerFeedback => powerFeedback;
        public bool ComponentFeedback => componentFeedback;
        public bool RejectionFeedback => rejectionFeedback;
        public bool HintFeedback => hintFeedback;
        public bool PropagationFeedback => propagationFeedback;
        public bool CompletionFeedback => completionFeedback;
        public float PressDuration => ScaleTime(pressDuration);
        public float PressSettleDuration => ScaleTime(pressSettleDuration);
        public float RotationDuration => ScaleTime(rotationDuration);
        public float PowerActivationDuration => ScaleTime(powerActivationDuration);
        public float PowerDeactivationDuration => ScaleTime(powerDeactivationDuration);
        public float ObjectiveActivationDuration => ScaleTime(objectiveActivationDuration);
        public float RejectionDuration => ScaleTime(rejectionDuration);
        public float HintEmphasisDuration => ScaleTime(hintEmphasisDuration);
        public float PropagationDepthDelay => ScaleTime(propagationDepthDelay);
        public float MaximumPropagationDelay => ScaleTime(maximumPropagationDelay);
        public float PropagationActivationDuration => ScaleTime(propagationActivationDuration);
        public float PropagationDeactivationDuration => ScaleTime(propagationDeactivationDuration);
        public float CompletionSettleDelay => ScaleTime(completionSettleDelay);
        public float CompletionPulseDuration => ScaleTime(completionPulseDuration);
        public float PressScale => ScaleAroundOne(pressScale);
        public float PowerActivationScale => ScaleAroundOne(powerActivationScale);
        public float PowerDeactivationScale => ScaleAroundOne(powerDeactivationScale);
        public float SourcePulseScale => ScaleAroundOne(sourcePulseScale);
        public float ObjectivePulseScale => ScaleAroundOne(objectivePulseScale);
        public float ComponentPulseScale => ScaleAroundOne(componentPulseScale);
        public float HintPulseScale => ScaleAroundOne(hintPulseScale);
        public float RejectionNudge => rejectionNudge * motionScale;
        public float PropagationHaloEmphasis => ScaleAroundOne(propagationHaloEmphasis);
        public float CompletionPulseScale => ScaleAroundOne(completionPulseScale);
        public float CompletionHaloEmphasis => ScaleAroundOne(completionHaloEmphasis);
        public Color CompletionSuccessColor => completionSuccessColor;

        public bool IsConfigured => motionScale >= 0f && motionScale <= 1f &&
                                    pressDuration >= 0f && pressSettleDuration >= 0f &&
                                    rotationDuration >= 0f && powerActivationDuration >= 0f &&
                                    powerDeactivationDuration >= 0f &&
                                    objectiveActivationDuration >= 0f &&
                                    rejectionDuration >= 0f && hintEmphasisDuration >= 0f &&
                                    propagationDepthDelay >= 0f &&
                                    maximumPropagationDelay >= 0f &&
                                    propagationActivationDuration >= 0f &&
                                    propagationDeactivationDuration >= 0f &&
                                    completionSettleDelay >= 0f &&
                                    completionPulseDuration >= 0f &&
                                    pressScale > 0f && powerActivationScale > 0f &&
                                    powerDeactivationScale > 0f && sourcePulseScale > 0f &&
                                    objectivePulseScale > 0f && componentPulseScale > 0f &&
                                    hintPulseScale > 0f && rejectionNudge >= 0f &&
                                    propagationHaloEmphasis >= 1f &&
                                    completionPulseScale > 0f &&
                                    completionHaloEmphasis >= 1f;

        private float ScaleTime(float duration) => duration * motionScale;

        private float ScaleAroundOne(float value) => 1f + (value - 1f) * motionScale;

#if UNITY_EDITOR
        public void SetMotionScale(float value)
        {
            motionScale = Mathf.Clamp01(value);
        }
#endif
    }
}

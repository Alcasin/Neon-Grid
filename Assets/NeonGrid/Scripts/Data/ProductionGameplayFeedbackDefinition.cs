using UnityEngine;

namespace NeonGrid.Data
{
    [CreateAssetMenu(fileName = "ProductionGameplayFeedback",
        menuName = "Neon Grid/Production Gameplay Feedback")]
    public sealed class ProductionGameplayFeedbackDefinition : ScriptableObject
    {
        [SerializeField] private CircuitJuiceDefinition circuitJuice;
        [SerializeField] private NeonGridAudioDefinition audioDefinition;
        [SerializeField] private NeonGridHapticsDefinition hapticsDefinition;

        public CircuitJuiceDefinition CircuitJuice => circuitJuice;
        public NeonGridAudioDefinition AudioDefinition => audioDefinition;
        public NeonGridHapticsDefinition HapticsDefinition => hapticsDefinition;
        public bool IsConfigured => circuitJuice != null && circuitJuice.IsConfigured &&
                                    audioDefinition != null && audioDefinition.IsConfigured &&
                                    audioDefinition.IsAmbienceConfigured &&
                                    hapticsDefinition != null && hapticsDefinition.IsConfigured;

#if UNITY_EDITOR
        public void SetData(CircuitJuiceDefinition juice, NeonGridAudioDefinition audio,
            NeonGridHapticsDefinition haptics)
        {
            circuitJuice = juice;
            audioDefinition = audio;
            hapticsDefinition = haptics;
        }
#endif
    }
}

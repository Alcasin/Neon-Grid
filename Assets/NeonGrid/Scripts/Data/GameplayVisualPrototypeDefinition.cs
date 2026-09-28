using UnityEngine;

namespace NeonGrid.Data
{
    [CreateAssetMenu(fileName = "GameplayVisualPrototype",
        menuName = "Neon Grid/Gameplay Visual Prototype")]
    public sealed class GameplayVisualPrototypeDefinition : ScriptableObject
    {
        [SerializeField] private CircuitVisualThemeDefinition visualTheme;
        [SerializeField] private CircuitVisualThemeDefinition productionVisualTheme;
        [SerializeField] private CircuitJuiceDefinition circuitJuice;
        [SerializeField] private NeonGridAudioDefinition audioDefinition;
        [SerializeField] private LevelDefinition simpleLevel;
        [SerializeField] private LevelDefinition denseLevel;

        public CircuitVisualThemeDefinition VisualTheme => visualTheme;
        public CircuitVisualThemeDefinition ProductionVisualTheme => productionVisualTheme;
        public CircuitJuiceDefinition CircuitJuice => circuitJuice;
        public NeonGridAudioDefinition AudioDefinition => audioDefinition;
        public LevelDefinition SimpleLevel => simpleLevel;
        public LevelDefinition DenseLevel => denseLevel;
        public bool IsConfigured => visualTheme != null && visualTheme.IsConfigured &&
                                    productionVisualTheme != null &&
                                    productionVisualTheme.IsConfigured &&
                                    simpleLevel != null && denseLevel != null;

#if UNITY_EDITOR
        public void SetData(CircuitVisualThemeDefinition theme,
            CircuitVisualThemeDefinition productionTheme, LevelDefinition simple,
            LevelDefinition dense)
        {
            visualTheme = theme;
            productionVisualTheme = productionTheme;
            simpleLevel = simple;
            denseLevel = dense;
        }

        public void SetCircuitJuice(CircuitJuiceDefinition value)
        {
            circuitJuice = value;
        }

        public void SetAudioDefinition(NeonGridAudioDefinition value)
        {
            audioDefinition = value;
        }
#endif
    }

    public static class GameplayVisualPrototypeCatalog
    {
        public const string ResourcePath = "VisualPrototypes/M15_GameplayVisualPrototype";

        public static GameplayVisualPrototypeDefinition Load()
        {
            return Resources.Load<GameplayVisualPrototypeDefinition>(ResourcePath);
        }
    }
}

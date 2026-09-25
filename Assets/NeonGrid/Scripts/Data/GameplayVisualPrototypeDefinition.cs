using UnityEngine;

namespace NeonGrid.Data
{
    [CreateAssetMenu(fileName = "GameplayVisualPrototype",
        menuName = "Neon Grid/Gameplay Visual Prototype")]
    public sealed class GameplayVisualPrototypeDefinition : ScriptableObject
    {
        [SerializeField] private CircuitVisualThemeDefinition visualTheme;
        [SerializeField] private CircuitVisualThemeDefinition productionVisualTheme;
        [SerializeField] private LevelDefinition simpleLevel;
        [SerializeField] private LevelDefinition denseLevel;

        public CircuitVisualThemeDefinition VisualTheme => visualTheme;
        public CircuitVisualThemeDefinition ProductionVisualTheme => productionVisualTheme;
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

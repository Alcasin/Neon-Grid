using UnityEngine;

namespace NeonGrid.Data
{
    [CreateAssetMenu(fileName = "GameplayVisualPrototype",
        menuName = "Neon Grid/Gameplay Visual Prototype")]
    public sealed class GameplayVisualPrototypeDefinition : ScriptableObject
    {
        [SerializeField] private CircuitVisualThemeDefinition visualTheme;
        [SerializeField] private LevelDefinition simpleLevel;
        [SerializeField] private LevelDefinition denseLevel;

        public CircuitVisualThemeDefinition VisualTheme => visualTheme;
        public LevelDefinition SimpleLevel => simpleLevel;
        public LevelDefinition DenseLevel => denseLevel;
        public bool IsConfigured => visualTheme != null && visualTheme.IsConfigured &&
                                    simpleLevel != null && denseLevel != null;

#if UNITY_EDITOR
        public void SetData(CircuitVisualThemeDefinition theme, LevelDefinition simple,
            LevelDefinition dense)
        {
            visualTheme = theme;
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

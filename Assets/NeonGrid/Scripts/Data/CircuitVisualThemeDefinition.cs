using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Data
{
    public enum CircuitVisualStyle
    {
        TechnicalPrototype,
        ProductionPrototype
    }

    [CreateAssetMenu(fileName = "CircuitVisualTheme",
        menuName = "Neon Grid/Circuit Visual Theme")]
    public sealed class CircuitVisualThemeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string themeId = "technical_neon_prototype";
        [SerializeField] private string displayName = "Technical Neon Infrastructure";
        [SerializeField] private CircuitVisualStyle visualStyle =
            CircuitVisualStyle.TechnicalPrototype;

        [Header("Semantic Palette")]
        [SerializeField] private Color background = new Color32(0x05, 0x08, 0x10, 0xFF);
        [SerializeField] private Color board = new Color32(0x0B, 0x12, 0x20, 0xFF);
        [SerializeField] private Color inactiveConductor = new Color32(0x26, 0x33, 0x49, 0xFF);
        [SerializeField] private Color poweredEnergy = new Color32(0x18, 0xDC, 0xCA, 0xFF);
        [SerializeField] private Color poweredHotCore = new Color32(0xBF, 0xFF, 0xFA, 0xFF);
        [SerializeField] private Color secondaryBlue = new Color32(0x27, 0xA9, 0xFF, 0xFF);
        [SerializeField] private Color powerSource = new Color32(0xFF, 0x3C, 0xB4, 0xFF);
        [SerializeField] private Color ledObjective = new Color32(0xFF, 0xD8, 0x4A, 0xFF);
        [SerializeField] private Color success = new Color32(0x22, 0xDF, 0xA5, 0xFF);
        [SerializeField] private Color directionalAccent = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        [SerializeField] private Color hint = new Color32(0xFF, 0xCF, 0x4A, 0xFF);

        [Header("Normalized 256 px Geometry")]
        [SerializeField] private float referenceCanvasSize = 256f;
        [SerializeField] private float conduitWidth = 30f;
        [SerializeField] private float poweredCoreWidth = 11f;
        [SerializeField] private float haloWidth = 28f;

        [Header("Future Production Art Slots (Optional)")]
        [SerializeField] private Sprite housingSprite;
        [SerializeField] private Sprite conductorSprite;
        [SerializeField] private Sprite sourceSymbolSprite;
        [SerializeField] private Sprite ledOffSprite;
        [SerializeField] private Sprite ledOnSprite;
        [SerializeField] private Sprite diodeSymbolSprite;
        [SerializeField] private Sprite switchOpenSprite;
        [SerializeField] private Sprite switchClosedSprite;
        [SerializeField] private Sprite andGateSprite;
        [SerializeField] private Sprite orGateSprite;
        [SerializeField] private Sprite lockOverlaySprite;
        [SerializeField] private Sprite hintOverlaySprite;

        public string ThemeId => themeId;
        public string DisplayName => displayName;
        public CircuitVisualStyle VisualStyle => visualStyle;
        public bool UsesProductionTreatment =>
            visualStyle == CircuitVisualStyle.ProductionPrototype;
        public Color Background => background;
        public Color Board => board;
        public Color InactiveConductor => inactiveConductor;
        public Color PoweredEnergy => poweredEnergy;
        public Color PoweredHotCore => poweredHotCore;
        public Color SecondaryBlue => secondaryBlue;
        public Color PowerSource => powerSource;
        public Color LedObjective => ledObjective;
        public Color Success => success;
        public Color DirectionalAccent => directionalAccent;
        public Color Hint => hint;
        public float ReferenceCanvasSize => referenceCanvasSize;
        public float ConduitWidth => conduitWidth;
        public float PoweredCoreWidth => poweredCoreWidth;
        public float HaloWidth => haloWidth;
        public Sprite HousingSprite => housingSprite;
        public Sprite ConductorSprite => conductorSprite;
        public Sprite SourceSymbolSprite => sourceSymbolSprite;
        public Sprite LedOffSprite => ledOffSprite;
        public Sprite LedOnSprite => ledOnSprite;
        public Sprite DiodeSymbolSprite => diodeSymbolSprite;
        public Sprite SwitchOpenSprite => switchOpenSprite;
        public Sprite SwitchClosedSprite => switchClosedSprite;
        public Sprite AndGateSprite => andGateSprite;
        public Sprite OrGateSprite => orGateSprite;
        public Sprite LockOverlaySprite => lockOverlaySprite;
        public Sprite HintOverlaySprite => hintOverlaySprite;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(themeId) &&
                                    referenceCanvasSize == 256f && conduitWidth > 0f &&
                                    poweredCoreWidth > 0f && haloWidth > 0f;

        public bool HasVisualEntry(TileType tileType)
        {
            return tileType >= TileType.Empty && tileType <= TileType.OrGate;
        }

        public float ToTileUnits(float referencePixels)
        {
            return referencePixels / referenceCanvasSize;
        }

#if UNITY_EDITOR
        public void SetIdentity(string id, string name, CircuitVisualStyle style)
        {
            themeId = id;
            displayName = name;
            visualStyle = style;
        }
#endif
    }

    public static class CircuitVisualThemeCatalog
    {
        public const string TechnicalNeonPrototypeResourcePath =
            "VisualThemes/TechnicalNeonPrototype";
        public const string TechnicalNeonProductionPrototypeResourcePath =
            "VisualThemes/TechnicalNeonProductionPrototype";

        public static CircuitVisualThemeDefinition LoadTechnicalNeonPrototype()
        {
            return Resources.Load<CircuitVisualThemeDefinition>(
                TechnicalNeonPrototypeResourcePath);
        }

        public static CircuitVisualThemeDefinition LoadTechnicalNeonProductionPrototype()
        {
            return Resources.Load<CircuitVisualThemeDefinition>(
                TechnicalNeonProductionPrototypeResourcePath);
        }
    }
}

using UnityEngine;

namespace NeonGrid.Data
{
    [CreateAssetMenu(fileName = "CampaignUiTheme",
        menuName = "Neon Grid/Campaign UI Theme")]
    public sealed class CampaignUiThemeDefinition : ScriptableObject
    {
        [SerializeField] private string themeId;
        [SerializeField] private string displayName;
        [Header("Surfaces")]
        [SerializeField] private Color background;
        [SerializeField] private Color backgroundDepth;
        [SerializeField] private Color panelSurface;
        [SerializeField] private Color insetSurface;
        [SerializeField] private Color panelEdge;
        [Header("Semantic Accents")]
        [SerializeField] private Color primaryAccent;
        [SerializeField] private Color neutralAccent;
        [SerializeField] private Color successAccent;
        [SerializeField] private Color warningAccent;
        [Header("Typography")]
        [SerializeField] private Color titleText;
        [SerializeField] private Color bodyText;
        [SerializeField] private Color subduedText;
        [Header("Buttons")]
        [SerializeField] private Color buttonNormal;
        [SerializeField] private Color buttonHighlighted;
        [SerializeField] private Color buttonPressed;
        [SerializeField] private Color buttonDisabled;
        [Header("Selector States")]
        [SerializeField] private Color lockedSurface;
        [SerializeField] private Color availableSurface;
        [SerializeField] private Color completedSurface;
        [Header("Lightweight Layering")]
        [SerializeField, Min(1f)] private float keylineThickness = 2f;
        [SerializeField, Min(0f)] private float panelInset = 12f;
        [SerializeField, Range(0f, 1f)] private float shadowAlpha = 0.32f;

        public string ThemeId => themeId;
        public string DisplayName => displayName;
        public Color Background => background;
        public Color BackgroundDepth => backgroundDepth;
        public Color PanelSurface => panelSurface;
        public Color InsetSurface => insetSurface;
        public Color PanelEdge => panelEdge;
        public Color PrimaryAccent => primaryAccent;
        public Color NeutralAccent => neutralAccent;
        public Color SuccessAccent => successAccent;
        public Color WarningAccent => warningAccent;
        public Color TitleText => titleText;
        public Color BodyText => bodyText;
        public Color SubduedText => subduedText;
        public Color ButtonNormal => buttonNormal;
        public Color ButtonHighlighted => buttonHighlighted;
        public Color ButtonPressed => buttonPressed;
        public Color ButtonDisabled => buttonDisabled;
        public Color LockedSurface => lockedSurface;
        public Color AvailableSurface => availableSurface;
        public Color CompletedSurface => completedSurface;
        public float KeylineThickness => keylineThickness;
        public float PanelInset => panelInset;
        public float ShadowAlpha => shadowAlpha;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(themeId) &&
                                    !string.IsNullOrWhiteSpace(displayName) &&
                                    background.a > 0f && panelSurface.a > 0f &&
                                    primaryAccent.a > 0f && titleText.a > 0f &&
                                    bodyText.a > 0f && buttonNormal.a > 0f &&
                                    lockedSurface.a > 0f && availableSurface.a > 0f &&
                                    completedSurface.a > 0f && keylineThickness > 0f;

#if UNITY_EDITOR
        public void SetData(string id, string label, Color backgroundColor,
            Color depthColor, Color panelColor, Color insetColor, Color edgeColor,
            Color primary, Color neutral, Color success, Color warning,
            Color title, Color body, Color subdued, Color normalButton,
            Color highlightedButton, Color pressedButton, Color disabledButton,
            Color locked, Color available, Color completed, float edgeThickness,
            float inset, float shadow)
        {
            themeId = id;
            displayName = label;
            background = backgroundColor;
            backgroundDepth = depthColor;
            panelSurface = panelColor;
            insetSurface = insetColor;
            panelEdge = edgeColor;
            primaryAccent = primary;
            neutralAccent = neutral;
            successAccent = success;
            warningAccent = warning;
            titleText = title;
            bodyText = body;
            subduedText = subdued;
            buttonNormal = normalButton;
            buttonHighlighted = highlightedButton;
            buttonPressed = pressedButton;
            buttonDisabled = disabledButton;
            lockedSurface = locked;
            availableSurface = available;
            completedSurface = completed;
            keylineThickness = edgeThickness;
            panelInset = inset;
            shadowAlpha = shadow;
        }
#endif
    }

    public static class CampaignUiThemeCatalog
    {
        public const string TechnicalNeonPrototypeResourcePath =
            "VisualThemes/TechnicalNeonCampaignUiPrototype";

        public static CampaignUiThemeDefinition LoadTechnicalNeonPrototype()
        {
            return Resources.Load<CampaignUiThemeDefinition>(
                TechnicalNeonPrototypeResourcePath);
        }
    }
}

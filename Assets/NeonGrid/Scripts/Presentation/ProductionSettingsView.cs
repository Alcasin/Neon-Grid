using System;
using NeonGrid.Data;
using NeonGrid.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    /// <summary>
    /// Production City Map settings surface. RuntimeUserSettingsController remains the
    /// authoritative state owner; this view only reflects and forwards user interaction.
    /// </summary>
    public sealed class ProductionSettingsView : MonoBehaviour
    {
        private RuntimeUserSettingsController settings;
        private Action<bool> modalVisibilityChanged;
        private CampaignUiThemeDefinition theme;
        private GameObject modalRoot;
        private Button settingsButton;
        private Button closeButton;
        private Slider masterSlider;
        private Slider sfxSlider;
        private Slider ambienceSlider;
        private Text masterPercentage;
        private Text sfxPercentage;
        private Text ambiencePercentage;
        private Toggle hapticsToggle;
        private Text hapticsState;
        private bool initialized;

        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;
        public bool IsEntryVisible => settingsButton != null && settingsButton.gameObject.activeInHierarchy;
        internal Button SettingsButton => settingsButton;
        internal Button CloseButton => closeButton;
        internal Slider MasterSlider => masterSlider;
        internal Slider SfxSlider => sfxSlider;
        internal Slider AmbienceSlider => ambienceSlider;
        internal Toggle HapticsToggle => hapticsToggle;
        internal Text MasterPercentage => masterPercentage;
        internal Text SfxPercentage => sfxPercentage;
        internal Text AmbiencePercentage => ambiencePercentage;
        internal Text HapticsState => hapticsState;
        internal GameObject ModalRoot => modalRoot;

        internal void Build(Transform canvas, Transform mapPanel,
            RuntimeUserSettingsController runtimeSettings, CampaignUiThemeDefinition uiTheme,
            Action<bool> onModalVisibilityChanged)
        {
            if (initialized) return;
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (mapPanel == null) throw new ArgumentNullException(nameof(mapPanel));
            settings = runtimeSettings ?? throw new ArgumentNullException(nameof(runtimeSettings));
            theme = uiTheme;
            modalVisibilityChanged = onModalVisibilityChanged;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            settingsButton = CreateButton(mapPanel, "Settings Button", "SETTINGS", font,
                new Vector2(1f, 1f),
                new Vector2(-ProgrammerUiMetrics.SettingsEntryRightInset,
                    -ProgrammerUiMetrics.SettingsEntryTopInset),
                new Vector2(ProgrammerUiMetrics.SettingsEntryWidth,
                    ProgrammerUiMetrics.SettingsEntryHeight), () => Open());
            settingsButton.gameObject.AddComponent<CityMapCornerSafeAreaLayout>()
                .Initialize(true);

            modalRoot = CreatePanel(canvas, "Settings Modal", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, WithAlpha(Background, 0.84f));
            modalRoot.transform.SetAsLastSibling();
            modalRoot.GetComponent<Image>().raycastTarget = true;

            GameObject panel = CreatePanel(modalRoot.transform, "Settings Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(ProgrammerUiMetrics.SettingsPanelWidth,
                    ProgrammerUiMetrics.SettingsPanelHeight), PanelSurface);
            AddOutline(panel, PanelEdge, 3f);
            RectTransform inset = CreatePanel(panel.transform, "Inset Surface", Vector2.zero,
                Vector2.one, Vector2.zero, Vector2.zero, WithAlpha(InsetSurface, 0.38f))
                .GetComponent<RectTransform>();
            inset.offsetMin = Vector2.one * 14f;
            inset.offsetMax = -Vector2.one * 14f;
            inset.GetComponent<Image>().raycastTarget = false;

            CreateText(panel.transform, "Settings Title", "SETTINGS", font,
                ProgrammerUiMetrics.SettingsTitleFontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 602f), new Vector2(760f, 90f),
                TitleText, FontStyle.Bold);
            CreateText(panel.transform, "Audio Section", "AUDIO", font,
                ProgrammerUiMetrics.SettingsSectionFontSize, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 500f),
                new Vector2(ProgrammerUiMetrics.SettingsContentWidth, 54f),
                PrimaryAccent, FontStyle.Bold);
            CreateKeyline(panel.transform, "Audio Keyline", new Vector2(0f, 460f));

            masterSlider = CreateSliderRow(panel.transform, font, "Master", "MASTER", 380f,
                out masterPercentage);
            sfxSlider = CreateSliderRow(panel.transform, font, "SFX", "SFX", 190f,
                out sfxPercentage);
            ambienceSlider = CreateSliderRow(panel.transform, font, "Ambience", "AMBIENCE", 0f,
                out ambiencePercentage);

            CreateText(panel.transform, "Feedback Section", "FEEDBACK", font,
                ProgrammerUiMetrics.SettingsSectionFontSize, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -190f),
                new Vector2(ProgrammerUiMetrics.SettingsContentWidth, 54f),
                PrimaryAccent, FontStyle.Bold);
            CreateKeyline(panel.transform, "Feedback Keyline", new Vector2(0f, -230f));
            CreateText(panel.transform, "Haptics Label", "HAPTICS", font,
                ProgrammerUiMetrics.SettingsControlLabelFontSize, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0.5f), new Vector2(ControlLabelCenterX, -330f),
                new Vector2(ProgrammerUiMetrics.SettingsControlLabelWidth, 70f),
                BodyText, FontStyle.Bold);
            hapticsToggle = CreateToggle(panel.transform, font,
                new Vector2(ToggleCenterX, -330f),
                out hapticsState);

            closeButton = CreateButton(panel.transform, "Close Button", "CLOSE", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -574f),
                new Vector2(520f, 112f), () => Close());

            masterSlider.onValueChanged.AddListener(OnMasterChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            ambienceSlider.onValueChanged.AddListener(OnAmbienceChanged);
            hapticsToggle.onValueChanged.AddListener(OnHapticsChanged);
            modalRoot.SetActive(false);
            initialized = true;
        }

        public bool Open()
        {
            if (!initialized || IsOpen) return false;
            RefreshFromAuthoritativeSettings();
            modalVisibilityChanged?.Invoke(true);
            modalRoot.SetActive(true);
            modalRoot.transform.SetAsLastSibling();
            return true;
        }

        public bool Close()
        {
            if (!initialized || !IsOpen) return false;
            modalRoot.SetActive(false);
            modalVisibilityChanged?.Invoke(false);
            PersistIfDirty();
            return true;
        }

        internal void RefreshFromAuthoritativeSettings()
        {
            UserSettings current = settings.CurrentSettings;
            masterSlider.SetValueWithoutNotify(current.MasterVolume);
            sfxSlider.SetValueWithoutNotify(current.SfxVolume);
            ambienceSlider.SetValueWithoutNotify(current.AmbienceVolume);
            hapticsToggle.SetIsOnWithoutNotify(current.HapticsEnabled);
            SetPercentage(masterPercentage, current.MasterVolume);
            SetPercentage(sfxPercentage, current.SfxVolume);
            SetPercentage(ambiencePercentage, current.AmbienceVolume);
            RefreshHapticsVisual(current.HapticsEnabled);
        }

        internal bool PersistIfDirty()
        {
            if (settings == null || !settings.HasUnpersistedChanges) return false;
            UserSettingsSaveResult result = settings.PersistCurrentSettings();
            if (!result.Succeeded)
                Debug.LogWarning("Could not save user settings: " + result.Message, this);
            return result.Succeeded;
        }

        private void OnMasterChanged(float value)
        {
            SetPercentage(masterPercentage, value);
            settings.SetMasterVolume(value);
        }

        private void OnSfxChanged(float value)
        {
            SetPercentage(sfxPercentage, value);
            settings.SetSfxVolume(value);
        }

        private void OnAmbienceChanged(float value)
        {
            SetPercentage(ambiencePercentage, value);
            settings.SetAmbienceVolume(value);
        }

        private void OnHapticsChanged(bool enabled)
        {
            settings.SetHapticsEnabled(enabled);
            RefreshHapticsVisual(enabled);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PersistIfDirty();
        }

        private void OnApplicationQuit()
        {
            PersistIfDirty();
        }

        private Slider CreateSliderRow(Transform parent, Font font, string name, string caption,
            float headerY, out Text percentage)
        {
            CreateText(parent, name + " Label", caption, font,
                ProgrammerUiMetrics.SettingsControlLabelFontSize, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0.5f), new Vector2(ControlLabelCenterX,
                    headerY + ProgrammerUiMetrics.SettingsAudioLabelOffsetY),
                new Vector2(ProgrammerUiMetrics.SettingsControlLabelWidth,
                    ProgrammerUiMetrics.SettingsAudioHeaderHeight), BodyText, FontStyle.Bold);
            percentage = CreateText(parent, name + " Percentage", "100%", font,
                ProgrammerUiMetrics.SettingsPercentageFontSize, TextAnchor.MiddleRight,
                new Vector2(0.5f, 0.5f), new Vector2(PercentageCenterX, headerY),
                new Vector2(ProgrammerUiMetrics.SettingsPercentageWidth,
                    ProgrammerUiMetrics.SettingsAudioHeaderHeight), PrimaryAccent,
                FontStyle.Bold);

            GameObject sliderObject = CreatePanel(parent, name + " Slider",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, headerY - ProgrammerUiMetrics.SettingsAudioSliderOffsetY),
                new Vector2(ProgrammerUiMetrics.SettingsSliderWidth,
                    ProgrammerUiMetrics.SettingsSliderTouchHeight), Color.clear);
            sliderObject.GetComponent<Image>().raycastTarget = true;
            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;

            RectTransform background = CreatePanel(sliderObject.transform, "Background",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
                new Vector2(-ProgrammerUiMetrics.SettingsSliderThumbSize,
                    ProgrammerUiMetrics.SettingsSliderTrackHeight), NeutralAccent)
                .GetComponent<RectTransform>();
            background.offsetMin = new Vector2(ProgrammerUiMetrics.SettingsSliderThumbSize * .5f,
                -ProgrammerUiMetrics.SettingsSliderTrackHeight * .5f);
            background.offsetMax = new Vector2(-ProgrammerUiMetrics.SettingsSliderThumbSize * .5f,
                ProgrammerUiMetrics.SettingsSliderTrackHeight * .5f);

            GameObject fillArea = CreateRect(sliderObject.transform, "Fill Area",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(ProgrammerUiMetrics.SettingsSliderThumbSize * .5f,
                    -ProgrammerUiMetrics.SettingsSliderTrackHeight * .5f),
                new Vector2(-ProgrammerUiMetrics.SettingsSliderThumbSize * .5f,
                    ProgrammerUiMetrics.SettingsSliderTrackHeight * .5f));
            Image fill = CreatePanel(fillArea.transform, "Fill", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, PrimaryAccent).GetComponent<Image>();
            fill.raycastTarget = false;

            GameObject handleArea = CreateRect(sliderObject.transform, "Handle Slide Area",
                Vector2.zero, Vector2.one,
                new Vector2(ProgrammerUiMetrics.SettingsSliderThumbSize * .5f, 0f),
                new Vector2(-ProgrammerUiMetrics.SettingsSliderThumbSize * .5f, 0f));
            Image handle = CreatePanel(handleArea.transform, "Handle",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(ProgrammerUiMetrics.SettingsSliderThumbSize,
                    ProgrammerUiMetrics.SettingsSliderThumbHeight), HotCore)
                .GetComponent<Image>();
            AddOutline(handle.gameObject, PrimaryAccent, 2f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            ColorBlock colors = slider.colors;
            colors.normalColor = HotCore;
            colors.highlightedColor = Color.white;
            colors.pressedColor = PrimaryAccent;
            colors.selectedColor = HotCore;
            slider.colors = colors;
            return slider;
        }

        private Toggle CreateToggle(Transform parent, Font font, Vector2 position,
            out Text stateLabel)
        {
            GameObject toggleObject = CreatePanel(parent, "Haptics Toggle",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
                new Vector2(ProgrammerUiMetrics.SettingsToggleWidth,
                    ProgrammerUiMetrics.SettingsToggleHeight), ButtonNormal);
            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = toggleObject.GetComponent<Image>();
            Image onIndicator = CreatePanel(toggleObject.transform, "On Indicator",
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 0f),
                new Vector2(12f, -20f), SuccessAccent).GetComponent<Image>();
            onIndicator.rectTransform.pivot = new Vector2(0f, 0.5f);
            onIndicator.raycastTarget = false;
            toggle.graphic = onIndicator;
            stateLabel = CreateText(toggleObject.transform, "State", "ON", font,
                ProgrammerUiMetrics.SettingsToggleFontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TitleText,
                FontStyle.Bold);
            stateLabel.rectTransform.anchorMin = Vector2.zero;
            stateLabel.rectTransform.anchorMax = Vector2.one;
            stateLabel.rectTransform.offsetMin = Vector2.zero;
            stateLabel.rectTransform.offsetMax = Vector2.zero;
            AddOutline(toggleObject, PanelEdge, 2f);
            return toggle;
        }

        private Button CreateButton(Transform parent, string name, string caption, Font font,
            Vector2 anchor, Vector2 position, Vector2 size, Action action)
        {
            GameObject buttonObject = CreatePanel(parent, name, anchor, anchor, position, size,
                ButtonNormal);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlighted;
            colors.selectedColor = ButtonHighlighted;
            colors.pressedColor = ButtonPressed;
            button.colors = colors;
            button.onClick.AddListener(() => action?.Invoke());
            Text label = CreateText(buttonObject.transform, "Label", caption, font,
                ProgrammerUiMetrics.SettingsButtonFontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TitleText,
                FontStyle.Bold);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            AddOutline(buttonObject, PanelEdge, 2f);
            return button;
        }

        private void CreateKeyline(Transform parent, string name, Vector2 position)
        {
            Image line = CreatePanel(parent, name, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position,
                new Vector2(ProgrammerUiMetrics.SettingsContentWidth, 3f),
                WithAlpha(PrimaryAccent, .72f)).GetComponent<Image>();
            line.raycastTarget = false;
        }

        private void RefreshHapticsVisual(bool enabled)
        {
            hapticsState.text = enabled ? "ON" : "OFF";
            hapticsToggle.targetGraphic.color = enabled
                ? Color.Lerp(ButtonNormal, SuccessAccent, .28f)
                : ButtonNormal;
            hapticsState.color = enabled ? SuccessAccent : SubduedText;
        }

        internal static string FormatPercentage(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        private static float ContentLeft => -ProgrammerUiMetrics.SettingsContentWidth * .5f;
        private static float ContentRight => ProgrammerUiMetrics.SettingsContentWidth * .5f;
        private static float ControlLabelCenterX =>
            ContentLeft + ProgrammerUiMetrics.SettingsControlLabelWidth * .5f;
        private static float PercentageCenterX =>
            ContentRight - ProgrammerUiMetrics.SettingsPercentageWidth * .5f;
        private static float ToggleCenterX =>
            ContentRight - ProgrammerUiMetrics.SettingsToggleWidth * .5f;

        private static void SetPercentage(Text text, float value)
        {
            text.text = FormatPercentage(value);
        }

        private static GameObject CreateRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            RectTransform rect = result.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return result;
        }

        private static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            result.transform.SetParent(parent, false);
            RectTransform rect = result.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
            {
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            result.GetComponent<Image>().color = color;
            return result;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font,
            int size, TextAnchor alignment, Vector2 anchor, Vector2 position,
            Vector2 dimensions, Color color, FontStyle style = FontStyle.Normal)
        {
            var result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Text));
            result.transform.SetParent(parent, false);
            RectTransform rect = result.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            Text text = result.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static Outline AddOutline(GameObject target, Color color, float thickness)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = true;
            return outline;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private Color Background => theme != null ? theme.Background : new Color(.02f, .03f, .06f);
        private Color PanelSurface => theme != null ? theme.PanelSurface : new Color(.04f, .07f, .13f);
        private Color InsetSurface => theme != null ? theme.InsetSurface : new Color(.05f, .1f, .17f);
        private Color PanelEdge => theme != null ? theme.PanelEdge : new Color(.09f, .86f, .79f);
        private Color PrimaryAccent => theme != null ? theme.PrimaryAccent : new Color(.09f, .86f, .79f);
        private Color NeutralAccent => theme != null ? theme.NeutralAccent : new Color(.15f, .2f, .29f);
        private Color SuccessAccent => theme != null ? theme.SuccessAccent : new Color(.13f, .87f, .65f);
        private Color TitleText => theme != null ? theme.TitleText : new Color(.75f, 1f, .98f);
        private Color BodyText => theme != null ? theme.BodyText : Color.white;
        private Color SubduedText => theme != null ? theme.SubduedText : new Color(.55f, .65f, .72f);
        private Color ButtonNormal => theme != null ? theme.ButtonNormal : new Color(.08f, .3f, .46f);
        private Color ButtonHighlighted => theme != null ? theme.ButtonHighlighted : new Color(.1f, .45f, .6f);
        private Color ButtonPressed => theme != null ? theme.ButtonPressed : new Color(.07f, .22f, .35f);
        private static Color HotCore => new Color(0.75f, 1f, 0.98f, 1f);
    }

    internal sealed class CityMapCornerSafeAreaLayout : MonoBehaviour
    {
        private RectTransform target;
        private Vector2 baselinePosition;
        private bool alignRight;

        internal void Initialize(bool rightAligned)
        {
            target = GetComponent<RectTransform>();
            baselinePosition = target.anchoredPosition;
            alignRight = rightAligned;
            ApplyCurrentSafeArea();
        }

        internal void ApplyLayout(Rect safeArea, float screenWidth, float screenHeight,
            float canvasScaleFactor)
        {
            if (target == null) return;
            float scale = Mathf.Max(.0001f, canvasScaleFactor);
            float topInset = Mathf.Max(0f, screenHeight - safeArea.yMax) / scale;
            float sideInset = alignRight
                ? Mathf.Max(0f, screenWidth - safeArea.xMax) / scale
                : Mathf.Max(0f, safeArea.xMin) / scale;
            target.anchoredPosition = baselinePosition +
                                      new Vector2(alignRight ? -sideInset : sideInset, -topInset);
        }

        private void OnEnable()
        {
            ApplyCurrentSafeArea();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyCurrentSafeArea();
        }

        private void ApplyCurrentSafeArea()
        {
            if (target == null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            ApplyLayout(Screen.safeArea, Screen.width, Screen.height,
                canvas != null ? canvas.scaleFactor : 1f);
        }
    }
}

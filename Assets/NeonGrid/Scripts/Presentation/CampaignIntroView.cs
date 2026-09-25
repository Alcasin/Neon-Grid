using System;
using NeonGrid.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class CampaignIntroView : MonoBehaviour
    {
        internal const float ReferenceWidth = 1080f;
        internal const float ReferenceHeight = 1920f;
        internal const float HeaderCenterY = -118f;
        internal const float TitleCenterY = 300f;
        internal const float BodyCenterY = 25f;
        internal const float IndicatorCenterY = -290f;
        internal const float ButtonCenterY = 135f;

        private static readonly Color Background = new Color(0.008f, 0.012f, 0.03f, 1f);
        private static readonly Color Cyan = new Color(0.08f, 0.68f, 0.86f, 1f);
        private static readonly Color Secondary = new Color(0.08f, 0.3f, 0.46f, 1f);

        private CampaignIntroSequence sequence;
        private Func<bool> requestCompletion;
        private GameObject canvasObject;
        private Text titleText;
        private Text bodyText;
        private Text indicatorText;
        private Text primaryLabel;
        private Button primaryButton;
        private Button skipButton;
        private bool transitionInProgress;

        public int CurrentPageIndex => sequence?.CurrentPageIndex ?? -1;
        public int PageCount => sequence?.PageCount ?? 0;
        public bool IsVisible => canvasObject != null && canvasObject.activeSelf;
        public bool IsInteractionEnabled => !transitionInProgress && primaryButton != null &&
                                            primaryButton.interactable && skipButton.interactable;
        internal Text TitleText => titleText;
        internal Text BodyText => bodyText;
        internal Text IndicatorText => indicatorText;
        internal Button PrimaryButton => primaryButton;
        internal Button SkipButton => skipButton;

        public void Build(CampaignNarrativeDefinition narrative, Func<bool> onRequestCompletion,
            CampaignUiThemeDefinition theme = null)
        {
            sequence = new CampaignIntroSequence(narrative);
            requestCompletion = onRequestCompletion ??
                                throw new ArgumentNullException(nameof(onRequestCompletion));
            EnsureEventSystem();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("Campaign Intro Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            canvasObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            CreatePanel(canvasObject.transform, "Background", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Background);
            Text header = CreateText(canvasObject.transform, "Terminal Header",
                "NEON GRID  /  SYSTEM TERMINAL", font, 32, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, HeaderCenterY), new Vector2(940f, 80f));
            header.color = Cyan;
            CreatePanel(canvasObject.transform, "Header Divider",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -190f), new Vector2(820f, 3f), Cyan);

            titleText = CreateText(canvasObject.transform, "Page Title", string.Empty, font, 62,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, TitleCenterY), new Vector2(940f, 180f));
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Cyan;
            bodyText = CreateText(canvasObject.transform, "Page Body", string.Empty, font, 38,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, BodyCenterY), new Vector2(900f, 360f));
            bodyText.lineSpacing = 1.25f;
            indicatorText = CreateText(canvasObject.transform, "Page Indicator", string.Empty, font,
                26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, IndicatorCenterY),
                new Vector2(400f, 60f));
            indicatorText.color = new Color(0.62f, 0.72f, 0.8f, 1f);

            skipButton = CreateButton(canvasObject.transform, "Skip", "SKIP", font,
                new Vector2(0.25f, 0f), new Vector2(0.25f, 0f),
                new Vector2(0f, ButtonCenterY), new Vector2(400f, 104f), PressSkip, Secondary);
            primaryButton = CreateButton(canvasObject.transform, "Primary Action", string.Empty, font,
                new Vector2(0.75f, 0f), new Vector2(0.75f, 0f),
                new Vector2(0f, ButtonCenterY), new Vector2(400f, 104f), PressPrimary, Cyan);
            primaryLabel = primaryButton.transform.Find("Label").GetComponent<Text>();
            PresentCurrentPage();
            if (theme != null)
                CampaignUiThemeApplicator.Apply(canvasObject, theme,
                    CampaignUiPreviewMode.Intro);
        }

        public void PressPrimary()
        {
            if (transitionInProgress || sequence == null) return;
            if (sequence.MoveNext())
            {
                PresentCurrentPage();
                return;
            }
            TryComplete();
        }

        public void PressSkip()
        {
            if (transitionInProgress || sequence == null) return;
            TryComplete();
        }

        public void SetVisible(bool visible)
        {
            if (canvasObject != null) canvasObject.SetActive(visible);
        }

        private void PresentCurrentPage()
        {
            CampaignIntroPage page = sequence.CurrentPage;
            titleText.text = page.Title;
            bodyText.text = page.Body;
            primaryLabel.text = page.PrimaryAction;
            indicatorText.text = $"{sequence.CurrentPageIndex + 1} / {sequence.PageCount}";
        }

        private void TryComplete()
        {
            transitionInProgress = true;
            SetInteractionEnabled(false);
            bool succeeded = false;
            try
            {
                succeeded = requestCompletion();
            }
            finally
            {
                if (!succeeded)
                {
                    transitionInProgress = false;
                    SetInteractionEnabled(true);
                }
            }
        }

        private void SetInteractionEnabled(bool enabled)
        {
            primaryButton.interactable = enabled;
            skipButton.interactable = enabled;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystemObject = new GameObject("Campaign Intro Event System");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            else
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0f;
            return panel;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font,
            int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string caption, Font font,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Action action,
            Color color)
        {
            GameObject buttonObject = CreatePanel(parent, name, anchorMin, anchorMax, position, size,
                color);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(() => action());
            Text label = CreateText(buttonObject.transform, "Label", caption, font, 34,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }
    }
}

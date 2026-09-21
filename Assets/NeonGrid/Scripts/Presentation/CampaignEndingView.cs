using System;
using NeonGrid.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class CampaignEndingView : MonoBehaviour
    {
        internal const float ReferenceWidth = 1080f;
        internal const float ReferenceHeight = 1920f;

        private static readonly Color Background = new Color(0.008f, 0.012f, 0.03f, 1f);
        private static readonly Color Cyan = new Color(0.08f, 0.68f, 0.86f, 1f);
        private static readonly Color Success = new Color(0.18f, 0.86f, 0.68f, 1f);

        private Func<bool> requestCompletion;
        private GameObject canvasObject;
        private Text titleText;
        private Text bodyText;
        private Text statusTitleText;
        private Text statusBodyText;
        private Text primaryLabel;
        private Button primaryButton;
        private bool transitionInProgress;

        public bool IsVisible => canvasObject != null && canvasObject.activeSelf;
        public bool IsInteractionEnabled => !transitionInProgress && primaryButton != null &&
                                            primaryButton.interactable;
        internal Text TitleText => titleText;
        internal Text BodyText => bodyText;
        internal Text StatusTitleText => statusTitleText;
        internal Text StatusBodyText => statusBodyText;
        internal Text PrimaryLabel => primaryLabel;
        internal Button PrimaryButton => primaryButton;

        public void Build(CampaignEndingNarrative narrative, Func<bool> onRequestCompletion)
        {
            if (narrative == null || !narrative.IsConfigured)
                throw new ArgumentException("A configured ending narrative is required.",
                    nameof(narrative));
            requestCompletion = onRequestCompletion ??
                                throw new ArgumentNullException(nameof(onRequestCompletion));
            EnsureEventSystem();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("Campaign Ending Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            canvasObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            CreatePanel(canvasObject.transform, "Background", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Background);
            Text header = CreateText(canvasObject.transform, "Terminal Header",
                "NEON GRID  /  SYSTEM TERMINAL", font,
                ProgrammerUiMetrics.EndingHeaderFontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f),
                new Vector2(940f, 80f));
            header.color = Cyan;
            CreatePanel(canvasObject.transform, "Header Divider", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(820f, 3f), Cyan);

            titleText = CreateText(canvasObject.transform, "Ending Title", narrative.Title, font,
                ProgrammerUiMetrics.EndingTitleFontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f),
                new Vector2(940f, 160f));
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Success;
            bodyText = CreateText(canvasObject.transform, "Ending Body", narrative.Body, font,
                ProgrammerUiMetrics.EndingBodyFontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f),
                new Vector2(900f, 150f));
            bodyText.lineSpacing = 1.2f;

            CreatePanel(canvasObject.transform, "Status Divider", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(620f, 3f), Success);
            statusTitleText = CreateText(canvasObject.transform, "Status Title",
                narrative.StatusTitle, font, ProgrammerUiMetrics.EndingStatusTitleFontSize,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -90f), new Vector2(900f, 90f));
            statusTitleText.fontStyle = FontStyle.Bold;
            statusTitleText.color = Success;
            statusBodyText = CreateText(canvasObject.transform, "Status Body",
                narrative.StatusBody, font, ProgrammerUiMetrics.EndingStatusBodyFontSize,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -195f), new Vector2(900f, 80f));

            primaryButton = CreateButton(canvasObject.transform, "Return To City",
                narrative.ReturnButtonLabel, font, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 170f), new Vector2(620f, 120f), PressReturnToCity, Success);
            primaryLabel = primaryButton.transform.Find("Label").GetComponent<Text>();
        }

        public void PressReturnToCity()
        {
            if (transitionInProgress || primaryButton == null) return;
            transitionInProgress = true;
            primaryButton.interactable = false;
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
                    primaryButton.interactable = true;
                }
            }
        }

        public void SetVisible(bool visible)
        {
            if (canvasObject != null) canvasObject.SetActive(visible);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystemObject = new GameObject("Campaign Ending Event System");
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
            Text label = CreateText(buttonObject.transform, "Label", caption, font,
                ProgrammerUiMetrics.EndingButtonFontSize, TextAnchor.MiddleCenter, Vector2.zero,
                Vector2.one, Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }
    }
}

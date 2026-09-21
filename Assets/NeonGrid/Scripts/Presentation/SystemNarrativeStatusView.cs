using NeonGrid.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class SystemNarrativeStatusView : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.025f, 0.07f, 0.12f, 0.96f);
        private static readonly Color AccentColor = new Color(0.08f, 0.68f, 0.86f, 1f);

        private GameObject panel;
        private Text titleText;
        private Text bodyText;
        private CanvasGroup canvasGroup;

        public bool IsVisible => panel != null && panel.activeSelf;
        public string CurrentChapterId { get; private set; }
        internal float Opacity => canvasGroup != null ? canvasGroup.alpha : 0f;
        internal RectTransform PanelRect => panel?.GetComponent<RectTransform>();
        internal Text TitleText => titleText;
        internal Text BodyText => bodyText;

        public void Build(Transform parent, Font font)
        {
            panel = new GameObject("Restoration Narrative Status", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f,
                -ProgrammerUiMetrics.RestorationStatusTopInset);
            rect.sizeDelta = new Vector2(ProgrammerUiMetrics.RestorationStatusWidth,
                ProgrammerUiMetrics.RestorationStatusHeight);
            Image image = panel.GetComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;
            canvasGroup = panel.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Text label = CreateText(rect, "System Status Label", "SYSTEM STATUS", font,
                ProgrammerUiMetrics.RestorationStatusLabelFontSize, TextAnchor.MiddleLeft,
                new Vector2(28f, -12f), new Vector2(824f, 32f));
            label.color = AccentColor;
            titleText = CreateText(rect, "Restoration Title", string.Empty, font,
                ProgrammerUiMetrics.RestorationStatusTitleFontSize, TextAnchor.MiddleLeft,
                new Vector2(28f, -50f), new Vector2(824f, 46f));
            titleText.fontStyle = FontStyle.Bold;
            bodyText = CreateText(rect, "Restoration Body", string.Empty, font,
                ProgrammerUiMetrics.RestorationStatusBodyFontSize, TextAnchor.UpperLeft,
                new Vector2(28f, -102f), new Vector2(824f, 70f));
            bodyText.lineSpacing = 0.86f;
            Hide();
        }

        public bool Show(CampaignChapterNarrativeEntry entry)
        {
            if (entry == null || !entry.HasRestorationStatus)
            {
                Hide();
                return false;
            }

            CurrentChapterId = entry.ChapterId;
            titleText.text = entry.RestoredTitle;
            bodyText.text = entry.RestoredBody;
            SetOpacity(1f);
            panel.SetActive(true);
            return true;
        }

        public void Hide()
        {
            CurrentChapterId = null;
            if (titleText != null) titleText.text = string.Empty;
            if (bodyText != null) bodyText.text = string.Empty;
            SetOpacity(1f);
            if (panel != null) panel.SetActive(false);
        }

        public void SetOpacity(float opacity)
        {
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(opacity);
        }

        private static Text CreateText(Transform parent, string name, string value, Font font,
            int fontSize, TextAnchor alignment, Vector2 topLeft, Vector2 size)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
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
    }
}

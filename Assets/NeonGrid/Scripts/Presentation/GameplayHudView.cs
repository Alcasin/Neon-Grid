using System;
using NeonGrid.Session;
using NeonGrid.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class GameplayHudView : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.025f, 0.035f, 0.09f, 0.94f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.18f, 0.35f, 0.96f);
        private static readonly Color AccentColor = new Color(0.05f, 0.9f, 1f);

        private Text moveText;
        private Text timerText;
        private Text hintText;
        private Text completionText;
        private Button undoButton;
        private Button restartButton;
        private Button hintButton;
        private GameObject completionPanel;

        public void Build(Action undo, Action restart, Action requestHint)
        {
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("Gameplay HUD Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            moveText = CreateText(canvasObject.transform, "Move Count", font, 36, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(360f, 70f));
            moveText.rectTransform.pivot = new Vector2(0f, 1f);
            moveText.rectTransform.anchoredPosition = new Vector2(32f, -32f);
            timerText = CreateText(canvasObject.transform, "Timer", font, 36, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(300f, 70f));
            timerText.rectTransform.pivot = new Vector2(0.5f, 1f);
            timerText.rectTransform.anchoredPosition = new Vector2(0f, -32f);
            hintText = CreateText(canvasObject.transform, "Hint Status", font, 28, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 152f), new Vector2(900f, 90f));

            undoButton = CreateButton(canvasObject.transform, "Undo Button", "UNDO", font,
                new Vector2(0.15f, 0f), new Vector2(0.15f, 0f), new Vector2(0f, 52f), undo);
            restartButton = CreateButton(canvasObject.transform, "Restart Button", "RESTART", font,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), restart);
            hintButton = CreateButton(canvasObject.transform, "Hint Button", "HINT", font,
                new Vector2(0.85f, 0f), new Vector2(0.85f, 0f), new Vector2(0f, 52f), requestHint);

            completionPanel = CreatePanel(canvasObject.transform, "Completion Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 520f));
            completionText = CreateText(completionPanel.transform, "Completion Text", font, 42,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            completionText.rectTransform.offsetMin = new Vector2(32f, 32f);
            completionText.rectTransform.offsetMax = new Vector2(-32f, -32f);
            completionPanel.SetActive(false);
        }

        public void Refresh(GameplaySession session)
        {
            if (session == null || moveText == null) return;

            moveText.text = $"Moves: {session.MoveCount}";
            timerText.text = $"Time: {FormatTime(session.ElapsedSeconds)}";
            undoButton.interactable = session.CanUndo;
            restartButton.interactable = true;
            hintButton.interactable = session.HintAvailability == HintStatus.HintAvailable;
            hintText.text = GetHintText(session);

            SessionCompletionResult result = session.CompletionResult;
            completionPanel.SetActive(result != null);
            if (result != null)
                completionText.text = FormatCompletion(result);
        }

        private static string GetHintText(GameplaySession session)
        {
            HintResult hint = session.LastHint;
            if (hint.Status == HintStatus.HintAvailable && hint.SuggestedAction.HasValue)
                return FormatAction(hint.SuggestedAction.Value);
            if (hint.Status == HintStatus.HintAvailable)
                return "Hint available";
            if (hint.Status == HintStatus.SolverLimitReached)
                return "Hint unavailable";
            if (hint.Status == HintStatus.UnsolvableOrInvalid)
                return "Hint unavailable: no valid solution";
            if (hint.Status == HintStatus.NoHintNeeded)
                return "No hint needed";

            int remaining = Mathf.Max(0, Mathf.CeilToInt(GameplaySession.HintUnlockSeconds - session.ElapsedSeconds));
            return $"Hint locked ({FormatTime(remaining)} remaining)";
        }

        private static string FormatAction(PuzzleAction action)
        {
            string verb = action.ActionType == PuzzleActionType.ToggleSwitch ? "Toggle" : "Rotate";
            return $"Hint: {verb} tile ({action.Position.x + 1},{action.Position.y + 1})";
        }

        private static string FormatCompletion(SessionCompletionResult result)
        {
            string optimal = result.OptimalMoves.HasValue ? result.OptimalMoves.Value.ToString() : "Unknown";
            string stars = result.StarRating.Status == StarEvaluationStatus.Rated
                ? result.StarRating.Stars.ToString()
                : "Unknown";
            return $"LEVEL COMPLETE\n\nMoves: {result.TotalMoves}\nOptimal: {optimal}\n" +
                   $"Time: {FormatTime(result.ElapsedSeconds)}\nStars: {stars}";
        }

        private static string FormatTime(float seconds)
        {
            int wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystemObject = new GameObject("Gameplay Event System");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = PanelColor;
            return panel;
        }

        private static Text CreateText(Transform parent, string name, Font font, int fontSize,
            TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string caption, Font font,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Action command)
        {
            GameObject buttonObject = CreatePanel(parent, name, anchorMin, anchorMax, anchoredPosition,
                new Vector2(280f, 100f));
            Image image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = AccentColor;
            colors.selectedColor = AccentColor;
            colors.disabledColor = new Color(0.12f, 0.13f, 0.18f, 0.75f);
            button.colors = colors;
            button.onClick.AddListener(() => command?.Invoke());

            Text text = CreateText(buttonObject.transform, "Label", font, 32, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.text = caption;
            return button;
        }
    }
}

using System;
using System.Collections.Generic;
using NeonGrid.Campaign;
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
        private Button backButton;
        private Button retryButton;
        private Button levelsButton;
        private Button mapButton;
        private Button nextButton;
        private GameObject completionPanel;
        private GameObject leaveConfirmationPanel;
        private GameplayResultActions resultActions;
        private GameplaySession displayedSession;

        public bool IsLeaveConfirmationOpen => leaveConfirmationPanel != null &&
                                               leaveConfirmationPanel.activeSelf;

        public void Build(Action undo, Action restart, Action requestHint)
        {
            Build(undo, restart, requestHint, null);
        }

        public void Build(Action undo, Action restart, Action requestHint,
            GameplayResultActions resultActions)
        {
            this.resultActions = resultActions;
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
            moveText = CreateText(canvasObject.transform, "Move Count", font, 36, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(300f, 70f));
            moveText.rectTransform.pivot = new Vector2(1f, 1f);
            moveText.rectTransform.anchoredPosition = new Vector2(-32f, -32f);
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

            if (resultActions != null)
                backButton = CreateButton(canvasObject.transform, "Back To Levels Button", "< LEVELS", font,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f),
                    RequestLeave, new Vector2(240f, 76f));
            if (backButton != null)
                backButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);

            completionPanel = CreatePanel(canvasObject.transform, "Completion Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                resultActions == null ? new Vector2(720f, 520f) : new Vector2(720f, 650f));
            completionText = CreateText(completionPanel.transform, "Completion Text", font, 42,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            completionText.rectTransform.offsetMin = new Vector2(32f, resultActions == null ? 32f : 150f);
            completionText.rectTransform.offsetMax = new Vector2(-32f, -32f);

            if (resultActions != null)
            {
                retryButton = CreateButton(completionPanel.transform, "Retry Button", "RETRY", font,
                    new Vector2(0.2f, 0f), new Vector2(0.2f, 0f), new Vector2(0f, 62f),
                    resultActions.Retry, new Vector2(170f, 82f));
                levelsButton = CreateButton(completionPanel.transform, "Levels Button", "LEVELS", font,
                    new Vector2(0.4f, 0f), new Vector2(0.4f, 0f), new Vector2(0f, 62f),
                    resultActions.Levels, new Vector2(170f, 82f));
                mapButton = CreateButton(completionPanel.transform, "Map Button", "MAP", font,
                    new Vector2(0.6f, 0f), new Vector2(0.6f, 0f), new Vector2(0f, 62f),
                    resultActions.Map, new Vector2(170f, 82f));
                nextButton = CreateButton(completionPanel.transform, "Next Button", "NEXT", font,
                    new Vector2(0.8f, 0f), new Vector2(0.8f, 0f), new Vector2(0f, 62f),
                    resultActions.Next, new Vector2(170f, 82f));

                leaveConfirmationPanel = CreatePanel(canvasObject.transform, "Leave Confirmation",
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                CreateText(leaveConfirmationPanel.transform, "Message", font, 42,
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 100f), new Vector2(900f, 240f)).text =
                    "Leave this level?\nCurrent attempt progress will be lost.";
                CreateButton(leaveConfirmationPanel.transform, "Cancel Button", "CANCEL", font,
                    new Vector2(0.32f, 0.5f), new Vector2(0.32f, 0.5f), new Vector2(0f, -100f),
                    CancelLeave, new Vector2(260f, 100f));
                CreateButton(leaveConfirmationPanel.transform, "Leave Button", "LEAVE", font,
                    new Vector2(0.68f, 0.5f), new Vector2(0.68f, 0.5f), new Vector2(0f, -100f),
                    ConfirmLeave, new Vector2(260f, 100f));
                leaveConfirmationPanel.SetActive(false);
            }
            completionPanel.SetActive(false);
        }

        public void Refresh(GameplaySession session)
        {
            if (session == null || moveText == null) return;
            displayedSession = session;

            moveText.text = $"Moves: {session.MoveCount}";
            timerText.text = $"Time: {FormatTime(session.ElapsedSeconds)}";
            undoButton.interactable = session.CanUndo;
            restartButton.interactable = true;
            hintButton.interactable = session.HintAvailability == HintStatus.HintAvailable;
            hintText.text = GetHintText(session);

            SessionCompletionResult result = session.CompletionResult;
            completionPanel.SetActive(result != null);
            if (backButton != null)
                backButton.gameObject.SetActive(result == null);
            if (result != null)
            {
                completionText.text = FormatCompletion(result);
                if (leaveConfirmationPanel != null)
                    leaveConfirmationPanel.SetActive(false);
                if (resultActions != null)
                    RefreshResultNavigation();
            }
        }

        private void RefreshResultNavigation()
        {
            CampaignResultNavigationState navigation = resultActions.GetNavigation != null
                ? resultActions.GetNavigation()
                : default;
            retryButton.gameObject.SetActive(navigation.ShowRetry);
            levelsButton.gameObject.SetActive(navigation.ShowLevels);
            mapButton.gameObject.SetActive(navigation.ShowMap);
            nextButton.gameObject.SetActive(navigation.ShowNext);
            nextButton.interactable = navigation.ShowNext;

            var visibleButtons = new List<Button>();
            if (navigation.ShowRetry) visibleButtons.Add(retryButton);
            if (navigation.ShowLevels) visibleButtons.Add(levelsButton);
            if (navigation.ShowMap) visibleButtons.Add(mapButton);
            if (navigation.ShowNext) visibleButtons.Add(nextButton);
            for (int index = 0; index < visibleButtons.Count; index++)
            {
                float anchorX = (index + 1f) / (visibleButtons.Count + 1f);
                RectTransform rect = visibleButtons[index].GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(anchorX, 0f);
                rect.anchorMax = new Vector2(anchorX, 0f);
                rect.anchoredPosition = new Vector2(0f, 62f);
            }
        }

        private void RequestLeave()
        {
            if (displayedSession == null || displayedSession.IsCompleted || leaveConfirmationPanel == null)
                return;
            leaveConfirmationPanel.SetActive(true);
            leaveConfirmationPanel.transform.SetAsLastSibling();
        }

        private void CancelLeave()
        {
            leaveConfirmationPanel?.SetActive(false);
        }

        private void ConfirmLeave()
        {
            if (displayedSession == null || displayedSession.IsCompleted) return;
            leaveConfirmationPanel.SetActive(false);
            resultActions.Leave?.Invoke();
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
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Action command,
            Vector2? size = null)
        {
            GameObject buttonObject = CreatePanel(parent, name, anchorMin, anchorMax, anchoredPosition,
                size ?? new Vector2(280f, 100f));
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

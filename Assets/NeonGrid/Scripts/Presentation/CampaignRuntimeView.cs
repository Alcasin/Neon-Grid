using System;
using System.Collections;
using System.Collections.Generic;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class CampaignRuntimeView : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.008f, 0.012f, 0.03f, 1f);
        private static readonly Color Panel = new Color(0.025f, 0.04f, 0.11f, 0.96f);
        private static readonly Color Available = new Color(0.08f, 0.3f, 0.46f, 1f);
        private static readonly Color Locked = new Color(0.12f, 0.13f, 0.18f, 0.9f);
        private static readonly Color Restored = new Color(0.05f, 0.55f, 0.4f, 1f);

        private readonly Dictionary<string, ChapterButton> chapterButtons =
            new Dictionary<string, ChapterButton>(StringComparer.Ordinal);
        private CampaignDefinition campaign;
        private CampaignProgressService progress;
        private Action<string> openChapter;
        private Action<string> startLevel;
        private GameObject canvasObject;
        private GameObject mapPanel;
        private GameObject levelPanel;
        private GameObject restorationOverlay;
        private Text totalStarsText;
        private Text restorationText;
        private Coroutine restorationRoutine;

        public void Build(CampaignDefinition definition, CampaignProgressService progressService,
            Action<string> onOpenChapter, Action<string> onStartLevel, Action onBackToMap)
        {
            campaign = definition;
            progress = progressService;
            openChapter = onOpenChapter;
            startLevel = onStartLevel;
            EnsureEventSystem();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("Campaign Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            CreatePanel(canvasObject.transform, "Background", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Background);

            mapPanel = CreatePanel(canvasObject.transform, "Campaign Map", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Color.clear);
            CreateText(mapPanel.transform, "Title", "NEON GRID - TEST CITY", font, 54,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(900f, 100f));
            totalStarsText = CreateText(mapPanel.transform, "Total Stars", string.Empty, font, 38,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -170f), new Vector2(700f, 80f));

            for (int index = 0; index < campaign.Chapters.Count; index++)
            {
                CampaignChapterDefinition chapter = campaign.Chapters[index];
                string capturedId = chapter.ChapterId;
                Button button = CreateButton(mapPanel.transform, $"Chapter {index + 1}", string.Empty,
                    font, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -360f - index * 240f), new Vector2(760f, 190f),
                    () => openChapter(capturedId));
                chapterButtons.Add(chapter.ChapterId,
                    new ChapterButton(button, button.transform.Find("Label").GetComponent<Text>()));
            }

            levelPanel = CreatePanel(canvasObject.transform, "Level Selection", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Panel);
            levelPanel.SetActive(false);
            CreateButton(levelPanel.transform, "Back To Map", "BACK TO MAP", font,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f),
                new Vector2(420f, 100f), onBackToMap);

            restorationOverlay = CreatePanel(canvasObject.transform, "Restoration Feedback",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.1f, 0.16f, 0.88f));
            restorationText = CreateText(restorationOverlay.transform, "Message", string.Empty, font, 58,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 240f));
            restorationOverlay.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            canvasObject.SetActive(visible);
        }

        public void ShowMap()
        {
            SetVisible(true);
            mapPanel.SetActive(true);
            levelPanel.SetActive(false);
            totalStarsText.text = $"Stars: {progress.TotalStars} / {progress.MaximumCampaignStars}";

            foreach (CampaignChapterDefinition chapter in campaign.Chapters)
            {
                CampaignChapterState state = progress.GetChapterState(chapter.ChapterId);
                ChapterButton item = chapterButtons[chapter.ChapterId];
                item.Button.interactable = state != CampaignChapterState.Locked;
                item.Button.targetGraphic.color = state == CampaignChapterState.Restored
                    ? Restored
                    : state == CampaignChapterState.Available ? Available : Locked;
                int completed = progress.GetCompletedLevelCount(chapter.ChapterId);
                string status = state == CampaignChapterState.Restored
                    ? "RESTORED"
                    : state == CampaignChapterState.Locked
                        ? "LOCKED"
                        : $"AVAILABLE  {completed} / {chapter.Levels.Count}";
                item.Label.text = $"{chapter.DisplayName}\n{status}";
            }
        }

        public void ShowChapter(CampaignChapterDefinition chapter)
        {
            SetVisible(true);
            mapPanel.SetActive(false);
            levelPanel.SetActive(true);
            ClearGeneratedLevelContent();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateText(levelPanel.transform, "Generated Chapter Title", chapter.DisplayName, font, 52,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -90f), new Vector2(900f, 100f));

            for (int index = 0; index < chapter.Levels.Count; index++)
            {
                CampaignLevelEntry level = chapter.Levels[index];
                LevelProgress levelProgress = progress.GetLevelProgress(level.LevelId);
                bool unlocked = progress.IsLevelUnlocked(level.LevelId);
                string status;
                if (levelProgress.Completed)
                {
                    string stars = levelProgress.HasKnownStars ? $"{levelProgress.BestStars} / 3 Stars" : "Stars Unknown";
                    status = $"COMPLETED - {stars}";
                }
                else
                {
                    status = unlocked ? "UNLOCKED" : "LOCKED";
                }

                string capturedId = level.LevelId;
                Button button = CreateButton(levelPanel.transform, $"Generated Level {index + 1}",
                    $"{index + 1:00}  {level.DisplayName}\n{status}", font,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -300f - index * 210f), new Vector2(760f, 160f),
                    () => startLevel(capturedId));
                button.interactable = unlocked;
                button.targetGraphic.color = unlocked ? Available : Locked;
            }
        }

        public void PlayRestoration(string chapterId)
        {
            CampaignChapterDefinition chapter = progress.FindChapter(chapterId);
            if (chapter == null) return;
            if (restorationRoutine != null) StopCoroutine(restorationRoutine);
            restorationText.text = $"{chapter.DisplayName.ToUpperInvariant()} RESTORED";
            restorationRoutine = StartCoroutine(RestorationSequence());
        }

        private IEnumerator RestorationSequence()
        {
            restorationOverlay.SetActive(true);
            restorationOverlay.transform.SetAsLastSibling();
            float elapsed = 0f;
            while (elapsed < 2f)
            {
                elapsed += Time.unscaledDeltaTime;
                float pulse = 1f + Mathf.Sin(elapsed * 8f) * 0.04f;
                restorationText.rectTransform.localScale = Vector3.one * pulse;
                yield return null;
            }

            restorationText.rectTransform.localScale = Vector3.one;
            restorationOverlay.SetActive(false);
            restorationRoutine = null;
        }

        private void ClearGeneratedLevelContent()
        {
            var generated = new List<GameObject>();
            foreach (Transform child in levelPanel.transform)
                if (child.name.StartsWith("Generated ", StringComparison.Ordinal))
                    generated.Add(child.gameObject);
            foreach (GameObject child in generated)
            {
                child.SetActive(false);
                DestroyPart(child);
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystemObject = new GameObject("Campaign Event System");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = anchorMin == Vector2.zero && anchorMax == Vector2.one
                ? Vector2.zero
                : rect.offsetMin;
            rect.offsetMax = anchorMin == Vector2.zero && anchorMax == Vector2.one
                ? Vector2.zero
                : rect.offsetMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0f;
            return panel;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font, int fontSize,
            TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
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
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Action action)
        {
            GameObject buttonObject = CreatePanel(parent, name, anchorMin, anchorMax, position, size, Available);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(() => action?.Invoke());
            Text label = CreateText(buttonObject.transform, "Label", caption, font, 32,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static void DestroyPart(GameObject part)
        {
            if (Application.isPlaying)
                Destroy(part);
            else
                DestroyImmediate(part);
        }

        private sealed class ChapterButton
        {
            public Button Button { get; }
            public Text Label { get; }

            public ChapterButton(Button button, Text label)
            {
                Button = button;
                Label = label;
            }
        }
    }
}

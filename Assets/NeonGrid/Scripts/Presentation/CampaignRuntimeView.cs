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
        private const int LevelGridColumns = 3;
        private const float LevelTileSize = 220f;
        private const float LevelTileHorizontalSpacing = 32f;

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
            CreateText(mapPanel.transform, "Title", "NEON GRID - TEST CITY", font,
                ProgrammerUiMetrics.MapTitleFontSize,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(940f, 110f));
            totalStarsText = CreateText(mapPanel.transform, "Total Stars", string.Empty, font,
                ProgrammerUiMetrics.MapSecondaryFontSize,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -180f), new Vector2(760f, 90f));

            for (int index = 0; index < campaign.Chapters.Count; index++)
            {
                CampaignChapterDefinition chapter = campaign.Chapters[index];
                string capturedId = chapter.ChapterId;
                Button button = CreateButton(mapPanel.transform, $"Chapter {index + 1}", string.Empty,
                    font, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -360f - index * 240f), new Vector2(760f, 190f),
                    () => openChapter(capturedId), ProgrammerUiMetrics.ChapterCardFontSize);
                chapterButtons.Add(chapter.ChapterId,
                    new ChapterButton(button, button.transform.Find("Label").GetComponent<Text>()));
            }

            levelPanel = CreatePanel(canvasObject.transform, "Level Selection", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Panel);
            levelPanel.SetActive(false);
            CreateButton(levelPanel.transform, "Back To Map", "BACK TO MAP", font,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, ProgrammerUiMetrics.SelectorBackButtonCenterY),
                new Vector2(420f, 100f), onBackToMap);

            restorationOverlay = CreatePanel(canvasObject.transform, "Restoration Feedback",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.1f, 0.16f, 0.88f));
            restorationText = CreateText(restorationOverlay.transform, "Message", string.Empty, font,
                ProgrammerUiMetrics.RestorationFontSize,
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
            CreateText(levelPanel.transform, "Generated Chapter Title", chapter.DisplayName, font,
                ProgrammerUiMetrics.ChapterTitleFontSize,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -90f), new Vector2(900f, 100f));

            int rowCount = Mathf.CeilToInt(chapter.Levels.Count / (float)LevelGridColumns);
            RectTransform grid = CreateLevelGrid(rowCount);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                Transform row = CreateLevelRow(grid, rowIndex + 1);
                int firstIndex = rowIndex * LevelGridColumns;
                int rowEnd = Mathf.Min(firstIndex + LevelGridColumns, chapter.Levels.Count);
                for (int index = firstIndex; index < rowEnd; index++)
                {
                    CampaignLevelEntry level = chapter.Levels[index];
                    CreateLevelTile(row, level, index, font);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
        }

        private RectTransform CreateLevelGrid(int rowCount)
        {
            var gridObject = new GameObject("Generated Level Grid", typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            gridObject.transform.SetParent(levelPanel.transform, false);
            RectTransform grid = gridObject.GetComponent<RectTransform>();
            grid.anchorMin = new Vector2(0.5f, 1f);
            grid.anchorMax = new Vector2(0.5f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.anchoredPosition = new Vector2(0f, -ProgrammerUiMetrics.SelectorGridTopInset);
            grid.sizeDelta = new Vector2(LevelTileSize * LevelGridColumns +
                                         LevelTileHorizontalSpacing * (LevelGridColumns - 1),
                LevelTileSize * rowCount + ProgrammerUiMetrics.SelectorVerticalSpacing *
                Mathf.Max(0, rowCount - 1));

            VerticalLayoutGroup layout = gridObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = ProgrammerUiMetrics.SelectorVerticalSpacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return grid;
        }

        private static Transform CreateLevelRow(Transform grid, int rowNumber)
        {
            var rowObject = new GameObject($"Generated Level Row {rowNumber}",
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(grid, false);
            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.sizeDelta = new Vector2(LevelTileSize * LevelGridColumns +
                                        LevelTileHorizontalSpacing * (LevelGridColumns - 1), LevelTileSize);
            LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.preferredWidth = row.sizeDelta.x;
            rowElement.preferredHeight = LevelTileSize;

            HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = LevelTileHorizontalSpacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        private void CreateLevelTile(Transform row, CampaignLevelEntry level, int index, Font font)
        {
            LevelProgress levelProgress = progress.GetLevelProgress(level.LevelId);
            bool completed = levelProgress.Completed;
            bool unlocked = progress.IsLevelUnlocked(level.LevelId);
            string capturedId = level.LevelId;
            Button button = CreateButton(row, $"Generated Level {index + 1}", string.Empty, font,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(LevelTileSize, LevelTileSize), () => startLevel(capturedId));
            LayoutElement tileElement = button.gameObject.AddComponent<LayoutElement>();
            tileElement.preferredWidth = LevelTileSize;
            tileElement.preferredHeight = LevelTileSize;
            button.interactable = unlocked;
            button.targetGraphic.color = completed ? Restored : unlocked ? Available : Locked;

            Text number = button.transform.Find("Label").GetComponent<Text>();
            number.name = "Level Number";
            number.text = $"{index + 1:00}";
            number.fontSize = 58;
            number.alignment = TextAnchor.MiddleCenter;
            number.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            number.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            number.rectTransform.anchoredPosition = completed || !unlocked
                ? new Vector2(0f, 28f)
                : Vector2.zero;
            number.rectTransform.sizeDelta = new Vector2(180f, 100f);

            string state = completed
                ? levelProgress.HasKnownStars ? BuildStars(levelProgress.BestStars) : "✓"
                : string.Empty;
            CreateText(button.transform, "State", state, font, 32, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 42f),
                new Vector2(190f, 60f));
            if (!unlocked) CreateLockIcon(button.transform);
        }

        private static string BuildStars(int count)
        {
            int clamped = Mathf.Clamp(count, 0, 3);
            return new string('★', clamped) + new string('☆', 3 - clamped);
        }

        private static void CreateLockIcon(Transform parent)
        {
            var lockObject = new GameObject("Lock Icon", typeof(RectTransform));
            lockObject.transform.SetParent(parent, false);
            RectTransform lockRect = lockObject.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(0.5f, 0f);
            lockRect.anchorMax = new Vector2(0.5f, 0f);
            lockRect.anchoredPosition = new Vector2(0f, 42f);
            lockRect.sizeDelta = new Vector2(58f, 58f);

            Image shackle = CreatePanel(lockRect, "Shackle", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(38f, 32f),
                Color.white).GetComponent<Image>();
            Image opening = CreatePanel(lockRect, "Shackle Opening", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 9f), new Vector2(22f, 23f),
                Locked).GetComponent<Image>();
            Image body = CreatePanel(lockRect, "Body", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(52f, 36f),
                Color.white).GetComponent<Image>();
            shackle.raycastTarget = false;
            opening.raycastTarget = false;
            body.raycastTarget = false;
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
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Action action,
            int labelFontSize = ProgrammerUiMetrics.PrimaryButtonFontSize)
        {
            GameObject buttonObject = CreatePanel(parent, name, anchorMin, anchorMax, position, size, Available);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(() => action?.Invoke());
            Text label = CreateText(buttonObject.transform, "Label", caption, font, labelFontSize,
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

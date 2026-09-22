using System;
using NeonGrid.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class GameplayVisualPrototypeController : MonoBehaviour
    {
        [SerializeField] private GameplayVisualPrototypeDefinition definition;
        [SerializeField] private bool startWithDenseLevel;
        [SerializeField] private bool holdCompletionPresentation = true;

        private GameObject boardRoot;
        private Text currentLevelLabel;
        private Text completionHoldLabel;

        public GameplayVisualPrototypeDefinition Definition => definition;
        public LevelDefinition CurrentLevel { get; private set; }
        public BoardController CurrentBoardController { get; private set; }
        public bool HoldCompletionPresentation => holdCompletionPresentation;

        private void Awake()
        {
            if (definition == null) definition = GameplayVisualPrototypeCatalog.Load();
            if (definition == null || !definition.IsConfigured)
            {
                Debug.LogError("M15 gameplay visual prototype definition is missing or invalid.",
                    this);
                return;
            }

            ConfigureCamera();
            BuildLevelSelector();
            ShowLevel(startWithDenseLevel ? definition.DenseLevel : definition.SimpleLevel);
        }

        public void ShowSimpleLevel()
        {
            if (definition != null) ShowLevel(definition.SimpleLevel);
        }

        public void ShowDenseLevel()
        {
            if (definition != null) ShowLevel(definition.DenseLevel);
        }

        private void ShowLevel(LevelDefinition level)
        {
            if (level == null) return;
            DestroyBoard();
            CurrentLevel = level;
            boardRoot = new GameObject($"Technical Neon Board - {level.name}");
            boardRoot.transform.SetParent(transform, false);
            CurrentBoardController = boardRoot.AddComponent<BoardController>();
            CurrentBoardController.Initialize(level, definition.VisualTheme);
            CurrentBoardController.SetCompletionPresentationHeld(holdCompletionPresentation);
            if (currentLevelLabel != null)
                currentLevelLabel.text = $"M15 TECHNICAL NEON PROTOTYPE  /  {level.name}";
        }

        private void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = definition.VisualTheme.Background;
            camera.targetDisplay = 0;
        }

        private void BuildLevelSelector()
        {
            EnsureEventSystem();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("M15 Prototype Selector");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            canvasObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            currentLevelLabel = CreateText(canvasObject.transform, "Prototype Label", font,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f),
                new Vector2(920f, 54f), 28, definition.VisualTheme.PoweredEnergy);
            CreateButton(canvasObject.transform, "Inspect PS01", "PS01", font,
                new Vector2(0.36f, 1f), new Vector2(0.36f, 1f), new Vector2(0f, -292f),
                definition.VisualTheme.Board, definition.VisualTheme.PoweredEnergy,
                ShowSimpleLevel);
            CreateButton(canvasObject.transform, "Inspect CG10", "CG10", font,
                new Vector2(0.64f, 1f), new Vector2(0.64f, 1f), new Vector2(0f, -292f),
                definition.VisualTheme.Board, definition.VisualTheme.PoweredEnergy,
                ShowDenseLevel);
            Button holdButton = CreateButton(canvasObject.transform, "Hold Completion", string.Empty,
                font, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -380f), definition.VisualTheme.Board,
                definition.VisualTheme.Hint, ToggleCompletionHold, new Vector2(430f, 68f));
            completionHoldLabel = holdButton.transform.Find("Label").GetComponent<Text>();
            RefreshCompletionHoldLabel();
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystemObject = new GameObject("M15 Prototype Event System");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static Text CreateText(Transform parent, string name, Font font,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, int fontSize,
            Color color)
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
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string caption, Font font,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Color background,
            Color foreground, Action action, Vector2? size = null)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size ?? new Vector2(240f, 72f);
            Image image = buttonObject.GetComponent<Image>();
            background.a = 0.96f;
            image.color = background;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Text text = CreateText(buttonObject.transform, "Label", font, Vector2.zero,
                Vector2.one, Vector2.zero, Vector2.zero, 30, foreground);
            text.text = caption;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        public void SetCompletionHold(bool held)
        {
            holdCompletionPresentation = held;
            CurrentBoardController?.SetCompletionPresentationHeld(held);
            RefreshCompletionHoldLabel();
        }

        private void ToggleCompletionHold()
        {
            SetCompletionHold(!holdCompletionPresentation);
        }

        private void RefreshCompletionHoldLabel()
        {
            if (completionHoldLabel != null)
                completionHoldLabel.text = holdCompletionPresentation
                    ? "HOLD COMPLETION: ON"
                    : "HOLD COMPLETION: OFF";
        }

        private void DestroyBoard()
        {
            if (boardRoot == null) return;
            boardRoot.SetActive(false);
            if (Application.isPlaying) Destroy(boardRoot);
            else DestroyImmediate(boardRoot);
            boardRoot = null;
            CurrentBoardController = null;
        }

#if UNITY_EDITOR
        public void SetDefinition(GameplayVisualPrototypeDefinition value)
        {
            definition = value;
        }
#endif
    }
}

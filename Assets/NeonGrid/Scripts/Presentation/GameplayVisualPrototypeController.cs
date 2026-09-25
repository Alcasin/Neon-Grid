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
        [SerializeField] private bool useProductionTheme = true;

        private GameObject boardRoot;
        private Text currentLevelLabel;
        private Text completionHoldLabel;
        private Text themeLabel;

        public GameplayVisualPrototypeDefinition Definition => definition;
        public LevelDefinition CurrentLevel { get; private set; }
        public BoardController CurrentBoardController { get; private set; }
        public bool HoldCompletionPresentation => holdCompletionPresentation;
        public bool UseProductionTheme => useProductionTheme;
        public CircuitVisualThemeDefinition ActiveTheme => useProductionTheme
            ? definition?.ProductionVisualTheme
            : definition?.VisualTheme;

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
            CurrentBoardController.Initialize(level, ActiveTheme);
            CurrentBoardController.SetCompletionPresentationHeld(holdCompletionPresentation);
            if (currentLevelLabel != null)
                currentLevelLabel.text = $"M15 VISUAL QA  /  {ActiveTheme.DisplayName}  /  {level.name}";
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
            camera.backgroundColor = ActiveTheme.Background;
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
                new Vector2(920f, 54f), 28, ActiveTheme.PoweredEnergy);
            CreateButton(canvasObject.transform, "Inspect PS01", "PS01", font,
                new Vector2(0.36f, 1f), new Vector2(0.36f, 1f), new Vector2(0f, -292f),
                ActiveTheme.Board, ActiveTheme.PoweredEnergy,
                ShowSimpleLevel);
            CreateButton(canvasObject.transform, "Inspect CG10", "CG10", font,
                new Vector2(0.64f, 1f), new Vector2(0.64f, 1f), new Vector2(0f, -292f),
                ActiveTheme.Board, ActiveTheme.PoweredEnergy,
                ShowDenseLevel);
            Button holdButton = CreateButton(canvasObject.transform, "Hold Completion", string.Empty,
                font, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -380f), ActiveTheme.Board,
                ActiveTheme.Hint, ToggleCompletionHold, new Vector2(430f, 68f));
            completionHoldLabel = holdButton.transform.Find("Label").GetComponent<Text>();
            RefreshCompletionHoldLabel();
            Button b1Button = CreateButton(canvasObject.transform, "Use B1 Theme", "B1 THEME",
                font, new Vector2(0.36f, 1f), new Vector2(0.36f, 1f),
                new Vector2(0f, -460f), ActiveTheme.Board, ActiveTheme.PoweredEnergy,
                () => SetProductionTheme(false));
            Button b2Button = CreateButton(canvasObject.transform, "Use B2 Theme", "B2 THEME",
                font, new Vector2(0.64f, 1f), new Vector2(0.64f, 1f),
                new Vector2(0f, -460f), ActiveTheme.Board, ActiveTheme.PoweredEnergy,
                () => SetProductionTheme(true));
            themeLabel = useProductionTheme
                ? b2Button.transform.Find("Label").GetComponent<Text>()
                : b1Button.transform.Find("Label").GetComponent<Text>();
            RefreshThemeControl();
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

        public void SetProductionTheme(bool enabled)
        {
            if (definition == null || useProductionTheme == enabled) return;
            useProductionTheme = enabled;
            ConfigureCamera();
            RefreshThemeControl();
            ShowLevel(CurrentLevel ?? (startWithDenseLevel
                ? definition.DenseLevel
                : definition.SimpleLevel));
        }

        private void RefreshThemeControl()
        {
            if (themeLabel == null) return;
            Transform selector = themeLabel.transform.parent.parent;
            Text b1 = selector.Find("Use B1 Theme/Label")?.GetComponent<Text>();
            Text b2 = selector.Find("Use B2 Theme/Label")?.GetComponent<Text>();
            if (b1 != null) b1.text = useProductionTheme ? "B1 THEME" : "B1 THEME  [ACTIVE]";
            if (b2 != null) b2.text = useProductionTheme ? "B2 THEME  [ACTIVE]" : "B2 THEME";
            themeLabel = useProductionTheme ? b2 : b1;
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

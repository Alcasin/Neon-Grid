using System;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Session;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class CampaignUiVisualPrototypeController : MonoBehaviour
    {
        [SerializeField] private CampaignUiVisualPrototypeDefinition definition;
        [SerializeField] private CampaignUiPreviewMode initialMode =
            CampaignUiPreviewMode.Intro;

        private GameObject introRoot;
        private GameObject campaignRoot;
        private GameObject endingRoot;
        private CampaignProgressService previewProgress;
        private bool initialized;

        public CampaignUiVisualPrototypeDefinition Definition => definition;
        public CampaignUiPreviewMode CurrentMode { get; private set; }
        internal CampaignIntroView IntroView { get; private set; }
        internal CampaignRuntimeView CampaignView { get; private set; }
        internal CampaignEndingView EndingView { get; private set; }
        internal CampaignProgressService PreviewProgress => previewProgress;

        private void Awake()
        {
            if (!initialized)
                Initialize(definition ?? CampaignUiVisualPrototypeCatalog.Load());
        }

        public void Initialize(CampaignUiVisualPrototypeDefinition prototype)
        {
            if (initialized) return;
            if (prototype == null || !prototype.IsConfigured)
                throw new ArgumentException(
                    "A configured M15 campaign UI prototype definition is required.",
                    nameof(prototype));

            definition = prototype;
            initialized = true;
            EnsureEventSystem();
            BuildPreviewViews();
            BuildPreviewControls();
            ShowPreview(initialMode);
        }

        public void ShowPreview(CampaignUiPreviewMode mode)
        {
            if (!initialized) return;
            CurrentMode = mode;
            IntroView.SetVisible(false);
            CampaignView.SetVisible(false);
            CampaignView.HideRestorationStatus();
            EndingView.SetVisible(false);

            switch (mode)
            {
                case CampaignUiPreviewMode.Intro:
                    IntroView.SetVisible(true);
                    CampaignUiThemeApplicator.Apply(introRoot, definition.Theme, mode);
                    break;
                case CampaignUiPreviewMode.Selector:
                    CampaignView.ShowChapter(definition.Campaign.Chapters[0]);
                    CampaignUiThemeApplicator.Apply(campaignRoot, definition.Theme, mode);
                    break;
                case CampaignUiPreviewMode.RestorationStatus:
                    CampaignView.ShowMap();
                    CampaignView.ShowRestorationStatus(definition.Campaign.Chapters[0].ChapterId);
                    CampaignUiThemeApplicator.Apply(campaignRoot, definition.Theme, mode);
                    break;
                case CampaignUiPreviewMode.Ending:
                    EndingView.SetVisible(true);
                    CampaignUiThemeApplicator.Apply(endingRoot, definition.Theme, mode);
                    break;
                case CampaignUiPreviewMode.MapChrome:
                    CampaignView.ShowMap();
                    CampaignUiThemeApplicator.Apply(campaignRoot, definition.Theme, mode);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private void BuildPreviewViews()
        {
            previewProgress = CreatePreviewProgress(definition.Campaign);

            introRoot = CreateRoot("M15-D1 Intro Preview");
            IntroView = introRoot.AddComponent<CampaignIntroView>();
            IntroView.Build(definition.Narrative, () => false);

            campaignRoot = CreateRoot("M15-D1 Campaign Preview");
            CampaignView = campaignRoot.AddComponent<CampaignRuntimeView>();
            CampaignView.Build(definition.Campaign, previewProgress, _ => { }, _ => { }, () => { });

            endingRoot = CreateRoot("M15-D1 Ending Preview");
            EndingView = endingRoot.AddComponent<CampaignEndingView>();
            EndingView.Build(definition.Narrative.EndingNarrative, () => false);
        }

        private CampaignProgressService CreatePreviewProgress(CampaignDefinition campaign)
        {
            var progress = new CampaignProgressService(campaign);
            CampaignChapterDefinition chapter = campaign.Chapters[0];
            int completedCount = Mathf.Min(2, chapter.Levels.Count);
            for (int index = 0; index < completedCount; index++)
            {
                CampaignLevelEntry entry = chapter.Levels[index];
                int optimal = entry.AuthoredOptimalMoves ?? 0;
                StarEvaluationResult stars = new StarEvaluator().Evaluate(true, optimal, optimal);
                progress.RecordCompletion(entry.LevelId, new SessionCompletionResult(
                    entry.LevelDefinition, optimal, 0f, optimal, false, stars));
            }
            return progress;
        }

        private void BuildPreviewControls()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("M15-D1 Preview Controls");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            CampaignUiPreviewMode[] modes =
            {
                CampaignUiPreviewMode.Intro,
                CampaignUiPreviewMode.Selector,
                CampaignUiPreviewMode.RestorationStatus,
                CampaignUiPreviewMode.Ending,
                CampaignUiPreviewMode.MapChrome
            };
            string[] labels = { "INTRO", "SELECTOR", "RESTORED", "ENDING", "MAP" };
            for (int index = 0; index < modes.Length; index++)
            {
                CampaignUiPreviewMode captured = modes[index];
                CreatePreviewButton(canvasObject.transform, labels[index], font,
                    new Vector2(0.1f + index * 0.2f, 0f), () => ShowPreview(captured));
            }
        }

        private void CreatePreviewButton(Transform parent, string label, Font font,
            Vector2 anchor, Action action)
        {
            var buttonObject = new GameObject($"Preview {label}", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = new Vector2(0f, 34f);
            rect.sizeDelta = new Vector2(188f, 56f);
            Image image = buttonObject.GetComponent<Image>();
            image.color = definition.Theme.ButtonNormal;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            ColorBlock colors = button.colors;
            colors.normalColor = definition.Theme.ButtonNormal;
            colors.highlightedColor = definition.Theme.ButtonHighlighted;
            colors.selectedColor = definition.Theme.ButtonHighlighted;
            colors.pressedColor = definition.Theme.ButtonPressed;
            colors.disabledColor = definition.Theme.ButtonDisabled;
            button.colors = colors;
            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = definition.Theme.PanelEdge;
            outline.effectDistance = new Vector2(definition.Theme.KeylineThickness,
                -definition.Theme.KeylineThickness);

            var textObject = new GameObject("Label", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = definition.Theme.BodyText;
            text.raycastTarget = false;
            text.text = label;
        }

        private GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            return root;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystemObject = new GameObject("M15-D1 Event System");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

#if UNITY_EDITOR
        public void SetDefinition(CampaignUiVisualPrototypeDefinition prototype)
        {
            definition = prototype;
        }
#endif
    }
}

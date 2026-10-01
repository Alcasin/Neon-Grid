using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Settings;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class CampaignRuntimeController : MonoBehaviour
    {
        [SerializeField] private CampaignDefinition campaign;

        private CampaignRuntimeView campaignView;
        private CampaignIntroView introView;
        private CampaignEndingView endingView;
        private CityRestorationSequenceController restorationSequence;
        private ProductionGameplayFeedbackController gameplayFeedback;
        private RuntimeUserSettingsController runtimeSettings;
        private GameObject boardRoot;
        private BoardController activeBoard;
        private CampaignNarrativeDefinition narrative;

        public CampaignFlowCoordinator Flow { get; private set; }
        public CampaignLoadStatus LoadStatus { get; private set; }
        public string SavePath => Flow?.SavePath;
        public CampaignIntroView IntroView => introView;
        public CampaignEndingView EndingView => endingView;
        public CampaignRuntimeView CampaignView => campaignView;
        public ProductionGameplayFeedbackController GameplayFeedback => gameplayFeedback;
        public RuntimeUserSettingsController RuntimeSettings => runtimeSettings;
        public BoardController ActiveBoard => activeBoard;

        private void Awake()
        {
            if (campaign == null)
            {
                Debug.LogError("CampaignRuntimeController requires a CampaignDefinition.", this);
                return;
            }

            Initialize(campaign,
                new CampaignSaveStore(CampaignSaveStore.GetDefaultSavePath(campaign.CampaignId)),
                UserSettingsStore.CreateDefault());
        }

        public void Initialize(CampaignDefinition definition, ICampaignProgressStore store)
        {
            Initialize(definition, store, null);
        }

        public void Initialize(CampaignDefinition definition, ICampaignProgressStore store,
            IUserSettingsStore settingsStore)
        {
            campaign = definition ?? throw new System.ArgumentNullException(nameof(definition));
            runtimeSettings = settingsStore != null
                ? new RuntimeUserSettingsController(settingsStore)
                : RuntimeUserSettingsController.CreateDefaultsWithoutPersistence();
            Camera displayCamera = EnsureDisplayCamera();
            if (campaign.GameplayFeedback != null && campaign.GameplayFeedback.IsConfigured)
            {
                gameplayFeedback = GetComponent<ProductionGameplayFeedbackController>() ??
                                   gameObject.AddComponent<ProductionGameplayFeedbackController>();
                gameplayFeedback.Initialize(campaign.GameplayFeedback, displayCamera);
                runtimeSettings.AttachServices(gameplayFeedback.AudioService,
                    gameplayFeedback.HapticsService);
            }

            CampaignLoadResult load = store.Load(campaign);
            LoadStatus = load.Status;
            Flow = new CampaignFlowCoordinator(campaign, load.Progress, store);
            foreach (string diagnostic in load.Diagnostics)
                Debug.LogWarning(diagnostic, this);
            Flow.ProgressRecorded += OnProgressRecorded;

            Debug.Log($"Neon Grid campaign save path: {store.SavePath}", this);
            campaignView = gameObject.AddComponent<CampaignRuntimeView>();
            campaignView.Build(campaign, load.Progress, id => OpenChapter(id),
                id => StartLevel(id), ShowMap, campaign.CampaignUiTheme);
            campaignView.ConfigureSettings(runtimeSettings);
            restorationSequence = gameObject.AddComponent<CityRestorationSequenceController>();
            restorationSequence.Initialize(campaignView, Flow, ShowEndingAfterFinalRestoration);
            narrative = CampaignNarrativeCatalog.LoadForCampaign(campaign.CampaignId);
            if (narrative != null && narrative.IntroPages.Count > 0 &&
                !load.Progress.IntroCompleted)
            {
                introView = gameObject.AddComponent<CampaignIntroView>();
                introView.Build(narrative, CompleteIntro, campaign.CampaignUiTheme);
            }
            else
            {
                EnterPostIntroPresentation();
            }
        }

        private bool CompleteIntro()
        {
            if (!Flow.TryCompleteIntro())
            {
                Debug.LogWarning(Flow.LastSaveResult?.Message ??
                                 "Could not persist intro completion.", this);
                return false;
            }

            introView.SetVisible(false);
            EnterPostIntroPresentation();
            return true;
        }

        private void EnterPostIntroPresentation()
        {
            if (Flow.PeekPendingRestoration() == null && TryShowEnding()) return;
            EnterMapPresentation();
        }

        private bool ShowEndingAfterFinalRestoration()
        {
            return TryShowEnding();
        }

        private bool TryShowEnding()
        {
            CampaignEndingNarrative ending = narrative?.EndingNarrative;
            if (ending == null || !ending.IsConfigured || !Flow.Progress.IsEndingRequired)
                return false;

            campaignView.SetVisible(false);
            gameplayFeedback?.EnterNonMapPresentation();
            if (endingView == null)
            {
                endingView = gameObject.AddComponent<CampaignEndingView>();
                endingView.Build(ending, CompleteEnding, campaign.CampaignUiTheme);
            }
            else
            {
                endingView.SetVisible(true);
            }
            return true;
        }

        private bool CompleteEnding()
        {
            if (!Flow.TryCompleteEnding())
            {
                Debug.LogWarning(Flow.LastSaveResult?.Message ??
                                 "Could not persist ending completion.", this);
                return false;
            }

            endingView.SetVisible(false);
            EnterMapPresentation();
            return true;
        }

        private void OnDestroy()
        {
            gameplayFeedback?.ExitGameplay();
            if (Flow != null)
                Flow.ProgressRecorded -= OnProgressRecorded;
        }

        public bool OpenChapter(string chapterId)
        {
            if (!Flow.OpenChapter(chapterId)) return false;
            restorationSequence.CancelStatusTail();
            campaignView.ShowChapter(Flow.SelectedChapter);
            return true;
        }

        public bool StartLevel(string levelId)
        {
            if (!Flow.StartLevel(levelId)) return false;
            ShowActiveGameplay();
            return true;
        }

        public void ShowMap()
        {
            DestroyBoard(NeonGridAmbienceMode.City);
            Flow.ReturnToMap();
            EnterMapPresentation();
        }

        public void ShowCurrentChapter()
        {
            if (!Flow.ReturnToLevelSelection()) return;
            restorationSequence.CancelStatusTail();
            DestroyBoard();
            campaignView.ShowChapter(Flow.SelectedChapter);
        }

        private void Retry()
        {
            if (!Flow.Retry()) return;
            ShowActiveGameplay();
        }

        private void Next()
        {
            if (!Flow.StartNextLevel()) return;
            ShowActiveGameplay();
        }

        private void OnProgressRecorded(CampaignProgressUpdate update)
        {
            if (Flow.LastSaveResult != null && !Flow.LastSaveResult.Succeeded)
                Debug.LogWarning(Flow.LastSaveResult.Message, this);
        }

        private void ShowActiveGameplay()
        {
            restorationSequence.CancelStatusTail();
            DestroyBoard(NeonGridAmbienceMode.Gameplay);
            campaignView.SetVisible(false);
            ConfigureCamera();
            boardRoot = new GameObject($"Campaign Gameplay - {Flow.ActiveLevel.LevelId}");
            boardRoot.transform.SetParent(transform, false);
            var resultActions = new GameplayResultActions(Retry, ShowCurrentChapter, ShowMap, Next,
                ShowCurrentChapter, () => Flow.ResultNavigation);
            activeBoard = boardRoot.AddComponent<BoardController>();
            activeBoard.Initialize(Flow.ActiveSession, resultActions,
                Flow.ActiveTutorial, FindLevelOrdinal(Flow.SelectedChapter, Flow.ActiveLevel),
                campaign.GameplayVisualTheme, campaign.GameplayFeedback?.CircuitJuice);
            gameplayFeedback?.EnterGameplay(activeBoard);
        }

        internal static int FindLevelOrdinal(CampaignChapterDefinition chapter,
            CampaignLevelEntry level)
        {
            if (chapter?.Levels == null || level == null) return 0;
            for (int index = 0; index < chapter.Levels.Count; index++)
                if (object.ReferenceEquals(chapter.Levels[index], level))
                    return index + 1;
            return 0;
        }

        private static void ConfigureCamera()
        {
            EnsureDisplayCamera();
        }

        internal static Camera EnsureDisplayCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
                camera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);

            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            else
            {
                camera.gameObject.SetActive(true);
                camera.gameObject.tag = "MainCamera";
            }

            camera.enabled = true;
            camera.targetDisplay = 0;
            camera.targetTexture = null;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.008f, 0.012f, 0.03f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            return camera;
        }

        private void EnterMapPresentation()
        {
            restorationSequence.EnterMap();
            gameplayFeedback?.EnterCityMap();
        }

        private void DestroyBoard(
            NeonGridAmbienceMode destinationAmbience = NeonGridAmbienceMode.None)
        {
            gameplayFeedback?.ExitGameplayTo(destinationAmbience);
            activeBoard = null;
            if (boardRoot == null) return;
            boardRoot.SetActive(false);
            if (Application.isPlaying)
                Destroy(boardRoot);
            else
                DestroyImmediate(boardRoot);
            boardRoot = null;
        }

#if UNITY_EDITOR
        public void SetCampaign(CampaignDefinition definition)
        {
            campaign = definition;
        }
#endif
    }
}

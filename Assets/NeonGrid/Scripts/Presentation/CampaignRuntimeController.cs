using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class CampaignRuntimeController : MonoBehaviour
    {
        [SerializeField] private CampaignDefinition campaign;

        private CampaignRuntimeView campaignView;
        private GameObject boardRoot;

        public CampaignFlowCoordinator Flow { get; private set; }
        public CampaignLoadStatus LoadStatus { get; private set; }
        public string SavePath => Flow?.SavePath;

        private void Awake()
        {
            if (campaign == null)
            {
                Debug.LogError("CampaignRuntimeController requires a CampaignDefinition.", this);
                return;
            }

            Initialize(campaign,
                new CampaignSaveStore(CampaignSaveStore.GetDefaultSavePath(campaign.CampaignId)));
        }

        public void Initialize(CampaignDefinition definition, ICampaignProgressStore store)
        {
            EnsureDisplayCamera();

            CampaignLoadResult load = store.Load(definition);
            LoadStatus = load.Status;
            Flow = new CampaignFlowCoordinator(definition, load.Progress, store);
            foreach (string diagnostic in load.Diagnostics)
                Debug.LogWarning(diagnostic, this);
            Flow.ProgressRecorded += OnProgressRecorded;

            Debug.Log($"Neon Grid campaign save path: {store.SavePath}", this);
            campaignView = gameObject.AddComponent<CampaignRuntimeView>();
            campaignView.Build(definition, load.Progress, id => OpenChapter(id),
                id => StartLevel(id), ShowMap);
            campaignView.ShowMap();
        }

        private void OnDestroy()
        {
            if (Flow != null)
                Flow.ProgressRecorded -= OnProgressRecorded;
        }

        public bool OpenChapter(string chapterId)
        {
            if (!Flow.OpenChapter(chapterId)) return false;
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
            DestroyBoard();
            Flow.ReturnToMap();
            campaignView.ShowMap();
            string restoredChapterId = Flow.ConsumePendingRestoration();
            if (restoredChapterId != null)
                campaignView.PlayRestoration(restoredChapterId);
        }

        public void ShowCurrentChapter()
        {
            if (!Flow.ReturnToLevelSelection()) return;
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
            DestroyBoard();
            campaignView.SetVisible(false);
            ConfigureCamera(Flow.ActiveLevel.LevelDefinition);
            boardRoot = new GameObject($"Campaign Gameplay - {Flow.ActiveLevel.LevelId}");
            boardRoot.transform.SetParent(transform, false);
            var resultActions = new GameplayResultActions(Retry, ShowCurrentChapter, ShowMap, Next,
                ShowCurrentChapter, () => Flow.ResultNavigation);
            boardRoot.AddComponent<BoardController>().Initialize(Flow.ActiveSession, resultActions);
        }

        private static void ConfigureCamera(LevelDefinition level)
        {
            Camera camera = EnsureDisplayCamera();
            camera.orthographicSize = Mathf.Max(level.Width, level.Height) * 0.72f;
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

        private void DestroyBoard()
        {
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

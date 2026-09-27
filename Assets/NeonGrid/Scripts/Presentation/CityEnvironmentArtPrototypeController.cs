using System;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class CityEnvironmentArtPrototypeController : MonoBehaviour
    {
        private static readonly string[] PreviewOrder =
        {
            CityEnvironmentArtDefinition.PowerStationId,
            CityEnvironmentArtDefinition.SubstationId,
            CityEnvironmentArtDefinition.ControlCenterId,
            CityEnvironmentArtDefinition.AutomationPlantId,
            CityEnvironmentArtDefinition.CentralGridId
        };

        [SerializeField] private CampaignDefinition campaign;
        [SerializeField] private CityEnvironmentArtDefinition environment;
        private Text statusLabel;

        public CampaignRuntimeView MapView { get; private set; }
        public CityEnvironmentArtView EnvironmentView { get; private set; }
        public CampaignDefinition Campaign => campaign;
        public CityEnvironmentArtDefinition Environment => environment;
        public int PreviewRestoredCount { get; private set; }

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (MapView != null) return;
            if (campaign == null || environment == null || !environment.IsConfigured)
                throw new InvalidOperationException(
                    "Assign the production campaign and valid E3A environment definition.");
            var mapRoot = new GameObject("E3A Map Context");
            mapRoot.transform.SetParent(transform, false);
            MapView = mapRoot.AddComponent<CampaignRuntimeView>();
            MapView.Build(campaign, new CampaignProgressService(campaign), _ => { }, _ => { },
                () => { }, campaign.CampaignUiTheme, true, false);
            MapView.ShowMap();

            RectTransform composition = MapView.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "City Composition");
            EnvironmentView = gameObject.AddComponent<CityEnvironmentArtView>();
            // Child 0 is City Ground. Inserting at 1 keeps environment behind ambient map
            // details, M13 paths, final buildings and all labels/UI.
            EnvironmentView.Initialize(environment, composition, 1);
            CampaignRuntimeView.SetLegacyBackdropVisible(composition, false);
            BuildControls();
            PreviewState(0);
        }

        public void PreviewState(int restoredDistrictCount)
        {
            if (restoredDistrictCount < 0 || restoredDistrictCount > PreviewOrder.Length)
                throw new ArgumentOutOfRangeException(nameof(restoredDistrictCount));
            PreviewRestoredCount = restoredDistrictCount;
            EnvironmentView.PresentRestoredDistricts(PreviewOrder.Take(restoredDistrictCount));
            if (statusLabel != null)
                statusLabel.text = $"DEV ENVIRONMENT QA — {restoredDistrictCount} / 5 RESTORED\n" +
                                   "Environment-only preview • no progress or save writes";
        }

        public void PresentProgress(CampaignProgressService progress)
        {
            EnvironmentView.Present(campaign, progress);
            PreviewRestoredCount = EnvironmentView.ActiveDistrictCount;
        }

        public void SetEnvironmentVisible(bool visible)
        {
            EnvironmentView.SetVisible(visible);
            if (statusLabel != null)
                statusLabel.text = visible
                    ? $"DEV ENVIRONMENT QA — {PreviewRestoredCount} / 5 RESTORED"
                    : "DEV ENVIRONMENT QA — ENVIRONMENT OFF";
        }

        private void BuildControls()
        {
            var root = new GameObject("E3A Developer Controls", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            statusLabel = Label(root.transform, "QA Status", new Vector2(0f, 205f),
                new Vector2(940f, 62f));
            Button(root.transform, "ENV OFF", -435f, 125f, 130f,
                () => SetEnvironmentVisible(false));
            Button(root.transform, "ENV ON", -295f, 125f, 130f,
                () => SetEnvironmentVisible(true));
            string[] labels = { "0", "PS", "PS+S", "PS+S+CC", "+AP", "ALL 5" };
            for (int index = 0; index < labels.Length; index++)
            {
                int captured = index;
                Button(root.transform, labels[index], -125f + index * 115f, 125f, 105f,
                    () => PreviewState(captured));
            }
        }

        private static Button Button(Transform parent, string text, float x, float y,
            float width, Action action)
        {
            var root = new GameObject("QA " + text, typeof(RectTransform), typeof(Image),
                typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 60f);
            Image image = root.GetComponent<Image>();
            image.color = new Color(0.18f, 0.20f, 0.25f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Text label = Label(root.transform, "Label", Vector2.zero, rect.sizeDelta);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax =
                new Vector2(0.5f, 0.5f);
            label.text = text;
            return button;
        }

        private static Text Label(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            Text label = root.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax =
                new Vector2(0.5f, 0f);
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            return label;
        }

#if UNITY_EDITOR
        public void SetData(CampaignDefinition source,
            CityEnvironmentArtDefinition environmentDefinition)
        {
            campaign = source;
            environment = environmentDefinition;
        }
#endif
    }
}

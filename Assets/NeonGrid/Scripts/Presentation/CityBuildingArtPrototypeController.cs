using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public enum CityBuildingArtPreviewSource
    {
        Prototype,
        Final
    }

    public sealed class CityBuildingArtPrototypeController : MonoBehaviour
    {
        [SerializeField] private CampaignDefinition campaign;
        [SerializeField] private CityBuildingArtDefinition powerStation;
        [SerializeField] private CityBuildingArtDefinition centralGrid;
        [SerializeField] private CityBuildingArtDefinition finalPowerStation;
        [SerializeField] private CityBuildingArtDefinition finalCentralGrid;
        [SerializeField] private CityBuildingArtDefinition finalSubstation;
        private CityBuildingArtView[] art;
        private CityChapterNodeView[] nodes;
        private int selected;
        private Text selectionLabel;
        private UnityEngine.UI.Button finalSourceButton;
        private Text finalSourceLabel;
        private Coroutine emphasis;
        public CampaignRuntimeView MapView { get; private set; }
        public CampaignProgressService PreviewProgress { get; private set; }
        public CityBuildingArtDefinition PowerStation => powerStation;
        public CityBuildingArtDefinition CentralGrid => centralGrid;
        public CampaignDefinition Campaign => campaign;
        public CityBuildingArtDefinition FinalPowerStation => finalPowerStation;
        public CityBuildingArtDefinition FinalCentralGrid => finalCentralGrid;
        public CityBuildingArtDefinition FinalSubstation => finalSubstation;
        public CityBuildingArtPreviewSource PreviewSource { get; private set; }
        public bool FinalArtAvailable => HasFinalArt(0) || HasFinalArt(1) || HasFinalArt(2);

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (MapView != null) return;
            if (campaign == null || powerStation == null || centralGrid == null)
                throw new InvalidOperationException("Assign the E1A campaign and two prototype art definitions.");
            PreviewProgress = new CampaignProgressService(campaign); // In-memory only; no repository/save access.
            var root = new GameObject("E1A Map Context");
            root.transform.SetParent(transform, false);
            MapView = root.AddComponent<CampaignRuntimeView>();
            MapView.Build(campaign, PreviewProgress, _ => { }, _ => { }, () => { },
                campaign.CampaignUiTheme, false);
            MapView.ShowMap();
            var nodeList = new List<CityChapterNodeView>
                { FindNode(powerStation.ChapterId), FindNode(centralGrid.ChapterId) };
            if (IsValidFinal(finalSubstation, "substation"))
                nodeList.Add(FindNode("substation"));
            nodes = nodeList.ToArray();
            art = new CityBuildingArtView[nodes.Length];
            for (int index = 0; index < nodes.Length; index++)
            {
                RectTransform region = (RectTransform)nodes[index].transform.Find("Building Silhouette");
                Image[] placeholders = region.GetComponentsInChildren<Image>();
                art[index] = nodes[index].gameObject.AddComponent<CityBuildingArtView>();
                CityBuildingArtDefinition initial = index == 0 ? powerStation :
                    index == 1 ? centralGrid : finalSubstation;
                art[index].Initialize(initial,
                    nodes[index].ChapterId, region, placeholders);
                PresentSample(index, 0);
                // Substation's comparison source is its existing programmer silhouette.
                if (index == 2) art[index].enabled = false;
            }
            // Map remains context, not a second campaign navigation implementation.
            MapView.SetMapInteractionEnabled(false);
            BuildControls();
            PreviewSource = CityBuildingArtPreviewSource.Prototype;
            SelectBuilding(0);
        }

        private CityChapterNodeView FindNode(string id) => MapView
            .GetComponentsInChildren<CityChapterNodeView>(true).Single(node => node.ChapterId == id);

        public void SelectBuilding(int index)
        {
            if (index < 0 || index >= art.Length) throw new ArgumentOutOfRangeException(nameof(index));
            StopEmphasis();
            selected = index;
            if (PreviewSource == CityBuildingArtPreviewSource.Final && !HasFinalArt(selected))
                ShowSource(CityBuildingArtPreviewSource.Prototype);
            UpdateFinalSourceButton();
            UpdateCaption();
        }

        public void ShowState(int stateIndex)
        {
            if (stateIndex < 0 || stateIndex > 4) throw new ArgumentOutOfRangeException(nameof(stateIndex));
            StopEmphasis();
            PresentSample(selected, stateIndex);
            UpdateCaption();
        }

        public bool ShowSource(CityBuildingArtPreviewSource source)
        {
            StopEmphasis();
            if (source == CityBuildingArtPreviewSource.Final && !HasFinalArt(selected))
            {
                UpdateCaption();
                return false;
            }
            CityBuildingArtDefinition[] prototypes = { powerStation, centralGrid, null };
            CityBuildingArtDefinition[] finals =
                { finalPowerStation, finalCentralGrid, finalSubstation };
            for (int index = 0; index < art.Length; index++)
            {
                if (index == 2)
                {
                    if (source == CityBuildingArtPreviewSource.Final)
                    {
                        art[index].enabled = true;
                        if (!art[index].TrySetDefinition(finals[index])) return false;
                    }
                    else art[index].enabled = false;
                    continue;
                }
                CityBuildingArtDefinition definition = source == CityBuildingArtPreviewSource.Final &&
                                                       HasFinalArt(index)
                    ? finals[index]
                    : prototypes[index];
                if (!art[index].TrySetDefinition(definition)) return false;
            }
            PreviewSource = source;
            UpdateCaption();
            return true;
        }

        private void PresentSample(int index, int stateIndex)
        {
            int[] completed = { 0, 0, 4, 7, 10 };
            CampaignChapterState state = stateIndex == 0 ? CampaignChapterState.Locked :
                stateIndex == 4 ? CampaignChapterState.Restored : CampaignChapterState.Available;
            art[index].Present(state, completed[stateIndex], 10);
            string name = campaign.Chapters.Single(chapter => chapter.ChapterId == nodes[index].ChapterId).DisplayName;
            string status = stateIndex == 0 ? "LOCKED" : stateIndex == 4 ? "RESTORED\n★ 30 / 30" :
                $"{completed[stateIndex]} / 10\n★ {completed[stateIndex] * 3} / 30";
            nodes[index].Present(art[index].VisualState, name + "\n" + status);
        }

        public void PreviewEmphasis()
        {
            StopEmphasis();
            emphasis = StartCoroutine(Emphasize());
        }

        private IEnumerator Emphasize()
        {
            // QA-only pulse. Does not invoke or modify production restoration timing/state.
            float elapsed = 0f;
            while (elapsed < 1f)
            {
                art[selected].ApplyEmphasis(Mathf.Sin(elapsed * Mathf.PI));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            art[selected].ResetEmphasis();
            emphasis = null;
        }

        private void StopEmphasis()
        {
            if (emphasis != null) StopCoroutine(emphasis);
            emphasis = null;
            if (art != null) foreach (CityBuildingArtView view in art) view.ResetEmphasis();
        }

        private void OnDisable() => StopEmphasis();

        private void UpdateCaption()
        {
            string availability = HasFinalArt(selected) ? string.Empty : " • FINAL ART MISSING";
            selectionLabel.text = $"DEV ART QA — {PreviewSource} / {nodes[selected].ChapterId} / " +
                                  $"{art[selected].VisualState}{availability}\n" +
                                  "Sample labels only • no progress or save writes";
        }

        private void BuildControls()
        {
            var root = new GameObject("E1A Developer Controls", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            selectionLabel = Label(root.transform, "QA Selection", new Vector2(0f, 198f), new Vector2(900f, 60f));
            Button(root.transform, "POWER STATION", -72f, 132f, 144f, () => SelectBuilding(0));
            if (nodes.Length > 2)
                Button(root.transform, "SUBSTATION", 80f, 132f, 144f,
                    () => SelectBuilding(2));
            Button(root.transform, "CENTRAL GRID", 240f, 132f, 144f, () => SelectBuilding(1));
            Button(root.transform, "EMPHASIS", 400f, 132f, 144f, PreviewEmphasis);
            Button(root.transform, "PROTOTYPE", -400f, 132f, 144f,
                () => ShowSource(CityBuildingArtPreviewSource.Prototype));
            finalSourceButton = Button(root.transform,
                HasFinalArt(selected) ? "FINAL" : "FINAL MISSING", -240f, 132f, 144f,
                () => ShowSource(CityBuildingArtPreviewSource.Final));
            finalSourceLabel = finalSourceButton.transform.Find("Label").GetComponent<Text>();
            UpdateFinalSourceButton();
            string[] labels = { "LOCKED", "STAGE 1", "STAGE 2", "STAGE 3", "RESTORED" };
            for (int index = 0; index < labels.Length; index++)
            {
                int captured = index;
                Button(root.transform, labels[index], (index - 2) * 180f, 56f, 168f, () => ShowState(captured));
            }
        }

        private static UnityEngine.UI.Button Button(Transform parent, string text, float x,
            float y, float width, Action action)
        {
            var root = new GameObject("QA " + text, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 60f);
            Image image = root.GetComponent<Image>();
            image.color = new Color(0.18f, 0.20f, 0.25f);
            UnityEngine.UI.Button button = root.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Text label = Label(root.transform, "Label", Vector2.zero, rect.sizeDelta);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.text = text;
            return button;
        }

        private static Text Label(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            Text label = root.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            return label;
        }

        private bool HasFinalArt(int index)
        {
            if (index < 0 || index > 2) return false;
            CityBuildingArtDefinition definition = index == 0 ? finalPowerStation :
                index == 1 ? finalCentralGrid : finalSubstation;
            string chapterId = index == 0 ? "power_station" :
                index == 1 ? "central_grid" : "substation";
            return IsValidFinal(definition, chapterId);
        }

        private static bool IsValidFinal(CityBuildingArtDefinition definition,
            string chapterId) => definition != null && definition.IsConfigured &&
                                 definition.ChapterId == chapterId;

        private void UpdateFinalSourceButton()
        {
            if (finalSourceButton == null) return;
            bool available = HasFinalArt(selected);
            finalSourceButton.interactable = available;
            if (finalSourceLabel != null)
                finalSourceLabel.text = available ? "FINAL" : "FINAL MISSING";
        }

#if UNITY_EDITOR
        public void SetData(CampaignDefinition source, CityBuildingArtDefinition ordinary,
            CityBuildingArtDefinition central)
        {
            campaign = source;
            powerStation = ordinary;
            centralGrid = central;
        }

        public void SetFinalDefinitions(CityBuildingArtDefinition ordinary,
            CityBuildingArtDefinition central, CityBuildingArtDefinition substation = null)
        {
            finalPowerStation = ordinary;
            finalCentralGrid = central;
            finalSubstation = substation;
        }
#endif
    }
}

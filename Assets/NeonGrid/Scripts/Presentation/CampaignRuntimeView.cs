using System;
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
        private readonly Dictionary<string, CityChapterNodeView> cityNodes =
            new Dictionary<string, CityChapterNodeView>(StringComparer.Ordinal);
        private readonly List<CityEnergyPathView> cityPaths = new List<CityEnergyPathView>();
        private CampaignDefinition campaign;
        private CampaignProgressService progress;
        private Action<string> openChapter;
        private Action<string> startLevel;
        private GameObject canvasObject;
        private GameObject mapPanel;
        private GameObject levelPanel;
        private CanvasGroup mapInteraction;
        private Text totalStarsText;
        private bool usesCityMap;

        public bool UsesCityMap => usesCityMap;
        public bool IsMapInteractionEnabled => mapInteraction != null && mapInteraction.interactable &&
                                               mapInteraction.blocksRaycasts;

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
            mapInteraction = mapPanel.AddComponent<CanvasGroup>();
            if (CityMapLayoutCatalog.TryGet(campaign, out CityMapLayoutDefinition cityLayout))
                BuildProductionCityMap(font, cityLayout);
            else
                BuildFallbackMap(font);

            levelPanel = CreatePanel(canvasObject.transform, "Level Selection", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Panel);
            levelPanel.SetActive(false);
            CreateButton(levelPanel.transform, "Back To Map", "BACK TO MAP", font,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, ProgrammerUiMetrics.SelectorBackButtonCenterY),
                new Vector2(420f, 100f), onBackToMap);

        }

        private void BuildFallbackMap(Font font)
        {
            usesCityMap = false;
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
        }

        private void BuildProductionCityMap(Font font, CityMapLayoutDefinition layout)
        {
            usesCityMap = true;
            Text title = CreateText(mapPanel.transform, "Title", "NEON GRID\nCITY RESTORATION", font,
                ProgrammerUiMetrics.CityMapHeaderFontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f),
                new Vector2(940f, 150f));
            title.lineSpacing = 0.82f;
            totalStarsText = CreateText(mapPanel.transform, "Total Stars", string.Empty, font,
                ProgrammerUiMetrics.CityMapStarsFontSize,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -212f), new Vector2(760f, 70f));

            GameObject composition = CreatePanel(mapPanel.transform, "City Composition",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                CityMapLayoutDefinition.CompositionOffset,
                new Vector2(ProgrammerUiMetrics.CityCompositionWidth,
                    ProgrammerUiMetrics.CityCompositionHeight), Color.clear);
            CreateCityBackdrop(composition.transform);

            for (int index = 0; index + 1 < layout.Entries.Count; index++)
                cityPaths.Add(CreateEnergyPath(composition.transform, layout, index));

            for (int index = 0; index < layout.Entries.Count; index++)
            {
                CityMapLayoutEntry entry = layout.Entries[index];
                CampaignChapterDefinition chapter = campaign.Chapters[index];
                string capturedId = chapter.ChapterId;
                Button button = CreateButton(composition.transform, $"City Node {index + 1}", string.Empty,
                    font, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), entry.Position,
                    entry.HitSize, () => openChapter(capturedId),
                    ProgrammerUiMetrics.CityNodeLabelFontSize);
                button.transition = Selectable.Transition.None;
                button.targetGraphic.color = new Color(1f, 1f, 1f, 0.001f);
                Text label = button.transform.Find("Label").GetComponent<Text>();
                label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchoredPosition = new Vector2(0f,
                    entry.Silhouette == CityBuildingSilhouette.CentralCore ? -103f : -88f);
                label.rectTransform.sizeDelta = new Vector2(entry.HitSize.x, 125f);
                label.fontStyle = FontStyle.Bold;

                Image glow = CreateDecor(button.transform, "Glow", new Vector2(0f, 34f),
                    entry.VisualSize + new Vector2(42f, 42f), Color.clear);
                var silhouette = new GameObject("Building Silhouette", typeof(RectTransform));
                silhouette.transform.SetParent(button.transform, false);
                RectTransform silhouetteRect = silhouette.GetComponent<RectTransform>();
                silhouetteRect.anchorMin = silhouetteRect.anchorMax = new Vector2(0.5f, 0.5f);
                silhouetteRect.anchoredPosition = new Vector2(0f, 38f);
                silhouetteRect.sizeDelta = entry.VisualSize;
                silhouetteRect.localScale = Vector3.one * entry.Scale;
                Image[] parts = CreateBuildingSilhouette(silhouetteRect, entry.Silhouette);

                CityChapterNodeView node = button.gameObject.AddComponent<CityChapterNodeView>();
                node.Initialize(chapter.ChapterId, button, label, glow, parts);
                cityNodes.Add(chapter.ChapterId, node);
            }
        }

        private void PresentProductionCityMap()
        {
            for (int index = 0; index < campaign.Chapters.Count; index++)
            {
                CampaignChapterDefinition chapter = campaign.Chapters[index];
                CampaignChapterState chapterState = progress.GetChapterState(chapter.ChapterId);
                int completed = progress.GetCompletedLevelCount(chapter.ChapterId);
                int stars = CityMapPresentationModel.GetChapterStars(progress, chapter);
                ChapterMapVisualState visualState = CityMapPresentationModel.GetChapterVisualState(
                    chapterState, completed, chapter.Levels.Count);
                string label = visualState == ChapterMapVisualState.Locked
                    ? $"{chapter.DisplayName}\nLOCKED"
                    : visualState == ChapterMapVisualState.Restored
                        ? $"{chapter.DisplayName}\nRESTORED\n★ {stars} / {chapter.Levels.Count * 3}"
                        : $"{chapter.DisplayName}\n{completed} / {chapter.Levels.Count}\n" +
                          $"★ {stars} / {chapter.Levels.Count * 3}";
                cityNodes[chapter.ChapterId].Present(visualState, label);
            }

            for (int index = 0; index < cityPaths.Count; index++)
                cityPaths[index].Present(CityMapPresentationModel.GetPathState(
                    progress.GetChapterState(campaign.Chapters[index].ChapterId),
                    progress.GetChapterState(campaign.Chapters[index + 1].ChapterId)));
        }

        private static void CreateCityBackdrop(Transform parent)
        {
            CreateDecor(parent, "City Ground", Vector2.zero,
                new Vector2(980f, 1430f), new Color(0.015f, 0.025f, 0.055f, 0.95f));
            CreateDecor(parent, "Road Horizontal", new Vector2(0f, -100f),
                new Vector2(920f, 54f), new Color(0.055f, 0.065f, 0.09f, 1f));
            CreateDecor(parent, "Road Vertical", new Vector2(80f, 90f),
                new Vector2(58f, 1180f), new Color(0.055f, 0.065f, 0.09f, 1f));
            CreateDecor(parent, "Road Diagonal", new Vector2(-130f, 120f),
                new Vector2(620f, 42f), new Color(0.045f, 0.055f, 0.08f, 1f), 32f);

            Vector2[] blocks =
            {
                new Vector2(-410f, 520f), new Vector2(-220f, 500f),
                new Vector2(310f, 520f), new Vector2(430f, 350f),
                new Vector2(-450f, 220f), new Vector2(-150f, 60f),
                new Vector2(210f, 170f), new Vector2(450f, -220f),
                new Vector2(-430f, -300f), new Vector2(-170f, -300f),
                new Vector2(210f, -390f), new Vector2(-80f, -560f),
                new Vector2(250f, -580f)
            };
            for (int index = 0; index < blocks.Length; index++)
                CreateDecor(parent, $"City Block {index + 1}", blocks[index],
                    index >= 8 ? new Vector2(130f, 96f) : new Vector2(150f, 115f),
                    new Color(0.025f, 0.045f, 0.085f, 1f));
        }

        private static CityEnergyPathView CreateEnergyPath(Transform parent,
            CityMapLayoutDefinition layout, int fromIndex)
        {
            var pathObject = new GameObject($"Energy Path {fromIndex + 1}", typeof(RectTransform));
            pathObject.transform.SetParent(parent, false);
            RectTransform pathRect = pathObject.GetComponent<RectTransform>();
            pathRect.anchorMin = Vector2.zero;
            pathRect.anchorMax = Vector2.one;
            pathRect.offsetMin = pathRect.offsetMax = Vector2.zero;

            CityMapLayoutEntry from = layout.Entries[fromIndex];
            CityMapLayoutEntry to = layout.Entries[fromIndex + 1];
            var points = new List<Vector2> { from.Position };
            points.AddRange(from.RouteToNext);
            points.Add(to.Position);
            var segments = new Image[points.Count - 1];
            for (int index = 0; index < segments.Length; index++)
            {
                Vector2 delta = points[index + 1] - points[index];
                Image segment = CreateDecor(pathRect, $"Segment {index + 1}",
                    (points[index] + points[index + 1]) * 0.5f,
                    new Vector2(delta.magnitude, 14f), Color.clear);
                segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                segments[index] = segment;
            }

            CityEnergyPathView view = pathObject.AddComponent<CityEnergyPathView>();
            view.Initialize(fromIndex, fromIndex + 1, segments);
            return view;
        }

        private static Image[] CreateBuildingSilhouette(RectTransform parent,
            CityBuildingSilhouette silhouette)
        {
            var parts = new List<Image>();
            void Part(string name, Vector2 position, Vector2 size)
            {
                parts.Add(CreateDecor(parent, name, position, size, Color.white));
            }

            Vector2 size = parent.sizeDelta;
            switch (silhouette)
            {
                case CityBuildingSilhouette.Generator:
                    Part("Generator Hall", new Vector2(0f, -18f), new Vector2(size.x, size.y * 0.55f));
                    Part("Stack Left", new Vector2(-size.x * 0.28f, size.y * 0.23f),
                        new Vector2(size.x * 0.18f, size.y * 0.58f));
                    Part("Stack Right", new Vector2(size.x * 0.26f, size.y * 0.16f),
                        new Vector2(size.x * 0.16f, size.y * 0.45f));
                    break;
                case CityBuildingSilhouette.Substation:
                    Part("Transformer Base", new Vector2(0f, -30f), new Vector2(size.x, size.y * 0.34f));
                    Part("Transformer Left", new Vector2(-size.x * 0.25f, 8f),
                        new Vector2(size.x * 0.22f, size.y * 0.52f));
                    Part("Transformer Right", new Vector2(size.x * 0.25f, 8f),
                        new Vector2(size.x * 0.22f, size.y * 0.52f));
                    Part("Bus Bar", new Vector2(0f, size.y * 0.28f),
                        new Vector2(size.x * 0.78f, 14f));
                    break;
                case CityBuildingSilhouette.ControlTower:
                    Part("Tower", new Vector2(0f, -5f), new Vector2(size.x * 0.52f, size.y * 0.78f));
                    Part("Command Deck", new Vector2(0f, size.y * 0.18f),
                        new Vector2(size.x * 0.85f, size.y * 0.22f));
                    Part("Antenna", new Vector2(0f, size.y * 0.48f), new Vector2(12f, size.y * 0.28f));
                    break;
                case CityBuildingSilhouette.Factory:
                    Part("Factory Hall", new Vector2(0f, -18f), new Vector2(size.x, size.y * 0.58f));
                    Part("Factory Stack", new Vector2(size.x * 0.33f, size.y * 0.20f),
                        new Vector2(size.x * 0.17f, size.y * 0.58f));
                    Part("Factory Annex", new Vector2(-size.x * 0.32f, size.y * 0.14f),
                        new Vector2(size.x * 0.28f, size.y * 0.30f));
                    break;
                default:
                    Part("Core", Vector2.zero, new Vector2(size.x * 0.50f, size.y * 0.92f));
                    Part("Core Left", new Vector2(-size.x * 0.34f, -18f),
                        new Vector2(size.x * 0.28f, size.y * 0.60f));
                    Part("Core Right", new Vector2(size.x * 0.34f, -18f),
                        new Vector2(size.x * 0.28f, size.y * 0.60f));
                    Part("Core Crown", new Vector2(0f, size.y * 0.34f),
                        new Vector2(size.x * 0.82f, size.y * 0.18f));
                    break;
            }
            return parts.ToArray();
        }

        private static Image CreateDecor(Transform parent, string name, Vector2 position,
            Vector2 size, Color color, float rotation = 0f)
        {
            Image image = CreatePanel(parent, name, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, size, color).GetComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            return image;
        }

        public void SetVisible(bool visible)
        {
            canvasObject.SetActive(visible);
        }

        public void ShowMap()
        {
            PrepareMapSurface();
            if (usesCityMap)
                PresentProductionCityMap();
            else
                PresentFallbackMap();
            SetMapInteractionEnabled(true);
            SetVisible(true);
        }

        internal bool TryPrepareRestoration(ChapterRestorationEvent restorationEvent,
            out CityRestorationSequencePlan plan)
        {
            plan = null;
            if (!usesCityMap || restorationEvent == null ||
                restorationEvent.RestoredChapterIndex < 0 ||
                restorationEvent.RestoredChapterIndex >= campaign.Chapters.Count)
                return false;

            int restoredIndex = restorationEvent.RestoredChapterIndex;
            CampaignChapterDefinition restoredChapter = campaign.Chapters[restoredIndex];
            if (!string.Equals(restoredChapter.ChapterId, restorationEvent.RestoredChapterId,
                    StringComparison.Ordinal) || !cityNodes.ContainsKey(restoredChapter.ChapterId))
                return false;

            int pathIndex = -1;
            CampaignChapterDefinition nextChapter = null;
            if (restorationEvent.HasNextChapter)
            {
                int nextIndex = restoredIndex + 1;
                if (nextIndex >= campaign.Chapters.Count ||
                    !string.Equals(campaign.Chapters[nextIndex].ChapterId,
                        restorationEvent.NextChapterId, StringComparison.Ordinal) ||
                    !cityNodes.ContainsKey(restorationEvent.NextChapterId))
                    return false;
                pathIndex = cityPaths.FindIndex(path =>
                    path.FromIndex == restoredIndex && path.ToIndex == nextIndex);
                if (pathIndex < 0) return false;
                nextChapter = campaign.Chapters[nextIndex];
            }

            int previousCompleted = Mathf.Max(0, restoredChapter.Levels.Count - 1);
            ChapterMapVisualState previousState =
                CityMapPresentationModel.GetChapterVisualState(CampaignChapterState.Available,
                    previousCompleted, restoredChapter.Levels.Count);
            plan = new CityRestorationSequencePlan(restorationEvent, pathIndex, previousState);

            PrepareMapSurface();
            PresentProductionCityMap();
            int restoredStars = CityMapPresentationModel.GetChapterStars(progress, restoredChapter);
            cityNodes[restoredChapter.ChapterId].Present(previousState,
                $"{restoredChapter.DisplayName}\nPOWERING UP\n" +
                $"★ {restoredStars} / {restoredChapter.Levels.Count * 3}");
            if (nextChapter != null)
            {
                cityNodes[nextChapter.ChapterId].Present(ChapterMapVisualState.Locked,
                    $"{nextChapter.DisplayName}\nLOCKED");
                cityPaths[pathIndex].Present(CityEnergyPathState.Locked);
            }
            SetMapInteractionEnabled(false);
            SetVisible(true);
            return true;
        }

        internal void ApplyRestoredNodePowerUp(CityRestorationSequencePlan plan, float progressValue)
        {
            CityChapterNodeView node = cityNodes[plan.RestoredChapterId];
            node.ApplyPowerUp(progressValue);
            if (progressValue >= 1f)
            {
                CampaignChapterDefinition chapter = progress.FindChapter(plan.RestoredChapterId);
                int stars = CityMapPresentationModel.GetChapterStars(progress, chapter);
                node.Label.text = $"{chapter.DisplayName}\nRESTORED\n" +
                                  $"★ {stars} / {chapter.Levels.Count * 3}";
            }
        }

        internal void ApplyRestoredNodeFocus(CityRestorationSequencePlan plan, float progressValue)
        {
            cityNodes[plan.RestoredChapterId].ApplyFocus(progressValue);
        }

        internal void ApplyEnergyTravel(CityRestorationSequencePlan plan, float progressValue)
        {
            if (plan.EnergyPathIndex >= 0)
                cityPaths[plan.EnergyPathIndex].ApplyEnergyTravel(progressValue);
        }

        internal void ApplyNextChapterReveal(CityRestorationSequencePlan plan, float progressValue)
        {
            if (plan.HasNextChapter)
            {
                CityChapterNodeView node = cityNodes[plan.NextChapterId];
                node.ApplyAvailableReveal(progressValue);
                if (progressValue >= 1f)
                {
                    CampaignChapterDefinition chapter = progress.FindChapter(plan.NextChapterId);
                    int completed = progress.GetCompletedLevelCount(plan.NextChapterId);
                    int stars = CityMapPresentationModel.GetChapterStars(progress, chapter);
                    node.Label.text = $"{chapter.DisplayName}\n{completed} / {chapter.Levels.Count}\n" +
                                      $"★ {stars} / {chapter.Levels.Count * 3}";
                }
            }
        }

        internal void ApplyFinalNetworkPulse(float progressValue)
        {
            foreach (CityChapterNodeView node in cityNodes.Values)
                if (node.VisualState == ChapterMapVisualState.Restored)
                    node.ApplyNetworkPulse(progressValue);
            foreach (CityEnergyPathView path in cityPaths)
                if (path.State == CityEnergyPathState.Restored)
                    path.ApplyNetworkPulse(progressValue);
        }

        internal void RestoreAuthoritativeMap(bool interactionEnabled)
        {
            PrepareMapSurface();
            if (usesCityMap)
                PresentProductionCityMap();
            else
                PresentFallbackMap();
            SetMapInteractionEnabled(interactionEnabled);
            SetVisible(true);
        }

        internal void SetMapInteractionEnabled(bool enabled)
        {
            mapInteraction.interactable = enabled;
            mapInteraction.blocksRaycasts = enabled;
        }

        private void PrepareMapSurface()
        {
            mapPanel.SetActive(true);
            levelPanel.SetActive(false);
            totalStarsText.text = usesCityMap
                ? $"★ {progress.TotalStars} / {progress.MaximumCampaignStars}"
                : $"Stars: {progress.TotalStars} / {progress.MaximumCampaignStars}";
        }

        private void PresentFallbackMap()
        {
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

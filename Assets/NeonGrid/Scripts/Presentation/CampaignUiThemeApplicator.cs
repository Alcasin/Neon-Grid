using System;
using System.Linq;
using NeonGrid.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public enum CampaignUiPreviewMode
    {
        Intro,
        Selector,
        RestorationStatus,
        Ending,
        MapChrome
    }

    public static class CampaignUiThemeApplicator
    {
        internal const float CentralGridSilhouetteScaleMultiplier = 0.86f;
        internal const float CentralGridSilhouetteCenterY = 76f;

        public static void Apply(GameObject viewRoot, CampaignUiThemeDefinition theme,
            CampaignUiPreviewMode mode)
        {
            if (viewRoot == null) throw new ArgumentNullException(nameof(viewRoot));
            if (theme == null || !theme.IsConfigured)
                throw new ArgumentException("A configured campaign UI theme is required.",
                    nameof(theme));

            Canvas canvas = viewRoot.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
                throw new InvalidOperationException("Campaign UI preview requires a Canvas.");

            StyleBackground(canvas.transform, theme);
            StyleTexts(canvas.transform, theme, mode);
            StyleButtons(canvas.transform, theme, mode);
            AddPurposefulPanel(canvas.transform, theme, mode);
            if (mode == CampaignUiPreviewMode.Selector)
                StyleSelectorStates(canvas.transform, theme);
            if (mode == CampaignUiPreviewMode.RestorationStatus)
                StyleRestorationStatus(canvas.transform, theme);
            if (mode == CampaignUiPreviewMode.Ending)
                StyleEnding(canvas.transform, theme);
            if (mode == CampaignUiPreviewMode.MapChrome ||
                mode == CampaignUiPreviewMode.RestorationStatus)
                StyleCentralGridSilhouette(canvas.transform);
        }

        private static void StyleBackground(Transform canvas, CampaignUiThemeDefinition theme)
        {
            Transform background = Find(canvas, "Background");
            if (background != null && background.TryGetComponent(out Image backgroundImage))
                backgroundImage.color = theme.Background;

            if (canvas.Find("Technical Neon Background Depth") != null) return;
            GameObject depth = CreatePanel(canvas, "Technical Neon Background Depth",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                WithAlpha(theme.BackgroundDepth, 0.44f));
            depth.transform.SetSiblingIndex(Mathf.Min(1, depth.transform.parent.childCount - 1));

            GameObject leftDepth = CreatePanel(depth.transform, "Left Depth Field",
                new Vector2(0f, 0f), new Vector2(0.18f, 1f), Vector2.zero, Vector2.zero,
                WithAlpha(theme.InsetSurface, 0.16f));
            leftDepth.GetComponent<Image>().raycastTarget = false;
            GameObject rightDepth = CreatePanel(depth.transform, "Right Depth Field",
                new Vector2(0.82f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
                WithAlpha(theme.InsetSurface, 0.16f));
            rightDepth.GetComponent<Image>().raycastTarget = false;
        }

        private static void StyleTexts(Transform root, CampaignUiThemeDefinition theme,
            CampaignUiPreviewMode mode)
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                string name = text.gameObject.name;
                text.color = theme.BodyText;
                if (name.Contains("Title", StringComparison.Ordinal) || name == "Title")
                    text.color = theme.TitleText;
                if (name.Contains("Header", StringComparison.Ordinal) ||
                    name.Contains("Briefing Title", StringComparison.Ordinal) ||
                    name == "System Status Label")
                    text.color = theme.PrimaryAccent;
                if (name.Contains("Indicator", StringComparison.Ordinal) ||
                    name == "Total Stars")
                    text.color = theme.SubduedText;
                if (name == "State" && !string.IsNullOrEmpty(text.text))
                    text.color = theme.SuccessAccent;
                if (mode == CampaignUiPreviewMode.MapChrome &&
                    text.GetComponentInParent<CityChapterNodeView>() != null)
                    continue;
                if (mode == CampaignUiPreviewMode.Ending &&
                    (name == "Ending Title" || name == "Status Title" ||
                     name == "Status Body"))
                    text.color = theme.SuccessAccent;
            }
        }

        private static void StyleButtons(Transform root, CampaignUiThemeDefinition theme,
            CampaignUiPreviewMode mode)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if ((mode == CampaignUiPreviewMode.MapChrome ||
                     mode == CampaignUiPreviewMode.RestorationStatus) &&
                    button.GetComponent<CityChapterNodeView>() != null)
                    continue;

                Image target = button.targetGraphic as Image;
                if (target == null) continue;
                target.color = button.interactable ? theme.ButtonNormal : theme.ButtonDisabled;
                ColorBlock colors = button.colors;
                colors.normalColor = theme.ButtonNormal;
                colors.highlightedColor = theme.ButtonHighlighted;
                colors.selectedColor = theme.ButtonHighlighted;
                colors.pressedColor = theme.ButtonPressed;
                colors.disabledColor = theme.ButtonDisabled;
                colors.colorMultiplier = 1f;
                button.colors = colors;
                AddOutline(button.gameObject, theme.PanelEdge, theme.KeylineThickness);
            }
        }

        private static void AddPurposefulPanel(Transform canvas,
            CampaignUiThemeDefinition theme, CampaignUiPreviewMode mode)
        {
            switch (mode)
            {
                case CampaignUiPreviewMode.Intro:
                    EnsureModule(canvas, "Technical Neon Narrative Module",
                        new Vector2(0.5f, 0.5f), new Vector2(0f, 45f),
                        new Vector2(940f, 820f), theme);
                    break;
                case CampaignUiPreviewMode.Ending:
                    EnsureModule(canvas, "Technical Neon Ending Module",
                        new Vector2(0.5f, 0.5f), new Vector2(0f, 45f),
                        new Vector2(940f, 940f), theme);
                    break;
                case CampaignUiPreviewMode.Selector:
                    Transform selector = Find(canvas, "Level Selection");
                    if (selector != null)
                    {
                        if (selector.TryGetComponent(out Image selectorImage))
                            selectorImage.color = theme.Background;
                        EnsureModule(selector, "Technical Neon Selector Module",
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -18f),
                            new Vector2(940f, 1540f), theme);
                    }
                    break;
                case CampaignUiPreviewMode.MapChrome:
                case CampaignUiPreviewMode.RestorationStatus:
                    Transform map = Find(canvas, "Campaign Map");
                    if (map != null)
                        EnsureModule(map, "Technical Neon Map Header Module",
                            new Vector2(0.5f, 1f), new Vector2(0f, -125f),
                            new Vector2(940f, 245f), theme);
                    break;
            }
        }

        private static void StyleSelectorStates(Transform root,
            CampaignUiThemeDefinition theme)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (!button.name.StartsWith("Generated Level ", StringComparison.Ordinal))
                    continue;
                Text state = button.transform.Find("State")?.GetComponent<Text>();
                bool completed = state != null && !string.IsNullOrWhiteSpace(state.text);
                Color surface = !button.interactable
                    ? theme.LockedSurface
                    : completed ? theme.CompletedSurface : theme.AvailableSurface;
                button.targetGraphic.color = surface;
                Outline outline = AddOutline(button.gameObject,
                    completed ? theme.SuccessAccent : button.interactable
                        ? theme.PrimaryAccent
                        : theme.NeutralAccent, theme.KeylineThickness);
                outline.useGraphicAlpha = true;
                Text number = button.transform.Find("Level Number")?.GetComponent<Text>();
                if (number != null)
                    number.color = button.interactable ? theme.TitleText : theme.SubduedText;
            }
        }

        private static void StyleRestorationStatus(Transform root,
            CampaignUiThemeDefinition theme)
        {
            Transform status = Find(root, "Restoration Narrative Status");
            if (status == null) return;
            Image image = status.GetComponent<Image>();
            image.color = Color.Lerp(theme.PanelSurface, theme.CompletedSurface, 0.32f);
            AddOutline(status.gameObject, theme.SuccessAccent, theme.KeylineThickness);
            Transform label = status.Find("System Status Label");
            if (label != null) label.GetComponent<Text>().color = theme.SuccessAccent;
            Transform title = status.Find("Restoration Title");
            if (title != null) title.GetComponent<Text>().color = theme.TitleText;
        }

        private static void StyleEnding(Transform root, CampaignUiThemeDefinition theme)
        {
            Transform divider = Find(root, "Status Divider");
            if (divider != null && divider.TryGetComponent(out Image dividerImage))
                dividerImage.color = theme.SuccessAccent;
            Transform button = Find(root, "Return To City");
            if (button != null)
                AddOutline(button.gameObject, theme.SuccessAccent, theme.KeylineThickness);
        }

        private static void StyleCentralGridSilhouette(Transform root)
        {
            CityChapterNodeView centralGrid = root
                .GetComponentsInChildren<CityChapterNodeView>(true)
                .FirstOrDefault(node => node.ChapterId == "central_grid");
            if (centralGrid == null) return;

            RectTransform silhouette = centralGrid.transform.Find("Building Silhouette")
                as RectTransform;
            if (silhouette == null) return;

            silhouette.localScale = centralGrid.BaseVisualScale *
                                    CentralGridSilhouetteScaleMultiplier;
            Vector2 position = silhouette.anchoredPosition;
            position.y = CentralGridSilhouetteCenterY;
            silhouette.anchoredPosition = position;
        }

        private static void EnsureModule(Transform parent, string name, Vector2 anchor,
            Vector2 position, Vector2 size, CampaignUiThemeDefinition theme)
        {
            if (parent.Find(name) != null) return;
            GameObject shadow = CreatePanel(parent, name + " Shadow", anchor, anchor,
                position + new Vector2(0f, -8f), size, WithAlpha(Color.black,
                    theme.ShadowAlpha));
            shadow.transform.SetSiblingIndex(0);
            GameObject module = CreatePanel(parent, name, anchor, anchor, position, size,
                theme.PanelSurface);
            module.transform.SetSiblingIndex(Mathf.Min(1, parent.childCount - 1));
            AddOutline(module, theme.PanelEdge, theme.KeylineThickness);

            RectTransform inset = CreatePanel(module.transform, "Inset Highlight",
                Vector2.zero, Vector2.one, Vector2.zero,
                new Vector2(-theme.PanelInset * 2f, -theme.PanelInset * 2f),
                WithAlpha(theme.InsetSurface, 0.18f)).GetComponent<RectTransform>();
            inset.offsetMin = Vector2.one * theme.PanelInset;
            inset.offsetMax = -Vector2.one * theme.PanelInset;
        }

        private static Outline AddOutline(GameObject target, Color color, float thickness)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = true;
            return outline;
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == name);
        }

        private static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
            {
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}

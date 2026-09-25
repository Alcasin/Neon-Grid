using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class M15CampaignUiThemeTests
    {
        private const string ScenePath =
            "Assets/NeonGrid/Scenes/M15_CampaignUiVisualPrototype.unity";
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private CampaignUiThemeDefinition theme;
        private CampaignUiVisualPrototypeDefinition definition;

        [SetUp]
        public void SetUp()
        {
            theme = CampaignUiThemeCatalog.LoadTechnicalNeonPrototype();
            definition = CampaignUiVisualPrototypeCatalog.Load();
            Assert.That(theme, Is.Not.Null);
            Assert.That(definition, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void ThemeAsset_ExistsValidatesAndUsesCampaignDomainIdentity()
        {
            Assert.That(theme.IsConfigured, Is.True);
            Assert.That(theme.ThemeId,
                Is.EqualTo("technical_neon_campaign_ui_prototype"));
            Assert.That(theme.DisplayName,
                Is.EqualTo("Technical Neon Campaign Interface"));
            Assert.That(AssetDatabase.GetAssetPath(theme), Is.EqualTo(
                "Assets/NeonGrid/Resources/VisualThemes/" +
                "TechnicalNeonCampaignUiPrototype.asset"));
            Assert.That(theme, Is.Not.InstanceOf<CircuitVisualThemeDefinition>());
        }

        [Test]
        public void Theme_ProvidesDistinctSemanticPaletteRoles()
        {
            Color[] required =
            {
                theme.Background, theme.BackgroundDepth, theme.PanelSurface,
                theme.InsetSurface, theme.PanelEdge, theme.PrimaryAccent,
                theme.NeutralAccent, theme.SuccessAccent, theme.WarningAccent,
                theme.TitleText, theme.BodyText, theme.SubduedText
            };
            Assert.That(required, Has.All.Matches<Color>(color => color.a > 0f));
            Assert.That(required.Select(ToHtml).Distinct().Count(),
                Is.EqualTo(required.Length));
            Assert.That(theme.PrimaryAccent, Is.Not.EqualTo(theme.SuccessAccent));
            Assert.That(theme.WarningAccent, Is.Not.EqualTo(theme.PrimaryAccent));
        }

        [Test]
        public void Theme_ProvidesPanelButtonTextAndSelectorRoles()
        {
            Assert.That(theme.KeylineThickness, Is.EqualTo(2f));
            Assert.That(theme.PanelInset, Is.EqualTo(12f));
            Assert.That(theme.ShadowAlpha, Is.EqualTo(0.32f).Within(0.001f));
            Assert.That(new[]
            {
                theme.ButtonNormal, theme.ButtonHighlighted, theme.ButtonPressed,
                theme.ButtonDisabled, theme.LockedSurface, theme.AvailableSurface,
                theme.CompletedSurface
            }, Has.All.Matches<Color>(color => color.a > 0f));
            Assert.That(theme.LockedSurface, Is.Not.EqualTo(theme.AvailableSurface));
            Assert.That(theme.AvailableSurface, Is.Not.EqualTo(theme.CompletedSurface));
        }

        [Test]
        public void ProductionViews_RemainAcceptedFallbackWithoutThemeAssignment()
        {
            var root = NewObject("Fallback Intro");
            CampaignIntroView view = root.AddComponent<CampaignIntroView>();
            view.Build(definition.Narrative, () => false);

            Image background = Find(root.transform, "Background").GetComponent<Image>();
            Assert.That(background.color,
                Is.EqualTo(new Color(0.008f, 0.012f, 0.03f, 1f)));
            Assert.That(Find(root.transform, "Technical Neon Background Depth"), Is.Null);
            Assert.That(root.GetComponentsInChildren<Outline>(true), Is.Empty);
        }

        [Test]
        public void ProductionCampaign_HasNoCampaignThemeAssociation()
        {
            Assert.That(definition.Campaign.CampaignId, Is.EqualTo("neon_grid_main"));
            Assert.That(typeof(CampaignDefinition).GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(CampaignUiThemeDefinition)), Is.False);
            Assert.That(typeof(CampaignRuntimeController).GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(CampaignUiThemeDefinition)), Is.False);
        }

        [Test]
        public void PrototypeDefinition_ExplicitlyReferencesProductionDataAndD1Theme()
        {
            Assert.That(definition.IsConfigured, Is.True);
            Assert.That(definition.Theme, Is.SameAs(theme));
            Assert.That(definition.Campaign.CampaignId, Is.EqualTo("neon_grid_main"));
            Assert.That(definition.Narrative.CampaignId, Is.EqualTo("neon_grid_main"));
            Assert.That(definition.Narrative,
                Is.SameAs(CampaignNarrativeCatalog.LoadForCampaign("neon_grid_main")));
        }

        [Test]
        public void PrototypeScene_IsExplicitlyThemedAndExcludedFromBuildSettings()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                CampaignUiVisualPrototypeController controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        CampaignUiVisualPrototypeController>(true)).Single();
                Assert.That(controller.Definition, Is.SameAs(definition));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            Assert.That(EditorBuildSettings.scenes.Select(item => item.path),
                Has.None.EqualTo(ScenePath));
        }

        [Test]
        public void IntroPreview_UsesExistingProductionNarrativeAndAcceptedTypeSizes()
        {
            CampaignUiVisualPrototypeController controller = CreateController();
            controller.ShowPreview(CampaignUiPreviewMode.Intro);

            CampaignIntroPage page = definition.Narrative.IntroPages[0];
            Assert.That(controller.IntroView.TitleText.text, Is.EqualTo(page.Title));
            Assert.That(controller.IntroView.BodyText.text, Is.EqualTo(page.Body));
            Assert.That(controller.IntroView.IndicatorText.text,
                Is.EqualTo($"1 / {definition.Narrative.IntroPages.Count}"));
            Assert.That(controller.IntroView.TitleText.fontSize, Is.GreaterThanOrEqualTo(62));
            Assert.That(controller.IntroView.BodyText.fontSize, Is.GreaterThanOrEqualTo(38));
            Assert.That(Find(controller.IntroView.transform,
                "Technical Neon Narrative Module"), Is.Not.Null);
        }

        [Test]
        public void SelectorPreview_UsesRealBriefingAndAcceptedGrid()
        {
            CampaignUiVisualPrototypeController controller = CreateController();
            controller.ShowPreview(CampaignUiPreviewMode.Selector);
            CampaignChapterDefinition chapter = definition.Campaign.Chapters[0];
            Assert.That(definition.Narrative.TryGetChapterNarrative(chapter.ChapterId,
                out CampaignChapterNarrativeEntry narrative), Is.True);

            Assert.That(controller.CampaignView.ChapterBriefingTitle.text,
                Is.EqualTo($"SYSTEM BRIEFING  /  {narrative.BriefingTitle}"));
            Assert.That(controller.CampaignView.ChapterBriefingBody.text,
                Is.EqualTo(narrative.BriefingBody));
            Assert.That(controller.CampaignView.ChapterBriefingTitle.fontSize,
                Is.GreaterThanOrEqualTo(36));
            Assert.That(controller.CampaignView.ChapterBriefingBody.fontSize,
                Is.GreaterThanOrEqualTo(46));
            HorizontalLayoutGroup[] rows = controller.CampaignView.LevelGrid
                .GetComponentsInChildren<HorizontalLayoutGroup>(true);
            Assert.That(rows.Select(row => row.transform.childCount),
                Is.EqualTo(new[] { 3, 3, 3, 1 }));
            Assert.That(rows[3].childAlignment, Is.EqualTo(TextAnchor.MiddleCenter));
        }

        [Test]
        public void SelectorStates_AreStructurallyAndVisuallyDistinct()
        {
            CampaignUiVisualPrototypeController controller = CreateController();
            controller.ShowPreview(CampaignUiPreviewMode.Selector);
            Button completed = Find(controller.CampaignView.transform,
                "Generated Level 1").GetComponent<Button>();
            Button available = Find(controller.CampaignView.transform,
                "Generated Level 3").GetComponent<Button>();
            Button locked = Find(controller.CampaignView.transform,
                "Generated Level 4").GetComponent<Button>();

            Assert.That(completed.interactable, Is.True);
            Assert.That(completed.targetGraphic.color, Is.EqualTo(theme.CompletedSurface));
            Assert.That(completed.transform.Find("State").GetComponent<Text>().text,
                Is.Not.Empty);
            Assert.That(available.interactable, Is.True);
            Assert.That(available.targetGraphic.color, Is.EqualTo(theme.AvailableSurface));
            Assert.That(available.transform.Find("State").GetComponent<Text>().text,
                Is.Empty);
            Assert.That(locked.interactable, Is.False);
            Assert.That(locked.targetGraphic.color, Is.EqualTo(theme.LockedSurface));
            Assert.That(locked.transform.Find("Lock Icon"), Is.Not.Null);
            Assert.That(new[] { completed, available, locked }
                .Select(button => button.GetComponent<Outline>().effectColor).Distinct().Count(),
                Is.EqualTo(3));
        }

        [Test]
        public void RestorationPreview_UsesExistingNonFinalCopyAndSuccessTreatment()
        {
            CampaignUiVisualPrototypeController controller = CreateController();
            controller.ShowPreview(CampaignUiPreviewMode.RestorationStatus);
            CampaignChapterDefinition chapter = definition.Campaign.Chapters[0];
            Assert.That(definition.Narrative.TryGetChapterNarrative(chapter.ChapterId,
                out CampaignChapterNarrativeEntry narrative), Is.True);

            SystemNarrativeStatusView status = controller.CampaignView.RestorationStatus;
            Assert.That(status.CurrentChapterId, Is.EqualTo(chapter.ChapterId));
            Assert.That(status.TitleText.text, Is.EqualTo(narrative.RestoredTitle));
            Assert.That(status.BodyText.text, Is.EqualTo(narrative.RestoredBody));
            Assert.That(status.TitleText.fontSize, Is.GreaterThanOrEqualTo(40));
            Assert.That(status.BodyText.fontSize, Is.GreaterThanOrEqualTo(34));
            Assert.That(status.PanelRect.GetComponent<Outline>().effectColor,
                Is.EqualTo(theme.SuccessAccent));
        }

        [Test]
        public void EndingPreview_UsesExistingCopyAndRestrainedSuccessTreatment()
        {
            CampaignUiVisualPrototypeController controller = CreateController();
            controller.ShowPreview(CampaignUiPreviewMode.Ending);
            CampaignEndingNarrative ending = definition.Narrative.EndingNarrative;

            Assert.That(controller.EndingView.TitleText.text, Is.EqualTo(ending.Title));
            Assert.That(controller.EndingView.BodyText.text, Is.EqualTo(ending.Body));
            Assert.That(controller.EndingView.StatusTitleText.text,
                Is.EqualTo(ending.StatusTitle));
            Assert.That(controller.EndingView.StatusBodyText.text,
                Is.EqualTo(ending.StatusBody));
            Assert.That(controller.EndingView.PrimaryLabel.text,
                Is.EqualTo(ending.ReturnButtonLabel));
            Assert.That(controller.EndingView.TitleText.fontSize,
                Is.GreaterThanOrEqualTo(72));
            Assert.That(controller.EndingView.PrimaryButton.GetComponent<Outline>()
                .effectColor, Is.EqualTo(theme.SuccessAccent));
        }

        [Test]
        public void MapChromePreview_UsesProductionHeaderAndGlobalStarFormat()
        {
            CampaignUiVisualPrototypeController controller = CreateController();
            controller.ShowPreview(CampaignUiPreviewMode.MapChrome);

            Assert.That(Find(controller.CampaignView.transform, "Title")
                .GetComponent<Text>().text, Is.EqualTo("NEON GRID\nCITY RESTORATION"));
            Assert.That(Find(controller.CampaignView.transform, "Total Stars")
                .GetComponent<Text>().text, Is.EqualTo("★ 6 / 150"));
            Assert.That(Find(controller.CampaignView.transform,
                "Technical Neon Map Header Module"), Is.Not.Null);
        }

        [Test]
        public void CentralGridPreview_ReducesOnlyInternalSilhouetteAndPreservesLabelSafeArea()
        {
            CityMapLayoutEntry authored = CityMapLayoutCatalog.Production.Entries
                .Single(entry => entry.ChapterId == "central_grid");
            CampaignUiVisualPrototypeController controller = CreateController();
            controller.ShowPreview(CampaignUiPreviewMode.MapChrome);
            CityChapterNodeView central = controller.CampaignView
                .GetComponentsInChildren<CityChapterNodeView>(true)
                .Single(node => node.ChapterId == "central_grid");
            RectTransform silhouette = central.transform.Find("Building Silhouette")
                .GetComponent<RectTransform>();

            Assert.That(central.HitArea.sizeDelta, Is.EqualTo(authored.HitSize),
                "The outer node/card dimensions must remain authored M13 values.");
            Assert.That(central.HitArea.anchoredPosition, Is.EqualTo(authored.Position),
                "The authored M13 map position must not move.");
            Assert.That(silhouette.sizeDelta, Is.EqualTo(authored.VisualSize));
            Assert.That(silhouette.localScale.x, Is.EqualTo(authored.Scale *
                CampaignUiThemeApplicator.CentralGridSilhouetteScaleMultiplier)
                .Within(0.0001f));
            Assert.That(silhouette.anchoredPosition,
                Is.EqualTo(new Vector2(0f,
                    CampaignUiThemeApplicator.CentralGridSilhouetteCenterY)));

            float silhouetteBottom = silhouette.anchoredPosition.y -
                                     silhouette.sizeDelta.y * silhouette.localScale.y * 0.5f;
            float labelTop = central.Label.rectTransform.anchoredPosition.y +
                             central.Label.rectTransform.sizeDelta.y * 0.5f;
            Assert.That(silhouetteBottom, Is.GreaterThan(labelTop),
                "The silhouette must preserve a positive lower label-safe region.");

            float largestOrdinaryWidth = controller.CampaignView
                .GetComponentsInChildren<CityChapterNodeView>(true)
                .Where(node => node.ChapterId != "central_grid")
                .Select(node =>
                {
                    RectTransform visual = node.transform.Find("Building Silhouette")
                        .GetComponent<RectTransform>();
                    return visual.sizeDelta.x * visual.localScale.x;
                }).Max();
            float centralWidth = silhouette.sizeDelta.x * silhouette.localScale.x;
            Assert.That(centralWidth / largestOrdinaryWidth,
                Is.InRange(1.2f, 1.3f));
        }

        [Test]
        public void ProductionMap_CentralGridSilhouetteRetainsAcceptedM13Presentation()
        {
            GameObject root = NewObject("Production Map Isolation");
            var progress = new NeonGrid.Campaign.CampaignProgressService(
                definition.Campaign);
            CampaignRuntimeView view = root.AddComponent<CampaignRuntimeView>();
            view.Build(definition.Campaign, progress, _ => { }, _ => { }, () => { });
            view.ShowMap();
            CityChapterNodeView central = root
                .GetComponentsInChildren<CityChapterNodeView>(true)
                .Single(node => node.ChapterId == "central_grid");
            RectTransform silhouette = central.transform.Find("Building Silhouette")
                .GetComponent<RectTransform>();
            CityMapLayoutEntry authored = CityMapLayoutCatalog.Production.Entries
                .Single(entry => entry.ChapterId == "central_grid");

            Assert.That(central.HitArea.sizeDelta, Is.EqualTo(authored.HitSize));
            Assert.That(central.HitArea.anchoredPosition, Is.EqualTo(authored.Position));
            Assert.That(silhouette.localScale, Is.EqualTo(Vector3.one * authored.Scale));
            Assert.That(silhouette.anchoredPosition, Is.EqualTo(new Vector2(0f, 38f)));
        }

        [Test]
        public void OpeningPrototype_UsesOnlyInMemoryPreviewProgressAndNoSaveStore()
        {
            CampaignUiVisualPrototypeController controller = CreateController();
            Assert.That(controller.PreviewProgress, Is.Not.Null);
            Assert.That(controller.GetComponentInChildren<CampaignRuntimeController>(true),
                Is.Null);
            Assert.That(typeof(CampaignUiVisualPrototypeController).GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.FieldType.Name.Contains("Store")), Is.False);
            Assert.That(definition.Campaign.Chapters[0].Levels[0].LevelDefinition,
                Is.SameAs(Resources.Load<LevelDefinition>("Levels/PowerStation/PS_01")));
        }

        [TestCase(CampaignUiPreviewMode.Intro, 1080, 1920)]
        [TestCase(CampaignUiPreviewMode.Intro, 1080, 2340)]
        [TestCase(CampaignUiPreviewMode.Intro, 720, 1280)]
        [TestCase(CampaignUiPreviewMode.Selector, 1080, 1920)]
        [TestCase(CampaignUiPreviewMode.Selector, 1080, 2340)]
        [TestCase(CampaignUiPreviewMode.Selector, 720, 1280)]
        [TestCase(CampaignUiPreviewMode.RestorationStatus, 1080, 1920)]
        [TestCase(CampaignUiPreviewMode.RestorationStatus, 1080, 2340)]
        [TestCase(CampaignUiPreviewMode.RestorationStatus, 720, 1280)]
        [TestCase(CampaignUiPreviewMode.Ending, 1080, 1920)]
        [TestCase(CampaignUiPreviewMode.Ending, 1080, 2340)]
        [TestCase(CampaignUiPreviewMode.Ending, 720, 1280)]
        [TestCase(CampaignUiPreviewMode.MapChrome, 1080, 1920)]
        [TestCase(CampaignUiPreviewMode.MapChrome, 1080, 2340)]
        [TestCase(CampaignUiPreviewMode.MapChrome, 720, 1280)]
        public void RepresentativePreview_FitsSupportedPortraitCanvas(
            CampaignUiPreviewMode mode, int width, int height)
        {
            CampaignUiVisualPrototypeController controller = CreateController();
            controller.ShowPreview(mode);
            GameObject active = mode == CampaignUiPreviewMode.Intro
                ? controller.IntroView.gameObject
                : mode == CampaignUiPreviewMode.Ending
                    ? controller.EndingView.gameObject
                    : controller.CampaignView.gameObject;
            CanvasScaler scaler = active.GetComponentInChildren<CanvasScaler>(true);
            float scale = Mathf.Sqrt((width / 1080f) * (height / 1920f));
            float virtualWidth = width / scale;
            float virtualHeight = height / scale;

            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1080f, 1920f)));
            Assert.That(active.GetComponentInChildren<ScrollRect>(true), Is.Null);
            Assert.That(Find(active.transform, "Technical Neon Background Depth"), Is.Not.Null);
            foreach (RectTransform module in active.GetComponentsInChildren<RectTransform>(true)
                         .Where(rect => rect.name.StartsWith("Technical Neon ") &&
                                        !rect.name.EndsWith("Shadow")))
            {
                Assert.That(module.sizeDelta.x, Is.LessThanOrEqualTo(virtualWidth + 0.01f));
                Assert.That(module.sizeDelta.y, Is.LessThanOrEqualTo(virtualHeight + 0.01f));
            }
        }

        [Test]
        public void ThemeAddsNoDecorativeUpdateLoopOrHeavyUiComponents()
        {
            Assert.That(typeof(CampaignUiVisualPrototypeController).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public), Is.Null);
            CampaignUiVisualPrototypeController controller = CreateController();
            Assert.That(controller.GetComponentsInChildren<Mask>(true), Is.Empty);
            Assert.That(controller.GetComponentsInChildren<RectMask2D>(true), Is.Empty);
            Assert.That(controller.GetComponentsInChildren<RawImage>(true), Is.Empty);
        }

        [Test]
        public void GameplayB2ThemeAndProductionBinding_RemainUnaffected()
        {
            CircuitVisualThemeDefinition b2 =
                CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype();
            Assert.That(b2, Is.Not.Null);
            Assert.That(b2.IsConfigured, Is.True);
            Assert.That(b2.UsesProductionTreatment, Is.True);
            Assert.That(definition.Campaign.GameplayVisualTheme, Is.SameAs(b2));
            Assert.That(definition.Theme, Is.Not.SameAs(b2));
        }

        private CampaignUiVisualPrototypeController CreateController()
        {
            GameObject root = NewObject("M15-D1 Test Root");
            CampaignUiVisualPrototypeController controller =
                root.AddComponent<CampaignUiVisualPrototypeController>();
            controller.Initialize(definition);
            return controller;
        }

        private GameObject NewObject(string name)
        {
            var instance = new GameObject(name);
            cleanup.Add(instance);
            return instance;
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == name);
        }

        private static string ToHtml(Color color)
        {
            return ColorUtility.ToHtmlStringRGBA(color);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Editor;
using NeonGrid.Presentation;
using NeonGrid.Session;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class M15ProductionCampaignUiRolloutTests
    {
        private const string PrototypeScene =
            "Assets/NeonGrid/Scenes/M15_CampaignUiVisualPrototype.unity";
        private const string GameplayPrototypeScene =
            "Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity";
        private static readonly string[] VerticalSlices =
        {
            "PowerStation_VerticalSlice", "Substation_VerticalSlice",
            "ControlCenter_VerticalSlice", "AutomationPlant_VerticalSlice",
            "CentralGrid_VerticalSlice"
        };

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private CampaignDefinition campaign;
        private CampaignUiThemeDefinition theme;
        private CampaignNarrativeDefinition narrative;

        [SetUp]
        public void SetUp()
        {
            campaign = Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
            theme = CampaignUiThemeCatalog.LoadTechnicalNeonPrototype();
            narrative = CampaignNarrativeCatalog.LoadForCampaign("neon_grid_main");
            Assert.That(campaign, Is.Not.Null);
            Assert.That(theme, Is.Not.Null);
            Assert.That(narrative, Is.Not.Null);
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
        public void ProductionAssociation_IsAuthoredWhileVerticalSlicesRemainFallback()
        {
            Assert.That(campaign.CampaignUiTheme, Is.SameAs(theme));
            Assert.That(campaign.GameplayVisualTheme,
                Is.SameAs(CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype()));
            foreach (string resource in VerticalSlices)
            {
                CampaignDefinition slice = Resources.Load<CampaignDefinition>(
                    $"Campaigns/{resource}");
                Assert.That(slice, Is.Not.Null, resource);
                Assert.That(slice.CampaignUiTheme, Is.Null, resource);
            }
        }

        [Test]
        public void FallbackAndD1PrototypeRemainExplicitAndValid()
        {
            GameObject fallbackRoot = NewObject("Fallback Campaign View");
            CampaignRuntimeView fallback = fallbackRoot.AddComponent<CampaignRuntimeView>();
            fallback.Build(campaign, new CampaignProgressService(campaign), _ => { },
                _ => { }, () => { });
            fallback.ShowMap();
            Assert.That(Find(fallbackRoot.transform, "Technical Neon Background Depth"),
                Is.Null);

            CampaignUiVisualPrototypeDefinition prototype =
                CampaignUiVisualPrototypeCatalog.Load();
            Assert.That(prototype, Is.Not.Null);
            Assert.That(prototype.IsConfigured, Is.True);
            Assert.That(prototype.Theme, Is.SameAs(theme));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo(PrototypeScene));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo(GameplayPrototypeScene));
        }

        [Test]
        public void FreshProductionIntro_IsThemedAndTransitionsToThemedMap()
        {
            CampaignProgressService progress =
                NarrativeQaStateBuilder.Build(campaign, NarrativeQaPreset.FreshIntro);
            CampaignRuntimeController controller = CreateController(progress);

            Assert.That(controller.IntroView.IsVisible, Is.True);
            Assert.That(Find(controller.IntroView.transform,
                "Technical Neon Narrative Module"), Is.Not.Null);
            Assert.That(controller.IntroView.TitleText.text,
                Is.EqualTo(narrative.IntroPages[0].Title));
            Assert.That(controller.IntroView.BodyText.text,
                Is.EqualTo(narrative.IntroPages[0].Body));
            Assert.That(controller.IntroView.PrimaryButton.interactable, Is.True);
            Assert.That(controller.IntroView.SkipButton.interactable, Is.True);

            controller.IntroView.PressSkip();

            Assert.That(progress.IntroCompleted, Is.True);
            Assert.That(controller.IntroView.IsVisible, Is.False);
            Assert.That(controller.CampaignView.IsVisible, Is.True);
            Assert.That(Find(controller.CampaignView.transform,
                "Technical Neon Map Header Module"), Is.Not.Null);
        }

        [Test]
        public void ProductionMapSelectorGameplayRoundTrip_PreservesIndependentB2Theme()
        {
            CampaignProgressService progress =
                NarrativeQaStateBuilder.Build(campaign, NarrativeQaPreset.FreshMap);
            CampaignRuntimeController controller = CreateController(progress);

            Assert.That(controller.OpenChapter("power_station"), Is.True);
            Assert.That(Find(controller.CampaignView.transform,
                "Technical Neon Selector Module"), Is.Not.Null);
            Assert.That(controller.StartLevel("power_01"), Is.True);
            BoardController board = controller.GetComponentInChildren<BoardController>(true);
            Assert.That(board, Is.Not.Null);
            Assert.That(board.BoardView.VisualTheme, Is.SameAs(campaign.GameplayVisualTheme));
            Assert.That(board.BoardView.VisualTheme, Is.Not.SameAs(campaign.CampaignUiTheme));

            controller.ShowCurrentChapter();
            Assert.That(controller.CampaignView.IsVisible, Is.True);
            Assert.That(Find(controller.CampaignView.transform,
                "Technical Neon Selector Module"), Is.Not.Null);
            controller.ShowMap();
            Assert.That(Find(controller.CampaignView.transform,
                "Technical Neon Map Header Module"), Is.Not.Null);
        }

        [Test]
        public void ThemedMap_PreservesM13GeometryAndCentralGridSafeAreaThroughScaling()
        {
            CampaignProgressService progress =
                NarrativeQaStateBuilder.Build(campaign, NarrativeQaPreset.FinalRestorationPending);
            CampaignRuntimeView view = BuildView(progress, theme, out GameObject root);
            Assert.That(view.TryPrepareRestoration(progress.PendingRestoration, out var plan),
                Is.True);

            CityChapterNodeView[] nodes = root
                .GetComponentsInChildren<CityChapterNodeView>(true)
                .OrderBy(node => Array.FindIndex(campaign.Chapters.ToArray(), chapter =>
                    chapter.ChapterId == node.ChapterId)).ToArray();
            for (int index = 0; index < nodes.Length; index++)
            {
                CityMapLayoutEntry authored = CityMapLayoutCatalog.Production.Entries[index];
                Assert.That(nodes[index].HitArea.sizeDelta, Is.EqualTo(authored.HitSize));
                Assert.That(nodes[index].HitArea.anchoredPosition,
                    Is.EqualTo(authored.Position));
            }

            CityChapterNodeView central = nodes.Single(node =>
                node.ChapterId == "central_grid");
            RectTransform silhouette = central.transform.Find("Building Silhouette")
                .GetComponent<RectTransform>();
            view.ApplyRestoredNodePowerUp(plan, 1f);
            Assert.That(central.HitArea.sizeDelta, Is.EqualTo(new Vector2(350f, 330f)));
            Assert.That(central.HitArea.anchoredPosition, Is.EqualTo(new Vector2(40f, -300f)));
            Assert.That(silhouette.localScale.x, Is.EqualTo(1.075f).Within(0.0001f));
            Assert.That(silhouette.anchoredPosition.y, Is.EqualTo(76f));
            float silhouetteBottom = silhouette.anchoredPosition.y -
                                     silhouette.sizeDelta.y * silhouette.localScale.y * 0.5f;
            float labelTop = central.Label.rectTransform.anchoredPosition.y +
                             central.Label.rectTransform.sizeDelta.y * 0.5f;
            Assert.That(silhouetteBottom, Is.GreaterThan(labelTop));
        }

        [Test]
        public void ThemedSelector_PreservesAcceptedLayoutAndDistinctStates()
        {
            var progress = new CampaignProgressService(campaign);
            progress.SetIntroCompleted(true);
            CompleteLevels(progress, campaign.Chapters[0], 2);
            CampaignRuntimeView view = BuildView(progress, theme, out GameObject root);
            view.ShowChapter(campaign.Chapters[0]);

            Assert.That(view.ChapterBriefingTitle.fontSize, Is.EqualTo(36));
            Assert.That(view.ChapterBriefingBody.fontSize, Is.EqualTo(46));
            Assert.That(view.LevelGrid.GetComponentsInChildren<HorizontalLayoutGroup>(true)
                .Select(row => row.transform.childCount), Is.EqualTo(new[] { 3, 3, 3, 1 }));
            foreach (Text number in view.LevelGrid.GetComponentsInChildren<Text>(true)
                         .Where(text => text.name == "Level Number"))
                Assert.That(number.fontSize, Is.EqualTo(76));
            Button back = Find(root.transform, "Back To Map").GetComponent<Button>();
            Assert.That(back.transform.Find("Label").GetComponent<Text>().fontSize,
                Is.EqualTo(48));
            Assert.That(back.interactable, Is.True);

            Button completed = Find(root.transform, "Generated Level 1")
                .GetComponent<Button>();
            Button available = Find(root.transform, "Generated Level 3")
                .GetComponent<Button>();
            Button locked = Find(root.transform, "Generated Level 4")
                .GetComponent<Button>();
            Assert.That(completed.targetGraphic.color, Is.EqualTo(theme.CompletedSurface));
            Assert.That(available.targetGraphic.color, Is.EqualTo(theme.AvailableSurface));
            Assert.That(locked.targetGraphic.color, Is.EqualTo(theme.LockedSurface));
            Assert.That(new[] { completed.interactable, available.interactable,
                locked.interactable }, Is.EqualTo(new[] { true, true, false }));
        }

        [TestCase("power_station")]
        [TestCase("substation")]
        [TestCase("control_center")]
        [TestCase("automation_plant")]
        public void NonFinalRestorationStatus_BindsAuthoredChapterAndUsesTheme(string chapterId)
        {
            CampaignProgressService progress =
                NarrativeQaStateBuilder.Build(campaign, NarrativeQaPreset.FreshMap);
            CampaignRuntimeView view = BuildView(progress, theme, out _);
            view.ShowMap();

            Assert.That(view.ShowRestorationStatus(chapterId), Is.True);
            Assert.That(view.RestorationStatus.CurrentChapterId, Is.EqualTo(chapterId));
            Assert.That(narrative.TryGetChapterNarrative(chapterId, out var entry), Is.True);
            Assert.That(view.RestorationStatus.TitleText.text, Is.EqualTo(entry.RestoredTitle));
            Assert.That(view.RestorationStatus.BodyText.text, Is.EqualTo(entry.RestoredBody));
            Assert.That(view.RestorationStatus.PanelRect.GetComponent<Outline>().effectColor,
                Is.EqualTo(theme.SuccessAccent));
        }

        [Test]
        public void FinalRestoration_HasNoStatusAndPreservesSequenceTiming()
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign,
                NarrativeQaPreset.FinalRestorationPending);
            CampaignRuntimeView view = BuildView(progress, theme, out GameObject root);
            Assert.That(view.TryPrepareRestoration(progress.PendingRestoration, out var plan),
                Is.True);
            Assert.That(plan.IncludesFinalNetworkPulse, Is.True);
            Assert.That(view.ShowRestorationStatus("central_grid"), Is.False);
            Assert.That(view.RestorationStatus.CurrentChapterId, Is.Null);

            var sequence = root.AddComponent<CityRestorationSequenceController>();
            Assert.That(sequence.StatusTailHoldSeconds,
                Is.EqualTo(ProgrammerUiMetrics.RestorationStatusHoldSeconds));
            Assert.That(sequence.StatusTailFadeSeconds,
                Is.EqualTo(ProgrammerUiMetrics.RestorationStatusFadeSeconds));
        }

        [Test]
        public void EndingPending_UsesThemedExactCopyAndReturnsToThemedMap()
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign,
                NarrativeQaPreset.EndingPending);
            CampaignRuntimeController controller = CreateController(progress);
            CampaignEndingNarrative ending = narrative.EndingNarrative;

            Assert.That(controller.EndingView.IsVisible, Is.True);
            Assert.That(controller.EndingView.TitleText.text, Is.EqualTo(ending.Title));
            Assert.That(controller.EndingView.BodyText.text, Is.EqualTo(ending.Body));
            Assert.That(controller.EndingView.StatusTitleText.text,
                Is.EqualTo(ending.StatusTitle));
            Assert.That(controller.EndingView.StatusBodyText.text,
                Is.EqualTo(ending.StatusBody));
            Assert.That(controller.EndingView.PrimaryLabel.text,
                Is.EqualTo(ending.ReturnButtonLabel));
            Assert.That(Find(controller.EndingView.transform,
                "Technical Neon Ending Module"), Is.Not.Null);

            controller.EndingView.PressReturnToCity();
            Assert.That(progress.EndingCompleted, Is.True);
            Assert.That(controller.EndingView.IsVisible, Is.False);
            Assert.That(Find(controller.CampaignView.transform,
                "Technical Neon Map Header Module"), Is.Not.Null);
        }

        [Test]
        public void ProductionThemeLayers_AreNonBlockingAndContainNoPrototypeControls()
        {
            CampaignRuntimeController controller = CreateController(
                NarrativeQaStateBuilder.Build(campaign, NarrativeQaPreset.FreshMap));
            controller.OpenChapter("power_station");
            Transform[] transforms = controller.GetComponentsInChildren<Transform>(true);
            string[] forbidden = { "Preview INTRO", "Preview SELECTOR", "Preview RESTORED",
                "Preview ENDING", "Preview MAP", "M15-D1 Preview Controls" };
            Assert.That(transforms.Select(item => item.name),
                Has.None.Matches<string>(name => forbidden.Contains(name)));
            foreach (Image image in controller.GetComponentsInChildren<Image>(true)
                         .Where(image => image.name.StartsWith("Technical Neon") ||
                                         image.transform.parent != null &&
                                         image.transform.parent.name.StartsWith("Technical Neon")))
                Assert.That(image.raycastTarget, Is.False, image.name);
        }

        [Test]
        public void RepeatedMapSelectorNavigation_DoesNotAccumulateThemeHierarchy()
        {
            CampaignRuntimeView view = BuildView(
                NarrativeQaStateBuilder.Build(campaign, NarrativeQaPreset.FreshMap), theme,
                out GameObject root);
            view.ShowMap();
            view.ShowChapter(campaign.Chapters[0]);
            view.ShowMap();
            int themeChildren = CountThemeObjects(root);
            int outlines = root.GetComponentsInChildren<Outline>(true).Length;

            for (int index = 0; index < 8; index++)
            {
                view.ShowChapter(campaign.Chapters[0]);
                view.ShowMap();
            }

            Assert.That(CountThemeObjects(root), Is.EqualTo(themeChildren));
            Assert.That(root.GetComponentsInChildren<Outline>(true).Length,
                Is.EqualTo(outlines));
        }

        [Test]
        public void ApplyingTheme_DoesNotMutateProgressAndThemeIsAbsentFromSaveJson()
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign,
                NarrativeQaPreset.FirstRestorationPending);
            int stars = progress.TotalStars;
            int pending = progress.PendingRestorationCount;
            bool intro = progress.IntroCompleted;
            bool ending = progress.EndingCompleted;
            CampaignRuntimeView view = BuildView(progress, theme, out _);
            view.ShowMap();
            view.ShowChapter(campaign.Chapters[0]);
            view.ShowMap();

            Assert.That(progress.TotalStars, Is.EqualTo(stars));
            Assert.That(progress.PendingRestorationCount, Is.EqualTo(pending));
            Assert.That(progress.IntroCompleted, Is.EqualTo(intro));
            Assert.That(progress.EndingCompleted, Is.EqualTo(ending));

            string directory = Path.Combine(Path.GetTempPath(), "NeonGridM15D2",
                Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, CampaignSaveStore.SaveFileName);
            try
            {
                Assert.That(new CampaignSaveStore(path).Save(progress).Succeeded, Is.True);
                string json = File.ReadAllText(path);
                Assert.That(json, Does.Not.Contain("campaignUiTheme"));
                Assert.That(json, Does.Not.Contain(theme.ThemeId));
                Assert.That(typeof(CampaignSaveData).GetFields(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(field => field.FieldType == typeof(CampaignUiThemeDefinition)), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [TestCase(NarrativeQaPreset.FreshIntro, true, false, false)]
        [TestCase(NarrativeQaPreset.FreshMap, false, true, false)]
        [TestCase(NarrativeQaPreset.FirstRestorationPending, false, true, false)]
        [TestCase(NarrativeQaPreset.FinalRestorationPending, false, true, false)]
        [TestCase(NarrativeQaPreset.EndingPending, false, false, true)]
        [TestCase(NarrativeQaPreset.PostEndingComplete, false, true, false)]
        public void StartupPrecedence_RemainsAuthoritativeAndThemed(NarrativeQaPreset preset,
            bool expectsIntro, bool expectsMap, bool expectsEnding)
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign, preset);
            CampaignRuntimeController controller = CreateController(progress);

            Assert.That(controller.IntroView != null && controller.IntroView.IsVisible,
                Is.EqualTo(expectsIntro));
            Assert.That(controller.CampaignView.IsVisible, Is.EqualTo(expectsMap));
            Assert.That(controller.EndingView != null && controller.EndingView.IsVisible,
                Is.EqualTo(expectsEnding));
            if (expectsIntro)
                Assert.That(Find(controller.IntroView.transform,
                    "Technical Neon Narrative Module"), Is.Not.Null);
            if (expectsMap)
                Assert.That(Find(controller.CampaignView.transform,
                    "Technical Neon Map Header Module"), Is.Not.Null);
            if (expectsEnding)
                Assert.That(Find(controller.EndingView.transform,
                    "Technical Neon Ending Module"), Is.Not.Null);
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2340)]
        [TestCase(720, 1280)]
        public void ProductionTheme_FitsAllRepresentativeSurfaces(int width, int height)
        {
            CampaignProgressService progress = NarrativeQaStateBuilder.Build(campaign,
                NarrativeQaPreset.FreshMap);
            CampaignRuntimeView view = BuildView(progress, theme, out GameObject root);
            view.ShowMap();
            AssertSurface(root, "Technical Neon Map Header Module", width, height);
            view.ShowChapter(campaign.Chapters[0]);
            AssertSurface(root, "Technical Neon Selector Module", width, height);
            view.ShowMap();
            Assert.That(view.ShowRestorationStatus("power_station"), Is.True);
            AssertSurface(root, "Restoration Narrative Status", width, height);

            GameObject introRoot = NewObject("Production Intro Layout");
            CampaignIntroView intro = introRoot.AddComponent<CampaignIntroView>();
            intro.Build(narrative, () => false, theme);
            AssertSurface(introRoot, "Technical Neon Narrative Module", width, height);
            GameObject endingRoot = NewObject("Production Ending Layout");
            CampaignEndingView ending = endingRoot.AddComponent<CampaignEndingView>();
            ending.Build(narrative.EndingNarrative, () => false, theme);
            AssertSurface(endingRoot, "Technical Neon Ending Module", width, height);
        }

        private CampaignRuntimeController CreateController(CampaignProgressService progress)
        {
            EnsureCamera();
            GameObject root = NewObject("M15-D2 Production Controller");
            var controller = root.AddComponent<CampaignRuntimeController>();
            controller.Initialize(campaign, new MemoryStore(progress));
            return controller;
        }

        private CampaignRuntimeView BuildView(CampaignProgressService progress,
            CampaignUiThemeDefinition uiTheme, out GameObject root)
        {
            root = NewObject("M15-D2 Campaign View");
            var view = root.AddComponent<CampaignRuntimeView>();
            view.Build(campaign, progress, _ => { }, _ => { }, () => { }, uiTheme);
            return view;
        }

        private void EnsureCamera()
        {
            if (Camera.main != null) return;
            GameObject cameraObject = NewObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
        }

        private GameObject NewObject(string name)
        {
            var instance = new GameObject(name);
            cleanup.Add(instance);
            return instance;
        }

        private static void CompleteLevels(CampaignProgressService progress,
            CampaignChapterDefinition chapter, int count)
        {
            for (int index = 0; index < count; index++)
            {
                CampaignLevelEntry level = chapter.Levels[index];
                int optimal = level.AuthoredOptimalMoves ?? 1;
                var stars = new StarEvaluator().Evaluate(true, optimal, optimal);
                var result = new SessionCompletionResult(level.LevelDefinition, optimal, 1f,
                    optimal, false, stars);
                Assert.That(progress.RecordCompletion(level.LevelId, result).Accepted, Is.True);
            }
        }

        private static int CountThemeObjects(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name.StartsWith("Technical Neon"));
        }

        private static void AssertSurface(GameObject root, string surfaceName,
            int width, int height)
        {
            Transform surface = Find(root.transform, surfaceName);
            Assert.That(surface, Is.Not.Null, surfaceName);
            RectTransform rect = surface.GetComponent<RectTransform>();
            float scale = Mathf.Sqrt((width / 1080f) * (height / 1920f));
            float virtualWidth = width / scale;
            float virtualHeight = height / scale;
            Assert.That(rect.sizeDelta.x, Is.LessThanOrEqualTo(virtualWidth + 0.01f));
            Assert.That(rect.sizeDelta.y, Is.LessThanOrEqualTo(virtualHeight + 0.01f));
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == name);
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            private readonly CampaignProgressService progress;
            public string SavePath => "memory://m15-d2";

            public MemoryStore(CampaignProgressService seededProgress)
            {
                progress = seededProgress;
            }

            public CampaignLoadResult Load(CampaignDefinition definition) =>
                new CampaignLoadResult(CampaignLoadStatus.Loaded, progress,
                    Array.Empty<string>());

            public CampaignSaveResult Save(CampaignProgressService current) =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);

            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }
    }
}

using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Session;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Tests
{
    public sealed class PresentationSmokeTests
    {
        [Test]
        public void TileInput_ForwardsOneTapWithItsGridPosition()
        {
            var gameObject = new GameObject("Input Test");
            var input = gameObject.AddComponent<CircuitTileInput>();
            int tapCount = 0;
            GridPosition tappedPosition = default;
            input.Initialize(new GridPosition(2, 3), position =>
            {
                tapCount++;
                tappedPosition = position;
            });

            input.HandleTap();

            Assert.That(tapCount, Is.EqualTo(1));
            Assert.That(tappedPosition, Is.EqualTo(new GridPosition(2, 3)));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SessionHudCommands_DoNotCreateAccidentalTileMoves()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_02");
            Assert.That(level, Is.Not.Null);
            var root = new GameObject("M5 Presentation Smoke Test");

            try
            {
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(level);
                Transform moveLabelTransform = root.transform.Find("Gameplay HUD Canvas/Move Count");
                Assert.That(moveLabelTransform, Is.Not.Null);
                Text moveLabel = moveLabelTransform.GetComponent<Text>();
                RectTransform moveRect = moveLabel.rectTransform;

                Assert.That(moveLabel.text, Is.EqualTo("Moves: 0"));
                Assert.That(moveRect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(moveRect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(moveRect.pivot, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(moveRect.anchoredPosition.x, Is.LessThanOrEqualTo(0f),
                    "The right-aligned label must end inside the canvas instead of extending off-screen.");

                Assert.That(controller.Session.InteractWithTile(new GridPosition(0, 0)), Is.False);
                Assert.That(moveLabel.text, Is.EqualTo("Moves: 0"));

                controller.Session.InteractWithTile(new GridPosition(1, 0));
                Assert.That(moveLabel.text, Is.EqualTo("Moves: 1"));

                Assert.That(controller.Undo(), Is.True);
                Assert.That(controller.Session.MoveCount, Is.EqualTo(1));
                Assert.That(moveLabel.text, Is.EqualTo("Moves: 1"));

                controller.Restart();
                Assert.That(controller.Session.MoveCount, Is.Zero);
                Assert.That(moveLabel.text, Is.EqualTo("Moves: 0"));

                controller.Session.InteractWithTile(new GridPosition(2, 0));
                Assert.That(moveLabel.text, Is.EqualTo("Moves: 1"));
                controller.Restart();
                Assert.That(moveLabel.text, Is.EqualTo("Moves: 0"));

                controller.Session.AdvanceTime(GameplaySession.HintUnlockSeconds);
                HintResult hint = controller.RequestHint();
                Assert.That(hint.Status, Is.EqualTo(HintStatus.HintAvailable));
                Assert.That(controller.Session.MoveCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(0, "LEVEL 1")]
        [TestCase(4, "LEVEL 5")]
        [TestCase(8, "LEVEL 9")]
        [TestCase(9, "LEVEL 10")]
        public void CampaignGameplayHud_DisplaysOrdinalFromProductionChapterOrdering(
            int levelIndex, string expectedLabel)
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            CampaignChapterDefinition chapter = campaign.Chapters[0];
            CampaignLevelEntry entry = chapter.Levels[levelIndex];
            var root = new GameObject("M7 Level Identity Smoke Test");

            try
            {
                int ordinal = CampaignRuntimeController.FindLevelOrdinal(chapter, entry);
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(new GameplaySession(entry.LevelDefinition),
                    new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                        () => { }, () => default), entry.Tutorial, ordinal);

                Transform canvas = root.transform.Find("Gameplay HUD Canvas");
                Text identity = canvas.Find("Level Identity").GetComponent<Text>();
                Text timer = canvas.Find("Timer").GetComponent<Text>();
                Assert.That(identity.text, Is.EqualTo(expectedLabel));
                Assert.That(identity.text, Does.Not.Match(@"LEVEL 0\d"));
                Assert.That(canvas.Find("Move Count"), Is.Not.Null);
                Assert.That(canvas.Find("Back To Levels Button"), Is.Not.Null);
                Assert.That(identity.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(timer.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(identity.rectTransform.anchoredPosition.x, Is.Zero);
                Assert.That(timer.rectTransform.anchoredPosition.x, Is.Zero);
                Assert.That(identity.fontSize, Is.GreaterThan(timer.fontSize));
                Assert.That(timer.fontSize,
                    Is.GreaterThan(canvas.Find("Move Count").GetComponent<Text>().fontSize));
                float identityBottom = identity.rectTransform.anchoredPosition.y -
                                       identity.rectTransform.sizeDelta.y;
                Assert.That(timer.rectTransform.anchoredPosition.y,
                    Is.LessThanOrEqualTo(identityBottom),
                    "Time must be directly below the centered level identity.");
                AssertInsideCanvas(identity.rectTransform, 1080f, 1920f);
                AssertInsideCanvas(timer.rectTransform, 1080f, 1920f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampaignGameplayOrdinal_DoesNotDependOnNumericStableId()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_02");
            var first = new CampaignLevelEntry("alpha_node", "Intro", level);
            var second = new CampaignLevelEntry("final_node", "Final", level);
            var chapter = new CampaignChapterDefinition("chapter", "Chapter", new[]
            {
                first,
                second
            });

            Assert.That(CampaignRuntimeController.FindLevelOrdinal(chapter, first), Is.EqualTo(1));
            Assert.That(CampaignRuntimeController.FindLevelOrdinal(chapter, second), Is.EqualTo(2));
        }

        [Test]
        public void TutorialPresentation_TracksAcceptedActionsAndCoexistsWithHintHighlight()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            LevelDefinition level = campaign.Chapters[0].Levels[2].LevelDefinition;
            LevelTutorialDefinition tutorial = campaign.Chapters[0].Levels[0].Tutorial;
            var root = new GameObject("M7 Tutorial Presentation Smoke Test");

            try
            {
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(new GameplaySession(level),
                    new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                        () => { }, () => default), tutorial);
                Transform canvas = root.transform.Find("Gameplay HUD Canvas");
                Transform panel = canvas.Find("Tutorial Panel");
                CircuitTileView targetView = root.transform.Find("Tile 1,0")
                    .GetComponent<CircuitTileView>();

                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.Find("Tutorial Message").GetComponent<Text>().text,
                    Is.EqualTo("Tap a wire to rotate it."));
                Assert.That(panel.GetComponent<Image>().raycastTarget, Is.False,
                    "The callout must not block board input.");
                Assert.That(panel.Find("Tutorial Message").GetComponent<Text>().raycastTarget, Is.False);
                Assert.That(targetView.IsTutorialHighlighted, Is.True);
                Assert.That(canvas.Find("Move Count"), Is.Not.Null);
                Assert.That(canvas.Find("Timer"), Is.Not.Null);
                Assert.That(canvas.Find("Undo Button"), Is.Not.Null);
                Assert.That(canvas.Find("Restart Button"), Is.Not.Null);
                Assert.That(canvas.Find("Hint Button"), Is.Not.Null);
                Assert.That(canvas.Find("Back To Levels Button"), Is.Not.Null);

                RectTransform tutorialRect = panel.GetComponent<RectTransform>();
                RectTransform timerRect = canvas.Find("Timer").GetComponent<RectTransform>();
                float tutorialTop = tutorialRect.anchoredPosition.y +
                                    tutorialRect.sizeDelta.y * (1f - tutorialRect.pivot.y);
                float timerBottom = timerRect.anchoredPosition.y -
                                    timerRect.sizeDelta.y * timerRect.pivot.y;
                Assert.That(tutorialTop, Is.LessThan(timerBottom),
                    "The tutorial callout must sit below the top HUD without overlap.");
                float tutorialBottomInset = -tutorialRect.anchoredPosition.y +
                                            tutorialRect.sizeDelta.y * tutorialRect.pivot.y;
                Assert.That(tutorialBottomInset,
                    Is.LessThanOrEqualTo(GameplayLayoutMetrics.TopHudReservedPixels),
                    "The tutorial must remain entirely within the accepted top reservation.");
                AssertInsideCanvas(tutorialRect, 1080f, 1920f);

                canvas.Find("Back To Levels Button").GetComponent<Button>().onClick.Invoke();
                Transform leaveConfirmation = canvas.Find("Leave Confirmation");
                Assert.That(leaveConfirmation.gameObject.activeSelf, Is.True);
                leaveConfirmation.Find("Cancel Button").GetComponent<Button>().onClick.Invoke();
                Assert.That(leaveConfirmation.gameObject.activeSelf, Is.False);
                Assert.That(controller.Tutorial.IsActive, Is.True);
                Assert.That(canvas.Find("Tutorial Panel"), Is.Not.Null,
                    "Cancelling leave must preserve attempt-local tutorial state.");

                Assert.That(controller.PerformPlayerAction(new GridPosition(0, 0)), Is.False);
                Assert.That(controller.Tutorial.IsActive, Is.True);
                Assert.That(controller.Session.MoveCount, Is.Zero);

                Assert.That(controller.PerformPlayerAction(new GridPosition(2, 0)), Is.True,
                    "An unrelated valid action remains available during onboarding.");
                Assert.That(controller.Tutorial.IsActive, Is.True);
                Assert.That(canvas.Find("Tutorial Panel"), Is.Not.Null);

                Assert.That(controller.PerformPlayerAction(new GridPosition(1, 0)), Is.True);
                Assert.That(controller.Tutorial.IsActive, Is.False);
                Assert.That(canvas.Find("Tutorial Panel"), Is.Null);
                Assert.That(targetView.IsTutorialHighlighted, Is.False);
                Assert.That(controller.Session.MoveCount, Is.EqualTo(2));

                Assert.That(controller.Undo(), Is.True);
                Assert.That(controller.Tutorial.IsActive, Is.False,
                    "Undo must not reopen an attempt-local completed step.");
                Assert.That(canvas.Find("Tutorial Panel"), Is.Null);

                controller.Restart();
                Assert.That(controller.Tutorial.IsActive, Is.True);
                Assert.That(canvas.Find("Tutorial Panel"), Is.Not.Null);
                Assert.That(controller.Session.MoveCount, Is.Zero);

                controller.Session.AdvanceTime(GameplaySession.HintUnlockSeconds);
                HintResult hint = controller.RequestHint();
                Assert.That(hint.Status, Is.EqualTo(HintStatus.HintAvailable));
                Assert.That(targetView.IsTutorialHighlighted, Is.True,
                    "Requesting a hint must not clear tutorial state.");
                CircuitTileView hintView = root.transform.Find(
                    $"Tile {hint.SuggestedAction.Value.Position.x},{hint.SuggestedAction.Value.Position.y}")
                    .GetComponent<CircuitTileView>();
                Assert.That(hintView.IsHintHighlighted, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GameplayReadability_UsesRoleHierarchyAndKeepsCriticalPanelsInBounds()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_02");
            var session = new GameplaySession(level);
            var root = new GameObject("M7 Readability Smoke Test");

            try
            {
                var hud = root.AddComponent<GameplayHudView>();
                hud.Build(() => { }, () => { }, () => { },
                    new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                        () => { }, () => CampaignResultNavigationState.Normal(true)));
                hud.ShowTutorial("T-junctions split power into multiple paths.");
                hud.Refresh(session);

                Transform canvas = root.transform.Find("Gameplay HUD Canvas");
                Text tutorial = canvas.Find("Tutorial Panel/Tutorial Message").GetComponent<Text>();
                Text timer = canvas.Find("Timer").GetComponent<Text>();
                Text moves = canvas.Find("Move Count").GetComponent<Text>();
                Text hint = canvas.Find("Hint Status").GetComponent<Text>();
                Assert.That(tutorial.fontSize, Is.GreaterThan(timer.fontSize));
                Assert.That(timer.fontSize, Is.GreaterThan(moves.fontSize));
                Assert.That(moves.fontSize, Is.GreaterThan(hint.fontSize));
                Canvas.ForceUpdateCanvases();
                Assert.That(tutorial.preferredWidth,
                    Is.LessThanOrEqualTo(tutorial.rectTransform.rect.width),
                    "The longest current tutorial must remain on one readable line.");
                AssertInsideCanvas(canvas.Find("Tutorial Panel").GetComponent<RectTransform>(),
                    1080f, 1920f);

                canvas.Find("Back To Levels Button").GetComponent<Button>().onClick.Invoke();
                Transform modal = canvas.Find("Leave Confirmation");
                Text modalMessage = modal.Find("Message").GetComponent<Text>();
                Text cancelLabel = modal.Find("Cancel Button/Label").GetComponent<Text>();
                Assert.That(modalMessage.fontSize, Is.GreaterThan(cancelLabel.fontSize));
                Assert.That(modal.Find("Cancel Button").GetComponent<RectTransform>().sizeDelta.y,
                    Is.GreaterThanOrEqualTo(100f));
                Assert.That(modal.Find("Leave Button").GetComponent<RectTransform>().sizeDelta.y,
                    Is.GreaterThanOrEqualTo(100f));
                modal.Find("Cancel Button").GetComponent<Button>().onClick.Invoke();

                session.InteractWithTile(new GridPosition(1, 0));
                session.InteractWithTile(new GridPosition(2, 0));
                hud.Refresh(session);

                Transform completion = canvas.Find("Completion Panel");
                Text title = completion.Find("Completion Title").GetComponent<Text>();
                Text stats = completion.Find("Completion Stats").GetComponent<Text>();
                Assert.That(title.text, Is.EqualTo("LEVEL COMPLETE"));
                Assert.That(stats.text, Does.Contain("Moves:"));
                Assert.That(stats.text, Does.Contain("Optimal:"));
                Assert.That(stats.text, Does.Contain("Time:"));
                Assert.That(stats.text, Does.Contain("Stars:"));
                Assert.That(title.fontSize, Is.GreaterThan(stats.fontSize));
                Assert.That(stats.fontSize, Is.GreaterThan(hint.fontSize));
                Assert.That(completion.Find("Retry Button/Label").GetComponent<Text>().fontSize,
                    Is.GreaterThan(hint.fontSize));
                Assert.That(completion.Find("Retry Button").GetComponent<RectTransform>().sizeDelta.y,
                    Is.GreaterThanOrEqualTo(90f));
                AssertInsideCanvas(completion.GetComponent<RectTransform>(), 1080f, 1920f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GameplayWithoutTutorial_HasNoTutorialUiOrHighlight()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            CampaignLevelEntry ps03 = campaign.Chapters[0].Levels[2];
            var root = new GameObject("M7 No Tutorial Presentation Smoke Test");

            try
            {
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(new GameplaySession(ps03.LevelDefinition), null, ps03.Tutorial);

                Assert.That(controller.Tutorial.IsActive, Is.False);
                Assert.That(root.transform.Find("Gameplay HUD Canvas/Tutorial Panel"), Is.Null);
                foreach (CircuitTileView view in root.GetComponentsInChildren<CircuitTileView>())
                    Assert.That(view.IsTutorialHighlighted, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampaignMapAndLevelSelection_ReflectProgressServiceState()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>("Campaigns/M6_Test_Campaign");
            Assert.That(campaign, Is.Not.Null);
            var progress = new CampaignProgressService(campaign);
            var root = new GameObject("M6 Campaign Presentation Smoke Test");
            string startedLevelId = null;
            int backToMapCount = 0;

            try
            {
                var view = root.AddComponent<CampaignRuntimeView>();
                view.Build(campaign, progress, _ => { }, id => startedLevelId = id,
                    () => backToMapCount++);
                view.ShowMap();

                Transform map = root.transform.Find("Campaign Canvas/Campaign Map");
                Assert.That(map.Find("Total Stars").GetComponent<Text>().text, Is.EqualTo("Stars: 0 / 18"));
                Assert.That(map.Find("Chapter 1/Label").GetComponent<Text>().text, Does.Contain("AVAILABLE"));
                Assert.That(map.Find("Chapter 1").GetComponent<Button>().interactable, Is.True);
                Assert.That(map.Find("Chapter 2/Label").GetComponent<Text>().text, Does.Contain("LOCKED"));
                Assert.That(map.Find("Chapter 2").GetComponent<Button>().interactable, Is.False);
                Text mapTitle = map.Find("Title").GetComponent<Text>();
                Text stars = map.Find("Total Stars").GetComponent<Text>();
                Text chapterLabel = map.Find("Chapter 1/Label").GetComponent<Text>();
                Assert.That(mapTitle.fontSize, Is.GreaterThan(stars.fontSize));
                Assert.That(stars.fontSize, Is.GreaterThan(chapterLabel.fontSize));
                AssertInsideCanvas(mapTitle.rectTransform, 1080f, 1920f);
                AssertInsideCanvas(stars.rectTransform, 1080f, 1920f);
                AssertInsideCanvas(map.Find("Chapter 1").GetComponent<RectTransform>(),
                    1080f, 1920f);

                view.ShowChapter(campaign.Chapters[0]);
                Transform selection = root.transform.Find("Campaign Canvas/Level Selection");
                Transform row = selection.Find("Generated Level Grid/Generated Level Row 1");
                Button first = row.Find("Generated Level 1").GetComponent<Button>();
                Button second = row.Find("Generated Level 2").GetComponent<Button>();
                Button third = row.Find("Generated Level 3").GetComponent<Button>();
                Assert.That(first.interactable, Is.True);
                Assert.That(second.interactable, Is.False);
                Assert.That(third.interactable, Is.False);
                Assert.That(second.transform.Find("Lock Icon"), Is.Not.Null);
                Assert.That(first.transform.Find("State").GetComponent<Text>().text, Is.Empty,
                    "An incomplete level must not display fake star results.");
                first.onClick.Invoke();
                Assert.That(startedLevelId, Is.EqualTo(campaign.Chapters[0].Levels[0].LevelId));

                Button back = selection.Find("Back To Map").GetComponent<Button>();
                Assert.That(back, Is.Not.Null);
                back.onClick.Invoke();
                Assert.That(backToMapCount, Is.EqualTo(1));

                CampaignLevelEntry completedLevel = campaign.Chapters[0].Levels[0];
                progress.RecordCompletion(completedLevel.LevelId,
                    CampaignTestFixture.Result(completedLevel.LevelDefinition, 2, 10f, 3));
                view.ShowChapter(campaign.Chapters[0]);

                selection = root.transform.Find("Campaign Canvas/Level Selection");
                row = selection.Find("Generated Level Grid/Generated Level Row 1");
                Assert.That(row.Find("Generated Level 1/State").GetComponent<Text>().text,
                    Is.EqualTo("★★★"));
                Assert.That(row.Find("Generated Level 1").GetComponent<Button>().interactable,
                    Is.True, "Completed levels must remain replayable.");
                Assert.That(row.Find("Generated Level 2").GetComponent<Button>().interactable,
                    Is.True, "Returning to the selected chapter must refresh newly unlocked levels.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PowerStationChapter_RendersTenTilesAsThreeThreeThreeOne()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/PowerStation_VerticalSlice");
            var progress = new CampaignProgressService(campaign);
            var root = new GameObject("M7 Ten Level Grid Smoke Test");

            try
            {
                var view = root.AddComponent<CampaignRuntimeView>();
                view.Build(campaign, progress, _ => { }, _ => { }, () => { });
                view.ShowChapter(campaign.Chapters[0]);

                Transform selection = root.transform.Find("Campaign Canvas/Level Selection");
                Transform grid = selection.Find("Generated Level Grid");
                Text chapterTitle = selection.Find("Generated Chapter Title").GetComponent<Text>();
                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
                Assert.That(grid.childCount, Is.EqualTo(4));
                Assert.That(grid.GetChild(0).childCount, Is.EqualTo(3));
                Assert.That(grid.GetChild(1).childCount, Is.EqualTo(3));
                Assert.That(grid.GetChild(2).childCount, Is.EqualTo(3));
                Assert.That(grid.GetChild(3).childCount, Is.EqualTo(1));
                Assert.That(grid.GetChild(3).GetComponent<HorizontalLayoutGroup>().childAlignment,
                    Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(chapterTitle.fontSize,
                    Is.GreaterThan(grid.GetChild(0).Find("Generated Level 1/Level Number")
                        .GetComponent<Text>().fontSize),
                    "The chapter name must be the selector's primary heading.");
                VerticalLayoutGroup verticalLayout = grid.GetComponent<VerticalLayoutGroup>();
                float horizontalSpacing = grid.GetChild(0).GetComponent<HorizontalLayoutGroup>().spacing;
                Assert.That(verticalLayout.spacing, Is.GreaterThan(horizontalSpacing),
                    "Selector rows should be opened vertically without changing card columns.");
                RectTransform finalRow = grid.GetChild(3).GetComponent<RectTransform>();
                RectTransform finalTile = grid.GetChild(3).GetChild(0).GetComponent<RectTransform>();
                Assert.That(finalTile.anchoredPosition.x,
                    Is.EqualTo(finalRow.rect.width * 0.5f).Within(0.1f));
                Assert.That(selection.GetComponentInChildren<ScrollRect>(true), Is.Null,
                    "The chapter grid must not depend on scrolling.");
                RectTransform gridRect = grid.GetComponent<RectTransform>();
                RectTransform backRect = selection.Find("Back To Map").GetComponent<RectTransform>();
                float gridBottom = 1920f + gridRect.anchoredPosition.y - gridRect.sizeDelta.y;
                float backTop = backRect.anchoredPosition.y +
                                backRect.sizeDelta.y * (1f - backRect.pivot.y);
                Assert.That(gridBottom, Is.GreaterThan(backTop),
                    "BACK TO MAP must remain below the grid with a positive visual gap.");
                AssertInsideCanvas(backRect, 1080f, 1920f);

                foreach (Transform row in grid)
                {
                    Assert.That(row.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
                    foreach (Transform tile in row)
                    {
                        RectTransform rect = tile.GetComponent<RectTransform>();
                        Assert.That(rect.sizeDelta.x, Is.EqualTo(rect.sizeDelta.y));
                        Assert.That(rect.sizeDelta.x, Is.LessThan(300f),
                            "Level entries must be compact tiles rather than full-width strips.");
                    }
                }

                Button first = grid.GetChild(0).Find("Generated Level 1").GetComponent<Button>();
                Assert.That(first.interactable, Is.True);
                for (int index = 2; index <= 10; index++)
                {
                    int rowIndex = (index - 1) / 3;
                    Button locked = grid.GetChild(rowIndex).Find($"Generated Level {index}")
                        .GetComponent<Button>();
                    Assert.That(locked.interactable, Is.False);
                    Assert.That(locked.transform.Find("Lock Icon"), Is.Not.Null);
                }

                CampaignLevelEntry completed = campaign.Chapters[0].Levels[0];
                progress.RecordCompletion(completed.LevelId,
                    CampaignTestFixture.Result(completed.LevelDefinition, 2, 10f, 2));
                view.ShowChapter(campaign.Chapters[0]);
                grid = selection.Find("Generated Level Grid");
                Assert.That(grid.GetChild(0).Find("Generated Level 1/State")
                    .GetComponent<Text>().text, Is.EqualTo("★★☆"));
                Assert.That(grid.GetChild(0).Find("Generated Level 2").GetComponent<Button>()
                    .interactable, Is.True);
                Assert.That(selection.Find("Back To Map"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampaignDisplayCamera_ReusesAndRepairsExistingCameraWithoutAddingAudioListener()
        {
            GameObject ownedCameraObject = null;
            Camera existingCamera = Camera.main;
            if (existingCamera == null)
            {
                ownedCameraObject = new GameObject("Existing Campaign Camera");
                ownedCameraObject.tag = "MainCamera";
                existingCamera = ownedCameraObject.AddComponent<Camera>();
            }

            int previousTargetDisplay = existingCamera.targetDisplay;
            RenderTexture previousTargetTexture = existingCamera.targetTexture;
            bool previousOrthographic = existingCamera.orthographic;
            Vector3 previousPosition = existingCamera.transform.position;
            Color previousBackgroundColor = existingCamera.backgroundColor;
            CameraClearFlags previousClearFlags = existingCamera.clearFlags;
            existingCamera.targetDisplay = 1;
            RenderTexture targetTexture = new RenderTexture(16, 16, 0);
            existingCamera.targetTexture = targetTexture;

            int cameraCountBefore = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int listenerCountBefore = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            try
            {
                Camera displayCamera = CampaignRuntimeController.EnsureDisplayCamera();

                Assert.That(displayCamera, Is.SameAs(existingCamera));
                Assert.That(displayCamera.gameObject.activeInHierarchy, Is.True);
                Assert.That(displayCamera.enabled, Is.True);
                Assert.That(displayCamera.targetDisplay, Is.Zero);
                Assert.That(displayCamera.targetTexture, Is.Null);
                Assert.That(displayCamera.CompareTag("MainCamera"), Is.True);
                Assert.That(Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                    Is.EqualTo(cameraCountBefore));
                Assert.That(Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                    Is.EqualTo(listenerCountBefore));
            }
            finally
            {
                if (ownedCameraObject != null)
                {
                    Object.DestroyImmediate(ownedCameraObject);
                }
                else
                {
                    existingCamera.targetDisplay = previousTargetDisplay;
                    existingCamera.targetTexture = previousTargetTexture;
                    existingCamera.orthographic = previousOrthographic;
                    existingCamera.transform.position = previousPosition;
                    existingCamera.backgroundColor = previousBackgroundColor;
                    existingCamera.clearFlags = previousClearFlags;
                }

                Object.DestroyImmediate(targetTexture);
            }
        }

        [Test]
        public void CampaignCompletionHud_NormalResultExposesRetryLevelsAndNext()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_02");
            var session = new GameplaySession(level);
            var root = new GameObject("M6 Result HUD Smoke Test");

            try
            {
                var hud = root.AddComponent<GameplayHudView>();
                hud.Build(() => { }, () => { }, () => { },
                    new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                        () => { }, () => CampaignResultNavigationState.Normal(true)));
                session.InteractWithTile(new GridPosition(1, 0));
                session.InteractWithTile(new GridPosition(2, 0));
                hud.Refresh(session);

                Transform result = root.transform.Find("Gameplay HUD Canvas/Completion Panel");
                Assert.That(result.gameObject.activeSelf, Is.True);
                Assert.That(result.Find("Retry Button").gameObject.activeSelf, Is.True);
                Assert.That(result.Find("Levels Button").gameObject.activeSelf, Is.True);
                Assert.That(result.Find("Map Button").gameObject.activeSelf, Is.False);
                Assert.That(result.Find("Next Button").gameObject.activeSelf, Is.True);
                Assert.That(result.Find("Next Button").GetComponent<Button>().interactable, Is.True);
                Assert.That(root.transform.Find("Gameplay HUD Canvas/Back To Levels Button").gameObject.activeSelf,
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampaignCompletionHud_FirstRestorationExposesRetryAndMapOnly()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_02");
            var session = new GameplaySession(level);
            var root = new GameObject("M6 First Restoration HUD Smoke Test");

            try
            {
                var hud = root.AddComponent<GameplayHudView>();
                hud.Build(() => { }, () => { }, () => { },
                    new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                        () => { }, CampaignResultNavigationState.FirstChapterRestoration));
                session.InteractWithTile(new GridPosition(1, 0));
                session.InteractWithTile(new GridPosition(2, 0));
                hud.Refresh(session);

                Transform result = root.transform.Find("Gameplay HUD Canvas/Completion Panel");
                Assert.That(result.Find("Retry Button").gameObject.activeSelf, Is.True);
                Assert.That(result.Find("Levels Button").gameObject.activeSelf, Is.False);
                Assert.That(result.Find("Map Button").gameObject.activeSelf, Is.True);
                Assert.That(result.Find("Next Button").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampaignCompletionHud_AlreadyRestoredFinalReplayExposesRetryAndLevelsOnly()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_02");
            var session = new GameplaySession(level);
            var root = new GameObject("M6 Final Replay HUD Smoke Test");

            try
            {
                var hud = root.AddComponent<GameplayHudView>();
                hud.Build(() => { }, () => { }, () => { },
                    new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                        () => { }, () => CampaignResultNavigationState.Normal(false)));
                session.InteractWithTile(new GridPosition(1, 0));
                session.InteractWithTile(new GridPosition(2, 0));
                hud.Refresh(session);

                Transform result = root.transform.Find("Gameplay HUD Canvas/Completion Panel");
                Assert.That(result.Find("Retry Button").gameObject.activeSelf, Is.True);
                Assert.That(result.Find("Levels Button").gameObject.activeSelf, Is.True);
                Assert.That(result.Find("Map Button").gameObject.activeSelf, Is.False);
                Assert.That(result.Find("Next Button").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ActiveGameplayBack_ConfirmsBeforeLeaveAndCancelPreservesAttempt()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>("Levels/M3_Test_02");
            var session = new GameplaySession(level);
            var root = new GameObject("M6 Active Back HUD Smoke Test");
            int leaveCount = 0;

            try
            {
                var hud = root.AddComponent<GameplayHudView>();
                hud.Build(() => { }, () => { }, () => { },
                    new GameplayResultActions(() => { }, () => { }, () => { }, () => { },
                        () => leaveCount++, () => default));
                session.InteractWithTile(new GridPosition(1, 0));
                session.AdvanceTime(GameplaySession.HintUnlockSeconds);
                session.RequestHint();
                hud.Refresh(session);

                int rotation = session.Board.GetTile(new GridPosition(1, 0)).Rotation;
                int moves = session.MoveCount;
                float elapsed = session.ElapsedSeconds;
                bool hintsUsed = session.HintsUsed;
                HintStatus hintStatus = session.LastHint.Status;
                bool canUndo = session.CanUndo;

                Transform canvas = root.transform.Find("Gameplay HUD Canvas");
                RectTransform backRect = canvas.Find("Back To Levels Button").GetComponent<RectTransform>();
                RectTransform timerRect = canvas.Find("Timer").GetComponent<RectTransform>();
                RectTransform moveRect = canvas.Find("Move Count").GetComponent<RectTransform>();
                RectTransform undoRect = canvas.Find("Undo Button").GetComponent<RectTransform>();
                RectTransform restartRect = canvas.Find("Restart Button").GetComponent<RectTransform>();
                RectTransform hintRect = canvas.Find("Hint Button").GetComponent<RectTransform>();
                Assert.That(backRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(backRect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(backRect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(timerRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(timerRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(timerRect.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(moveRect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(moveRect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(moveRect.pivot, Is.EqualTo(new Vector2(1f, 1f)));

                Vector2 backBounds = HorizontalBounds(backRect, 1080f);
                Vector2 timerBounds = HorizontalBounds(timerRect, 1080f);
                Vector2 moveBounds = HorizontalBounds(moveRect, 1080f);
                AssertInsideCanvas(backRect, 1080f, 1920f);
                AssertInsideCanvas(timerRect, 1080f, 1920f);
                AssertInsideCanvas(moveRect, 1080f, 1920f);
                AssertInsideCanvas(undoRect, 1080f, 1920f);
                AssertInsideCanvas(restartRect, 1080f, 1920f);
                AssertInsideCanvas(hintRect, 1080f, 1920f);
                float controlsBottom = undoRect.anchoredPosition.y -
                                       undoRect.sizeDelta.y * undoRect.pivot.y;
                float controlsTop = undoRect.anchoredPosition.y +
                                    undoRect.sizeDelta.y * (1f - undoRect.pivot.y);
                RectTransform hintStatusRect = canvas.Find("Hint Status").GetComponent<RectTransform>();
                float hintStatusBottom = hintStatusRect.anchoredPosition.y -
                                         hintStatusRect.sizeDelta.y * hintStatusRect.pivot.y;
                Assert.That(controlsBottom, Is.GreaterThan(28f),
                    "Bottom controls must have more safe-area breathing room than the prior layout.");
                Assert.That(hintStatusBottom, Is.GreaterThan(controlsTop),
                    "Hint status must remain separated above the bottom controls.");
                Assert.That(backBounds.y, Is.LessThan(timerBounds.x),
                    "Back/Levels must not overlap the centered timer.");
                Assert.That(timerBounds.y, Is.LessThan(moveBounds.x),
                    "The centered timer must not overlap the Moves label.");

                int backControlCount = 0;
                foreach (Transform child in canvas)
                    if (child.name == "Back To Levels Button") backControlCount++;
                Assert.That(backControlCount, Is.EqualTo(1));

                canvas.Find("Back To Levels Button").GetComponent<Button>().onClick.Invoke();
                GameObject confirmation = canvas.Find("Leave Confirmation").gameObject;
                Assert.That(confirmation.activeSelf, Is.True);
                Assert.That(hud.IsLeaveConfirmationOpen, Is.True);
                Assert.That(leaveCount, Is.Zero,
                    "Requesting leave must not discard the session before confirmation.");

                confirmation.transform.Find("Cancel Button").GetComponent<Button>().onClick.Invoke();

                Assert.That(confirmation.activeSelf, Is.False);
                Assert.That(hud.IsLeaveConfirmationOpen, Is.False);
                Assert.That(session.Board.GetTile(new GridPosition(1, 0)).Rotation, Is.EqualTo(rotation));
                Assert.That(session.MoveCount, Is.EqualTo(moves));
                Assert.That(session.ElapsedSeconds, Is.EqualTo(elapsed));
                Assert.That(session.HintsUsed, Is.EqualTo(hintsUsed));
                Assert.That(session.LastHint.Status, Is.EqualTo(hintStatus));
                Assert.That(session.CanUndo, Is.EqualTo(canUndo));
                Assert.That(session.Undo(), Is.True, "Cancel must preserve the undo history.");

                canvas.Find("Back To Levels Button").GetComponent<Button>().onClick.Invoke();
                confirmation.transform.Find("Leave Button").GetComponent<Button>().onClick.Invoke();
                Assert.That(leaveCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Lamp_UsesDifferentColorsForPoweredAndUnpoweredStates()
        {
            CircuitTileState unpoweredLamp = new CircuitSimulation(new BoardState(1, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.OutputLamp, 0, false)
            })).Board.GetTile(new GridPosition(0, 0));

            CircuitTileState poweredLamp = new CircuitSimulation(new BoardState(2, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.OutputLamp, 0, false)
            })).Board.GetTile(new GridPosition(1, 0));

            Color inactiveColor = RenderAndReadColor(unpoweredLamp);
            Color poweredColor = RenderAndReadColor(poweredLamp);

            Assert.That(unpoweredLamp.IsPowered, Is.False);
            Assert.That(poweredLamp.IsPowered, Is.True);
            Assert.That(poweredColor, Is.Not.EqualTo(inactiveColor));
        }

        [Test]
        public void LockedDiode_ShowsDirectionAndLockProgrammerArtMarkers()
        {
            CircuitTileState diode = new BoardState(1, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.Diode, 0, false)
            }).GetTile(new GridPosition(0, 0));
            var root = new GameObject("Marker Test");
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            var view = root.AddComponent<CircuitTileView>();

            view.Build(sprite);
            view.Refresh(diode, sprite);

            Assert.That(root.transform.Find("Diode Output"), Is.Not.Null);
            Assert.That(root.transform.Find("Lock Indicator"), Is.Not.Null);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Switch_UsesDistinctOnOffProgrammerArt()
        {
            CircuitTileState offSwitch = new BoardState(1, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.Switch, 0, false, false)
            }).GetTile(new GridPosition(0, 0));
            CircuitTileState onSwitch = new BoardState(1, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.Switch, 0, false, true)
            }).GetTile(new GridPosition(0, 0));

            Color offColor = RenderAndReadColor(offSwitch);
            Color onColor = RenderAndReadColor(onSwitch);

            Assert.That(onColor, Is.Not.EqualTo(offColor));
            Assert.That(RenderAndReadLabel(offSwitch), Is.EqualTo("OFF"));
            Assert.That(RenderAndReadLabel(onSwitch), Is.EqualTo("ON"));
        }

        [TestCase(TileType.AndGate, "AND")]
        [TestCase(TileType.OrGate, "OR")]
        public void LogicGate_UsesIdentifyingProgrammerArtLabel(TileType gateType, string expectedLabel)
        {
            CircuitTileState gate = new BoardState(1, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), gateType, 0, false)
            }).GetTile(new GridPosition(0, 0));

            Assert.That(RenderAndReadLabel(gate), Is.EqualTo(expectedLabel));
        }

        private static Color RenderAndReadColor(CircuitTileState state)
        {
            var root = new GameObject("View Test");
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            var view = root.AddComponent<CircuitTileView>();
            view.Build(sprite);
            view.Refresh(state, sprite);
            Color result = view.CurrentCircuitColor;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
            return result;
        }

        private static Vector2 HorizontalBounds(RectTransform rect, float canvasWidth)
        {
            float pivotPosition = rect.anchorMin.x * canvasWidth + rect.anchoredPosition.x;
            return new Vector2(
                pivotPosition - rect.sizeDelta.x * rect.pivot.x,
                pivotPosition + rect.sizeDelta.x * (1f - rect.pivot.x));
        }

        private static void AssertInsideCanvas(RectTransform rect, float canvasWidth, float canvasHeight)
        {
            Vector2 horizontal = HorizontalBounds(rect, canvasWidth);
            float pivotPositionY = rect.anchorMin.y * canvasHeight + rect.anchoredPosition.y;
            float bottom = pivotPositionY - rect.sizeDelta.y * rect.pivot.y;
            float top = pivotPositionY + rect.sizeDelta.y * (1f - rect.pivot.y);
            Assert.That(horizontal.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(horizontal.y, Is.LessThanOrEqualTo(canvasWidth));
            Assert.That(bottom, Is.GreaterThanOrEqualTo(0f));
            Assert.That(top, Is.LessThanOrEqualTo(canvasHeight));
        }

        private static string RenderAndReadLabel(CircuitTileState state)
        {
            var root = new GameObject("Label Test");
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            var view = root.AddComponent<CircuitTileView>();
            view.Build(sprite);
            view.Refresh(state, sprite);
            string result = view.CurrentLabel;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
            return result;
        }
    }
}

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

        [Test]
        public void CampaignMapAndLevelSelection_ReflectProgressServiceState()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>("Campaigns/M6_Test_Campaign");
            Assert.That(campaign, Is.Not.Null);
            var progress = new CampaignProgressService(campaign);
            var root = new GameObject("M6 Campaign Presentation Smoke Test");

            try
            {
                var view = root.AddComponent<CampaignRuntimeView>();
                view.Build(campaign, progress, _ => { }, _ => { }, () => { });
                view.ShowMap();

                Transform map = root.transform.Find("Campaign Canvas/Campaign Map");
                Assert.That(map.Find("Total Stars").GetComponent<Text>().text, Is.EqualTo("Stars: 0 / 18"));
                Assert.That(map.Find("Chapter 1/Label").GetComponent<Text>().text, Does.Contain("AVAILABLE"));
                Assert.That(map.Find("Chapter 1").GetComponent<Button>().interactable, Is.True);
                Assert.That(map.Find("Chapter 2/Label").GetComponent<Text>().text, Does.Contain("LOCKED"));
                Assert.That(map.Find("Chapter 2").GetComponent<Button>().interactable, Is.False);

                view.ShowChapter(campaign.Chapters[0]);
                Transform selection = root.transform.Find("Campaign Canvas/Level Selection");
                Assert.That(selection.Find("Generated Level 1").GetComponent<Button>().interactable, Is.True);
                Assert.That(selection.Find("Generated Level 2").GetComponent<Button>().interactable, Is.False);
                Assert.That(selection.Find("Generated Level 3").GetComponent<Button>().interactable, Is.False);

                CampaignLevelEntry completedLevel = campaign.Chapters[0].Levels[0];
                progress.RecordCompletion(completedLevel.LevelId,
                    CampaignTestFixture.Result(completedLevel.LevelDefinition, 2, 10f, 3));
                view.ShowChapter(campaign.Chapters[0]);

                selection = root.transform.Find("Campaign Canvas/Level Selection");
                Assert.That(selection.Find("Generated Level 1/Label").GetComponent<Text>().text,
                    Does.Contain("COMPLETED"));
                Assert.That(selection.Find("Generated Level 2").GetComponent<Button>().interactable,
                    Is.True, "Returning to the selected chapter must refresh newly unlocked levels.");
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

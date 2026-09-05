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
                Assert.That(moveRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(moveRect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(moveRect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(moveRect.anchoredPosition.x, Is.GreaterThanOrEqualTo(0f),
                    "The left-aligned label must begin inside the canvas instead of extending off-screen.");

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

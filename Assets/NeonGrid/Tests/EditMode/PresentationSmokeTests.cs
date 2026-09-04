using NeonGrid.Presentation;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;

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
    }
}

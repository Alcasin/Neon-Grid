using System;
using System.Collections.Generic;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class BoardView : MonoBehaviour
    {
        private readonly Dictionary<GridPosition, CircuitTileView> tileViews = new Dictionary<GridPosition, CircuitTileView>();
        private Sprite squareSprite;
        private bool showCompleted;

        public void Build(BoardState board, Action<GridPosition> onTileTapped)
        {
            squareSprite = CreateSquareSprite();

            foreach (CircuitTileState tile in board.AllTiles())
            {
                var tileObject = new GameObject($"Tile {tile.Position.x},{tile.Position.y}");
                tileObject.transform.SetParent(transform, false);
                tileObject.transform.localPosition = new Vector3(tile.Position.x, tile.Position.y, 0f);

                var view = tileObject.AddComponent<CircuitTileView>();
                view.Build(squareSprite);
                tileObject.AddComponent<BoxCollider2D>().size = Vector2.one * 0.9f;
                tileObject.AddComponent<CircuitTileInput>().Initialize(tile.Position, onTileTapped);
                tileViews.Add(tile.Position, view);
            }

            gameObject.AddComponent<BoardPointerInput>().Initialize(Camera.main);
            transform.position = new Vector3(-(board.Width - 1) * 0.5f, -(board.Height - 1) * 0.5f, 0f);
            Refresh(board);
        }

        public void Refresh(BoardState board)
        {
            foreach (CircuitTileState tile in board.AllTiles())
                tileViews[tile.Position].Refresh(tile, squareSprite);
        }

        public void SetCompleted(bool completed) => showCompleted = completed;

        private void OnGUI()
        {
            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(20, Screen.height / 24),
                fontStyle = FontStyle.Bold,
                normal = { textColor = showCompleted ? new Color(0.2f, 1f, 0.7f) : new Color(0.65f, 0.7f, 0.85f) }
            };

            string message = showCompleted ? "LEVEL COMPLETE" : "Rotate tiles to power the lamp";
            GUI.Label(new Rect(0, 20, Screen.width, 60), message, title);
        }

        private static Sprite CreateSquareSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Programmer Art",
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}

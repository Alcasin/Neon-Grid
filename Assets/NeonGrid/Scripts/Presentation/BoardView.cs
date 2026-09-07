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
        private BoardPointerInput pointerInput;
        private GridPosition? hintPosition;
        private GridPosition? tutorialPosition;

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

            pointerInput = gameObject.AddComponent<BoardPointerInput>();
            pointerInput.Initialize(Camera.main);
            transform.position = new Vector3(-(board.Width - 1) * 0.5f, -(board.Height - 1) * 0.5f, 0f);
            Refresh(board);
        }

        public void Refresh(BoardState board)
        {
            foreach (CircuitTileState tile in board.AllTiles())
                tileViews[tile.Position].Refresh(tile, squareSprite);
        }

        public void HighlightHint(GridPosition? position)
        {
            hintPosition = position;
            ApplyHighlights();
        }

        public void HighlightTutorial(GridPosition? position)
        {
            tutorialPosition = position;
            ApplyHighlights();
        }

        public void SetCompleted(bool completed)
        {
            pointerInput?.SetBoardInputEnabled(!completed);
        }

        public Bounds GetWorldBounds()
        {
            BoxCollider2D[] colliders = GetComponentsInChildren<BoxCollider2D>();
            if (colliders.Length == 0) return new Bounds(transform.position, Vector3.zero);

            Bounds bounds = GetWorldBounds(colliders[0]);
            for (int index = 1; index < colliders.Length; index++)
                bounds.Encapsulate(GetWorldBounds(colliders[index]));
            return bounds;
        }

        private static Bounds GetWorldBounds(BoxCollider2D collider)
        {
            Vector3 scale = collider.transform.lossyScale;
            var size = new Vector3(Mathf.Abs(collider.size.x * scale.x),
                Mathf.Abs(collider.size.y * scale.y), 0f);
            return new Bounds(collider.transform.TransformPoint(collider.offset), size);
        }

        private void ApplyHighlights()
        {
            foreach (KeyValuePair<GridPosition, CircuitTileView> pair in tileViews)
            {
                TileHighlightReason reasons = TileHighlightReason.None;
                if (hintPosition.HasValue && pair.Key.Equals(hintPosition.Value))
                    reasons |= TileHighlightReason.Hint;
                if (tutorialPosition.HasValue && pair.Key.Equals(tutorialPosition.Value))
                    reasons |= TileHighlightReason.Tutorial;
                pair.Value.SetHighlightReasons(reasons);
            }
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

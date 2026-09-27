using System;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class BoardView : MonoBehaviour
    {
        private readonly Dictionary<GridPosition, CircuitTileView> tileViews = new Dictionary<GridPosition, CircuitTileView>();
        private readonly HashSet<GridPosition> poweredPositions = new HashSet<GridPosition>();
        private Sprite squareSprite;
        private BoardPointerInput pointerInput;
        private GridPosition? hintPosition;
        private GridPosition? tutorialPosition;
        public CircuitVisualThemeDefinition VisualTheme { get; private set; }
        public bool UsesVisualTheme => VisualTheme != null;
        public bool UsesProductionSkin => VisualTheme != null &&
                                          VisualTheme.UsesProductionTreatment;
        public CircuitJuiceCoordinator JuiceCoordinator { get; private set; }
        public bool UsesCircuitJuice => JuiceCoordinator != null;

        public void Build(BoardState board, Action<GridPosition> onTileTapped)
        {
            Build(board, onTileTapped, null);
        }

        public void Build(BoardState board, Action<GridPosition> onTileTapped,
            CircuitVisualThemeDefinition visualTheme)
        {
            Build(board, onTileTapped, visualTheme, null);
        }

        public void Build(BoardState board, Action<GridPosition> onTileTapped,
            CircuitVisualThemeDefinition visualTheme, CircuitJuiceDefinition juiceDefinition)
        {
            VisualTheme = visualTheme != null && visualTheme.IsConfigured ? visualTheme : null;
            if (VisualTheme != null && juiceDefinition != null && juiceDefinition.IsConfigured)
            {
                JuiceCoordinator = gameObject.AddComponent<CircuitJuiceCoordinator>();
                JuiceCoordinator.Initialize(juiceDefinition);
            }
            squareSprite = CreateSquareSprite();
            if (UsesProductionSkin)
                _ = new TechnicalNeonBoardRenderer(transform, squareSprite, VisualTheme,
                    board.Width, board.Height);

            foreach (CircuitTileState tile in board.AllTiles())
            {
                var tileObject = new GameObject($"Tile {tile.Position.x},{tile.Position.y}");
                tileObject.transform.SetParent(transform, false);
                tileObject.transform.localPosition = new Vector3(tile.Position.x, tile.Position.y, 0f);

                var view = tileObject.AddComponent<CircuitTileView>();
                view.Build(squareSprite, VisualTheme, juiceDefinition, JuiceCoordinator);
                tileObject.AddComponent<BoxCollider2D>().size = Vector2.one * 0.9f;
                tileObject.AddComponent<CircuitTileInput>().Initialize(tile.Position, onTileTapped);
                tileViews.Add(tile.Position, view);
            }

            pointerInput = gameObject.AddComponent<BoardPointerInput>();
            pointerInput.Initialize(Camera.main);
            transform.position = new Vector3(-(board.Width - 1) * 0.5f, -(board.Height - 1) * 0.5f, 0f);
            Refresh(board, CircuitJuiceTransition.Synchronize);
        }

        public void Refresh(BoardState board)
        {
            Refresh(board, CircuitJuiceTransition.Synchronize);
        }

        public void Refresh(BoardState board, CircuitJuiceTransition transition)
        {
            if (transition.Kind == CircuitJuiceRefreshKind.Restart)
                JuiceCoordinator?.CancelAll();
            var newlyPowered = new HashSet<GridPosition>();
            var newlyUnpowered = new HashSet<GridPosition>(poweredPositions);
            foreach (CircuitTileState tile in board.AllTiles())
            {
                if (tile.IsPowered)
                {
                    if (!poweredPositions.Contains(tile.Position))
                        newlyPowered.Add(tile.Position);
                    newlyUnpowered.Remove(tile.Position);
                }
                tileViews[tile.Position].Refresh(tile, squareSprite, transition);
            }
            poweredPositions.Clear();
            foreach (CircuitTileState tile in board.AllTiles())
                if (tile.IsPowered) poweredPositions.Add(tile.Position);

            bool scheduleTransition = transition.Kind == CircuitJuiceRefreshKind.PlayerAction ||
                                      transition.Kind == CircuitJuiceRefreshKind.Undo;
            if (JuiceCoordinator != null && scheduleTransition)
                JuiceCoordinator.SchedulePowerPresentation(board, newlyPowered,
                    newlyUnpowered, tileViews);
            else if (transition.Kind == CircuitJuiceRefreshKind.PlayerAction)
                foreach (CircuitTileView view in tileViews.Values)
                    view.PresentSourcePulse();
        }

        public void HighlightHint(GridPosition? position)
        {
            bool newlyTargeted = position.HasValue &&
                                 (!hintPosition.HasValue ||
                                  !hintPosition.Value.Equals(position.Value));
            hintPosition = position;
            ApplyHighlights();
            if (newlyTargeted && tileViews.TryGetValue(position.Value,
                    out CircuitTileView view))
                view.PresentHintTargeted();
        }

        public void PresentPressed(GridPosition position)
        {
            if (tileViews.TryGetValue(position, out CircuitTileView view))
                view.PresentPressed();
        }

        public void PresentRejected(GridPosition position)
        {
            if (tileViews.TryGetValue(position, out CircuitTileView view))
                view.PresentRejected();
        }

        public void PresentCompletion()
        {
            if (JuiceCoordinator != null)
            {
                JuiceCoordinator.RequestCompletion();
                return;
            }
            foreach (CircuitTileView view in tileViews.Values)
                view.PresentCompletion();
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

        public CircuitTileView GetTileView(GridPosition position)
        {
            return tileViews.TryGetValue(position, out CircuitTileView view) ? view : null;
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

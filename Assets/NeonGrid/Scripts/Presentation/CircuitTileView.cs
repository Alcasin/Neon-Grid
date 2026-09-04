using System.Collections.Generic;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class CircuitTileView : MonoBehaviour
    {
        private static readonly Color TileBackground = new Color(0.035f, 0.045f, 0.09f);
        private static readonly Color InactiveWire = new Color(0.22f, 0.17f, 0.38f);
        private static readonly Color PoweredWire = new Color(0.05f, 0.95f, 1f);
        private static readonly Color SourceColor = new Color(1f, 0.15f, 0.75f);
        private static readonly Color InactiveLamp = new Color(0.35f, 0.20f, 0.06f);
        private static readonly Color PoweredLamp = new Color(1f, 0.9f, 0.15f);
        private static readonly Color DirectionMarker = new Color(1f, 0.75f, 0.1f);
        private static readonly Color LockMarker = new Color(0.7f, 0.75f, 0.85f);

        private readonly List<SpriteRenderer> arms = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
        private SpriteRenderer center;
        private SpriteRenderer background;

        public Color CurrentCircuitColor => center != null ? center.color : Color.clear;

        public void Build(Sprite squareSprite)
        {
            background = CreatePart("Background", squareSprite, Vector3.zero, new Vector3(0.9f, 0.9f, 1f), 0);
            center = CreatePart("Center", squareSprite, Vector3.zero, new Vector3(0.30f, 0.30f, 1f), 2);
        }

        public void Refresh(CircuitTileState state, Sprite squareSprite)
        {
            background.color = TileBackground;
            center.gameObject.SetActive(state.TileType != TileType.Empty);

            foreach (SpriteRenderer arm in arms) Destroy(arm.gameObject);
            arms.Clear();
            foreach (SpriteRenderer marker in markers) Destroy(marker.gameObject);
            markers.Clear();

            Color circuitColor = GetCircuitColor(state);
            center.color = circuitColor;

            foreach (CardinalDirection direction in DirectionUtility.CardinalDirections)
            {
                if ((state.Connections & direction) == 0) continue;
                CreateArm(direction, squareSprite, circuitColor);
            }

            if (state.TileType == TileType.Diode)
                CreateDiodeOutputMarker(state, squareSprite);

            if (!state.IsRotatable && IsLockableCircuitTile(state.TileType))
                CreateLockMarker(squareSprite);

            if (state.TileType == TileType.PowerSource)
                center.transform.localScale = new Vector3(0.48f, 0.48f, 1f);
            else if (state.TileType == TileType.OutputLamp)
                center.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            else
                center.transform.localScale = new Vector3(0.30f, 0.30f, 1f);
        }

        private void CreateDiodeOutputMarker(CircuitTileState state, Sprite sprite)
        {
            CardinalDirection output = TilePowerFlow.GetOutputSides(state.TileType, state.Rotation);
            Vector3 position;
            switch (output)
            {
                case CardinalDirection.Up: position = new Vector3(0f, 0.23f, 0f); break;
                case CardinalDirection.Right: position = new Vector3(0.23f, 0f, 0f); break;
                case CardinalDirection.Down: position = new Vector3(0f, -0.23f, 0f); break;
                default: position = new Vector3(-0.23f, 0f, 0f); break;
            }

            SpriteRenderer marker = CreatePart("Diode Output", sprite, position, new Vector3(0.16f, 0.16f, 1f), 3);
            marker.color = DirectionMarker;
            markers.Add(marker);
        }

        private void CreateLockMarker(Sprite sprite)
        {
            SpriteRenderer marker = CreatePart("Lock Indicator", sprite,
                new Vector3(-0.32f, 0.32f, 0f), new Vector3(0.13f, 0.13f, 1f), 3);
            marker.color = LockMarker;
            markers.Add(marker);
        }

        private static bool IsLockableCircuitTile(TileType tileType)
        {
            return tileType == TileType.StraightWire || tileType == TileType.CornerWire ||
                   tileType == TileType.TJunction || tileType == TileType.CrossJunction ||
                   tileType == TileType.Diode;
        }

        private Color GetCircuitColor(CircuitTileState state)
        {
            if (state.TileType == TileType.PowerSource) return SourceColor;
            if (state.TileType == TileType.OutputLamp) return state.IsPowered ? PoweredLamp : InactiveLamp;
            return state.IsPowered ? PoweredWire : InactiveWire;
        }

        private void CreateArm(CardinalDirection direction, Sprite sprite, Color color)
        {
            bool vertical = direction == CardinalDirection.Up || direction == CardinalDirection.Down;
            float x = direction == CardinalDirection.Right ? 0.3f : direction == CardinalDirection.Left ? -0.3f : 0f;
            float y = direction == CardinalDirection.Up ? 0.3f : direction == CardinalDirection.Down ? -0.3f : 0f;
            Vector3 scale = vertical ? new Vector3(0.16f, 0.6f, 1f) : new Vector3(0.6f, 0.16f, 1f);
            SpriteRenderer arm = CreatePart("Connection", sprite, new Vector3(x, y, 0f), scale, 1);
            arm.color = color;
            arms.Add(arm);
        }

        private SpriteRenderer CreatePart(string objectName, Sprite sprite, Vector3 localPosition, Vector3 localScale, int order)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}

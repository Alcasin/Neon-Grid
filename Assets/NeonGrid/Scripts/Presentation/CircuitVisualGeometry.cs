using System;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public static class CircuitVisualGeometry
    {
        public const float ReferenceCanvasSize = 256f;
        public static readonly Vector2 TopConnector = new Vector2(128f, 256f);
        public static readonly Vector2 RightConnector = new Vector2(256f, 128f);
        public static readonly Vector2 BottomConnector = new Vector2(128f, 0f);
        public static readonly Vector2 LeftConnector = new Vector2(0f, 128f);

        public static Vector2 GetConnectorCenter(CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.Up: return TopConnector;
                case CardinalDirection.Right: return RightConnector;
                case CardinalDirection.Down: return BottomConnector;
                case CardinalDirection.Left: return LeftConnector;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public static Vector2 GetConnectorCenterNormalized(CardinalDirection direction)
        {
            Vector2 center = GetConnectorCenter(direction);
            return center / ReferenceCanvasSize - Vector2.one * 0.5f;
        }

        public static Vector2 RotateClockwise(Vector2 normalizedPoint, int quarterTurns)
        {
            int rotation = ((quarterTurns % 4) + 4) % 4;
            for (int index = 0; index < rotation; index++)
                normalizedPoint = new Vector2(normalizedPoint.y, -normalizedPoint.x);
            return normalizedPoint;
        }

        public static Vector2 GetRotatedConnectorCenterNormalized(
            CardinalDirection baseDirection, int rotation)
        {
            return RotateClockwise(GetConnectorCenterNormalized(baseDirection), rotation);
        }
    }
}

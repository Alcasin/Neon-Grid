using System;

namespace NeonGrid.Simulation
{
    [Flags]
    public enum CardinalDirection
    {
        None = 0,
        Up = 1 << 0,
        Right = 1 << 1,
        Down = 1 << 2,
        Left = 1 << 3
    }

    public static class CardinalDirectionExtensions
    {
        private const CardinalDirection All = CardinalDirection.Up | CardinalDirection.Right |
                                                 CardinalDirection.Down | CardinalDirection.Left;

        public static CardinalDirection Opposite(this CardinalDirection direction)
        {
            CardinalDirection result = CardinalDirection.None;
            if ((direction & CardinalDirection.Up) != 0) result |= CardinalDirection.Down;
            if ((direction & CardinalDirection.Right) != 0) result |= CardinalDirection.Left;
            if ((direction & CardinalDirection.Down) != 0) result |= CardinalDirection.Up;
            if ((direction & CardinalDirection.Left) != 0) result |= CardinalDirection.Right;
            return result;
        }

        public static CardinalDirection RotateClockwise(this CardinalDirection directions, int quarterTurns)
        {
            quarterTurns = ((quarterTurns % 4) + 4) % 4;
            CardinalDirection result = directions & ~All;

            foreach (CardinalDirection direction in DirectionUtility.CardinalDirections)
            {
                if ((directions & direction) == 0) continue;

                CardinalDirection rotated = direction;
                for (int i = 0; i < quarterTurns; i++)
                {
                    switch (rotated)
                    {
                        case CardinalDirection.Up: rotated = CardinalDirection.Right; break;
                        case CardinalDirection.Right: rotated = CardinalDirection.Down; break;
                        case CardinalDirection.Down: rotated = CardinalDirection.Left; break;
                        case CardinalDirection.Left: rotated = CardinalDirection.Up; break;
                    }
                }

                result |= rotated;
            }

            return result;
        }
    }

    public static class DirectionUtility
    {
        public static readonly CardinalDirection[] CardinalDirections =
        {
            CardinalDirection.Up,
            CardinalDirection.Right,
            CardinalDirection.Down,
            CardinalDirection.Left
        };
    }
}

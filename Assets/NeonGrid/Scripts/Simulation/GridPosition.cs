using System;

namespace NeonGrid.Simulation
{
    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {
        public int x;
        public int y;

        public GridPosition(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public GridPosition Neighbour(CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.Up: return new GridPosition(x, y + 1);
                case CardinalDirection.Right: return new GridPosition(x + 1, y);
                case CardinalDirection.Down: return new GridPosition(x, y - 1);
                case CardinalDirection.Left: return new GridPosition(x - 1, y);
                default: return this;
            }
        }

        public bool Equals(GridPosition other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => unchecked((x * 397) ^ y);
        public override string ToString() => $"({x}, {y})";
    }
}

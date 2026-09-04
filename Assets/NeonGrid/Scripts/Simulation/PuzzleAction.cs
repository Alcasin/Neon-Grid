using System;

namespace NeonGrid.Simulation
{
    public enum PuzzleActionType
    {
        RotateClockwise,
        ToggleSwitch
    }

    public readonly struct PuzzleAction : IEquatable<PuzzleAction>
    {
        public GridPosition Position { get; }
        public PuzzleActionType ActionType { get; }

        public PuzzleAction(GridPosition position, PuzzleActionType actionType)
        {
            Position = position;
            ActionType = actionType;
        }

        public bool Equals(PuzzleAction other)
        {
            return Position.Equals(other.Position) && ActionType == other.ActionType;
        }

        public override bool Equals(object obj) => obj is PuzzleAction other && Equals(other);
        public override int GetHashCode() => unchecked((Position.GetHashCode() * 397) ^ (int)ActionType);
        public override string ToString() => $"{ActionType} {Position}";
    }
}

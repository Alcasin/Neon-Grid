using System;

namespace NeonGrid.Simulation
{
    public readonly struct PuzzleStateKey : IEquatable<PuzzleStateKey>
    {
        private readonly string value;

        internal PuzzleStateKey(string value)
        {
            this.value = value ?? string.Empty;
        }

        public bool Equals(PuzzleStateKey other)
        {
            return string.Equals(value, other.value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is PuzzleStateKey other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
    }
}

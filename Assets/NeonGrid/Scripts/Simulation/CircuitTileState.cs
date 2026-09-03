namespace NeonGrid.Simulation
{
    public sealed class CircuitTileState
    {
        public GridPosition Position { get; }
        public TileType TileType { get; }
        public bool IsRotatable { get; }
        public int Rotation { get; private set; }
        public bool IsPowered { get; internal set; }
        public CardinalDirection Connections => TileConnections.GetConnections(TileType, Rotation);

        public CircuitTileState(TileDefinition definition)
        {
            Position = definition.position;
            TileType = definition.tileType;
            IsRotatable = definition.isRotatable && definition.tileType != TileType.Empty;
            Rotation = NormalizeRotation(definition.startingRotation);
        }

        internal bool RotateClockwise()
        {
            if (!IsRotatable) return false;
            Rotation = (Rotation + 1) % 4;
            return true;
        }

        private static int NormalizeRotation(int rotation) => ((rotation % 4) + 4) % 4;
    }
}

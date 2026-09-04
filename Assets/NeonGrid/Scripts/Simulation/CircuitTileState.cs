namespace NeonGrid.Simulation
{
    public sealed class CircuitTileState
    {
        public GridPosition Position { get; }
        public TileType TileType { get; }
        public bool IsRotatable { get; }
        public int Rotation { get; private set; }
        public bool IsPowered { get; internal set; }
        public bool IsSwitchOn { get; private set; }
        public CardinalDirection EnergizedInputSides { get; internal set; }
        public CardinalDirection ActiveOutputSides { get; internal set; }
        internal CardinalDirection PropagatedOutputSides { get; set; }
        public CardinalDirection Connections => TileConnections.GetConnections(TileType, Rotation);

        public CircuitTileState(TileDefinition definition)
        {
            Position = definition.position;
            TileType = definition.tileType;
            IsRotatable = definition.isRotatable && definition.tileType != TileType.Empty &&
                          definition.tileType != TileType.Switch;
            Rotation = NormalizeRotation(definition.startingRotation);
            IsSwitchOn = definition.tileType == TileType.Switch && definition.startingSwitchOn;
        }

        internal bool RotateClockwise()
        {
            if (!IsRotatable) return false;
            Rotation = (Rotation + 1) % 4;
            return true;
        }

        internal bool Interact()
        {
            if (TileType == TileType.Switch)
            {
                IsSwitchOn = !IsSwitchOn;
                return true;
            }

            return RotateClockwise();
        }

        internal void ResetTransientPower()
        {
            IsPowered = false;
            EnergizedInputSides = CardinalDirection.None;
            ActiveOutputSides = CardinalDirection.None;
            PropagatedOutputSides = CardinalDirection.None;
        }

        internal void SeedPower()
        {
            IsPowered = true;
            ActiveOutputSides = Connections;
        }

        internal bool RecordEnergizedInput(CardinalDirection side)
        {
            if ((EnergizedInputSides & side) != 0) return false;
            EnergizedInputSides |= side;
            IsPowered = true;
            return true;
        }

        private static int NormalizeRotation(int rotation) => ((rotation % 4) + 4) % 4;
    }
}

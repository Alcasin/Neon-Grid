namespace NeonGrid.Simulation
{
    public static class TileConnections
    {
        public static CardinalDirection GetBaseConnections(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.StraightWire:
                    return CardinalDirection.Up | CardinalDirection.Down;
                case TileType.CornerWire:
                    return CardinalDirection.Up | CardinalDirection.Right;
                case TileType.PowerSource:
                    return CardinalDirection.Right;
                case TileType.OutputLamp:
                    return CardinalDirection.Left;
                default:
                    return CardinalDirection.None;
            }
        }

        public static CardinalDirection GetConnections(TileType tileType, int rotation)
        {
            return GetBaseConnections(tileType).RotateClockwise(rotation);
        }
    }
}

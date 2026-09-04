namespace NeonGrid.Simulation
{
    public static class TilePowerFlow
    {
        private const CardinalDirection DiodeBaseInput = CardinalDirection.Left;
        private const CardinalDirection DiodeBaseOutput = CardinalDirection.Right;

        public static CardinalDirection GetInputSides(TileType tileType, int rotation)
        {
            return tileType == TileType.Diode
                ? DiodeBaseInput.RotateClockwise(rotation)
                : TileConnections.GetConnections(tileType, rotation);
        }

        public static CardinalDirection GetOutputSides(TileType tileType, int rotation)
        {
            return tileType == TileType.Diode
                ? DiodeBaseOutput.RotateClockwise(rotation)
                : TileConnections.GetConnections(tileType, rotation);
        }

        public static bool CanReceiveFrom(CircuitTileState tile, CardinalDirection side)
        {
            return (GetInputSides(tile.TileType, tile.Rotation) & side) != 0;
        }

        public static bool CanSendToward(CircuitTileState tile, CardinalDirection side)
        {
            return (GetOutputSides(tile.TileType, tile.Rotation) & side) != 0;
        }
    }
}

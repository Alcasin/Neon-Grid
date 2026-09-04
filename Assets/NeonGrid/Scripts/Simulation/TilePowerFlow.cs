namespace NeonGrid.Simulation
{
    public static class TilePowerFlow
    {
        private const CardinalDirection DiodeBaseInput = CardinalDirection.Left;
        private const CardinalDirection DiodeBaseOutput = CardinalDirection.Right;
        private const CardinalDirection GateBaseInputs = CardinalDirection.Left | CardinalDirection.Right;
        private const CardinalDirection GateBaseOutput = CardinalDirection.Up;

        public static CardinalDirection GetInputSides(TileType tileType, int rotation)
        {
            switch (tileType)
            {
                case TileType.Diode:
                    return DiodeBaseInput.RotateClockwise(rotation);
                case TileType.AndGate:
                case TileType.OrGate:
                    return GateBaseInputs.RotateClockwise(rotation);
                default:
                    return TileConnections.GetConnections(tileType, rotation);
            }
        }

        public static CardinalDirection GetOutputSides(TileType tileType, int rotation)
        {
            switch (tileType)
            {
                case TileType.Diode:
                    return DiodeBaseOutput.RotateClockwise(rotation);
                case TileType.AndGate:
                case TileType.OrGate:
                    return GateBaseOutput.RotateClockwise(rotation);
                default:
                    return TileConnections.GetConnections(tileType, rotation);
            }
        }

        public static bool CanReceiveFrom(CircuitTileState tile, CardinalDirection side)
        {
            return (GetInputSides(tile.TileType, tile.Rotation) & side) != 0;
        }

        public static CardinalDirection EvaluateActiveOutputSides(CircuitTileState tile)
        {
            CardinalDirection inputs = GetInputSides(tile.TileType, tile.Rotation);
            CardinalDirection energizedInputs = tile.EnergizedInputSides & inputs;

            switch (tile.TileType)
            {
                case TileType.PowerSource:
                    return tile.Connections;
                case TileType.Switch:
                    return tile.IsSwitchOn && energizedInputs != CardinalDirection.None
                        ? tile.Connections
                        : CardinalDirection.None;
                case TileType.Diode:
                case TileType.AndGate:
                    return energizedInputs == inputs
                        ? GetOutputSides(tile.TileType, tile.Rotation)
                        : CardinalDirection.None;
                case TileType.OrGate:
                    return energizedInputs != CardinalDirection.None
                        ? GetOutputSides(tile.TileType, tile.Rotation)
                        : CardinalDirection.None;
                case TileType.Empty:
                    return CardinalDirection.None;
                default:
                    return tile.IsPowered ? tile.Connections : CardinalDirection.None;
            }
        }
    }
}

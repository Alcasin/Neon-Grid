namespace NeonGrid.Simulation
{
    public static class CircuitCompletion
    {
        public static bool IsCompleted(BoardState board)
        {
            bool hasLamp = false;
            foreach (CircuitTileState tile in board.AllTiles())
            {
                if (tile.TileType != TileType.OutputLamp) continue;
                hasLamp = true;
                if (!tile.IsPowered) return false;
            }

            return hasLamp;
        }
    }
}

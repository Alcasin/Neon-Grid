using System.Collections.Generic;

namespace NeonGrid.Simulation
{
    public sealed class PowerPropagationService
    {
        public void Recalculate(BoardState board)
        {
            var frontier = new Queue<CircuitTileState>();

            foreach (CircuitTileState tile in board.AllTiles())
            {
                tile.IsPowered = false;
                if (tile.TileType == TileType.PowerSource)
                {
                    tile.IsPowered = true;
                    frontier.Enqueue(tile);
                }
            }

            PropagateFromSeededTiles(board, frontier);
        }

        internal void PropagateFromSeededTiles(BoardState board, Queue<CircuitTileState> frontier)
        {
            while (frontier.Count > 0)
            {
                CircuitTileState current = frontier.Dequeue();

                foreach (CardinalDirection direction in DirectionUtility.CardinalDirections)
                {
                    if ((current.Connections & direction) == 0) continue;
                    if (!TilePowerFlow.CanSendToward(current, direction)) continue;

                    GridPosition neighbourPosition = current.Position.Neighbour(direction);
                    if (!board.Contains(neighbourPosition)) continue;

                    CircuitTileState neighbour = board.GetTile(neighbourPosition);
                    CardinalDirection neighbourSide = direction.Opposite();
                    if ((neighbour.Connections & neighbourSide) == 0) continue;
                    if (!TilePowerFlow.CanReceiveFrom(neighbour, neighbourSide)) continue;
                    if (neighbour.IsPowered) continue;

                    neighbour.IsPowered = true;
                    frontier.Enqueue(neighbour);
                }
            }
        }
    }
}

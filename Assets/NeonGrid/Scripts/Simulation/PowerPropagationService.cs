using System.Collections.Generic;

namespace NeonGrid.Simulation
{
    public sealed class PowerPropagationService
    {
        public void Recalculate(BoardState board)
        {
            var sources = new List<CircuitTileState>();
            foreach (CircuitTileState tile in board.AllTiles())
            {
                if (tile.TileType == TileType.PowerSource)
                    sources.Add(tile);
            }

            RecalculateFromSeeds(board, sources);
        }

        internal void RecalculateFromSeeds(BoardState board, IEnumerable<CircuitTileState> seeds)
        {
            foreach (CircuitTileState tile in board.AllTiles())
                tile.ResetTransientPower();

            var frontier = new Queue<CircuitTileState>();
            foreach (CircuitTileState seed in seeds)
            {
                seed.SeedPower();
                frontier.Enqueue(seed);
            }

            PropagateUntilStable(board, frontier);
        }

        private static void PropagateUntilStable(BoardState board, Queue<CircuitTileState> frontier)
        {
            while (frontier.Count > 0)
            {
                CircuitTileState current = frontier.Dequeue();
                CardinalDirection pendingOutputs = current.ActiveOutputSides & ~current.PropagatedOutputSides;
                current.PropagatedOutputSides |= pendingOutputs;

                foreach (CardinalDirection direction in DirectionUtility.CardinalDirections)
                {
                    if ((pendingOutputs & direction) == 0) continue;
                    if ((current.Connections & direction) == 0) continue;

                    GridPosition neighbourPosition = current.Position.Neighbour(direction);
                    if (!board.Contains(neighbourPosition)) continue;

                    CircuitTileState neighbour = board.GetTile(neighbourPosition);
                    CardinalDirection neighbourSide = direction.Opposite();
                    if ((neighbour.Connections & neighbourSide) == 0) continue;
                    if (!TilePowerFlow.CanReceiveFrom(neighbour, neighbourSide)) continue;
                    if (!neighbour.RecordEnergizedInput(neighbourSide)) continue;

                    neighbour.ActiveOutputSides = TilePowerFlow.EvaluateActiveOutputSides(neighbour);
                    if ((neighbour.ActiveOutputSides & ~neighbour.PropagatedOutputSides) != 0)
                        frontier.Enqueue(neighbour);
                }
            }
        }
    }
}

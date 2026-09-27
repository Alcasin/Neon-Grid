using System;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;

namespace NeonGrid.Presentation
{
    public readonly struct PropagationPresentationStep
    {
        public int Depth { get; }
        public float DelaySeconds { get; }
        public IReadOnlyList<GridPosition> Positions { get; }

        internal PropagationPresentationStep(int depth, float delaySeconds,
            IReadOnlyList<GridPosition> positions)
        {
            Depth = depth;
            DelaySeconds = delaySeconds;
            Positions = positions;
        }
    }

    public sealed class PropagationPresentationPlan
    {
        private readonly List<PropagationPresentationStep> steps;
        private readonly List<GridPosition> sources;

        public IReadOnlyList<PropagationPresentationStep> Steps => steps;
        public IReadOnlyList<GridPosition> Sources => sources;
        public int MaximumDepth { get; }
        public float MaximumDelaySeconds { get; }
        public int TileCount { get; }

        private PropagationPresentationPlan(List<PropagationPresentationStep> steps,
            List<GridPosition> sources, int maximumDepth, float maximumDelaySeconds,
            int tileCount)
        {
            this.steps = steps;
            this.sources = sources;
            MaximumDepth = maximumDepth;
            MaximumDelaySeconds = maximumDelaySeconds;
            TileCount = tileCount;
        }

        public static PropagationPresentationPlan Create(BoardState board,
            ISet<GridPosition> newlyPowered, CircuitJuiceDefinition definition)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (newlyPowered == null) throw new ArgumentNullException(nameof(newlyPowered));
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var sources = new List<GridPosition>();
            var distances = new Dictionary<GridPosition, int>();
            var frontier = new Queue<GridPosition>();
            foreach (CircuitTileState tile in board.AllTiles())
            {
                if (!tile.IsPowered || tile.TileType != TileType.PowerSource) continue;
                sources.Add(tile.Position);
                distances[tile.Position] = 0;
                frontier.Enqueue(tile.Position);
            }

            while (frontier.Count > 0)
            {
                GridPosition currentPosition = frontier.Dequeue();
                CircuitTileState current = board.GetTile(currentPosition);
                int nextDepth = distances[currentPosition] + 1;
                foreach (CardinalDirection direction in DirectionUtility.CardinalDirections)
                {
                    if ((current.ActiveOutputSides & direction) == 0 ||
                        (current.Connections & direction) == 0) continue;
                    GridPosition neighbourPosition = currentPosition.Neighbour(direction);
                    if (!board.Contains(neighbourPosition) ||
                        distances.ContainsKey(neighbourPosition)) continue;
                    CircuitTileState neighbour = board.GetTile(neighbourPosition);
                    CardinalDirection neighbourSide = direction.Opposite();
                    if (!neighbour.IsPowered ||
                        (neighbour.Connections & neighbourSide) == 0 ||
                        !TilePowerFlow.CanReceiveFrom(neighbour, neighbourSide)) continue;
                    distances[neighbourPosition] = nextDepth;
                    frontier.Enqueue(neighbourPosition);
                }
            }

            var grouped = new SortedDictionary<int, List<GridPosition>>();
            int maximumDepth = 0;
            int tileCount = 0;
            foreach (GridPosition position in newlyPowered)
            {
                if (!board.Contains(position)) continue;
                CircuitTileState tile = board.GetTile(position);
                if (!tile.IsPowered || !distances.TryGetValue(position, out int depth)) continue;
                if (!grouped.TryGetValue(depth, out List<GridPosition> positions))
                {
                    positions = new List<GridPosition>();
                    grouped.Add(depth, positions);
                }
                positions.Add(position);
                maximumDepth = Math.Max(maximumDepth, depth);
                tileCount++;
            }

            float depthDelay = definition.PropagationDepthDelay;
            if (maximumDepth > 0)
                depthDelay = Math.Min(depthDelay,
                    definition.MaximumPropagationDelay / maximumDepth);
            var steps = new List<PropagationPresentationStep>(grouped.Count);
            float maximumDelay = 0f;
            foreach (KeyValuePair<int, List<GridPosition>> group in grouped)
            {
                float delay = group.Key * depthDelay;
                maximumDelay = Math.Max(maximumDelay, delay);
                steps.Add(new PropagationPresentationStep(group.Key, delay, group.Value));
            }

            return new PropagationPresentationPlan(steps, sources, maximumDepth,
                maximumDelay, tileCount);
        }
    }
}

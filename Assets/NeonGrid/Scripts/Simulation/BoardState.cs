using System;
using System.Collections.Generic;

namespace NeonGrid.Simulation
{
    public sealed class BoardState
    {
        private readonly CircuitTileState[,] tiles;

        public int Width { get; }
        public int Height { get; }

        public BoardState(int width, int height, IEnumerable<TileDefinition> definitions)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            tiles = new CircuitTileState[width, height];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var position = new GridPosition(x, y);
                tiles[x, y] = new CircuitTileState(new TileDefinition(position, TileType.Empty, 0, false));
            }

            if (definitions == null) return;
            var occupied = new HashSet<GridPosition>();
            foreach (TileDefinition definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("Tile definitions cannot contain null entries.", nameof(definitions));
                if (!Contains(definition.position))
                    throw new ArgumentException($"Tile {definition.position} is outside the {width}x{height} board.", nameof(definitions));
                if (!occupied.Add(definition.position))
                    throw new ArgumentException($"More than one tile is defined at {definition.position}.", nameof(definitions));
                tiles[definition.position.x, definition.position.y] = new CircuitTileState(definition);
            }
        }

        public bool Contains(GridPosition position)
        {
            return position.x >= 0 && position.x < Width && position.y >= 0 && position.y < Height;
        }

        public CircuitTileState GetTile(GridPosition position)
        {
            if (!Contains(position)) throw new ArgumentOutOfRangeException(nameof(position));
            return tiles[position.x, position.y];
        }

        public IEnumerable<CircuitTileState> AllTiles()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return tiles[x, y];
        }

        internal bool TryRotateClockwise(GridPosition position)
        {
            return Contains(position) && tiles[position.x, position.y].RotateClockwise();
        }
    }
}

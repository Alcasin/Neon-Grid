using System;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using NeonGrid.Validation;

namespace NeonGrid.Editor.Authoring
{
    public enum UnsavedChangesChoice
    {
        Save,
        Discard,
        Cancel
    }

    public static class UnsavedChangesNavigation
    {
        public static bool CanNavigate(UnsavedChangesChoice choice, Func<bool> trySave)
        {
            switch (choice)
            {
                case UnsavedChangesChoice.Save:
                    if (trySave == null) throw new ArgumentNullException(nameof(trySave));
                    return trySave();
                case UnsavedChangesChoice.Discard:
                    return true;
                default:
                    return false;
            }
        }
    }

    public readonly struct LevelCellData : IEquatable<LevelCellData>
    {
        public TileType TileType { get; }
        public int Rotation { get; }
        public bool IsRotatable { get; }
        public bool StartingSwitchOn { get; }

        public LevelCellData(TileType tileType, int rotation, bool isRotatable, bool startingSwitchOn)
        {
            TileType = tileType;
            Rotation = rotation;
            IsRotatable = isRotatable;
            StartingSwitchOn = startingSwitchOn;
        }

        public bool Equals(LevelCellData other)
        {
            return TileType == other.TileType && Rotation == other.Rotation &&
                   IsRotatable == other.IsRotatable && StartingSwitchOn == other.StartingSwitchOn;
        }

        public override bool Equals(object obj) => obj is LevelCellData other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)TileType;
                hash = (hash * 397) ^ Rotation;
                hash = (hash * 397) ^ IsRotatable.GetHashCode();
                return (hash * 397) ^ StartingSwitchOn.GetHashCode();
            }
        }
    }

    public sealed class LevelResizeImpact
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<GridPosition> RemovedNonEmptyCells { get; }
        public bool RequiresDestructiveConfirmation => RemovedNonEmptyCells.Count > 0;

        internal LevelResizeImpact(int width, int height, IReadOnlyList<GridPosition> removedNonEmptyCells)
        {
            Width = width;
            Height = height;
            RemovedNonEmptyCells = removedNonEmptyCells;
        }
    }

    public sealed class LevelAuthoringModel
    {
        private LevelCellData[,] cells;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public LevelDefinition SourceAsset { get; private set; }
        public bool HasUnsavedChanges { get; private set; }

        public event Action Changed;

        public LevelAuthoringModel(int width = 4, int height = 4)
        {
            InitializeNew(width, height);
        }

        public void InitializeNew(int width, int height)
        {
            ValidateDimensions(width, height);
            Width = width;
            Height = height;
            cells = CreateEmptyCells(width, height);
            SourceAsset = null;
            HasUnsavedChanges = true;
            Changed?.Invoke();
        }

        public void Load(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            ValidateDimensions(level.Width, level.Height);
            if (level.Tiles == null || level.Tiles.Count != level.Width * level.Height)
                throw new ArgumentException("The level must contain exactly width * height tile records.", nameof(level));

            var loaded = CreateEmptyCells(level.Width, level.Height);
            var occupied = new HashSet<GridPosition>();
            foreach (TileDefinition tile in level.Tiles)
            {
                if (tile == null) throw new ArgumentException("Tile records cannot be null.", nameof(level));
                if (tile.position.x < 0 || tile.position.x >= level.Width ||
                    tile.position.y < 0 || tile.position.y >= level.Height)
                    throw new ArgumentException($"Tile {tile.position} is outside the level grid.", nameof(level));
                if (!occupied.Add(tile.position))
                    throw new ArgumentException($"Duplicate tile position {tile.position}.", nameof(level));
                if (!Enum.IsDefined(typeof(TileType), tile.tileType))
                    throw new ArgumentException($"Unknown tile type {(int)tile.tileType} at {tile.position}.", nameof(level));
                if (tile.startingRotation < 0 || tile.startingRotation > 3)
                    throw new ArgumentException($"Invalid rotation {tile.startingRotation} at {tile.position}.", nameof(level));

                loaded[tile.position.x, tile.position.y] = new LevelCellData(
                    tile.tileType, tile.startingRotation, tile.isRotatable, tile.startingSwitchOn);
            }

            Width = level.Width;
            Height = level.Height;
            cells = loaded;
            SourceAsset = level;
            HasUnsavedChanges = false;
            Changed?.Invoke();
        }

        public LevelCellData GetCell(GridPosition position)
        {
            EnsureInBounds(position);
            return cells[position.x, position.y];
        }

        public void Place(GridPosition position, TileType tileType)
        {
            if (!Enum.IsDefined(typeof(TileType), tileType))
                throw new ArgumentOutOfRangeException(nameof(tileType));

            SetCell(position, new LevelCellData(tileType, 0, DefaultRotatable(tileType), false));
        }

        public void Erase(GridPosition position)
        {
            SetCell(position, EmptyCell());
        }

        public void SetTileType(GridPosition position, TileType tileType)
        {
            LevelCellData current = GetCell(position);
            if (current.TileType == tileType) return;
            Place(position, tileType);
        }

        public void SetRotation(GridPosition position, int rotation)
        {
            if (rotation < 0 || rotation > 3)
                throw new ArgumentOutOfRangeException(nameof(rotation), "Rotation must be from 0 through 3.");
            LevelCellData current = GetCell(position);
            if (current.TileType == TileType.Empty) rotation = 0;
            SetCell(position, new LevelCellData(current.TileType, rotation,
                current.IsRotatable, current.StartingSwitchOn));
        }

        public void RotateClockwise(GridPosition position)
        {
            LevelCellData current = GetCell(position);
            if (current.TileType == TileType.Empty) return;
            SetRotation(position, ((current.Rotation % 4) + 5) % 4);
        }

        public void SetRotatable(GridPosition position, bool isRotatable)
        {
            LevelCellData current = GetCell(position);
            if (current.TileType == TileType.Empty || current.TileType == TileType.Switch)
                isRotatable = false;
            SetCell(position, new LevelCellData(current.TileType, current.Rotation,
                isRotatable, current.StartingSwitchOn));
        }

        public void SetStartingSwitchOn(GridPosition position, bool startingSwitchOn)
        {
            LevelCellData current = GetCell(position);
            if (current.TileType != TileType.Switch)
                throw new InvalidOperationException("StartingSwitchOn is only valid for Switch tiles.");
            SetCell(position, new LevelCellData(current.TileType, current.Rotation,
                false, startingSwitchOn));
        }

        public LevelResizeImpact AnalyzeResize(int width, int height)
        {
            ValidateDimensions(width, height);
            var removed = new List<GridPosition>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (x < width && y < height) continue;
                if (cells[x, y].TileType != TileType.Empty)
                    removed.Add(new GridPosition(x, y));
            }

            return new LevelResizeImpact(width, height, removed.AsReadOnly());
        }

        public bool Resize(int width, int height, bool confirmDestructiveShrink = false)
        {
            LevelResizeImpact impact = AnalyzeResize(width, height);
            if (impact.RequiresDestructiveConfirmation && !confirmDestructiveShrink) return false;
            if (width == Width && height == Height) return true;

            LevelCellData[,] resized = CreateEmptyCells(width, height);
            int copyWidth = Math.Min(width, Width);
            int copyHeight = Math.Min(height, Height);
            for (int y = 0; y < copyHeight; y++)
            for (int x = 0; x < copyWidth; x++)
                resized[x, y] = cells[x, y];

            Width = width;
            Height = height;
            cells = resized;
            MarkChanged();
            return true;
        }

        public IReadOnlyList<TileDefinition> CreateSnapshot()
        {
            var definitions = new List<TileDefinition>(Width * Height);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                LevelCellData cell = cells[x, y];
                definitions.Add(new TileDefinition(new GridPosition(x, y), cell.TileType,
                    cell.Rotation, cell.IsRotatable, cell.StartingSwitchOn));
            }

            return definitions.AsReadOnly();
        }

        public LevelValidationResult Validate()
        {
            return new LevelValidator().Validate(Width, Height, CreateSnapshot());
        }

        public LevelValidationReport Analyze(PuzzleSolverOptions options = null)
        {
            return new LevelValidator().ValidateWithSolver(Width, Height, CreateSnapshot(),
                options ?? PuzzleSolverProfiles.AuthoringExact);
        }

        internal void MarkSaved(LevelDefinition sourceAsset)
        {
            SourceAsset = sourceAsset;
            HasUnsavedChanges = false;
            Changed?.Invoke();
        }

        internal void DiscardDirtyFlag()
        {
            HasUnsavedChanges = false;
            Changed?.Invoke();
        }

        private void SetCell(GridPosition position, LevelCellData value)
        {
            EnsureInBounds(position);
            if (cells[position.x, position.y].Equals(value)) return;
            cells[position.x, position.y] = value;
            MarkChanged();
        }

        private void MarkChanged()
        {
            HasUnsavedChanges = true;
            Changed?.Invoke();
        }

        private void EnsureInBounds(GridPosition position)
        {
            if (position.x < 0 || position.x >= Width || position.y < 0 || position.y >= Height)
                throw new ArgumentOutOfRangeException(nameof(position));
        }

        private static LevelCellData[,] CreateEmptyCells(int width, int height)
        {
            var result = new LevelCellData[width, height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                result[x, y] = EmptyCell();
            return result;
        }

        private static LevelCellData EmptyCell()
        {
            return new LevelCellData(TileType.Empty, 0, false, false);
        }

        private static bool DefaultRotatable(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.StraightWire:
                case TileType.CornerWire:
                case TileType.TJunction:
                case TileType.Diode:
                    return true;
                default:
                    return false;
            }
        }

        private static void ValidateDimensions(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        }
    }
}

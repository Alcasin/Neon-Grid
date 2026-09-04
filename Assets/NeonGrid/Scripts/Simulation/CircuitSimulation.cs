using System;

namespace NeonGrid.Simulation
{
    public sealed class CircuitSimulation
    {
        private readonly PowerPropagationService powerPropagation;

        public BoardState Board { get; }
        public bool IsLevelCompleted { get; private set; }

        public event Action BoardChanged;
        public event Action LevelCompleted;

        public CircuitSimulation(BoardState board, PowerPropagationService powerPropagation = null)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            this.powerPropagation = powerPropagation ?? new PowerPropagationService();
            Recalculate();
        }

        public bool RotateTileClockwise(GridPosition position)
        {
            if (!Board.TryRotateClockwise(position)) return false;

            Recalculate();
            BoardChanged?.Invoke();
            return true;
        }

        public bool InteractWithTile(GridPosition position)
        {
            if (!Board.TryInteract(position)) return false;

            Recalculate();
            BoardChanged?.Invoke();
            return true;
        }

        private void Recalculate()
        {
            powerPropagation.Recalculate(Board);

            bool hasLamp = false;
            bool allLampsPowered = true;
            foreach (CircuitTileState tile in Board.AllTiles())
            {
                if (tile.TileType != TileType.OutputLamp) continue;
                hasLamp = true;
                if (!tile.IsPowered) allLampsPowered = false;
            }

            bool completedNow = hasLamp && allLampsPowered;
            if (!IsLevelCompleted && completedNow)
            {
                IsLevelCompleted = true;
                LevelCompleted?.Invoke();
            }
            else if (!completedNow)
            {
                IsLevelCompleted = false;
            }
        }
    }
}

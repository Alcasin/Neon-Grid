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
            return ApplyAction(new PuzzleAction(position, PuzzleActionType.RotateClockwise));
        }

        public bool InteractWithTile(GridPosition position)
        {
            return Board.TryGetPlayerAction(position, out PuzzleAction action) && ApplyAction(action);
        }

        public bool ApplyAction(PuzzleAction action)
        {
            if (!Board.TryApplyAction(action)) return false;

            Recalculate();
            BoardChanged?.Invoke();
            return true;
        }

        private void Recalculate()
        {
            powerPropagation.Recalculate(Board);

            bool completedNow = CircuitCompletion.IsCompleted(Board);
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

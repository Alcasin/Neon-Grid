using System;
using System.Collections.Generic;
using System.Text;

namespace NeonGrid.Simulation
{
    public sealed class PuzzleSearchState
    {
        private readonly BoardState board;
        private readonly PowerPropagationService powerPropagation;

        public PuzzleStateKey Key => CreateKey(board);
        public bool IsSolved => CircuitCompletion.IsCompleted(board);
        internal BoardState Board => board;

        private PuzzleSearchState(BoardState ownedBoard)
        {
            board = ownedBoard ?? throw new ArgumentNullException(nameof(ownedBoard));
            powerPropagation = new PowerPropagationService();
            powerPropagation.Recalculate(board);
        }

        public static PuzzleSearchState FromBoard(BoardState source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new PuzzleSearchState(source.CreateIndependentCopy());
        }

        public PuzzleSearchState CreateIndependentCopy()
        {
            return new PuzzleSearchState(board.CreateIndependentCopy());
        }

        public IReadOnlyList<PuzzleAction> GetValidActions() => board.GetValidActions();

        public bool ApplyAction(PuzzleAction action)
        {
            if (!board.TryApplyAction(action)) return false;
            powerPropagation.Recalculate(board);
            return true;
        }

        private static PuzzleStateKey CreateKey(BoardState source)
        {
            var builder = new StringBuilder();
            foreach (CircuitTileState tile in source.AllTiles())
            {
                if (tile.TileType == TileType.Switch)
                {
                    builder.Append(tile.IsSwitchOn ? '1' : '0');
                }
                else if (tile.IsRotatable)
                {
                    builder.Append((char)('0' + tile.Rotation));
                }
            }

            return new PuzzleStateKey(builder.ToString());
        }
    }
}

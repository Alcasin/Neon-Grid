using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace NeonGrid.Simulation
{
    public sealed class PuzzleSearchState
    {
        private readonly BoardState board;
        private readonly PowerPropagationService powerPropagation;
        private readonly SolverProfile profile;
        private bool powerIsCurrent;

        public PuzzleStateKey Key
        {
            get
            {
                long start = profile == null ? 0 : Stopwatch.GetTimestamp();
                PuzzleStateKey key = CreateKey(board);
                if (profile != null) { profile.Keys++; profile.KeyTicks += Stopwatch.GetTimestamp() - start; }
                return key;
            }
        }
        public bool IsSolved
        {
            get { EnsurePower(); return CircuitCompletion.IsCompleted(board); }
        }
        internal BoardState Board
        {
            get { EnsurePower(); return board; }
        }

        private PuzzleSearchState(BoardState ownedBoard, SolverProfile profile)
        {
            this.profile = profile;
            board = ownedBoard ?? throw new ArgumentNullException(nameof(ownedBoard));
            powerPropagation = new PowerPropagationService();
        }

        public static PuzzleSearchState FromBoard(BoardState source)
        {
            return FromBoard(source, null);
        }

        internal static PuzzleSearchState FromBoard(BoardState source, SolverProfile profile)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new PuzzleSearchState(Copy(source, profile), profile);
        }

        public PuzzleSearchState CreateIndependentCopy()
        {
            return new PuzzleSearchState(Copy(board, profile), profile);
        }

        public IReadOnlyList<PuzzleAction> GetValidActions()
        {
            long start = profile == null ? 0 : Stopwatch.GetTimestamp();
            var actions = board.GetValidActions();
            if (profile != null) { profile.ActionLists++; profile.ActionTicks += Stopwatch.GetTimestamp() - start; }
            return actions;
        }

        public bool ApplyAction(PuzzleAction action)
        {
            if (!board.TryApplyAction(action)) return false;
            powerIsCurrent = false;
            return true;
        }

        private void EnsurePower()
        {
            // Legal actions and canonical identity depend only on persistent state.
            // Duplicate successors need neither a fixed-point solve nor completion.
            if (powerIsCurrent) return;
            Recalculate();
            powerIsCurrent = true;
        }

        private static BoardState Copy(BoardState source, SolverProfile profile)
        {
            long start = profile == null ? 0 : Stopwatch.GetTimestamp();
            BoardState copy = source.CreateIndependentCopy();
            if (profile != null) { profile.Copies++; profile.CopyTicks += Stopwatch.GetTimestamp() - start; }
            return copy;
        }

        private void Recalculate()
        {
            long start = profile == null ? 0 : Stopwatch.GetTimestamp();
            powerPropagation.Recalculate(board);
            if (profile != null) { profile.Powers++; profile.PowerTicks += Stopwatch.GetTimestamp() - start; }
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

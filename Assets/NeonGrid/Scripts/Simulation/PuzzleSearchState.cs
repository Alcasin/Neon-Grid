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
            get { return CreateProfiledKey(null); }
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

        internal PuzzleStateKey GetSuccessorKey(PuzzleAction action)
        {
            if (!board.IsActionValid(action))
                throw new ArgumentException($"Cannot derive a successor key for invalid action: {action}.",
                    nameof(action));

            return CreateProfiledKey(action);
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

        private PuzzleStateKey CreateProfiledKey(PuzzleAction? pendingAction)
        {
            long start = profile == null ? 0 : Stopwatch.GetTimestamp();
            PuzzleStateKey key = CreateKey(board, pendingAction);
            if (profile != null)
            {
                profile.Keys++;
                profile.KeyTicks += Stopwatch.GetTimestamp() - start;
            }
            return key;
        }

        private static PuzzleStateKey CreateKey(BoardState source,
            PuzzleAction? pendingAction)
        {
            var builder = new StringBuilder();
            foreach (CircuitTileState tile in source.AllTiles())
            {
                if (tile.TileType == TileType.Switch)
                {
                    bool isOn = tile.IsSwitchOn;
                    if (Targets(tile, pendingAction)) isOn = !isOn;
                    builder.Append(isOn ? '1' : '0');
                }
                else if (tile.IsRotatable)
                {
                    int rotation = tile.Rotation;
                    if (Targets(tile, pendingAction)) rotation = (rotation + 1) % 4;
                    builder.Append((char)('0' + rotation));
                }
            }

            return new PuzzleStateKey(builder.ToString());
        }

        private static bool Targets(CircuitTileState tile, PuzzleAction? pendingAction)
        {
            return pendingAction.HasValue &&
                   pendingAction.Value.Position.Equals(tile.Position);
        }
    }
}

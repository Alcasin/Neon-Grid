using System;
using System.Collections.Generic;

namespace NeonGrid.Simulation
{
    public enum PuzzleSolverStatus
    {
        Solved,
        Unsolvable,
        SearchLimitReached
    }

    public sealed class PuzzleSolverOptions
    {
        public const int DefaultMaximumExploredStates = 100000;
        public const int DefaultMaximumDepth = 64;

        public int MaximumExploredStates { get; set; } = DefaultMaximumExploredStates;
        public int MaximumDepth { get; set; } = DefaultMaximumDepth;
    }

    public static class PuzzleSolverProfiles
    {
        public const int AuthoringExactMaximumExploredStates = 500000;
        public const int RuntimeHintMaximumExploredStates = 250000;

        // Both contexts retain the established depth guard; authoring differs only by
        // allowing more states for exact solvability and minimum-move verification.
        public static PuzzleSolverOptions AuthoringExact => new PuzzleSolverOptions
        {
            MaximumExploredStates = AuthoringExactMaximumExploredStates,
            MaximumDepth = PuzzleSolverOptions.DefaultMaximumDepth
        };

        public static PuzzleSolverOptions RuntimeHint => new PuzzleSolverOptions
        {
            MaximumExploredStates = RuntimeHintMaximumExploredStates,
            MaximumDepth = PuzzleSolverOptions.DefaultMaximumDepth
        };
    }

    public sealed class PuzzleSolverResult
    {
        private static readonly PuzzleAction[] NoActions = Array.Empty<PuzzleAction>();

        public PuzzleSolverStatus Status { get; }
        public int MinimumMoveCount { get; }
        public IReadOnlyList<PuzzleAction> Solution { get; }
        public int ExploredStateCount { get; }
        public int DeepestSearchDepth { get; }

        internal PuzzleSolverResult(PuzzleSolverStatus status, IReadOnlyList<PuzzleAction> solution,
            int exploredStateCount, int deepestSearchDepth)
        {
            Status = status;
            if (solution == null || solution.Count == 0)
            {
                Solution = NoActions;
            }
            else
            {
                var copy = new PuzzleAction[solution.Count];
                for (int index = 0; index < solution.Count; index++) copy[index] = solution[index];
                Solution = Array.AsReadOnly(copy);
            }
            MinimumMoveCount = status == PuzzleSolverStatus.Solved ? Solution.Count : -1;
            ExploredStateCount = exploredStateCount;
            DeepestSearchDepth = deepestSearchDepth;
        }
    }

    public sealed class PuzzleSolver
    {
        public PuzzleSolverResult Solve(BoardState sourceBoard, PuzzleSolverOptions options = null)
        {
            if (sourceBoard == null) throw new ArgumentNullException(nameof(sourceBoard));
            options = options ?? new PuzzleSolverOptions();
            ValidateOptions(options);

            PuzzleSearchState initialState = PuzzleSearchState.FromBoard(sourceBoard);
            var initialPath = Array.Empty<PuzzleAction>();
            if (initialState.IsSolved)
                return Result(PuzzleSolverStatus.Solved, initialPath, 1, 0);

            var frontier = new Queue<SearchNode>();
            frontier.Enqueue(new SearchNode(initialState, initialPath));
            var visited = new HashSet<PuzzleStateKey> { initialState.Key };
            int deepestDepth = 0;
            bool depthLimitPreventedExpansion = false;

            while (frontier.Count > 0)
            {
                SearchNode current = frontier.Dequeue();
                IReadOnlyList<PuzzleAction> actions = current.State.GetValidActions();
                if (current.Path.Length >= options.MaximumDepth)
                {
                    if (HasUnvisitedSuccessor(current.State, actions, visited))
                        depthLimitPreventedExpansion = true;
                    continue;
                }

                foreach (PuzzleAction action in actions)
                {
                    PuzzleSearchState nextState = current.State.CreateIndependentCopy();
                    if (!nextState.ApplyAction(action))
                        throw new InvalidOperationException($"Generated action became invalid: {action}.");
                    if (visited.Contains(nextState.Key)) continue;

                    if (visited.Count >= options.MaximumExploredStates)
                        return Result(PuzzleSolverStatus.SearchLimitReached, null, visited.Count, deepestDepth);

                    visited.Add(nextState.Key);
                    PuzzleAction[] nextPath = Append(current.Path, action);
                    deepestDepth = Math.Max(deepestDepth, nextPath.Length);
                    if (nextState.IsSolved)
                        return Result(PuzzleSolverStatus.Solved, nextPath, visited.Count, deepestDepth);

                    frontier.Enqueue(new SearchNode(nextState, nextPath));
                }
            }

            PuzzleSolverStatus status = depthLimitPreventedExpansion
                ? PuzzleSolverStatus.SearchLimitReached
                : PuzzleSolverStatus.Unsolvable;
            return Result(status, null, visited.Count, deepestDepth);
        }

        private static void ValidateOptions(PuzzleSolverOptions options)
        {
            if (options.MaximumExploredStates <= 0)
                throw new ArgumentOutOfRangeException(nameof(options.MaximumExploredStates));
            if (options.MaximumDepth < 0)
                throw new ArgumentOutOfRangeException(nameof(options.MaximumDepth));
        }

        private static PuzzleAction[] Append(PuzzleAction[] path, PuzzleAction action)
        {
            var result = new PuzzleAction[path.Length + 1];
            Array.Copy(path, result, path.Length);
            result[path.Length] = action;
            return result;
        }

        private static bool HasUnvisitedSuccessor(PuzzleSearchState state,
            IReadOnlyList<PuzzleAction> actions, HashSet<PuzzleStateKey> visited)
        {
            foreach (PuzzleAction action in actions)
            {
                PuzzleSearchState successor = state.CreateIndependentCopy();
                if (!successor.ApplyAction(action))
                    throw new InvalidOperationException($"Generated action became invalid: {action}.");
                if (!visited.Contains(successor.Key)) return true;
            }

            return false;
        }

        private static PuzzleSolverResult Result(PuzzleSolverStatus status, IReadOnlyList<PuzzleAction> solution,
            int exploredStates, int deepestDepth)
        {
            return new PuzzleSolverResult(status, solution, exploredStates, deepestDepth);
        }

        private sealed class SearchNode
        {
            public PuzzleSearchState State { get; }
            public PuzzleAction[] Path { get; }

            public SearchNode(PuzzleSearchState state, PuzzleAction[] path)
            {
                State = state;
                Path = path;
            }
        }
    }
}

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
        public const int AuthoringExactMaximumDepth = 64;
        public const int RuntimeHintMaximumExploredStates = 250000;

        // Both contexts retain the established depth guard; authoring differs only by
        // allowing more states for exact solvability and minimum-move verification.
        public static PuzzleSolverOptions AuthoringExact => new PuzzleSolverOptions
        {
            MaximumExploredStates = AuthoringExactMaximumExploredStates,
            MaximumDepth = AuthoringExactMaximumDepth
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
            return SolveProfiled(sourceBoard, options, null);
        }

        internal PuzzleSolverResult SolveProfiled(BoardState sourceBoard, PuzzleSolverOptions options,
            SolverProfile profile)
        {
            if (sourceBoard == null) throw new ArgumentNullException(nameof(sourceBoard));
            options = options ?? new PuzzleSolverOptions();
            ValidateOptions(options);
            long solverStart = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();

            PuzzleSearchState initialState = PuzzleSearchState.FromBoard(sourceBoard, profile);
            var initialPath = Array.Empty<PuzzleAction>();
            if (initialState.IsSolved)
                return Result(PuzzleSolverStatus.Solved, initialPath, 1, 0, profile,
                    solverStart);

            var frontier = new Queue<SearchNode>();
            frontier.Enqueue(new SearchNode(initialState, initialPath));
            var visited = new HashSet<PuzzleStateKey> { initialState.Key };
            if (profile != null) profile.MaximumFrontierSize = 1;
            int deepestDepth = 0;
            bool depthLimitPreventedExpansion = false;

            while (frontier.Count > 0)
            {
                SearchNode current = frontier.Dequeue();
                if (profile != null) profile.NodesExpanded++;
                IReadOnlyList<PuzzleAction> actions = current.State.GetValidActions();
                if (current.Path.Length >= options.MaximumDepth)
                {
                    if (HasUnvisitedSuccessor(current.State, actions, visited, profile))
                        depthLimitPreventedExpansion = true;
                    continue;
                }

                foreach (PuzzleAction action in actions)
                {
                    if (profile != null) profile.SuccessorsGenerated++;
                    PuzzleStateKey nextKey = current.State.GetSuccessorKey(action);
                    if (visited.Contains(nextKey))
                    {
                        if (profile != null) profile.DuplicateSuccessorsRejected++;
                        continue;
                    }

                    if (visited.Count >= options.MaximumExploredStates)
                        return Result(PuzzleSolverStatus.SearchLimitReached, null, visited.Count,
                            deepestDepth, profile, solverStart);

                    PuzzleSearchState nextState = current.State.CreateIndependentCopy();
                    if (!nextState.ApplyAction(action))
                        throw new InvalidOperationException($"Generated action became invalid: {action}.");

                    visited.Add(nextKey);
                    PuzzleAction[] nextPath = Append(current.Path, action, profile);
                    deepestDepth = Math.Max(deepestDepth, nextPath.Length);
                    if (nextState.IsSolved)
                        return Result(PuzzleSolverStatus.Solved, nextPath, visited.Count,
                            deepestDepth, profile, solverStart);

                    frontier.Enqueue(new SearchNode(nextState, nextPath));
                    if (profile != null)
                        profile.MaximumFrontierSize = Math.Max(profile.MaximumFrontierSize,
                            frontier.Count);
                }
            }

            PuzzleSolverStatus status = depthLimitPreventedExpansion
                ? PuzzleSolverStatus.SearchLimitReached
                : PuzzleSolverStatus.Unsolvable;
            return Result(status, null, visited.Count, deepestDepth, profile, solverStart);
        }

        private static void ValidateOptions(PuzzleSolverOptions options)
        {
            if (options.MaximumExploredStates <= 0)
                throw new ArgumentOutOfRangeException(nameof(options.MaximumExploredStates));
            if (options.MaximumDepth < 0)
                throw new ArgumentOutOfRangeException(nameof(options.MaximumDepth));
        }

        private static PuzzleAction[] Append(PuzzleAction[] path, PuzzleAction action,
            SolverProfile profile)
        {
            var result = new PuzzleAction[path.Length + 1];
            Array.Copy(path, result, path.Length);
            result[path.Length] = action;
            if (profile != null)
            {
                profile.PathArraysAllocated++;
                profile.PathElementsCopied += path.Length;
            }
            return result;
        }

        private static bool HasUnvisitedSuccessor(PuzzleSearchState state,
            IReadOnlyList<PuzzleAction> actions, HashSet<PuzzleStateKey> visited,
            SolverProfile profile)
        {
            foreach (PuzzleAction action in actions)
            {
                if (profile != null) profile.SuccessorsGenerated++;
                PuzzleStateKey successorKey = state.GetSuccessorKey(action);
                if (!visited.Contains(successorKey)) return true;
                if (profile != null) profile.DuplicateSuccessorsRejected++;
            }

            return false;
        }

        private static PuzzleSolverResult Result(PuzzleSolverStatus status, IReadOnlyList<PuzzleAction> solution,
            int exploredStates, int deepestDepth, SolverProfile profile, long solverStart)
        {
            var result = new PuzzleSolverResult(status, solution, exploredStates, deepestDepth);
            if (profile != null)
            {
                profile.SolverTicks = System.Diagnostics.Stopwatch.GetTimestamp() - solverStart;
                profile.UniqueStatesVisited = exploredStates;
                profile.DeepestDepthReached = deepestDepth;
                profile.FinalSolutionLength = result.Solution.Count;
                profile.TerminalStatus = status;
            }
            return result;
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

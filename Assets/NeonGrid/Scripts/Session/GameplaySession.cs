using System;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;

namespace NeonGrid.Session
{
    public sealed class GameplaySession
    {
        public const float HintUnlockSeconds = 180f;

        private readonly Stack<BoardPersistentSnapshot> undoHistory =
            new Stack<BoardPersistentSnapshot>();
        private readonly PuzzleSolver solver;
        private readonly PuzzleSolverOptions solverOptions;
        private readonly StarEvaluator starEvaluator;
        private CircuitSimulation simulation;

        public LevelDefinition ActiveLevel { get; }
        public BoardState Board => simulation.Board;
        public int MoveCount { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int? OptimalMoves { get; }
        public PuzzleSolverStatus OptimalSolverStatus { get; }
        public bool HintsUsed { get; private set; }
        public bool IsCompleted => simulation.IsLevelCompleted;
        public bool CanUndo => !IsCompleted && undoHistory.Count > 0;
        public bool CanInteract => !IsCompleted;
        public HintResult LastHint { get; private set; }
        public SessionCompletionResult CompletionResult { get; private set; }
        public HintStatus HintAvailability => IsCompleted
            ? HintStatus.NoHintNeeded
            : ElapsedSeconds >= HintUnlockSeconds
                ? HintStatus.HintAvailable
                : HintStatus.HintLocked;

        public event Action BoardChanged;
        public event Action SessionChanged;
        public event Action<SessionCompletionResult> LevelCompleted;

        public GameplaySession(LevelDefinition levelDefinition, PuzzleSolverOptions solverOptions = null,
            StarEvaluator starEvaluator = null)
        {
            ActiveLevel = levelDefinition ?? throw new ArgumentNullException(nameof(levelDefinition));
            solver = new PuzzleSolver();
            this.solverOptions = CopyOptions(solverOptions ?? new PuzzleSolverOptions());
            this.starEvaluator = starEvaluator ?? new StarEvaluator();

            PuzzleSolverResult baseline = solver.Solve(ActiveLevel.CreateBoardState(), this.solverOptions);
            OptimalSolverStatus = baseline.Status;
            OptimalMoves = baseline.Status == PuzzleSolverStatus.Solved
                ? baseline.MinimumMoveCount
                : (int?)null;

            ReplaceSimulation(ActiveLevel.CreateBoardState());
            LastHint = HintResult.WithoutAction(IsCompleted
                ? HintStatus.NoHintNeeded
                : HintStatus.HintLocked);
            if (IsCompleted)
                CompleteAttempt(false);
        }

        public bool InteractWithTile(GridPosition position)
        {
            return CanInteract && Board.TryGetPlayerAction(position, out PuzzleAction action) &&
                   PerformAction(action);
        }

        public bool PerformAction(PuzzleAction action)
        {
            if (!CanInteract || !Board.IsActionValid(action)) return false;

            BoardPersistentSnapshot beforeAction = BoardPersistentSnapshot.Capture(Board);
            MoveCount++;
            undoHistory.Push(beforeAction);
            ClearHint();

            if (simulation.ApplyAction(action))
                return true;

            // The validity check and application share the same authoritative board rules.
            // This rollback protects session accounting if that invariant ever changes.
            undoHistory.Pop();
            MoveCount--;
            SessionChanged?.Invoke();
            return false;
        }

        public bool Undo()
        {
            if (!CanUndo) return false;

            BoardState restored = undoHistory.Pop().Restore();
            ReplaceSimulation(restored);
            CompletionResult = null;
            ClearHint();
            BoardChanged?.Invoke();
            SessionChanged?.Invoke();
            return true;
        }

        public void Restart()
        {
            undoHistory.Clear();
            MoveCount = 0;
            ElapsedSeconds = 0f;
            HintsUsed = false;
            CompletionResult = null;
            ReplaceSimulation(ActiveLevel.CreateBoardState());
            LastHint = HintResult.WithoutAction(IsCompleted
                ? HintStatus.NoHintNeeded
                : HintStatus.HintLocked);

            if (IsCompleted)
                CompleteAttempt(false);

            BoardChanged?.Invoke();
            SessionChanged?.Invoke();
        }

        public void AdvanceTime(float deltaSeconds)
        {
            if (deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (deltaSeconds == 0f || IsCompleted) return;
            bool hintWasLocked = ElapsedSeconds < HintUnlockSeconds;
            ElapsedSeconds += deltaSeconds;
            if (hintWasLocked && ElapsedSeconds >= HintUnlockSeconds &&
                LastHint.Status == HintStatus.HintLocked)
                LastHint = HintResult.WithoutAction(HintStatus.HintAvailable);
            SessionChanged?.Invoke();
        }

        public HintResult RequestHint(PuzzleSolverOptions options = null)
        {
            if (IsCompleted)
                return SetHint(HintResult.WithoutAction(HintStatus.NoHintNeeded));
            if (ElapsedSeconds < HintUnlockSeconds)
                return SetHint(HintResult.WithoutAction(HintStatus.HintLocked));

            PuzzleSolverResult result;
            try
            {
                result = solver.Solve(Board.CreateIndependentCopy(),
                    CopyOptions(options ?? solverOptions));
            }
            catch (ArgumentException)
            {
                return SetHint(HintResult.WithoutAction(HintStatus.UnsolvableOrInvalid));
            }

            switch (result.Status)
            {
                case PuzzleSolverStatus.Solved:
                    if (result.Solution.Count == 0)
                        return SetHint(HintResult.WithoutAction(HintStatus.NoHintNeeded));
                    HintsUsed = true;
                    return SetHint(HintResult.Available(result.Solution[0]));
                case PuzzleSolverStatus.SearchLimitReached:
                    return SetHint(HintResult.WithoutAction(HintStatus.SolverLimitReached));
                default:
                    return SetHint(HintResult.WithoutAction(HintStatus.UnsolvableOrInvalid));
            }
        }

        private HintResult SetHint(HintResult hint)
        {
            LastHint = hint;
            SessionChanged?.Invoke();
            return hint;
        }

        private void ClearHint()
        {
            LastHint = HintResult.WithoutAction(HintAvailability);
            SessionChanged?.Invoke();
        }

        private void ReplaceSimulation(BoardState board)
        {
            if (simulation != null)
            {
                simulation.BoardChanged -= HandleBoardChanged;
                simulation.LevelCompleted -= HandleLevelCompleted;
            }

            simulation = new CircuitSimulation(board);
            simulation.BoardChanged += HandleBoardChanged;
            simulation.LevelCompleted += HandleLevelCompleted;
        }

        private void HandleBoardChanged()
        {
            BoardChanged?.Invoke();
            SessionChanged?.Invoke();
        }

        private void HandleLevelCompleted()
        {
            CompleteAttempt(true);
        }

        private void CompleteAttempt(bool raiseEvent)
        {
            StarEvaluationResult rating = starEvaluator.Evaluate(true, MoveCount, OptimalMoves);
            CompletionResult = new SessionCompletionResult(ActiveLevel, MoveCount, ElapsedSeconds,
                OptimalMoves, HintsUsed, rating);
            LastHint = HintResult.WithoutAction(HintStatus.NoHintNeeded);
            if (raiseEvent)
                LevelCompleted?.Invoke(CompletionResult);
        }

        private static PuzzleSolverOptions CopyOptions(PuzzleSolverOptions source)
        {
            return new PuzzleSolverOptions
            {
                MaximumExploredStates = source.MaximumExploredStates,
                MaximumDepth = source.MaximumDepth
            };
        }
    }
}

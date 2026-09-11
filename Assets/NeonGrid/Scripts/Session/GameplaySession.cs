using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;

namespace NeonGrid.Session
{
    public sealed class GameplaySession : IDisposable
    {
        public const float HintUnlockSeconds = 180f;

        private readonly Stack<BoardPersistentSnapshot> undoHistory =
            new Stack<BoardPersistentSnapshot>();
        private readonly PuzzleSolverOptions solverOptions;
        private readonly StarEvaluator starEvaluator;
        private readonly IHintSolverRunner hintSolverRunner;
        private readonly ConcurrentQueue<HintSolverCompletion> hintCompletions =
            new ConcurrentQueue<HintSolverCompletion>();
        private CircuitSimulation simulation;
        private int boardVersion;
        private int nextHintRequestId;
        private int activeHintRequestId;

        public LevelDefinition ActiveLevel { get; }
        public BoardState Board => simulation.Board;
        public int MoveCount { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int? OptimalMoves { get; }
        public PuzzleSolverStatus OptimalSolverStatus { get; }
        public bool HintsUsed { get; private set; }
        public bool IsCompleted => simulation.IsLevelCompleted;
        public bool CanUndo => !IsDisposed && !IsCompleted && undoHistory.Count > 0;
        public bool CanInteract => !IsDisposed && !IsCompleted;
        public bool IsHintSearchInProgress => activeHintRequestId != 0;
        public bool IsDisposed { get; private set; }
        public HintResult LastHint { get; private set; }
        public SessionCompletionResult CompletionResult { get; private set; }
        public HintStatus HintAvailability => IsCompleted
            ? HintStatus.NoHintNeeded
            : IsHintSearchInProgress
                ? HintStatus.HintSearching
                : ElapsedSeconds >= HintUnlockSeconds
                    ? HintStatus.HintAvailable
                    : HintStatus.HintLocked;

        public event Action BoardChanged;
        public event Action SessionChanged;
        public event Action<SessionCompletionResult> LevelCompleted;
        public event Action<Exception> HintSearchFailed;

        public GameplaySession(LevelDefinition levelDefinition, PuzzleSolverOptions solverOptions = null,
            StarEvaluator starEvaluator = null)
            : this(levelDefinition, null, new BackgroundHintSolverRunner(), solverOptions,
                starEvaluator, true)
        {
        }

        public GameplaySession(LevelDefinition levelDefinition, int authoredOptimalMoves,
            IHintSolverRunner hintSolverRunner = null, PuzzleSolverOptions solverOptions = null,
            StarEvaluator starEvaluator = null)
            : this(levelDefinition, authoredOptimalMoves,
                hintSolverRunner ?? new BackgroundHintSolverRunner(), solverOptions,
                starEvaluator, false)
        {
            if (authoredOptimalMoves < 0)
                throw new ArgumentOutOfRangeException(nameof(authoredOptimalMoves));
        }

        private GameplaySession(LevelDefinition levelDefinition, int? authoredOptimalMoves,
            IHintSolverRunner hintSolverRunner, PuzzleSolverOptions solverOptions,
            StarEvaluator starEvaluator, bool calculateBaseline)
        {
            ActiveLevel = levelDefinition ?? throw new ArgumentNullException(nameof(levelDefinition));
            this.solverOptions = CopyOptions(solverOptions ?? PuzzleSolverProfiles.RuntimeHint);
            this.starEvaluator = starEvaluator ?? new StarEvaluator();
            this.hintSolverRunner = hintSolverRunner ??
                                    throw new ArgumentNullException(nameof(hintSolverRunner));

            if (calculateBaseline)
            {
                PuzzleSolverResult baseline = new PuzzleSolver().Solve(
                    ActiveLevel.CreateBoardState(), this.solverOptions);
                OptimalSolverStatus = baseline.Status;
                OptimalMoves = baseline.Status == PuzzleSolverStatus.Solved
                    ? baseline.MinimumMoveCount
                    : (int?)null;
            }
            else
            {
                OptimalSolverStatus = PuzzleSolverStatus.Solved;
                OptimalMoves = authoredOptimalMoves;
            }

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
            InvalidatePendingHint();
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
            if (deltaSeconds == 0f || IsDisposed || IsCompleted) return;
            bool hintWasLocked = ElapsedSeconds < HintUnlockSeconds;
            ElapsedSeconds += deltaSeconds;
            if (hintWasLocked && ElapsedSeconds >= HintUnlockSeconds &&
                LastHint.Status == HintStatus.HintLocked)
                LastHint = HintResult.WithoutAction(HintStatus.HintAvailable);
            SessionChanged?.Invoke();
        }

        public HintResult RequestHint(PuzzleSolverOptions options = null)
        {
            if (IsDisposed)
                return SetHint(HintResult.WithoutAction(HintStatus.UnsolvableOrInvalid));
            if (IsCompleted)
                return SetHint(HintResult.WithoutAction(HintStatus.NoHintNeeded));
            if (ElapsedSeconds < HintUnlockSeconds)
                return SetHint(HintResult.WithoutAction(HintStatus.HintLocked));
            if (IsHintSearchInProgress)
                return LastHint;

            BoardState snapshot = Board.CreateIndependentCopy();
            PuzzleSolverOptions requestOptions = CopyOptions(options ?? solverOptions);
            int requestId = ++nextHintRequestId;
            int requestBoardVersion = boardVersion;
            activeHintRequestId = requestId;
            SetHint(HintResult.WithoutAction(HintStatus.HintSearching));
            try
            {
                hintSolverRunner.Start(snapshot, requestOptions,
                    result => hintCompletions.Enqueue(
                        HintSolverCompletion.Succeeded(requestId, requestBoardVersion, result)),
                    exception => hintCompletions.Enqueue(
                        HintSolverCompletion.Failed(requestId, requestBoardVersion, exception)));
            }
            catch (Exception exception)
            {
                activeHintRequestId = 0;
                HintSearchFailed?.Invoke(exception);
                return SetHint(HintResult.WithoutAction(HintStatus.UnsolvableOrInvalid));
            }

            return LastHint;
        }

        public bool UpdateHintRequest()
        {
            bool changed = false;
            while (hintCompletions.TryDequeue(out HintSolverCompletion completion))
            {
                if (IsDisposed || completion.RequestId != activeHintRequestId ||
                    completion.BoardVersion != boardVersion)
                    continue;

                activeHintRequestId = 0;
                if (completion.Exception != null)
                {
                    HintSearchFailed?.Invoke(completion.Exception);
                    SetHint(HintResult.WithoutAction(HintStatus.UnsolvableOrInvalid));
                }
                else
                {
                    ApplyHintResult(completion.Result);
                }
                changed = true;
            }

            return changed;
        }

        private HintResult ApplyHintResult(PuzzleSolverResult result)
        {
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
            InvalidatePendingHint();
            LastHint = HintResult.WithoutAction(HintAvailability);
            SessionChanged?.Invoke();
        }

        private void InvalidatePendingHint()
        {
            boardVersion++;
            activeHintRequestId = 0;
            if (LastHint?.Status == HintStatus.HintSearching)
            {
                HintStatus status = IsDisposed
                    ? HintStatus.UnsolvableOrInvalid
                    : IsCompleted
                        ? HintStatus.NoHintNeeded
                        : ElapsedSeconds >= HintUnlockSeconds
                            ? HintStatus.HintAvailable
                            : HintStatus.HintLocked;
                LastHint = HintResult.WithoutAction(status);
            }
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
            InvalidatePendingHint();
            StarEvaluationResult rating = starEvaluator.Evaluate(true, MoveCount, OptimalMoves);
            CompletionResult = new SessionCompletionResult(ActiveLevel, MoveCount, ElapsedSeconds,
                OptimalMoves, HintsUsed, rating);
            LastHint = HintResult.WithoutAction(HintStatus.NoHintNeeded);
            if (raiseEvent)
                LevelCompleted?.Invoke(CompletionResult);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            InvalidatePendingHint();
            if (simulation != null)
            {
                simulation.BoardChanged -= HandleBoardChanged;
                simulation.LevelCompleted -= HandleLevelCompleted;
            }
        }

        private static PuzzleSolverOptions CopyOptions(PuzzleSolverOptions source)
        {
            return new PuzzleSolverOptions
            {
                MaximumExploredStates = source.MaximumExploredStates,
                MaximumDepth = source.MaximumDepth
            };
        }

        private sealed class HintSolverCompletion
        {
            public int RequestId { get; }
            public int BoardVersion { get; }
            public PuzzleSolverResult Result { get; }
            public Exception Exception { get; }

            private HintSolverCompletion(int requestId, int boardVersion,
                PuzzleSolverResult result, Exception exception)
            {
                RequestId = requestId;
                BoardVersion = boardVersion;
                Result = result;
                Exception = exception;
            }

            public static HintSolverCompletion Succeeded(int requestId, int boardVersion,
                PuzzleSolverResult result)
            {
                return new HintSolverCompletion(requestId, boardVersion,
                    result ?? throw new ArgumentNullException(nameof(result)), null);
            }

            public static HintSolverCompletion Failed(int requestId, int boardVersion,
                Exception exception)
            {
                return new HintSolverCompletion(requestId, boardVersion, null,
                    exception ?? new InvalidOperationException("Hint solver failed without an exception."));
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using NeonGrid.Simulation;

namespace NeonGrid.Session
{
    public interface IHintSolverRunner
    {
        void Start(BoardState boardSnapshot, PuzzleSolverOptions options,
            Action<PuzzleSolverResult> completed, Action<Exception> failed);
    }

    public sealed class BackgroundHintSolverRunner : IHintSolverRunner
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        , IInstrumentedHintSolverRunner
#endif
    {
        public void Start(BoardState boardSnapshot, PuzzleSolverOptions options,
            Action<PuzzleSolverResult> completed, Action<Exception> failed)
        {
            StartCore(boardSnapshot, options,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                null,
#endif
                completed, failed);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void IInstrumentedHintSolverRunner.Start(BoardState boardSnapshot,
            PuzzleSolverOptions options, HintPerformanceTrace trace,
            Action<PuzzleSolverResult> completed, Action<Exception> failed)
        {
            StartCore(boardSnapshot, options, trace, completed, failed);
        }
#endif

        private static void StartCore(BoardState boardSnapshot, PuzzleSolverOptions options,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HintPerformanceTrace trace,
#endif
            Action<PuzzleSolverResult> completed, Action<Exception> failed)
        {
            if (boardSnapshot == null) throw new ArgumentNullException(nameof(boardSnapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (failed == null) throw new ArgumentNullException(nameof(failed));

            Task.Run(() =>
            {
                try
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    trace?.MarkWorkerStarted();
#if UNITY_EDITOR
                    long allocationStart = GC.GetAllocatedBytesForCurrentThread();
#endif
                    var profile = trace == null ? null : new SolverProfile();
                    trace?.MarkSolverStarted();
                    PuzzleSolverResult result = profile == null
                        ? new PuzzleSolver().Solve(boardSnapshot, options)
                        : new PuzzleSolver().SolveProfiled(boardSnapshot, options, profile);
                    trace?.MarkSolverFinished(result, profile);
                    long allocatedBytes = -1;
#if UNITY_EDITOR
                    allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
#endif
                    trace?.SetSolverAllocatedBytes(allocatedBytes);
                    completed(result);
#else
                    completed(new PuzzleSolver().Solve(boardSnapshot, options));
#endif
                }
                catch (Exception exception)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    trace?.MarkWorkerFailed(exception);
#endif
                    failed(exception);
                }
            });
        }
    }
}

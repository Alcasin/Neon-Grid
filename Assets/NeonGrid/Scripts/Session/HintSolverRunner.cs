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
    {
        public void Start(BoardState boardSnapshot, PuzzleSolverOptions options,
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
                    completed(new PuzzleSolver().Solve(boardSnapshot, options));
                }
                catch (Exception exception)
                {
                    failed(exception);
                }
            });
        }
    }
}

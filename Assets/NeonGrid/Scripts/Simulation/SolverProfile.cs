using System.Diagnostics;

namespace NeonGrid.Simulation
{
    // Per-call counters used only by explicit Editor/development diagnostics.
    internal sealed class SolverProfile
    {
        internal long CopyTicks, PowerTicks, KeyTicks, ActionTicks;
        internal long Copies, Powers, Keys, ActionLists;
        internal long SolverTicks;
        internal long UniqueStatesVisited;
        internal long NodesExpanded;
        internal long SuccessorsGenerated;
        internal long DuplicateSuccessorsRejected;
        // With parent-linked BFS these count only the final solution reconstruction:
        // one array and one element write per returned action for a successful non-zero path.
        internal long PathArraysAllocated;
        internal long PathElementsCopied;
        internal int MaximumFrontierSize;
        internal int DeepestDepthReached;
        internal int FinalSolutionLength;
        internal PuzzleSolverStatus TerminalStatus;

        internal double SolverMilliseconds => SolverTicks * 1000d / Stopwatch.Frequency;

        public override string ToString()
        {
            double scale = 1000d / Stopwatch.Frequency;
            return $"status={TerminalStatus} solverMs={SolverMilliseconds:F2} " +
                $"states={UniqueStatesVisited} expanded={NodesExpanded} " +
                $"successors={SuccessorsGenerated} duplicates={DuplicateSuccessorsRejected} " +
                $"copies={Copies} copyMs={CopyTicks * scale:F2} " +
                $"powers={Powers} powerMs={PowerTicks * scale:F2} " +
                $"keys={Keys} keyMs={KeyTicks * scale:F2} " +
                $"actionLists={ActionLists} actionMs={ActionTicks * scale:F2} " +
                $"pathArrays={PathArraysAllocated} pathElements={PathElementsCopied} " +
                $"maxFrontier={MaximumFrontierSize} depth={DeepestDepthReached} " +
                $"solutionLength={FinalSolutionLength}";
        }
    }
}

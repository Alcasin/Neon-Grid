#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Session
{
    /// <summary>
    /// Development-only, request-correlated Hint timing. Worker methods only capture
    /// Stopwatch timestamps and data; the single Unity log is emitted on the main thread.
    /// </summary>
    internal sealed class HintPerformanceTrace
    {
        private readonly string levelName;
        private readonly long buttonTimestamp;
        private long sessionTimestamp;
        private long snapshotStartTimestamp;
        private long snapshotEndTimestamp;
        private long workerStartTimestamp;
        private long solverStartTimestamp;
        private long solverEndTimestamp;
        private long mainThreadTimestamp;
        private long selectionTimestamp;
        private long presentationTimestamp;
        private int emitted;
        private PuzzleSolverResult result;
        private SolverProfile profile;
        private Exception failure;

        internal int RequestId { get; private set; }
        internal int RequestedBoardVersion { get; private set; }
        internal int CurrentBoardVersion { get; private set; }
        internal bool IsStale { get; private set; }
        internal bool OverlappedAnotherSearch { get; private set; }
        internal long SolverAllocatedBytes { get; private set; } = -1;
        internal bool HasSolverFinished => Interlocked.Read(ref solverEndTimestamp) != 0;
        internal SolverProfile Profile => profile;

        private HintPerformanceTrace(string level)
        {
            levelName = string.IsNullOrWhiteSpace(level) ? "unknown" : level;
            buttonTimestamp = Stopwatch.GetTimestamp();
        }

        internal static HintPerformanceTrace Begin(string levelName)
        {
            return new HintPerformanceTrace(levelName);
        }

        internal void MarkSessionEntered()
        {
            sessionTimestamp = Stopwatch.GetTimestamp();
        }

        internal void MarkSnapshotStarted()
        {
            snapshotStartTimestamp = Stopwatch.GetTimestamp();
        }

        internal void MarkSnapshotFinished()
        {
            snapshotEndTimestamp = Stopwatch.GetTimestamp();
        }

        internal void BindRequest(int requestId, int boardVersion)
        {
            RequestId = requestId;
            RequestedBoardVersion = boardVersion;
            CurrentBoardVersion = boardVersion;
        }

        internal void MarkOverlap()
        {
            OverlappedAnotherSearch = true;
        }

        internal void MarkWorkerStarted()
        {
            Interlocked.Exchange(ref workerStartTimestamp, Stopwatch.GetTimestamp());
        }

        internal void MarkSolverStarted()
        {
            Interlocked.Exchange(ref solverStartTimestamp, Stopwatch.GetTimestamp());
        }

        internal void MarkSolverFinished(PuzzleSolverResult solverResult,
            SolverProfile solverProfile)
        {
            result = solverResult;
            profile = solverProfile;
            Interlocked.Exchange(ref solverEndTimestamp, Stopwatch.GetTimestamp());
        }

        internal void SetSolverAllocatedBytes(long allocatedBytes)
        {
            SolverAllocatedBytes = allocatedBytes;
        }

        internal void MarkWorkerFailed(Exception exception)
        {
            failure = exception;
            Interlocked.Exchange(ref solverEndTimestamp, Stopwatch.GetTimestamp());
        }

        internal void MarkMainThreadHandled(int currentBoardVersion, bool stale)
        {
            CurrentBoardVersion = currentBoardVersion;
            IsStale = stale;
            mainThreadTimestamp = Stopwatch.GetTimestamp();
        }

        internal void MarkMoveSelected()
        {
            selectionTimestamp = Stopwatch.GetTimestamp();
        }

        internal void MarkPresentationStarted()
        {
            presentationTimestamp = Stopwatch.GetTimestamp();
        }

        internal void EmitOnce()
        {
            if (Interlocked.Exchange(ref emitted, 1) != 0) return;

            long terminalTimestamp = presentationTimestamp != 0
                ? presentationTimestamp
                : mainThreadTimestamp != 0
                    ? mainThreadTimestamp
                    : Stopwatch.GetTimestamp();
            SolverProfile counters = profile;
            string status = result != null
                ? result.Status.ToString()
                : failure != null
                    ? "Failed:" + failure.GetType().Name
                    : "Unknown";
            var text = new StringBuilder(640);
            text.Append("HintPerf ")
                .Append("request=").Append(RequestId)
                .Append(" level=").Append(levelName)
                .Append(" boardVersion=").Append(RequestedBoardVersion)
                .Append(" currentBoardVersion=").Append(CurrentBoardVersion)
                .Append(" status=").Append(status)
                .Append(" stale=").Append(IsStale.ToString().ToLowerInvariant())
                .Append(" overlap=").Append(OverlappedAnotherSearch.ToString().ToLowerInvariant())
                .Append(" totalMs=").Append(Milliseconds(buttonTimestamp, terminalTimestamp))
                .Append(" buttonToSessionMs=").Append(Milliseconds(buttonTimestamp,
                    sessionTimestamp))
                .Append(" snapshotMs=").Append(Milliseconds(snapshotStartTimestamp,
                    snapshotEndTimestamp))
                .Append(" queueToWorkerMs=").Append(Milliseconds(snapshotEndTimestamp,
                    Interlocked.Read(ref workerStartTimestamp)))
                .Append(" solverMs=").Append(Milliseconds(
                    Interlocked.Read(ref solverStartTimestamp),
                    Interlocked.Read(ref solverEndTimestamp)))
                .Append(" workerToMainMs=").Append(Milliseconds(
                    Interlocked.Read(ref solverEndTimestamp), mainThreadTimestamp))
                .Append(" selectionToPresentationMs=").Append(Milliseconds(selectionTimestamp,
                    presentationTimestamp))
                .Append(" states=").Append(counters?.UniqueStatesVisited ?? -1)
                .Append(" expanded=").Append(counters?.NodesExpanded ?? -1)
                .Append(" successors=").Append(counters?.SuccessorsGenerated ?? -1)
                .Append(" duplicates=").Append(counters?.DuplicateSuccessorsRejected ?? -1)
                .Append(" copies=").Append(counters?.Copies ?? -1)
                .Append(" copyMs=").Append(ProfileMilliseconds(counters?.CopyTicks))
                .Append(" keys=").Append(counters?.Keys ?? -1)
                .Append(" keyMs=").Append(ProfileMilliseconds(counters?.KeyTicks))
                .Append(" power=").Append(counters?.Powers ?? -1)
                .Append(" powerMs=").Append(ProfileMilliseconds(counters?.PowerTicks))
                .Append(" actionLists=").Append(counters?.ActionLists ?? -1)
                .Append(" actionListMs=").Append(ProfileMilliseconds(counters?.ActionTicks))
                .Append(" pathArrays=").Append(counters?.PathArraysAllocated ?? -1)
                .Append(" pathElementsCopied=").Append(counters?.PathElementsCopied ?? -1)
                .Append(" maxFrontier=").Append(counters?.MaximumFrontierSize ?? -1)
                .Append(" depth=").Append(counters?.DeepestDepthReached ?? -1)
                .Append(" solutionLength=").Append(counters?.FinalSolutionLength ?? -1)
                .Append(" solverTotalMs=").Append(counters == null
                    ? "-1.00"
                    : Format(counters.SolverMilliseconds))
                .Append(" allocatedBytes=").Append(SolverAllocatedBytes);
            UnityEngine.Debug.Log(text.ToString());
        }

        private static string Milliseconds(long start, long end)
        {
            if (start == 0 || end == 0 || end < start) return "-1.00";
            return Format((end - start) * 1000d / Stopwatch.Frequency);
        }

        private static string ProfileMilliseconds(long? ticks)
        {
            return ticks.HasValue
                ? Format(ticks.Value * 1000d / Stopwatch.Frequency)
                : "-1.00";
        }

        private static string Format(double value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }
    }

    internal interface IInstrumentedHintSolverRunner
    {
        void Start(BoardState boardSnapshot, PuzzleSolverOptions options,
            HintPerformanceTrace trace, Action<PuzzleSolverResult> completed,
            Action<Exception> failed);
    }
}
#endif

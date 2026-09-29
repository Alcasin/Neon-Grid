using System;
using System.Collections.Generic;
using System.Linq;
using NeonGrid.Data;
using NeonGrid.Session;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class HintPerformanceInstrumentationTests
    {
        [Test]
        public void ProfiledSearchMatchesUnprofiledResultLengthAndDeterministicOrdering()
        {
            LevelDefinition level = Load("Levels/AutomationPlant/AP_07");
            PuzzleSolverResult production = new PuzzleSolver().Solve(
                level.CreateBoardState(), PuzzleSolverProfiles.RuntimeHint);
            var firstProfile = new SolverProfile();
            PuzzleSolverResult profiled = new PuzzleSolver().SolveProfiled(
                level.CreateBoardState(), PuzzleSolverProfiles.RuntimeHint, firstProfile);
            var secondProfile = new SolverProfile();
            PuzzleSolverResult repeated = new PuzzleSolver().SolveProfiled(
                level.CreateBoardState(), PuzzleSolverProfiles.RuntimeHint, secondProfile);

            Assert.That(profiled.Status, Is.EqualTo(production.Status));
            Assert.That(profiled.MinimumMoveCount, Is.EqualTo(production.MinimumMoveCount));
            Assert.That(profiled.Solution.ToArray(), Is.EqualTo(production.Solution.ToArray()));
            Assert.That(repeated.Solution.ToArray(), Is.EqualTo(profiled.Solution.ToArray()));
            Assert.That(profiled.ExploredStateCount, Is.EqualTo(production.ExploredStateCount));
            Assert.That(profiled.DeepestSearchDepth, Is.EqualTo(production.DeepestSearchDepth));
            Assert.That(secondProfile.UniqueStatesVisited,
                Is.EqualTo(firstProfile.UniqueStatesVisited));
            Assert.That(secondProfile.SuccessorsGenerated,
                Is.EqualTo(firstProfile.SuccessorsGenerated));
        }

        [Test]
        public void SolverProfileCapturesSummaryCountersWithoutPerNodeCallbacks()
        {
            var profile = new SolverProfile();
            PuzzleSolverResult result = new PuzzleSolver().SolveProfiled(
                Load("Levels/PowerStation/PS_01").CreateBoardState(),
                PuzzleSolverProfiles.RuntimeHint, profile);

            Assert.That(result.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            Assert.That(profile.TerminalStatus, Is.EqualTo(result.Status));
            Assert.That(profile.UniqueStatesVisited, Is.EqualTo(result.ExploredStateCount));
            Assert.That(profile.DeepestDepthReached, Is.EqualTo(result.DeepestSearchDepth));
            Assert.That(profile.FinalSolutionLength, Is.EqualTo(result.Solution.Count));
            Assert.That(profile.NodesExpanded, Is.GreaterThan(0));
            Assert.That(profile.SuccessorsGenerated, Is.GreaterThan(0));
            Assert.That(profile.Copies, Is.GreaterThan(0));
            Assert.That(profile.Keys, Is.GreaterThan(0));
            Assert.That(profile.Powers, Is.EqualTo(profile.UniqueStatesVisited));
            Assert.That(profile.ActionLists, Is.EqualTo(profile.NodesExpanded));
            Assert.That(profile.PathArraysAllocated, Is.EqualTo(result.ExploredStateCount - 1));
            Assert.That(profile.MaximumFrontierSize, Is.GreaterThan(0));
            Assert.That(profile.SolverTicks, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void NonInstrumentedRunnerPreservesAuthoritativeHintSelection()
        {
            LevelDefinition level = Load("Levels/PowerStation/PS_01");
            PuzzleAction expected = new PuzzleSolver().Solve(level.CreateBoardState(),
                PuzzleSolverProfiles.RuntimeHint).Solution[0];
            var runner = new ImmediateNonInstrumentedRunner();
            using (var session = new GameplaySession(level, 1, runner))
            {
                session.AdvanceTime(GameplaySession.HintUnlockSeconds);

                Assert.That(session.RequestHint().Status, Is.EqualTo(HintStatus.HintSearching));
                Assert.That(session.UpdateHintRequest(), Is.True);
                Assert.That(session.LastHint.Status, Is.EqualTo(HintStatus.HintAvailable));
                Assert.That(session.LastHint.SuggestedAction, Is.EqualTo(expected));
                Assert.That(session.HintsUsed, Is.True);
            }
        }

        [Test]
        public void InstrumentedStaleResultIsRejectedAndOverlapIsReported()
        {
            LevelDefinition level = Load("Levels/M3_Test_02");
            var runner = new ControlledInstrumentedRunner();
            using (var session = new GameplaySession(level, 2, runner))
            {
                session.AdvanceTime(GameplaySession.HintUnlockSeconds);
                HintPerformanceTrace first = HintPerformanceTrace.Begin(level.name);
                Assert.That(session.RequestHint(null, first).Status,
                    Is.EqualTo(HintStatus.HintSearching));

                PuzzleAction mutation = session.Board.GetValidActions()[0];
                Assert.That(session.PerformAction(mutation), Is.True);
                HintPerformanceTrace second = HintPerformanceTrace.Begin(level.name);
                Assert.That(session.RequestHint(null, second).Status,
                    Is.EqualTo(HintStatus.HintSearching));
                Assert.That(first.OverlappedAnotherSearch, Is.True);
                Assert.That(second.OverlappedAnotherSearch, Is.True);

                runner.Complete(0);
                Assert.That(session.UpdateHintRequest(), Is.False);
                Assert.That(first.IsStale, Is.True);
                Assert.That(session.IsHintSearchInProgress, Is.True);

                runner.Complete(1);
                Assert.That(session.UpdateHintRequest(), Is.True);
                Assert.That(second.IsStale, Is.False);
                Assert.That(session.LastHint.Status, Is.EqualTo(HintStatus.HintAvailable));
                Assert.That(session.LastHint.SuggestedAction,
                    Is.EqualTo(runner.Results[1].Solution[0]));
            }
        }

        private static LevelDefinition Load(string path)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(path);
            Assert.That(level, Is.Not.Null, path);
            return level;
        }

        private sealed class ImmediateNonInstrumentedRunner : IHintSolverRunner
        {
            public void Start(BoardState boardSnapshot, PuzzleSolverOptions options,
                Action<PuzzleSolverResult> completed, Action<Exception> failed)
            {
                try
                {
                    completed(new PuzzleSolver().Solve(boardSnapshot, options));
                }
                catch (Exception exception)
                {
                    failed(exception);
                }
            }
        }

        private sealed class ControlledInstrumentedRunner : IHintSolverRunner,
            IInstrumentedHintSolverRunner
        {
            private readonly List<Request> requests = new List<Request>();
            public readonly List<PuzzleSolverResult> Results =
                new List<PuzzleSolverResult>();

            public void Start(BoardState boardSnapshot, PuzzleSolverOptions options,
                Action<PuzzleSolverResult> completed, Action<Exception> failed)
            {
                requests.Add(new Request(boardSnapshot, options, null, completed));
            }

            void IInstrumentedHintSolverRunner.Start(BoardState boardSnapshot,
                PuzzleSolverOptions options, HintPerformanceTrace trace,
                Action<PuzzleSolverResult> completed, Action<Exception> failed)
            {
                requests.Add(new Request(boardSnapshot, options, trace, completed));
            }

            public void Complete(int index)
            {
                Request request = requests[index];
                request.Trace?.MarkWorkerStarted();
                request.Trace?.MarkSolverStarted();
                var profile = request.Trace == null ? null : new SolverProfile();
                PuzzleSolverResult result = profile == null
                    ? new PuzzleSolver().Solve(request.Board, request.Options)
                    : new PuzzleSolver().SolveProfiled(request.Board, request.Options, profile);
                request.Trace?.MarkSolverFinished(result, profile);
                while (Results.Count <= index) Results.Add(null);
                Results[index] = result;
                request.Completed(result);
            }

            private sealed class Request
            {
                public readonly BoardState Board;
                public readonly PuzzleSolverOptions Options;
                public readonly HintPerformanceTrace Trace;
                public readonly Action<PuzzleSolverResult> Completed;

                public Request(BoardState board, PuzzleSolverOptions options,
                    HintPerformanceTrace trace, Action<PuzzleSolverResult> completed)
                {
                    Board = board;
                    Options = options;
                    Trace = trace;
                    Completed = completed;
                }
            }
        }
    }
}

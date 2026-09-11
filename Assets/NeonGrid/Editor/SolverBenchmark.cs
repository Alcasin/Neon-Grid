using System;
using System.Diagnostics;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Editor
{
    // Explicit development benchmark, never part of normal correctness tests.
    public static class SolverBenchmark
    {
        public static void CompareCopies()
        {
            BoardState source = Resources.Load<LevelDefinition>("Levels/Substation/S_10").CreateBoardState();
            foreach (bool reference in new[] { true, false })
            {
                for (int warm = 0; warm < 100; warm++) CopyForBenchmark(source, reference);
                for (int run = 0; run < 3; run++)
                {
                    GC.Collect();
                    int collections = GC.CollectionCount(0);
                    var timer = Stopwatch.StartNew();
                    for (int copy = 0; copy < 50000; copy++) CopyForBenchmark(source, reference);
                    timer.Stop();
                    UnityEngine.Debug.Log($"COPY {(reference ? "before" : "after")} run={run + 1} " +
                        $"copies=50000 ms={timer.Elapsed.TotalMilliseconds:F2} " +
                        $"gcCollections={GC.CollectionCount(0) - collections}");
                }
            }
        }

        private static BoardState CopyForBenchmark(BoardState source, bool reference)
        {
            if (!reference) return source.CreateIndependentCopy();
            // Retain only the original copy operation as an allocation comparison;
            // this is not a second solver or an alternate runtime path.
            var definitions = new List<TileDefinition>(source.Width * source.Height);
            foreach (CircuitTileState tile in source.AllTiles())
                definitions.Add(new TileDefinition(tile.Position, tile.TileType,
                    tile.Rotation, tile.IsRotatable, tile.IsSwitchOn));
            return new BoardState(source.Width, source.Height, definitions);
        }

        public static void Run()
        {
            foreach (string fixture in new[] { "S_09", "S_10", "S_10_scrambled" })
            {
                BoardState board = Resources.Load<LevelDefinition>("Levels/Substation/" +
                    (fixture == "S_09" ? "S_09" : "S_10")).CreateBoardState();
                if (fixture.EndsWith("scrambled"))
                {
                    var simulation = new CircuitSimulation(board);
                    simulation.ApplyAction(board.GetValidActions()[0]);
                }
                new PuzzleSolver().Solve(board, PuzzleSolverProfiles.RuntimeHint);
                for (int run = 0; run < 3; run++)
                {
                    GC.Collect();
                    long allocated = GC.GetAllocatedBytesForCurrentThread();
                    int collections = GC.CollectionCount(0);
                    var timer = Stopwatch.StartNew();
                    PuzzleSolverResult result = new PuzzleSolver().Solve(board, PuzzleSolverProfiles.RuntimeHint);
                    timer.Stop();
                    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    string allocation = allocated == 0 ? "unavailable" : allocated.ToString();
                    UnityEngine.Debug.Log($"BENCH {fixture} run={run + 1} ms={timer.Elapsed.TotalMilliseconds:F2} " +
                        $"bytes={allocation} gcCollections={GC.CollectionCount(0) - collections} states={result.ExploredStateCount} depth={result.DeepestSearchDepth} " +
                        $"status={result.Status} moves={result.MinimumMoveCount} sequence={string.Join("|", result.Solution)}");
                }
                var profile = new SolverProfile();
                new PuzzleSolver().SolveProfiled(board, PuzzleSolverProfiles.RuntimeHint, profile);
                UnityEngine.Debug.Log($"PROFILE {fixture} {profile}");
            }
        }
    }
}

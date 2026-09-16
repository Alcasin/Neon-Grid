using System;
using System.Diagnostics;
using NeonGrid.Data;
using NeonGrid.Simulation;
using NeonGrid.Validation;
using UnityEditor;

namespace NeonGrid.Editor
{
    /// <summary>Read-only M10 content gate; it never dirties or saves production assets.</summary>
    public static class AutomationPlantPreflight
    {
        private static readonly int[] ExpectedMinima = { 5, 6, 8, 5, 6, 7, 7, 6, 6, 9 };

        public static void Run()
        {
            for (int index = 0; index < ExpectedMinima.Length; index++)
            {
                string name = $"AP_{index + 1:D2}";
                string path = $"Assets/NeonGrid/Resources/Levels/Automation Plant/{name}.asset";
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level == null) throw new InvalidOperationException($"Missing {path}.");

                LevelValidationResult validation = new LevelValidator().Validate(level);
                var timer = Stopwatch.StartNew();
                PuzzleSolverResult result = validation.IsValid
                    ? new PuzzleSolver().Solve(level.CreateBoardState(), PuzzleSolverProfiles.AuthoringExact)
                    : null;
                timer.Stop();
                string issues = string.Join(" | ", validation.Errors);
                UnityEngine.Debug.Log($"M10 PREFLIGHT {name} path={path} size={level.Width}x{level.Height} " +
                    $"valid={validation.IsValid} warnings={validation.Warnings.Count} " +
                    $"status={result?.Status.ToString() ?? "NotRun"} minimum={result?.MinimumMoveCount ?? -1} " +
                    $"explored={result?.ExploredStateCount ?? -1} depth={result?.DeepestSearchDepth ?? -1} " +
                    $"ms={timer.Elapsed.TotalMilliseconds:F2} solution={Format(result)} issues={issues}");

                if (!validation.IsValid || result.Status != PuzzleSolverStatus.Solved ||
                    result.MinimumMoveCount != ExpectedMinima[index])
                    throw new InvalidOperationException($"M10 preflight failed for {name}; production content was not changed.");
            }

            UnityEngine.Debug.Log("M10 PREFLIGHT PASSED: all Automation Plant assets structurally valid with expected exact minima.");
        }

        private static string Format(PuzzleSolverResult result)
        {
            return result == null ? string.Empty : string.Join("|", result.Solution);
        }
    }
}

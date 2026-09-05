using System.Text;
using NeonGrid.Simulation;
using NeonGrid.Validation;

namespace NeonGrid.Editor.Authoring
{
    public static class LevelValidationReportFormatter
    {
        public static string Format(string levelName, LevelValidationReport report)
        {
            var text = new StringBuilder();
            text.AppendLine($"Level: {levelName}");
            text.AppendLine($"Structural Validation: {(report.StructuralValidation.IsValid ? "PASS" : "FAIL")}");

            foreach (LevelValidationIssue error in report.StructuralValidation.Errors)
                text.AppendLine($"ERROR [{error.Code}]: {error.Message}");
            foreach (LevelValidationIssue warning in report.StructuralValidation.Warnings)
                text.AppendLine($"WARNING [{warning.Code}]: {warning.Message}");

            PuzzleSolverResult solver = report.SolverResult;
            if (solver == null) return text.ToString().TrimEnd();

            text.AppendLine($"Solver Status: {FormatStatus(solver.Status)}");
            if (solver.Status == PuzzleSolverStatus.Solved)
                text.AppendLine($"Minimum Moves: {solver.MinimumMoveCount}");
            text.AppendLine($"Explored States: {solver.ExploredStateCount}");
            text.AppendLine($"Deepest Search Depth: {solver.DeepestSearchDepth}");

            if (solver.Status == PuzzleSolverStatus.Solved && solver.Solution.Count > 0)
            {
                text.AppendLine("Solution:");
                for (int index = 0; index < solver.Solution.Count; index++)
                    text.AppendLine($"{index + 1}. {solver.Solution[index]}");
            }

            return text.ToString().TrimEnd();
        }

        private static string FormatStatus(PuzzleSolverStatus status)
        {
            switch (status)
            {
                case PuzzleSolverStatus.SearchLimitReached: return "SEARCH LIMIT REACHED";
                case PuzzleSolverStatus.Unsolvable: return "UNSOLVABLE";
                default: return "SOLVED";
            }
        }
    }
}

using System.Text;
using NeonGrid.Data;
using NeonGrid.Simulation;
using NeonGrid.Validation;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionInspector : UnityEditor.Editor
    {
        private LevelValidationReport lastReport;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("Validate Level"))
            {
                var level = (LevelDefinition)target;
                lastReport = new LevelValidator().ValidateWithSolver(level, new PuzzleSolverOptions());
                Debug.Log(FormatReport(level.name, lastReport), level);
            }

            DrawLastReport();
        }

        private void DrawLastReport()
        {
            if (lastReport == null) return;

            MessageType structureType = lastReport.StructuralValidation.IsValid
                ? MessageType.Info
                : MessageType.Error;
            EditorGUILayout.HelpBox(FormatReport(((LevelDefinition)target).name, lastReport), structureType);
        }

        private static string FormatReport(string levelName, LevelValidationReport report)
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

using NeonGrid.Editor.Authoring;
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
                lastReport = new LevelValidator().ValidateWithSolver(level,
                    PuzzleSolverProfiles.AuthoringExact);
                Debug.Log(LevelValidationReportFormatter.Format(level.name, lastReport), level);
            }

            DrawLastReport();
        }

        private void DrawLastReport()
        {
            if (lastReport == null) return;

            MessageType structureType = lastReport.StructuralValidation.IsValid
                ? MessageType.Info
                : MessageType.Error;
            EditorGUILayout.HelpBox(
                LevelValidationReportFormatter.Format(((LevelDefinition)target).name, lastReport), structureType);
        }
    }
}

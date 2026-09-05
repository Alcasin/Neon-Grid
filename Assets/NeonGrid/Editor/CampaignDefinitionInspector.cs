using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    [CustomEditor(typeof(CampaignDefinition))]
    public sealed class CampaignDefinitionInspector : UnityEditor.Editor
    {
        private CampaignValidationReport report;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Validate Campaign"))
                report = new CampaignValidator().Validate((CampaignDefinition)target);

            if (report == null) return;
            if (report.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Campaign validation passed.", MessageType.Info);
                return;
            }

            foreach (CampaignValidationIssue issue in report.Issues)
            {
                MessageType type = issue.Severity == CampaignValidationSeverity.Error
                    ? MessageType.Error
                    : MessageType.Warning;
                EditorGUILayout.HelpBox($"{issue.Code}: {issue.Message}", type);
            }
        }
    }
}

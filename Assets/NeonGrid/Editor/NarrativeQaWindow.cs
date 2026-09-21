using System;
using System.IO;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public sealed class NarrativeQaWindow : EditorWindow
    {
        internal const string ProductionCampaignResourcePath = "Campaigns/NeonGrid_Main";
        internal const string ProductionNarrativeResourcePath =
            "Narratives/NeonGrid_Main_Narrative";

        private CampaignDefinition campaign;
        private CampaignNarrativeDefinition narrative;
        private NarrativeQaSnapshot snapshot;
        private NarrativeQaBackupService backupService;
        private string savePath;
        private Vector2 scroll;
        private bool destructiveActionConfirmed;
        private bool automaticBackupCreated;

        [MenuItem("Neon Grid/Narrative QA")]
        public static void Open()
        {
            GetWindow<NarrativeQaWindow>("Narrative QA");
        }

        private void OnEnable()
        {
            campaign = Resources.Load<CampaignDefinition>(ProductionCampaignResourcePath);
            narrative = Resources.Load<CampaignNarrativeDefinition>(
                ProductionNarrativeResourcePath);
            if (campaign == null) return;
            savePath = CampaignSaveStore.GetDefaultSavePath(campaign.CampaignId);
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string backupDirectory = Path.Combine(projectRoot, "Library", "NeonGrid",
                "NarrativeQA");
            backupService = new NarrativeQaBackupService(savePath, backupDirectory,
                campaign.CampaignId);
            RefreshState();
        }

        private void OnGUI()
        {
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                EditorGUILayout.LabelField("Neon Grid Narrative QA", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Editor-only lifecycle presets. Preparing a state overwrites the production " +
                    "development save; it never modifies campaign, narrative, or level assets.",
                    MessageType.Info);

                if (campaign == null || narrative == null)
                {
                    EditorGUILayout.HelpBox("Production campaign or narrative asset is missing.",
                        MessageType.Error);
                    return;
                }

                DrawCurrentState();
                EditorGUILayout.Space();
                DrawBackupControls();
                EditorGUILayout.Space();
                DrawPresetControls();
                EditorGUILayout.Space();
                DrawValidation();
            }
        }

        private void DrawCurrentState()
        {
            EditorGUILayout.LabelField("Current Production Save", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Campaign",
                $"{campaign.DisplayName} / {campaign.CampaignId}");
            EditorGUILayout.LabelField("Save path", savePath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Load status", snapshot.LoadStatus.ToString());
            EditorGUILayout.LabelField("Intro", snapshot.IntroStatus);
            EditorGUILayout.LabelField("Campaign progress",
                $"{snapshot.CompletedLevels} / {snapshot.TotalLevels} completed");
            EditorGUILayout.LabelField("Stars",
                $"★ {snapshot.Progress.TotalStars} / {snapshot.Progress.MaximumCampaignStars}");
            EditorGUILayout.LabelField("Pending restoration", snapshot.PendingRestoration);
            EditorGUILayout.LabelField("Ending", snapshot.EndingStatus);
            if (GUILayout.Button("Refresh Current State")) RefreshState();
        }

        private void DrawBackupControls()
        {
            EditorGUILayout.LabelField("Save Protection", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Backup",
                backupService.HasBackup ? backupService.BackupPath : "No QA backup recorded",
                EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Backup Current Save")) BackupCurrentSave();
                using (new EditorGUI.DisabledScope(!backupService.HasBackup))
                    if (GUILayout.Button("Restore Last QA Backup")) RestoreLastBackup();
            }
        }

        private void DrawPresetControls()
        {
            EditorGUILayout.LabelField("Prepare Narrative Test State", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each Prepare action changes the current production development save.",
                MessageType.Warning);
            DrawPresetButton("Prepare FRESH INTRO", NarrativeQaPreset.FreshIntro);
            DrawPresetButton("Prepare FRESH MAP / INTRO COMPLETE", NarrativeQaPreset.FreshMap);
            DrawPresetButton("Prepare FIRST RESTORATION PENDING",
                NarrativeQaPreset.FirstRestorationPending);
            DrawPresetButton("Prepare FINAL RESTORATION PENDING",
                NarrativeQaPreset.FinalRestorationPending);
            DrawPresetButton("Prepare ENDING PENDING", NarrativeQaPreset.EndingPending);
            DrawPresetButton("Prepare POST-ENDING COMPLETE",
                NarrativeQaPreset.PostEndingComplete);
        }

        private void DrawPresetButton(string label, NarrativeQaPreset preset)
        {
            if (GUILayout.Button(label, GUILayout.Height(30f))) PreparePreset(preset);
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (!GUILayout.Button("Validate Current Narrative State")) return;
            RefreshState();
            var issues = NarrativeQaValidator.Validate(campaign, narrative, snapshot);
            if (issues.Count == 0)
                EditorUtility.DisplayDialog("Narrative QA", "PASS", "OK");
            else
                EditorUtility.DisplayDialog("Narrative QA",
                    string.Join("\n", issues), "OK");
        }

        private void PreparePreset(NarrativeQaPreset preset)
        {
            if (!destructiveActionConfirmed)
            {
                bool confirmed = EditorUtility.DisplayDialog("Overwrite production save?",
                    "Narrative QA will replace the current Neon Grid production development " +
                    "save. A project-local backup will be created first.",
                    "Backup and Prepare", "Cancel");
                if (!confirmed) return;
                destructiveActionConfirmed = true;
            }

            try
            {
                if (!automaticBackupCreated)
                {
                    Debug.Log(backupService.BackupCurrentSave());
                    automaticBackupCreated = true;
                }
                CampaignSaveResult result = NarrativeQaStateBuilder.PrepareAndSave(campaign,
                    preset, savePath);
                if (!result.Succeeded)
                {
                    Debug.LogError(result.Message);
                    EditorUtility.DisplayDialog("Narrative QA save failed", result.Message, "OK");
                    return;
                }
                RefreshState();
                Debug.Log($"Narrative QA prepared '{preset}' for '{campaign.CampaignId}'. " +
                          $"Save path: {savePath}");
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Narrative QA failed", exception.Message, "OK");
            }
        }

        private void BackupCurrentSave()
        {
            try
            {
                string message = backupService.BackupCurrentSave();
                automaticBackupCreated = true;
                Debug.Log(message);
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Narrative QA backup failed", exception.Message, "OK");
            }
        }

        private void RestoreLastBackup()
        {
            bool confirmed = EditorUtility.DisplayDialog("Restore Narrative QA backup?",
                "This replaces the current production development save with the last QA backup.",
                "Restore Backup", "Cancel");
            if (!confirmed) return;
            try
            {
                string message = backupService.RestoreLastBackup();
                RefreshState();
                Debug.Log(message);
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Narrative QA restore failed", exception.Message,
                    "OK");
            }
        }

        private void RefreshState()
        {
            if (campaign == null || string.IsNullOrWhiteSpace(savePath)) return;
            snapshot = NarrativeQaSnapshot.Inspect(campaign, savePath);
        }
    }
}

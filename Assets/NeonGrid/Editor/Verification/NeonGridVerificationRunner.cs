using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using TestRunnerMode = UnityEditor.TestTools.TestRunner.Api.TestMode;

namespace NeonGrid.Editor
{
    /// <summary>
    /// Manually runs Neon Grid verification suites inside a normally opened,
    /// licensed Unity Editor. Nothing runs automatically on import, compile, play, or build.
    /// </summary>
    [InitializeOnLoad]
    public static class NeonGridVerificationRunner
    {
        private const string MenuRoot = "Neon Grid/Verification/";
        private const string ResultsDirectory = "Logs/M16B2Verification";
        private const string PendingPlayModeKey =
            "NeonGrid.Verification.M16B2.AudioPlayMode.Pending";
        private const string PlayModeStartTicksKey =
            "NeonGrid.Verification.M16B2.AudioPlayMode.StartTicks";
        private const string PendingPlayModeSuiteKey =
            "NeonGrid.Verification.PlayMode.Suite";
        private const string M17A2RegressionPackActiveKey =
            "NeonGrid.Verification.M17A2.RegressionPack.Active";
        private const string M17A2RegressionPackStateKey =
            "NeonGrid.Verification.M17A2.RegressionPack.State";
        private const string M17A2RegressionPackRelativePath =
            ResultsDirectory + "/M17_A2_Full_Regression_Pack.txt";
        private const int M17A2RegressionPackSuiteCount = 9;
        private const string M17B1RegressionPackActiveKey =
            "NeonGrid.Verification.M17B1.RegressionPack.Active";
        private const string M17B1RegressionPackStateKey =
            "NeonGrid.Verification.M17B1.RegressionPack.State";
        private const string M17B1RegressionPackRelativePath =
            ResultsDirectory + "/M17_B1_Full_Regression_Pack.txt";
        private const int M17B1RegressionPackSuiteCount = 11;
        private const double ContinuationDelaySeconds = 0.5d;
        private const int RequiredStableEditorFrames = 5;

        private static readonly string[] BroaderRegressionFixtures =
        {
            "NeonGrid.Tests.ProductionCityEnvironmentRolloutTests",
            "NeonGrid.Tests.CityEnvironmentFinalArtPreparationTests",
            "NeonGrid.Tests.CityEnvironmentArtPrototypeTests",
            "NeonGrid.Tests.ProductionCityBuildingArtRolloutTests",
            "NeonGrid.Tests.CityBuildingArtTests",
            "NeonGrid.Tests.CityBuildingFinalArtPreparationTests",
            "NeonGrid.Tests.AutomationPlantFinalArtPreparationTests",
            "NeonGrid.Tests.ControlCenterFinalArtPreparationTests",
            "NeonGrid.Tests.SubstationFinalArtPreparationTests",
            "NeonGrid.Tests.PowerStationFinalOverlayTests",
            "NeonGrid.Tests.CentralGridFinalOverlayTests",
            "NeonGrid.Tests.M15ProductionCampaignUiRolloutTests",
            "NeonGrid.Tests.M15CampaignUiThemeTests",
            "NeonGrid.Tests.M15ProductionGameplayRolloutTests",
            "NeonGrid.Tests.TechnicalNeonProductionThemeTests",
            "NeonGrid.Tests.TechnicalNeonVisualThemeTests",
            "NeonGrid.Tests.CampaignEndingTests",
            "NeonGrid.Tests.CampaignIntroTests",
            "NeonGrid.Tests.ChapterNarrativeShellTests",
            "NeonGrid.Tests.NarrativeIntegrationQaTests",
            "NeonGrid.Tests.ChapterRestorationEventTests",
            "NeonGrid.Tests.CityRestorationSequenceTests",
            "NeonGrid.Tests.CityMapPresentationTests",
            "NeonGrid.Tests.CampaignPersistenceTests",
            "NeonGrid.Tests.CampaignProgressTests",
            "NeonGrid.Tests.CampaignFlowTests",
            "NeonGrid.Tests.CampaignValidationTests",
            "NeonGrid.Tests.ProductionAutomationPlantSliceTests",
            "NeonGrid.Tests.ProductionCentralGridSliceTests",
            "NeonGrid.Tests.ProductionControlCenterSliceTests",
            "NeonGrid.Tests.ProductionMainCampaignTests",
            "NeonGrid.Tests.ProductionPowerStationSliceTests",
            "NeonGrid.Tests.ProductionSubstationSliceTests",
            "NeonGrid.Tests.PresentationSmokeTests",
            "NeonGrid.Tests.GameplaySessionTests",
            "NeonGrid.Tests.CircuitSimulationTests",
            "NeonGrid.Tests.StatefulComponentTests",
            "NeonGrid.Tests.TutorialRuntimeTests",
            "NeonGrid.Tests.BoardViewportFitterTests"
        };

        private static readonly Queue<SuiteDefinition> PendingSuites =
            new Queue<SuiteDefinition>();
        private static readonly List<SuiteOutcome> CompletedSuites =
            new List<SuiteOutcome>();

        private static TestRunnerApi testRunnerApi;
        private static RunnerCallbacks callbacks;
        private static SuiteDefinition currentSuite;
        private static DateTime currentStartUtc;
        private static bool isRunning;
        private static bool isCompleteVerification;
        private static bool isM17A2RegressionPack;
        private static bool isM17B1RegressionPack;
        private static bool testRunActive;
        private static bool waitingForStableEditor;
        private static double continuationNotBefore;
        private static int stableEditorFrames;
        private static DateTime m17A2RegressionPackStartUtc;
        private static DateTime m17B1RegressionPackStartUtc;

        static NeonGridVerificationRunner()
        {
            RecoverM17B1RegressionPackState();
            RecoverM17A2RegressionPackState();
            RecoverPendingPlayModeRun();
        }

        [MenuItem(MenuRoot + "M16 B2 - Focused")]
        private static void RunM16B2Focused()
        {
            StartSingle(FocusedB2());
        }

        [MenuItem(MenuRoot + "M16 C1 - Focused")]
        private static void RunM16C1Focused()
        {
            StartSingle(FocusedC1());
        }

        [MenuItem(MenuRoot + "M16 D1 - Focused")]
        private static void RunM16D1Focused()
        {
            StartSingle(FocusedD1());
        }

        [MenuItem(MenuRoot + "M16 D2 - Focused")]
        private static void RunM16D2Focused()
        {
            StartSingle(FocusedD2());
        }

        [MenuItem(MenuRoot + "M17 A1 - Focused")]
        private static void RunM17A1Focused()
        {
            StartSingle(FocusedM17A1());
        }

        [MenuItem(MenuRoot + "M17 A2 - Focused")]
        private static void RunM17A2Focused()
        {
            StartSingle(FocusedM17A2());
        }

        [MenuItem(MenuRoot + "M17 A2 Runtime PlayMode")]
        private static void RunM17A2RuntimePlayMode()
        {
            StartSingle(M17A2RuntimePlayMode());
        }

        [MenuItem(MenuRoot + "M17 A2 - Full Regression Pack")]
        private static void RunM17A2FullRegressionPack()
        {
            StartWorkflow(new[]
            {
                FocusedM17A2(),
                M17A2RuntimePlayMode(),
                FullEditMode(),
                FocusedB2(),
                AudioPlayMode(),
                FocusedC1(),
                HapticsPlayMode(),
                FocusedD1(),
                M16D2AmbiencePlayMode()
            }, false, true);
        }

        [MenuItem(MenuRoot + "M17 B1 - Focused")]
        private static void RunM17B1Focused()
        {
            StartSingle(FocusedM17B1());
        }

        [MenuItem(MenuRoot + "M17 B1 Settings PlayMode")]
        private static void RunM17B1SettingsPlayMode()
        {
            StartSingle(M17B1SettingsPlayMode());
        }

        [MenuItem(MenuRoot + "M17 B1 - Full Regression Pack")]
        private static void RunM17B1FullRegressionPack()
        {
            StartWorkflow(new[]
            {
                FocusedM17B1(),
                M17B1SettingsPlayMode(),
                FocusedM17A2(),
                M17A2RuntimePlayMode(),
                FullEditMode(),
                FocusedB2(),
                AudioPlayMode(),
                FocusedC1(),
                HapticsPlayMode(),
                FocusedD1(),
                M16D2AmbiencePlayMode()
            }, false, false, true);
        }

        [MenuItem(MenuRoot + "Hint Performance - Focused")]
        private static void RunHintPerformanceFocused()
        {
            StartSingle(HintPerformanceFocused());
        }

        [MenuItem(MenuRoot + "M16 B1 - Regression")]
        private static void RunM16B1Regression()
        {
            StartSingle(B1Regression());
        }

        [MenuItem(MenuRoot + "M16 A1-A2 - Regression")]
        private static void RunM16A1A2Regression()
        {
            StartSingle(A1A2Regression());
        }

        [MenuItem(MenuRoot + "Broader Regression")]
        private static void RunBroaderRegression()
        {
            StartSingle(BroaderRegression());
        }

        [MenuItem(MenuRoot + "Full EditMode")]
        private static void RunFullEditMode()
        {
            StartSingle(FullEditMode());
        }

        [MenuItem(MenuRoot + "Audio PlayMode")]
        private static void RunAudioPlayMode()
        {
            StartSingle(AudioPlayMode());
        }

        [MenuItem(MenuRoot + "Haptics PlayMode")]
        private static void RunHapticsPlayMode()
        {
            StartSingle(HapticsPlayMode());
        }

        [MenuItem(MenuRoot + "Production Feedback PlayMode")]
        private static void RunProductionFeedbackPlayMode()
        {
            StartSingle(ProductionFeedbackPlayMode());
        }

        [MenuItem(MenuRoot + "M16 D2 Ambience PlayMode")]
        private static void RunM16D2AmbiencePlayMode()
        {
            StartSingle(M16D2AmbiencePlayMode());
        }

        [MenuItem(MenuRoot + "M16 B2 - Complete Verification")]
        private static void RunCompleteVerification()
        {
            EditorUtility.DisplayDialog("M16 B2 Complete Verification",
                "This command safely runs the complete EditMode verification chain. " +
                "When it finishes, run 'Audio PlayMode' separately so Unity's domain reload " +
                "cannot interrupt or corrupt the EditMode workflow state.", "Run EditMode Chain");
            StartWorkflow(new[]
            {
                FocusedB2(),
                B1Regression(),
                A1A2Regression(),
                BroaderRegression(),
                FullEditMode()
            }, true);
        }

        [MenuItem(MenuRoot + "M16 B2 - Focused", true)]
        [MenuItem(MenuRoot + "M16 C1 - Focused", true)]
        [MenuItem(MenuRoot + "M16 D1 - Focused", true)]
        [MenuItem(MenuRoot + "M16 D2 - Focused", true)]
        [MenuItem(MenuRoot + "M17 A1 - Focused", true)]
        [MenuItem(MenuRoot + "M17 A2 - Focused", true)]
        [MenuItem(MenuRoot + "M17 A2 Runtime PlayMode", true)]
        [MenuItem(MenuRoot + "M17 A2 - Full Regression Pack", true)]
        [MenuItem(MenuRoot + "M17 B1 - Focused", true)]
        [MenuItem(MenuRoot + "M17 B1 Settings PlayMode", true)]
        [MenuItem(MenuRoot + "M17 B1 - Full Regression Pack", true)]
        [MenuItem(MenuRoot + "Hint Performance - Focused", true)]
        [MenuItem(MenuRoot + "M16 B1 - Regression", true)]
        [MenuItem(MenuRoot + "M16 A1-A2 - Regression", true)]
        [MenuItem(MenuRoot + "Broader Regression", true)]
        [MenuItem(MenuRoot + "Full EditMode", true)]
        [MenuItem(MenuRoot + "Audio PlayMode", true)]
        [MenuItem(MenuRoot + "Haptics PlayMode", true)]
        [MenuItem(MenuRoot + "Production Feedback PlayMode", true)]
        [MenuItem(MenuRoot + "M16 D2 Ambience PlayMode", true)]
        [MenuItem(MenuRoot + "M16 B2 - Complete Verification", true)]
        private static bool ValidateCommands()
        {
            return !isRunning && !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating &&
                   !EditorApplication.isPlayingOrWillChangePlaymode &&
                   !SessionState.GetBool(PendingPlayModeKey, false);
        }

        private static void StartSingle(SuiteDefinition suite)
        {
            StartWorkflow(new[] { suite }, false);
        }

        private static void StartWorkflow(IEnumerable<SuiteDefinition> suites, bool complete,
            bool m17A2RegressionPack = false, bool m17B1RegressionPack = false)
        {
            if (isRunning)
            {
                EditorUtility.DisplayDialog("Neon Grid Verification",
                    "A Neon Grid verification run is already active.", "OK");
                return;
            }

            PendingSuites.Clear();
            CompletedSuites.Clear();
            ClearM17A2RegressionPackState();
            ClearM17B1RegressionPackState();
            foreach (SuiteDefinition suite in suites)
            {
                PendingSuites.Enqueue(suite);
            }

            isCompleteVerification = complete;
            isM17A2RegressionPack = m17A2RegressionPack;
            isM17B1RegressionPack = m17B1RegressionPack;
            m17A2RegressionPackStartUtc = DateTime.UtcNow;
            m17B1RegressionPackStartUtc = DateTime.UtcNow;
            isRunning = true;
            EnsureResultsDirectory();
            if (complete)
            {
                WriteCompleteRunningSummary();
            }
            if (isM17A2RegressionPack)
            {
                PersistM17A2RegressionPackState();
                WriteM17A2RegressionPackSummary(false);
            }
            if (isM17B1RegressionPack)
            {
                PersistM17B1RegressionPackState();
                WriteM17B1RegressionPackSummary(false);
            }
            RunNextSuite();
        }

        private static void RunNextSuite()
        {
            if (!IsEditorStableForTestStart())
            {
                ScheduleNextSuite();
                return;
            }

            if (PendingSuites.Count == 0)
            {
                FinishWorkflow();
                return;
            }

            currentSuite = PendingSuites.Dequeue();
            currentStartUtc = DateTime.UtcNow;
            if (isM17A2RegressionPack)
            {
                PersistM17A2RegressionPackState();
            }
            if (isM17B1RegressionPack)
            {
                PersistM17B1RegressionPackState();
            }
            WriteRunningSummary(currentSuite, currentStartUtc);

            RegisterRunnerCallbacks();

            try
            {
                var settings = new ExecutionSettings(currentSuite.CreateFilter())
                {
                    runSynchronously = false
                };
                if (currentSuite.IsPlayMode)
                {
                    PersistPendingPlayModeRun();
                }
                testRunActive = true;
                testRunnerApi.Execute(settings);
                Debug.Log("Neon Grid verification started: " + currentSuite.DisplayName);
            }
            catch (Exception exception)
            {
                FinishWithRunnerError(exception.ToString());
            }
        }

        private static void OnRunStarted(ITestAdaptor testsToRun)
        {
            if (!testRunActive || currentSuite == null)
            {
                return;
            }

            WriteRunningSummary(currentSuite, currentStartUtc,
                testsToRun != null ? testsToRun.TestCaseCount : 0);
        }

        private static void OnRunFinished(ITestResultAdaptor result)
        {
            if (!testRunActive || currentSuite == null)
            {
                return;
            }

            try
            {
                DateTime endUtc = DateTime.UtcNow;
                string xmlRelativePath = XmlRelativePath(currentSuite);
                TestRunnerApi.SaveResultToFile(result, xmlRelativePath);

                List<string> failedTests = CollectFailedLeafTests(result);
                var outcome = new SuiteOutcome(
                    currentSuite.DisplayName,
                    result.PassCount,
                    result.FailCount,
                    result.SkipCount,
                    result.InconclusiveCount,
                    currentStartUtc,
                    endUtc,
                    failedTests);
                CompletedSuites.Add(outcome);
                WriteFinishedSummary(currentSuite, outcome, xmlRelativePath);
                Debug.Log("Neon Grid verification finished: " + currentSuite.DisplayName +
                          " — " + outcome.Status + " (" + outcome.Passed + "/" +
                          outcome.Total + " passed)");

                testRunActive = false;
                if (currentSuite.IsPlayMode)
                {
                    ClearPendingPlayModeRun();
                }
                if (isM17A2RegressionPack)
                {
                    PersistM17A2RegressionPackState();
                    WriteM17A2RegressionPackSummary(false);
                }
                if (isM17B1RegressionPack)
                {
                    PersistM17B1RegressionPackState();
                    WriteM17B1RegressionPackSummary(false);
                }
                ReleaseRunnerObjects();
                ScheduleNextSuite();
            }
            catch (Exception exception)
            {
                FinishWithRunnerError(exception.ToString());
            }
        }

        private static void FinishWithRunnerError(string message)
        {
            if (!testRunActive || currentSuite == null)
            {
                return;
            }

            testRunActive = false;
            if (currentSuite.IsPlayMode)
            {
                ClearPendingPlayModeRun();
            }
            DateTime endUtc = DateTime.UtcNow;
            var outcome = new SuiteOutcome(
                currentSuite.DisplayName, 0, 0, 0, 0,
                currentStartUtc, endUtc,
                new List<string> { "TestRunnerAPI error: " + message }, true);
            CompletedSuites.Add(outcome);
            WriteRunnerErrorSummary(currentSuite, outcome);
            Debug.LogError("Neon Grid verification runner failed: " + message);
            ReleaseRunnerObjects();
            PendingSuites.Clear();
            FinishWorkflow();
        }

        private static void FinishWorkflow()
        {
            StopWaitingForStableEditor();
            bool completedEditModeChain = isCompleteVerification;
            bool completedM17A2RegressionPack = isM17A2RegressionPack;
            bool completedM17B1RegressionPack = isM17B1RegressionPack;
            if (completedEditModeChain)
            {
                WriteCompleteSummary();
            }
            if (completedM17A2RegressionPack)
            {
                WriteM17A2RegressionPackSummary(true);
            }
            if (completedM17B1RegressionPack)
            {
                WriteM17B1RegressionPackSummary(true);
            }

            isRunning = false;
            isCompleteVerification = false;
            isM17A2RegressionPack = false;
            isM17B1RegressionPack = false;
            ClearM17A2RegressionPackState();
            ClearM17B1RegressionPackState();
            if (completedM17B1RegressionPack)
            {
                bool passed = CompletedSuites.Count == M17B1RegressionPackSuiteCount &&
                              CompletedSuites.All(outcome => outcome.Status == "PASS");
                Debug.Log("M17 B1 Full Regression Pack — " + (passed ? "PASS" : "FAIL") +
                          ". Results: " +
                          ProjectAbsolutePath(M17B1RegressionPackRelativePath));
            }
            else if (completedM17A2RegressionPack)
            {
                bool passed = CompletedSuites.Count == M17A2RegressionPackSuiteCount &&
                              CompletedSuites.All(outcome => outcome.Status == "PASS");
                Debug.Log("M17 A2 Full Regression Pack — " + (passed ? "PASS" : "FAIL") +
                          ". Results: " +
                          ProjectAbsolutePath(M17A2RegressionPackRelativePath));
            }
            else
            {
                Debug.Log("Neon Grid verification workflow complete. Results: " +
                          ProjectAbsolutePath(ResultsDirectory));
            }
            if (completedEditModeChain && CompletedSuites.Count > 0 &&
                CompletedSuites.All(outcome => outcome.Status == "PASS"))
            {
                EditorUtility.DisplayDialog("M16 B2 EditMode Verification Complete",
                    "The EditMode chain completed successfully. Now run " +
                    "Neon Grid > Verification > Audio PlayMode as a separate command.", "OK");
            }
        }

        private static bool IsEditorStableForTestStart()
        {
            return !testRunActive && !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating && !EditorApplication.isPlaying &&
                   !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void ScheduleNextSuite()
        {
            continuationNotBefore = EditorApplication.timeSinceStartup + ContinuationDelaySeconds;
            stableEditorFrames = 0;
            if (waitingForStableEditor)
            {
                return;
            }

            waitingForStableEditor = true;
            EditorApplication.update += WaitForStableEditor;
        }

        private static void WaitForStableEditor()
        {
            if (!isRunning)
            {
                StopWaitingForStableEditor();
                return;
            }

            if (EditorApplication.timeSinceStartup < continuationNotBefore ||
                !IsEditorStableForTestStart())
            {
                stableEditorFrames = 0;
                return;
            }

            stableEditorFrames++;
            if (stableEditorFrames < RequiredStableEditorFrames)
            {
                return;
            }

            StopWaitingForStableEditor();
            RunNextSuite();
        }

        private static void StopWaitingForStableEditor()
        {
            if (waitingForStableEditor)
            {
                EditorApplication.update -= WaitForStableEditor;
            }
            waitingForStableEditor = false;
            stableEditorFrames = 0;
        }

        private static void RegisterRunnerCallbacks()
        {
            if (testRunnerApi != null || callbacks != null)
            {
                return;
            }

            testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = ScriptableObject.CreateInstance<RunnerCallbacks>();
            callbacks.hideFlags = HideFlags.HideAndDontSave;
            testRunnerApi.RegisterCallbacks(callbacks);
        }

        private static void PersistPendingPlayModeRun()
        {
            SessionState.SetBool(PendingPlayModeKey, true);
            SessionState.SetString(PendingPlayModeSuiteKey, currentSuite.FileStem);
            SessionState.SetString(PlayModeStartTicksKey,
                currentStartUtc.Ticks.ToString());
        }

        private static void PersistM17A2RegressionPackState()
        {
            if (!isM17A2RegressionPack) return;

            var state = new SerializedRegressionPackState
            {
                startTicks = m17A2RegressionPackStartUtc.Ticks,
                pendingSuiteFileStems = PendingSuites.Select(suite => suite.FileStem).ToArray(),
                completedOutcomes = CompletedSuites.Select(SerializedSuiteOutcome.FromOutcome)
                    .ToArray()
            };
            SessionState.SetBool(M17A2RegressionPackActiveKey, true);
            SessionState.SetString(M17A2RegressionPackStateKey, JsonUtility.ToJson(state));
        }

        private static void RecoverM17A2RegressionPackState()
        {
            if (!SessionState.GetBool(M17A2RegressionPackActiveKey, false)) return;

            if (!SessionState.GetBool(PendingPlayModeKey, false))
            {
                ClearM17A2RegressionPackState();
                return;
            }

            string json = SessionState.GetString(M17A2RegressionPackStateKey, string.Empty);
            SerializedRegressionPackState state;
            try
            {
                state = JsonUtility.FromJson<SerializedRegressionPackState>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not recover the M17 A2 regression pack: " +
                               exception.Message);
                ClearM17A2RegressionPackState();
                return;
            }

            if (state == null)
            {
                Debug.LogError("Could not recover the M17 A2 regression pack state.");
                ClearM17A2RegressionPackState();
                return;
            }

            PendingSuites.Clear();
            CompletedSuites.Clear();
            foreach (string fileStem in state.pendingSuiteFileStems ?? Array.Empty<string>())
            {
                if (!TryResolveSuite(fileStem, out SuiteDefinition suite))
                {
                    Debug.LogError("Could not recover M17 A2 regression suite '" + fileStem +
                                   "'. The aggregate run was cancelled.");
                    PendingSuites.Clear();
                    CompletedSuites.Clear();
                    ClearM17A2RegressionPackState();
                    return;
                }
                PendingSuites.Enqueue(suite);
            }

            foreach (SerializedSuiteOutcome serialized in
                     state.completedOutcomes ?? Array.Empty<SerializedSuiteOutcome>())
            {
                CompletedSuites.Add(serialized.ToOutcome());
            }

            m17A2RegressionPackStartUtc = state.startTicks > 0
                ? new DateTime(state.startTicks, DateTimeKind.Utc)
                : DateTime.UtcNow;
            isM17A2RegressionPack = true;
            isCompleteVerification = false;
        }

        private static void ClearM17A2RegressionPackState()
        {
            SessionState.SetBool(M17A2RegressionPackActiveKey, false);
            SessionState.EraseString(M17A2RegressionPackStateKey);
        }

        private static void PersistM17B1RegressionPackState()
        {
            if (!isM17B1RegressionPack) return;

            var state = new SerializedRegressionPackState
            {
                startTicks = m17B1RegressionPackStartUtc.Ticks,
                pendingSuiteFileStems = PendingSuites.Select(suite => suite.FileStem).ToArray(),
                completedOutcomes = CompletedSuites.Select(SerializedSuiteOutcome.FromOutcome)
                    .ToArray()
            };
            SessionState.SetBool(M17B1RegressionPackActiveKey, true);
            SessionState.SetString(M17B1RegressionPackStateKey, JsonUtility.ToJson(state));
        }

        private static void RecoverM17B1RegressionPackState()
        {
            if (!SessionState.GetBool(M17B1RegressionPackActiveKey, false)) return;
            if (!SessionState.GetBool(PendingPlayModeKey, false))
            {
                ClearM17B1RegressionPackState();
                return;
            }

            string json = SessionState.GetString(M17B1RegressionPackStateKey, string.Empty);
            SerializedRegressionPackState state;
            try
            {
                state = JsonUtility.FromJson<SerializedRegressionPackState>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not recover the M17 B1 regression pack: " +
                               exception.Message);
                ClearM17B1RegressionPackState();
                return;
            }

            if (state == null)
            {
                Debug.LogError("Could not recover the M17 B1 regression pack state.");
                ClearM17B1RegressionPackState();
                return;
            }

            PendingSuites.Clear();
            CompletedSuites.Clear();
            foreach (string fileStem in state.pendingSuiteFileStems ?? Array.Empty<string>())
            {
                if (!TryResolveSuite(fileStem, out SuiteDefinition suite))
                {
                    Debug.LogError("Could not recover M17 B1 regression suite '" + fileStem +
                                   "'. The aggregate run was cancelled.");
                    PendingSuites.Clear();
                    CompletedSuites.Clear();
                    ClearM17B1RegressionPackState();
                    return;
                }
                PendingSuites.Enqueue(suite);
            }

            foreach (SerializedSuiteOutcome serialized in
                     state.completedOutcomes ?? Array.Empty<SerializedSuiteOutcome>())
                CompletedSuites.Add(serialized.ToOutcome());

            m17B1RegressionPackStartUtc = state.startTicks > 0
                ? new DateTime(state.startTicks, DateTimeKind.Utc)
                : DateTime.UtcNow;
            isM17B1RegressionPack = true;
            isCompleteVerification = false;
        }

        private static void ClearM17B1RegressionPackState()
        {
            SessionState.SetBool(M17B1RegressionPackActiveKey, false);
            SessionState.EraseString(M17B1RegressionPackStateKey);
        }

        private static void RecoverPendingPlayModeRun()
        {
            if (!SessionState.GetBool(PendingPlayModeKey, false) ||
                testRunnerApi != null || callbacks != null)
            {
                return;
            }

            string fileStem = SessionState.GetString(PendingPlayModeSuiteKey, string.Empty);
            if (!TryResolveSuite(fileStem, out currentSuite) || !currentSuite.IsPlayMode)
            {
                Debug.LogError("Could not recover PlayMode verification suite '" + fileStem +
                               "'. Pending runner state was cleared without starting a test.");
                ClearPendingPlayModeRun();
                ClearM17A2RegressionPackState();
                ClearM17B1RegressionPackState();
                isM17A2RegressionPack = false;
                isM17B1RegressionPack = false;
                return;
            }
            string serializedTicks = SessionState.GetString(PlayModeStartTicksKey, "");
            if (!long.TryParse(serializedTicks, out long ticks))
            {
                ticks = DateTime.UtcNow.Ticks;
            }

            currentStartUtc = new DateTime(ticks, DateTimeKind.Utc);
            isRunning = true;
            isCompleteVerification = false;
            testRunActive = true;
            EnsureResultsDirectory();
            RegisterRunnerCallbacks();
            Debug.Log("Neon Grid verification resumed result observation after " +
                      currentSuite.DisplayName +
                      " domain reload. No second test run was started.");
        }

        private static void ClearPendingPlayModeRun()
        {
            SessionState.SetBool(PendingPlayModeKey, false);
            SessionState.EraseString(PendingPlayModeSuiteKey);
            SessionState.EraseString(PlayModeStartTicksKey);
        }

        private static void ReleaseRunnerObjects()
        {
            if (testRunnerApi != null && callbacks != null)
            {
                testRunnerApi.UnregisterCallbacks(callbacks);
            }

            if (callbacks != null)
            {
                UnityEngine.Object.DestroyImmediate(callbacks);
            }

            if (testRunnerApi != null)
            {
                UnityEngine.Object.DestroyImmediate(testRunnerApi);
            }

            callbacks = null;
            testRunnerApi = null;
        }

        private static SuiteDefinition FocusedB2()
        {
            return SuiteDefinition.ForTests("M16 B2 Focused", "M16_B2_Focused",
                TestRunnerMode.EditMode, "NeonGrid.Tests.M16AmbientAudioFoundationTests");
        }

        private static SuiteDefinition FocusedC1()
        {
            return SuiteDefinition.ForTests("M16 C1 Focused", "M16_C1_Focused",
                TestRunnerMode.EditMode, "NeonGrid.Tests.M16HapticsFoundationTests");
        }

        private static SuiteDefinition FocusedD1()
        {
            return SuiteDefinition.ForTests("M16 D1 Focused", "M16_D1_Focused",
                TestRunnerMode.EditMode,
                "NeonGrid.Tests.M16ProductionGameplayFeedbackTests");
        }

        private static SuiteDefinition FocusedD2()
        {
            return SuiteDefinition.ForTests("M16 D2 Focused", "M16_D2_Focused",
                TestRunnerMode.EditMode,
                "NeonGrid.Tests.M16ProductionGameplayFeedbackTests");
        }

        private static SuiteDefinition FocusedM17A1()
        {
            return SuiteDefinition.ForTests("M17 A1 Focused", "M17_A1_Focused",
                TestRunnerMode.EditMode,
                "NeonGrid.Tests.UserSettingsPersistenceTests");
        }

        private static SuiteDefinition FocusedM17A2()
        {
            return SuiteDefinition.ForTests("M17 A2 Focused", "M17_A2_Focused",
                TestRunnerMode.EditMode,
                "NeonGrid.Tests.RuntimeUserSettingsTests");
        }

        private static SuiteDefinition FocusedM17B1()
        {
            return SuiteDefinition.ForTests("M17 B1 Focused", "M17_B1_Focused",
                TestRunnerMode.EditMode,
                "NeonGrid.Tests.ProductionSettingsUiTests");
        }

        private static SuiteDefinition HintPerformanceFocused()
        {
            return SuiteDefinition.ForTests("Hint Performance Focused",
                "Hint_Performance_Focused", TestRunnerMode.EditMode,
                "NeonGrid.Tests.HintPerformanceInstrumentationTests",
                "NeonGrid.Tests.PuzzleSolverTests");
        }

        private static SuiteDefinition B1Regression()
        {
            return SuiteDefinition.ForTests("M16 B1 Regression", "M16_B1_Regression",
                TestRunnerMode.EditMode, "NeonGrid.Tests.M16AudioFoundationTests");
        }

        private static SuiteDefinition A1A2Regression()
        {
            return SuiteDefinition.ForTests("M16 A1-A2 Regression", "M16_A1_A2_Regression",
                TestRunnerMode.EditMode,
                "NeonGrid.Tests.M16CircuitJuiceFoundationTests",
                "NeonGrid.Tests.M16PowerPropagationJuiceTests");
        }

        private static SuiteDefinition BroaderRegression()
        {
            return SuiteDefinition.ForTests("Broader Regression", "Broader_Regression",
                TestRunnerMode.EditMode, BroaderRegressionFixtures);
        }

        private static SuiteDefinition FullEditMode()
        {
            return SuiteDefinition.ForAssembly("Full EditMode", "Full_EditMode",
                TestRunnerMode.EditMode, "NeonGrid.Tests.EditMode");
        }

        private static SuiteDefinition AudioPlayMode()
        {
            return SuiteDefinition.ForTests("Audio PlayMode", "Audio_PlayMode",
                TestRunnerMode.PlayMode, "NeonGrid.Tests.M16AudioRuntimeTests");
        }

        private static SuiteDefinition HapticsPlayMode()
        {
            return SuiteDefinition.ForTests("Haptics PlayMode", "Haptics_PlayMode",
                TestRunnerMode.PlayMode, "NeonGrid.Tests.M16HapticsRuntimeTests");
        }

        private static SuiteDefinition ProductionFeedbackPlayMode()
        {
            return SuiteDefinition.ForTests("Production Feedback PlayMode",
                "Production_Feedback_PlayMode", TestRunnerMode.PlayMode,
                "NeonGrid.Tests.M16ProductionFeedbackRuntimeTests");
        }

        private static SuiteDefinition M16D2AmbiencePlayMode()
        {
            return SuiteDefinition.ForTests("M16 D2 Ambience PlayMode",
                "M16_D2_Ambience_PlayMode", TestRunnerMode.PlayMode,
                "NeonGrid.Tests.M16ProductionFeedbackRuntimeTests");
        }

        private static SuiteDefinition M17A2RuntimePlayMode()
        {
            return SuiteDefinition.ForTests("M17 A2 Runtime PlayMode",
                "M17_A2_Runtime_PlayMode", TestRunnerMode.PlayMode,
                "NeonGrid.Tests.M17RuntimeSettingsPlayModeTests");
        }

        private static SuiteDefinition M17B1SettingsPlayMode()
        {
            return SuiteDefinition.ForTests("M17 B1 Settings PlayMode",
                "M17_B1_Settings_PlayMode", TestRunnerMode.PlayMode,
                "NeonGrid.Tests.M17ProductionSettingsPlayModeTests");
        }

        private static bool TryResolveSuite(string fileStem, out SuiteDefinition suite)
        {
            switch (fileStem)
            {
                case "M16_B2_Focused": suite = FocusedB2(); return true;
                case "M16_C1_Focused": suite = FocusedC1(); return true;
                case "M16_D1_Focused": suite = FocusedD1(); return true;
                case "M16_D2_Focused": suite = FocusedD2(); return true;
                case "M17_A1_Focused": suite = FocusedM17A1(); return true;
                case "M17_A2_Focused": suite = FocusedM17A2(); return true;
                case "M17_B1_Focused": suite = FocusedM17B1(); return true;
                case "Hint_Performance_Focused": suite = HintPerformanceFocused(); return true;
                case "M16_B1_Regression": suite = B1Regression(); return true;
                case "M16_A1_A2_Regression": suite = A1A2Regression(); return true;
                case "Broader_Regression": suite = BroaderRegression(); return true;
                case "Full_EditMode": suite = FullEditMode(); return true;
                case "Audio_PlayMode": suite = AudioPlayMode(); return true;
                case "Haptics_PlayMode": suite = HapticsPlayMode(); return true;
                case "Production_Feedback_PlayMode":
                    suite = ProductionFeedbackPlayMode(); return true;
                case "M16_D2_Ambience_PlayMode":
                    suite = M16D2AmbiencePlayMode(); return true;
                case "M17_A2_Runtime_PlayMode":
                    suite = M17A2RuntimePlayMode(); return true;
                case "M17_B1_Settings_PlayMode":
                    suite = M17B1SettingsPlayMode(); return true;
                default:
                    suite = null;
                    return false;
            }
        }

        private static void EnsureResultsDirectory()
        {
            Directory.CreateDirectory(ProjectAbsolutePath(ResultsDirectory));
        }

        private static string ProjectAbsolutePath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string SummaryRelativePath(SuiteDefinition suite)
        {
            return ResultsDirectory + "/" + suite.FileStem + ".txt";
        }

        private static string XmlRelativePath(SuiteDefinition suite)
        {
            return ResultsDirectory + "/" + suite.FileStem + ".xml";
        }

        private static string RunStatusRelativePath(SuiteDefinition suite)
        {
            return ResultsDirectory + "/" + suite.FileStem + "_RunStatus.txt";
        }

        private static string RunnerErrorRelativePath(SuiteDefinition suite)
        {
            return ResultsDirectory + "/" + suite.FileStem + "_RunnerError.txt";
        }

        private static void WriteRunningSummary(
            SuiteDefinition suite, DateTime startUtc, int discoveredTests = 0)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Neon Grid Verification");
            builder.AppendLine("Suite: " + suite.DisplayName);
            builder.AppendLine("Status: RUNNING");
            builder.AppendLine("Start UTC: " + startUtc.ToString("O"));
            builder.AppendLine("Discovered tests: " + discoveredTests);
            builder.AppendLine("End result: pending");
            File.WriteAllText(ProjectAbsolutePath(RunStatusRelativePath(suite)),
                builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteFinishedSummary(
            SuiteDefinition suite, SuiteOutcome outcome, string xmlRelativePath)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Neon Grid Verification");
            builder.AppendLine("Suite: " + suite.DisplayName);
            builder.AppendLine("Status: " + outcome.Status);
            builder.AppendLine("Start UTC: " + outcome.StartUtc.ToString("O"));
            builder.AppendLine("End UTC: " + outcome.EndUtc.ToString("O"));
            builder.AppendLine("Total: " + outcome.Total);
            builder.AppendLine("Passed: " + outcome.Passed);
            builder.AppendLine("Failed: " + outcome.Failed);
            builder.AppendLine("Skipped: " + outcome.Skipped);
            builder.AppendLine("Inconclusive: " + outcome.Inconclusive);
            builder.AppendLine("NUnit XML: " + xmlRelativePath);
            builder.AppendLine("Failed tests:");
            if (outcome.FailedTests.Count == 0)
            {
                builder.AppendLine("- none");
            }
            else
            {
                foreach (string failedTest in outcome.FailedTests)
                {
                    builder.AppendLine("- " + failedTest);
                }
            }

            string contents = builder.ToString();
            File.WriteAllText(ProjectAbsolutePath(SummaryRelativePath(suite)), contents,
                new UTF8Encoding(false));
            File.WriteAllText(ProjectAbsolutePath(RunStatusRelativePath(suite)), contents,
                new UTF8Encoding(false));
        }

        private static void WriteRunnerErrorSummary(
            SuiteDefinition suite, SuiteOutcome outcome)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Neon Grid Verification");
            builder.AppendLine("Suite: " + suite.DisplayName);
            builder.AppendLine("Status: ABORTED / RUNNER ERROR");
            builder.AppendLine("Start UTC: " + outcome.StartUtc.ToString("O"));
            builder.AppendLine("End UTC: " + outcome.EndUtc.ToString("O"));
            builder.AppendLine("Completed PASS/FAIL result preserved: " +
                               SummaryRelativePath(suite));
            builder.AppendLine("Runner errors:");
            foreach (string error in outcome.FailedTests)
            {
                builder.AppendLine("- " + error);
            }

            string contents = builder.ToString();
            File.WriteAllText(ProjectAbsolutePath(RunnerErrorRelativePath(suite)), contents,
                new UTF8Encoding(false));
            File.WriteAllText(ProjectAbsolutePath(RunStatusRelativePath(suite)), contents,
                new UTF8Encoding(false));
        }

        private static void WriteCompleteRunningSummary()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Neon Grid M16-B2 Complete EditMode Verification");
            builder.AppendLine("Status: RUNNING");
            builder.AppendLine("Suites planned: 5 EditMode suites");
            builder.AppendLine("Audio PlayMode: requires separate manual menu command");
            File.WriteAllText(ProjectAbsolutePath(
                    ResultsDirectory + "/M16_B2_Complete_Verification_RunStatus.txt"),
                builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteCompleteSummary()
        {
            var builder = new StringBuilder();
            bool aborted = CompletedSuites.Any(outcome => outcome.IsAborted);
            bool passed = !aborted && CompletedSuites.Count == 5 &&
                          CompletedSuites.All(outcome => outcome.Status == "PASS");
            builder.AppendLine("Neon Grid M16-B2 Complete EditMode Verification");
            builder.AppendLine("Status: " + (aborted ? "ABORTED / RUNNER ERROR" :
                passed ? "PASS" : "FAIL"));
            builder.AppendLine("Suites completed: " + CompletedSuites.Count);
            foreach (SuiteOutcome outcome in CompletedSuites)
            {
                builder.AppendLine(outcome.DisplayName + ": " + outcome.Status +
                                   " — total " + outcome.Total + ", passed " + outcome.Passed +
                                   ", failed " + outcome.Failed + ", skipped " + outcome.Skipped +
                                   ", inconclusive " + outcome.Inconclusive);
            }
            builder.AppendLine("Audio PlayMode: NOT RUN by this EditMode chain");
            builder.AppendLine("Next command: Neon Grid > Verification > Audio PlayMode");

            string contents = builder.ToString();
            File.WriteAllText(ProjectAbsolutePath(
                    ResultsDirectory + "/M16_B2_Complete_Verification.txt"),
                contents, new UTF8Encoding(false));
            File.WriteAllText(ProjectAbsolutePath(
                    ResultsDirectory + "/M16_B2_Complete_Verification_RunStatus.txt"),
                contents, new UTF8Encoding(false));
        }

        private static void WriteM17A2RegressionPackSummary(bool final)
        {
            bool aborted = CompletedSuites.Any(outcome => outcome.IsAborted);
            bool passed = final && !aborted &&
                          CompletedSuites.Count == M17A2RegressionPackSuiteCount &&
                          CompletedSuites.All(outcome => outcome.Status == "PASS");
            string status = !final
                ? "RUNNING"
                : aborted
                    ? "ABORTED / RUNNER ERROR"
                    : passed ? "PASS" : "FAIL";

            var builder = new StringBuilder();
            builder.AppendLine("Neon Grid M17 A2 Full Regression Pack");
            builder.AppendLine("Status: " + status);
            builder.AppendLine("Start UTC: " + m17A2RegressionPackStartUtc.ToString("O"));
            if (final)
            {
                DateTime endUtc = CompletedSuites.Count > 0
                    ? CompletedSuites[CompletedSuites.Count - 1].EndUtc
                    : DateTime.UtcNow;
                builder.AppendLine("End UTC: " + endUtc.ToString("O"));
            }
            builder.AppendLine("Total suites: " + M17A2RegressionPackSuiteCount);
            builder.AppendLine("Suites completed: " + CompletedSuites.Count);
            builder.AppendLine("Passed suites: " +
                               CompletedSuites.Count(outcome => outcome.Status == "PASS"));
            builder.AppendLine("Failed suites: " +
                               CompletedSuites.Count(outcome => outcome.Status != "PASS"));
            builder.AppendLine("Suite results:");
            if (CompletedSuites.Count == 0)
            {
                builder.AppendLine("- none completed");
            }
            else
            {
                foreach (SuiteOutcome outcome in CompletedSuites)
                {
                    builder.AppendLine("- " + outcome.DisplayName + ": " + outcome.Status +
                                       " — " + outcome.Passed + "/" + outcome.Total +
                                       " passed, failed " + outcome.Failed +
                                       ", skipped " + outcome.Skipped +
                                       ", inconclusive " + outcome.Inconclusive);
                    foreach (string failedTest in outcome.FailedTests)
                    {
                        builder.AppendLine("  - " + failedTest);
                    }
                }
            }

            if (!final)
            {
                builder.AppendLine("Suites remaining: " + PendingSuites.Count);
                foreach (SuiteDefinition pendingSuite in PendingSuites)
                {
                    builder.AppendLine("- " + pendingSuite.DisplayName);
                }
            }

            File.WriteAllText(ProjectAbsolutePath(M17A2RegressionPackRelativePath),
                builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteM17B1RegressionPackSummary(bool final)
        {
            bool aborted = CompletedSuites.Any(outcome => outcome.IsAborted);
            bool passed = final && !aborted &&
                          CompletedSuites.Count == M17B1RegressionPackSuiteCount &&
                          CompletedSuites.All(outcome => outcome.Status == "PASS");
            string status = !final
                ? "RUNNING"
                : aborted
                    ? "ABORTED / RUNNER ERROR"
                    : passed ? "PASS" : "FAIL";

            var builder = new StringBuilder();
            builder.AppendLine("Neon Grid M17 B1 Full Regression Pack");
            builder.AppendLine("Status: " + status);
            builder.AppendLine("Start UTC: " + m17B1RegressionPackStartUtc.ToString("O"));
            if (final)
            {
                DateTime endUtc = CompletedSuites.Count > 0
                    ? CompletedSuites[CompletedSuites.Count - 1].EndUtc
                    : DateTime.UtcNow;
                builder.AppendLine("End UTC: " + endUtc.ToString("O"));
            }
            builder.AppendLine("Total suites: " + M17B1RegressionPackSuiteCount);
            builder.AppendLine("Suites completed: " + CompletedSuites.Count);
            builder.AppendLine("Passed suites: " +
                               CompletedSuites.Count(outcome => outcome.Status == "PASS"));
            builder.AppendLine("Failed suites: " +
                               CompletedSuites.Count(outcome => outcome.Status != "PASS"));
            builder.AppendLine("Suite results:");
            if (CompletedSuites.Count == 0)
            {
                builder.AppendLine("- none completed");
            }
            else
            {
                foreach (SuiteOutcome outcome in CompletedSuites)
                {
                    builder.AppendLine("- " + outcome.DisplayName + ": " + outcome.Status +
                                       " — " + outcome.Passed + "/" + outcome.Total +
                                       " passed, failed " + outcome.Failed +
                                       ", skipped " + outcome.Skipped +
                                       ", inconclusive " + outcome.Inconclusive);
                    foreach (string failedTest in outcome.FailedTests)
                        builder.AppendLine("  - " + failedTest);
                }
            }

            if (!final)
            {
                builder.AppendLine("Suites remaining: " + PendingSuites.Count);
                foreach (SuiteDefinition pendingSuite in PendingSuites)
                    builder.AppendLine("- " + pendingSuite.DisplayName);
            }

            File.WriteAllText(ProjectAbsolutePath(M17B1RegressionPackRelativePath),
                builder.ToString(), new UTF8Encoding(false));
        }

        private static List<string> CollectFailedLeafTests(ITestResultAdaptor root)
        {
            var failures = new List<string>();
            CollectFailedLeafTests(root, failures);
            failures.Sort(StringComparer.Ordinal);
            return failures;
        }

        private static void CollectFailedLeafTests(
            ITestResultAdaptor result, ICollection<string> failures)
        {
            if (result.HasChildren)
            {
                foreach (ITestResultAdaptor child in result.Children)
                {
                    CollectFailedLeafTests(child, failures);
                }
                return;
            }

            if (result.ResultState.StartsWith("Failed", StringComparison.Ordinal))
            {
                string detail = string.IsNullOrWhiteSpace(result.Message)
                    ? result.FullName
                    : result.FullName + " — " + result.Message.Replace('\r', ' ').Replace('\n', ' ');
                failures.Add(detail);
            }
        }

        private sealed class RunnerCallbacks : ScriptableObject, IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                OnRunStarted(testsToRun);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                OnRunFinished(result);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            public void OnError(string message)
            {
                FinishWithRunnerError(message);
            }
        }

        [Serializable]
        private sealed class SerializedRegressionPackState
        {
            public long startTicks;
            public string[] pendingSuiteFileStems = Array.Empty<string>();
            public SerializedSuiteOutcome[] completedOutcomes =
                Array.Empty<SerializedSuiteOutcome>();
        }

        [Serializable]
        private sealed class SerializedSuiteOutcome
        {
            public string displayName;
            public int passed;
            public int failed;
            public int skipped;
            public int inconclusive;
            public long startTicks;
            public long endTicks;
            public string[] failedTests = Array.Empty<string>();
            public bool isAborted;

            public static SerializedSuiteOutcome FromOutcome(SuiteOutcome outcome)
            {
                return new SerializedSuiteOutcome
                {
                    displayName = outcome.DisplayName,
                    passed = outcome.Passed,
                    failed = outcome.Failed,
                    skipped = outcome.Skipped,
                    inconclusive = outcome.Inconclusive,
                    startTicks = outcome.StartUtc.Ticks,
                    endTicks = outcome.EndUtc.Ticks,
                    failedTests = outcome.FailedTests.ToArray(),
                    isAborted = outcome.IsAborted
                };
            }

            public SuiteOutcome ToOutcome()
            {
                return new SuiteOutcome(displayName, passed, failed, skipped, inconclusive,
                    new DateTime(startTicks, DateTimeKind.Utc),
                    new DateTime(endTicks, DateTimeKind.Utc),
                    new List<string>(failedTests ?? Array.Empty<string>()), isAborted);
            }
        }

        private sealed class SuiteDefinition
        {
            private readonly TestRunnerMode testMode;
            private readonly string[] testNames;
            private readonly string assemblyName;

            private SuiteDefinition(
                string displayName, string fileStem, TestRunnerMode testMode,
                string[] testNames, string assemblyName)
            {
                DisplayName = displayName;
                FileStem = fileStem;
                this.testMode = testMode;
                this.testNames = testNames;
                this.assemblyName = assemblyName;
            }

            public string DisplayName { get; }
            public string FileStem { get; }
            public bool IsPlayMode => testMode == TestRunnerMode.PlayMode;

            public static SuiteDefinition ForTests(
                string displayName, string fileStem, TestRunnerMode mode, params string[] names)
            {
                return new SuiteDefinition(displayName, fileStem, mode, names, null);
            }

            public static SuiteDefinition ForAssembly(
                string displayName, string fileStem, TestRunnerMode mode, string assembly)
            {
                return new SuiteDefinition(displayName, fileStem, mode, null, assembly);
            }

            public Filter CreateFilter()
            {
                var filter = new Filter { testMode = testMode };
                if (testNames != null)
                {
                    filter.testNames = testNames;
                }
                if (!string.IsNullOrEmpty(assemblyName))
                {
                    filter.assemblyNames = new[] { assemblyName };
                }
                return filter;
            }
        }

        private sealed class SuiteOutcome
        {
            public SuiteOutcome(
                string displayName, int passed, int failed, int skipped, int inconclusive,
                DateTime startUtc, DateTime endUtc, List<string> failedTests,
                bool isAborted = false)
            {
                DisplayName = displayName;
                Passed = passed;
                Failed = failed;
                Skipped = skipped;
                Inconclusive = inconclusive;
                StartUtc = startUtc;
                EndUtc = endUtc;
                FailedTests = failedTests;
                IsAborted = isAborted;
            }

            public string DisplayName { get; }
            public int Passed { get; }
            public int Failed { get; }
            public int Skipped { get; }
            public int Inconclusive { get; }
            public DateTime StartUtc { get; }
            public DateTime EndUtc { get; }
            public List<string> FailedTests { get; }
            public bool IsAborted { get; }
            public int Total => Passed + Failed + Skipped + Inconclusive;
            public string Status => IsAborted
                ? "ABORTED"
                : Total > 0 && Failed == 0 && Skipped == 0 && Inconclusive == 0
                ? "PASS"
                : "FAIL";
        }
    }
}

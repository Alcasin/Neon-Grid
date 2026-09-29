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
    /// Manually runs the accepted M16 verification matrix inside a normally opened,
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
        private static bool testRunActive;
        private static bool waitingForStableEditor;
        private static double continuationNotBefore;
        private static int stableEditorFrames;

        static NeonGridVerificationRunner()
        {
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
        [MenuItem(MenuRoot + "M16 B1 - Regression", true)]
        [MenuItem(MenuRoot + "M16 A1-A2 - Regression", true)]
        [MenuItem(MenuRoot + "Broader Regression", true)]
        [MenuItem(MenuRoot + "Full EditMode", true)]
        [MenuItem(MenuRoot + "Audio PlayMode", true)]
        [MenuItem(MenuRoot + "Haptics PlayMode", true)]
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

        private static void StartWorkflow(IEnumerable<SuiteDefinition> suites, bool complete)
        {
            if (isRunning)
            {
                EditorUtility.DisplayDialog("Neon Grid Verification",
                    "A Neon Grid verification run is already active.", "OK");
                return;
            }

            PendingSuites.Clear();
            CompletedSuites.Clear();
            foreach (SuiteDefinition suite in suites)
            {
                PendingSuites.Enqueue(suite);
            }

            isCompleteVerification = complete;
            isRunning = true;
            EnsureResultsDirectory();
            if (complete)
            {
                WriteCompleteRunningSummary();
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
            if (completedEditModeChain)
            {
                WriteCompleteSummary();
            }

            isRunning = false;
            isCompleteVerification = false;
            Debug.Log("Neon Grid verification workflow complete. Results: " +
                      ProjectAbsolutePath(ResultsDirectory));
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

        private static void RecoverPendingPlayModeRun()
        {
            if (!SessionState.GetBool(PendingPlayModeKey, false) ||
                testRunnerApi != null || callbacks != null)
            {
                return;
            }

            string fileStem = SessionState.GetString(PendingPlayModeSuiteKey,
                "Audio_PlayMode");
            currentSuite = fileStem == "Haptics_PlayMode" ? HapticsPlayMode() :
                AudioPlayMode();
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

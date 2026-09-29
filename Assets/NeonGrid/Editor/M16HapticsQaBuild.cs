using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class M16HapticsQaBuild
    {
        public const string MenuPath = "Neon Grid/M16/Build Haptics QA APK";
        public const string PrototypeScenePath =
            "Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity";
        public const string OutputPath =
            "Builds/QA/M16C1/NeonGrid_M16C1_Haptics_QA.apk";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayDialog("M16-C1 Haptics QA Build",
                    "Select Android as the active build platform before building the " +
                    "isolated haptics QA APK. The QA command will not switch platforms.",
                    "OK");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Could not resolve the Unity project root.");

            string scenePath = Path.Combine(projectRoot,
                PrototypeScenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(scenePath))
                throw new FileNotFoundException("The isolated M15 gameplay prototype scene is missing.",
                    scenePath);

            string absoluteOutput = Path.Combine(projectRoot,
                OutputPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput) ?? projectRoot);

            BuildReport report = BuildPipeline.BuildPlayer(CreateBuildPlayerOptions());
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"M16-C1 haptics QA APK build ended with {report.summary.result}.");

            Debug.Log($"Built isolated M16-C1 haptics QA APK: {OutputPath}");
        }

        public static BuildPlayerOptions CreateBuildPlayerOptions()
        {
            return new BuildPlayerOptions
            {
                scenes = new[] { PrototypeScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateBuild()
        {
            return !BuildPipeline.isBuildingPlayer;
        }
    }
}

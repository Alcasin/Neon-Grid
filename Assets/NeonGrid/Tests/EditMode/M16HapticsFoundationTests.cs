using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Editor;
using NeonGrid.Presentation;
using NeonGrid.Session;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeonGrid.Tests
{
    public sealed class M16HapticsFoundationTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private NeonGridHapticsDefinition definition;

        [SetUp]
        public void SetUp()
        {
            NeonGridHapticsSettings.HapticsEnabled = true;
            definition = AssetDatabase.LoadAssetAtPath<NeonGridHapticsDefinition>(
                M16HapticsPrototypeBuilder.DefinitionPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.IsConfigured, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
            NeonGridHapticsSettings.HapticsEnabled = true;
        }

        [TestCase(NeonGridHapticEvent.TileRotate, 12, .18f, .045f)]
        [TestCase(NeonGridHapticEvent.SwitchToggle, 18, .28f, .08f)]
        [TestCase(NeonGridHapticEvent.ObjectiveActivate, 28, .42f, .15f)]
        [TestCase(NeonGridHapticEvent.Hint, 32, .38f, .25f)]
        [TestCase(NeonGridHapticEvent.LockedReject, 36, .62f, .12f)]
        [TestCase(NeonGridHapticEvent.Completion, 55, .68f, 1f)]
        public void SemanticDefinitionsAreRestrainedAndComplete(
            NeonGridHapticEvent eventType, int duration, float intensity, float cooldown)
        {
            Assert.That(definition.TryGetCue(eventType, out NeonGridHapticCue cue), Is.True);
            Assert.That(cue.DurationMilliseconds, Is.EqualTo(duration));
            Assert.That(cue.Intensity, Is.EqualTo(intensity).Within(.0001f));
            Assert.That(cue.CooldownSeconds, Is.EqualTo(cooldown).Within(.0001f));
        }

        [Test]
        public void PerEventCooldownBoundsRapidRequests()
        {
            var backend = new FakeBackend();
            NeonGridHapticsService service = BuildService(backend);
            Assert.That(service.TryPlay(NeonGridHapticEvent.TileRotate, 1f), Is.True);
            Assert.That(service.TryPlay(NeonGridHapticEvent.TileRotate, 1.02f), Is.False);
            Assert.That(service.TryPlay(NeonGridHapticEvent.TileRotate, 1.05f), Is.True);
            Assert.That(backend.Pulses, Has.Count.EqualTo(2));
        }

        [Test]
        public void EditorAndUnsupportedBackendsAreSafeNoOps()
        {
            NeonGridHapticsService editorService = BuildService(null);
            Assert.That(editorService.BackendSupported, Is.False);
            Assert.DoesNotThrow(() =>
                editorService.TryPlay(NeonGridHapticEvent.TileRotate, 1f));
            Assert.That(editorService.SuccessfulPulseCount, Is.Zero);

            var unsupported = new FakeBackend(false);
            NeonGridHapticsService unsupportedService = BuildService(unsupported);
            Assert.That(unsupportedService.TryPlay(
                NeonGridHapticEvent.Completion, 1f), Is.False);
            Assert.That(unsupported.Pulses, Is.Empty);
        }

        [Test]
        public void BackendFailureNeverEscapesIntoGameplay()
        {
            NeonGridHapticsService service = BuildService(new ThrowingBackend());
            Assert.DoesNotThrow(() =>
                service.TryPlay(NeonGridHapticEvent.LockedReject, 1f));
            Assert.That(service.SuccessfulPulseCount, Is.Zero);
        }

        [Test]
        public void DisableSuppressesAndReenableRestoresRequests()
        {
            var backend = new FakeBackend();
            NeonGridHapticsService service = BuildService(backend);
            service.SetHapticsEnabled(false);
            Assert.That(service.TryPlay(NeonGridHapticEvent.Hint, 1f), Is.False);
            Assert.That(backend.Pulses, Is.Empty);
            service.SetHapticsEnabled(true);
            Assert.That(service.TryPlay(NeonGridHapticEvent.Hint, 1f), Is.True);
            Assert.That(backend.Pulses, Has.Count.EqualTo(1));
        }

        [Test]
        public void CompletionIsOncePerLifecycleAndResettable()
        {
            var backend = new FakeBackend();
            NeonGridHapticsService service = BuildService(backend);
            Assert.That(service.TryPlay(NeonGridHapticEvent.Completion, 1f), Is.True);
            Assert.That(service.TryPlay(NeonGridHapticEvent.Completion, 20f), Is.False);
            service.ResetCompletionLifecycle();
            Assert.That(service.TryPlay(NeonGridHapticEvent.Completion, 20f), Is.True);
            Assert.That(backend.Pulses, Has.Count.EqualTo(2));
        }

        [Test]
        public void ExistingPresentationEventsDriveOnlyApprovedSemantics()
        {
            var backend = new FakeBackend();
            GameObject root = NewObject("Haptics Semantic Integration");
            CircuitJuiceCoordinator coordinator = root.AddComponent<CircuitJuiceCoordinator>();
            NeonGridHapticsService service = root.AddComponent<NeonGridHapticsService>();
            service.Initialize(definition, coordinator, backend);
            MethodInfo publish = typeof(CircuitJuiceCoordinator).GetMethod(
                "PublishPresentationEvent", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(publish, Is.Not.Null);
            CircuitJuiceEventType[] approved =
            {
                CircuitJuiceEventType.RotationAccepted,
                CircuitJuiceEventType.InteractionRejected,
                CircuitJuiceEventType.SwitchChanged,
                CircuitJuiceEventType.ObjectiveActivated,
                CircuitJuiceEventType.HintTargeted,
                CircuitJuiceEventType.CompletionTriggered
            };
            foreach (CircuitJuiceEventType eventType in approved)
                publish.Invoke(coordinator, new object[] { eventType });
            Assert.That(backend.Pulses, Has.Count.EqualTo(6));

            publish.Invoke(coordinator, new object[] { CircuitJuiceEventType.PowerActivated });
            publish.Invoke(coordinator, new object[] { CircuitJuiceEventType.PowerDeactivated });
            publish.Invoke(coordinator, new object[] { CircuitJuiceEventType.GateActivated });
            Assert.That(backend.Pulses, Has.Count.EqualTo(6));
        }

        [Test]
        public void RepeatedUseCreatesNoObjectsOrComponents()
        {
            var backend = new FakeBackend();
            NeonGridHapticsService service = BuildService(backend);
            int transforms = service.GetComponentsInChildren<Transform>(true).Length;
            int components = service.GetComponentsInChildren<Component>(true).Length;
            for (int index = 0; index < 100; index++)
                service.TryPlay(NeonGridHapticEvent.TileRotate, index * .1f);
            Assert.That(service.GetComponentsInChildren<Transform>(true),
                Has.Length.EqualTo(transforms));
            Assert.That(service.GetComponentsInChildren<Component>(true),
                Has.Length.EqualTo(components));
        }

        [Test]
        public void ServiceHasNoPerFramePollingOrThirdPartyDependency()
        {
            string serviceSource = File.ReadAllText(ProjectPath(
                "Assets/NeonGrid/Scripts/Presentation/NeonGridHapticsService.cs"));
            string packages = File.ReadAllText(ProjectPath("Packages/manifest.json"));
            Assert.That(serviceSource, Does.Not.Contain("void Update("));
            Assert.That(serviceSource, Does.Not.Contain("Handheld.Vibrate"));
            Assert.That(packages, Does.Not.Contain("haptic").IgnoreCase);
        }

        [Test]
        public void ProductionRemainsUnboundAndPrototypeIsOptIn()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            Assert.That(campaign, Is.Not.Null);
            Assert.That(typeof(CampaignDefinition).GetFields(BindingFlags.Instance |
                BindingFlags.NonPublic).Any(field =>
                field.FieldType == typeof(NeonGridHapticsDefinition)), Is.False);

            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            Assert.That(prototype.HapticsDefinition, Is.SameAs(definition));
            Assert.That(prototype.HapticsDefinition.IsConfigured, Is.True);
            GameObject root = NewObject("Production Haptics Isolation");
            BoardController production = root.AddComponent<BoardController>();
            production.Initialize(Level());
            Assert.That(production.HapticsService, Is.Null);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo("Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity"));
        }

        [Test]
        public void AndroidLibraryManifestAddsOnlyVibratePermission()
        {
            const string relative =
                "Assets/Plugins/Android/NeonGridHaptics.androidlib/src/main/AndroidManifest.xml";
            var document = new XmlDocument();
            document.Load(ProjectPath(relative));
            XmlElement manifest = document.DocumentElement;
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.Name, Is.EqualTo("manifest"));
            XmlNodeList permissions = manifest.SelectNodes("uses-permission");
            Assert.That(permissions, Is.Not.Null);
            Assert.That(permissions.Count, Is.EqualTo(1),
                "The merge fragment must not add unrelated Android permissions.");
            const string androidNamespace = "http://schemas.android.com/apk/res/android";
            Assert.That(((XmlElement)permissions[0]).GetAttribute("name", androidNamespace),
                Is.EqualTo("android.permission.VIBRATE"));
            Assert.That(manifest.SelectSingleNode("application"), Is.Null,
                "The library fragment must merge a permission, not replace Unity's application manifest.");
        }

        [Test]
        public void HapticsQaBuildTargetsOnlyPrototypeWithoutMutatingBuildSettings()
        {
            string[] before = EditorBuildSettings.scenes
                .Select(scene => $"{scene.enabled}:{scene.path}").ToArray();

            BuildPlayerOptions options = M16HapticsQaBuild.CreateBuildPlayerOptions();

            Assert.That(options.target, Is.EqualTo(BuildTarget.Android));
            Assert.That(options.options, Is.EqualTo(BuildOptions.Development));
            Assert.That(options.scenes, Is.EqualTo(new[]
            {
                "Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity"
            }));
            Assert.That(options.locationPathName, Is.EqualTo(
                "Builds/QA/M16C1/NeonGrid_M16C1_Haptics_QA.apk"));
            Assert.That(EditorBuildSettings.scenes
                .Select(scene => $"{scene.enabled}:{scene.path}").ToArray(), Is.EqualTo(before));
        }

        [Test]
        public void HapticsQaBuilderIsEditorOnlyAndHasNoRuntimeDependency()
        {
            Assert.That(typeof(M16HapticsQaBuild).Assembly.GetName().Name,
                Is.EqualTo("NeonGrid.Editor"));
            string editorSource = ProjectPath("Assets/NeonGrid/Editor/M16HapticsQaBuild.cs");
            Assert.That(File.Exists(editorSource), Is.True);
            string[] runtimeReferences = Directory.GetFiles(
                    ProjectPath("Assets/NeonGrid/Scripts"), "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(nameof(M16HapticsQaBuild)))
                .ToArray();
            Assert.That(runtimeReferences, Is.Empty);
        }

        [Test]
        public void QaLevelsExposeAllSixHapticSemanticsThroughNaturalGameplay()
        {
            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            Assert.That(prototype, Is.Not.Null);
            Assert.That(prototype.SimpleLevel.name, Is.EqualTo("PS_01"));
            Assert.That(prototype.DenseLevel.name, Is.EqualTo("CG_10"));

            TileDefinition[] simple = prototype.SimpleLevel.Tiles.ToArray();
            TileDefinition[] dense = prototype.DenseLevel.Tiles.ToArray();
            Assert.That(simple.Any(tile => tile.isRotatable), Is.True,
                "PS01 must expose TileRotate.");
            Assert.That(simple.Any(tile => tile.tileType != TileType.Empty && !tile.isRotatable),
                Is.True, "PS01 must expose LockedReject.");
            Assert.That(dense.Any(tile => tile.tileType == TileType.Switch), Is.True,
                "CG10 must expose SwitchToggle.");
            Assert.That(simple.Any(tile => tile.tileType == TileType.OutputLamp), Is.True,
                "PS01 must expose ObjectiveActivate and Completion.");

            string controller = File.ReadAllText(ProjectPath(
                "Assets/NeonGrid/Scripts/Presentation/GameplayVisualPrototypeController.cs"));
            Assert.That(controller, Does.Contain("HAPTICS: ON"));
            Assert.That(controller, Does.Contain(nameof(
                GameplayVisualPrototypeController.PrepareHintForQa)));
        }

        [Test]
        public void HapticRequestsDoNotMutateGameplaySession()
        {
            LevelDefinition level = Level();
            var session = new GameplaySession(level, 8);
            int moves = session.MoveCount;
            int rotation = session.Board.GetTile(new GridPosition(1, 0)).Rotation;
            bool completed = session.IsCompleted;
            NeonGridHapticsService service = BuildService(new FakeBackend());
            foreach (NeonGridHapticEvent eventType in
                     Enum.GetValues(typeof(NeonGridHapticEvent)))
                service.TryPlay(eventType, 10f + (int)eventType);
            Assert.That(session.MoveCount, Is.EqualTo(moves));
            Assert.That(session.Board.GetTile(new GridPosition(1, 0)).Rotation,
                Is.EqualTo(rotation));
            Assert.That(session.IsCompleted, Is.EqualTo(completed));
            session.Dispose();
        }

        private NeonGridHapticsService BuildService(INeonGridHapticsBackend backend)
        {
            GameObject root = NewObject("M16 C1 Haptics Service");
            NeonGridHapticsService service = root.AddComponent<NeonGridHapticsService>();
            service.Initialize(definition, null, backend);
            return service;
        }

        private LevelDefinition Level()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.SetData(3, 1, new[]
            {
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 0, true),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false)
            });
            cleanup.Add(level);
            return level;
        }

        private GameObject NewObject(string name)
        {
            var result = new GameObject(name);
            cleanup.Add(result);
            return result;
        }

        private static string ProjectPath(string relative)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private sealed class FakeBackend : INeonGridHapticsBackend
        {
            public readonly List<(int Duration, float Intensity)> Pulses =
                new List<(int Duration, float Intensity)>();

            public FakeBackend(bool supported = true)
            {
                IsSupported = supported;
            }

            public bool IsSupported { get; }

            public bool TryPulse(int durationMilliseconds, float intensity)
            {
                if (!IsSupported) return false;
                Pulses.Add((durationMilliseconds, intensity));
                return true;
            }
        }

        private sealed class ThrowingBackend : INeonGridHapticsBackend
        {
            public bool IsSupported => true;

            public bool TryPulse(int durationMilliseconds, float intensity)
            {
                throw new InvalidOperationException("Synthetic unsupported-device failure.");
            }
        }
    }
}

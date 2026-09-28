using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
    public sealed class M16AudioFoundationTests
    {
        private const string ManifestPath = "Tools/AudioGeneration/audio_manifest.json";
        private readonly List<Object> cleanup = new List<Object>();
        private NeonGridAudioDefinition audio;
        private CircuitVisualThemeDefinition theme;
        private CircuitJuiceDefinition juice;

        [SetUp]
        public void SetUp()
        {
            audio = AssetDatabase.LoadAssetAtPath<NeonGridAudioDefinition>(
                M16AudioPrototypeBuilder.DefinitionPath);
            theme = CircuitVisualThemeCatalog.LoadTechnicalNeonProductionPrototype();
            juice = AssetDatabase.LoadAssetAtPath<CircuitJuiceDefinition>(
                M16CircuitJuicePrototypeBuilder.JuicePath);
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.IsConfigured, Is.True);
            Assert.That(theme, Is.Not.Null);
            Assert.That(juice, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void ProductionRemainsUnboundAndPrototypeIsOptIn()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            Assert.That(campaign, Is.Not.Null);
            Assert.That(prototype.AudioDefinition, Is.SameAs(audio));
            Assert.That(typeof(CampaignDefinition).GetFields(BindingFlags.Instance |
                BindingFlags.NonPublic).Any(field =>
                field.FieldType == typeof(NeonGridAudioDefinition)), Is.False);

            BoardController production = BuildController(RotationLevel(), null);
            Assert.That(production.AudioService, Is.Null);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo("Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity"));
        }

        [Test]
        public void AudioDisabledLeavesGameplayAndHierarchyUnchanged()
        {
            LevelDefinition level = RotationLevel();
            BoardController disabled = BuildController(level, null);
            var reference = new GameplaySession(level, 8);
            PuzzleAction action = new PuzzleAction(new GridPosition(0, 0),
                PuzzleActionType.RotateClockwise);
            Assert.That(disabled.PerformPlayerAction(action.Position), Is.True);
            Assert.That(reference.PerformAction(action), Is.True);
            AssertBoardsEqual(reference.Board, disabled.Session.Board);
            Assert.That(disabled.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            reference.Dispose();
        }

        [Test]
        public void PlaybackDoesNotAlterSessionState()
        {
            BoardController controller = BuildController(RotationLevel(), audio);
            var before = Snapshot(controller.Session.Board);
            int moves = controller.Session.MoveCount;
            float elapsed = controller.Session.ElapsedSeconds;
            Assert.That(controller.AudioService.TryPlay(
                NeonGridAudioEvent.UIButtonPressed, 10f), Is.True);
            CollectionAssert.AreEqual(before, Snapshot(controller.Session.Board));
            Assert.That(controller.Session.MoveCount, Is.EqualTo(moves));
            Assert.That(controller.Session.ElapsedSeconds, Is.EqualTo(elapsed));
        }

        [TestCase(CircuitJuiceEventType.RotationAccepted,
            NeonGridAudioEvent.TileRotateAccepted, "SFX_TileRotate_01")]
        [TestCase(CircuitJuiceEventType.InteractionRejected,
            NeonGridAudioEvent.InteractionRejected, "SFX_LockedReject_01")]
        [TestCase(CircuitJuiceEventType.PowerActivated,
            NeonGridAudioEvent.PowerActivated, "SFX_PowerActivate_01")]
        [TestCase(CircuitJuiceEventType.PowerDeactivated,
            NeonGridAudioEvent.PowerDeactivated, "SFX_PowerDeactivate_01")]
        [TestCase(CircuitJuiceEventType.SourcePulse,
            NeonGridAudioEvent.SourcePulse, "SFX_SourcePulse_01")]
        [TestCase(CircuitJuiceEventType.SwitchChanged,
            NeonGridAudioEvent.SwitchChanged, "SFX_SwitchToggle_01")]
        [TestCase(CircuitJuiceEventType.GateActivated,
            NeonGridAudioEvent.GateActivated, "SFX_GateActivate_01")]
        [TestCase(CircuitJuiceEventType.GateDeactivated,
            NeonGridAudioEvent.GateDeactivated, "SFX_PowerDeactivate_01")]
        [TestCase(CircuitJuiceEventType.ObjectiveActivated,
            NeonGridAudioEvent.ObjectiveActivated, "SFX_ObjectiveActivate_01")]
        [TestCase(CircuitJuiceEventType.HintTargeted,
            NeonGridAudioEvent.HintActivated, "SFX_Hint_01")]
        [TestCase(CircuitJuiceEventType.CompletionTriggered,
            NeonGridAudioEvent.CompletionTriggered, "SFX_Completion_01")]
        public void SemanticPresentationEventMapsToAuthoredCue(
            CircuitJuiceEventType presentationEvent, NeonGridAudioEvent audioEvent,
            string clipName)
        {
            GameObject root = NewObject("M16 B1 Event Mapping");
            CircuitJuiceCoordinator coordinator = root.AddComponent<CircuitJuiceCoordinator>();
            coordinator.Initialize(juice);
            NeonGridAudioService service = root.AddComponent<NeonGridAudioService>();
            service.Initialize(audio, coordinator);
            coordinator.PublishPresentationEvent(presentationEvent);
            Assert.That(service.LastPlayback.HasValue, Is.True);
            Assert.That(service.LastPlayback.Value.EventType, Is.EqualTo(audioEvent));
            Assert.That(service.LastPlayback.Value.Clip.name, Is.EqualTo(clipName));
        }

        [Test]
        public void UiButtonMapsToDryTapCue()
        {
            NeonGridAudioService service = BuildService();
            Assert.That(service.TryPlay(NeonGridAudioEvent.UIButtonPressed, 1f), Is.True);
            Assert.That(service.LastPlayback.Value.Clip.name, Is.EqualTo("SFX_UIButton_01"));
        }

        [Test]
        public void SourcePulseIsOptionalAndQuietlyAuthored()
        {
            Assert.That(audio.TryGetCue(NeonGridAudioEvent.SourcePulse,
                out NeonGridAudioCue cue), Is.True);
            Assert.That(cue.Clip.name, Is.EqualTo("SFX_SourcePulse_01"));
            Assert.That(cue.Gain, Is.LessThan(0.3f));
        }

        [Test]
        public void AudioSourcePoolIsBoundedAndRepeatedPlaybackDoesNotGrowHierarchy()
        {
            NeonGridAudioService service = BuildService();
            int initialTransforms = service.GetComponentsInChildren<Transform>(true).Length;
            AudioSource[] initialSources =
                service.GetComponentsInChildren<AudioSource>(true);
            Assert.That(service.VoiceCount, Is.EqualTo(6));
            Assert.That(service.AmbienceVoiceCount, Is.EqualTo(2));
            Assert.That(initialSources, Has.Length.EqualTo(8));
            Assert.That(initialSources.Count(source => !source.loop), Is.EqualTo(6));
            Assert.That(initialSources.Count(source => source.loop), Is.EqualTo(2));
            for (int index = 0; index < 40; index++)
                Assert.That(service.TryPlay(NeonGridAudioEvent.UIButtonPressed,
                    index + 1f), Is.True);
            Assert.That(service.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(initialTransforms));
            AudioSource[] finalSources = service.GetComponentsInChildren<AudioSource>(true);
            Assert.That(finalSources, Has.Length.EqualTo(8));
            Assert.That(finalSources.Count(source => !source.loop),
                Is.EqualTo(service.VoiceCount));
            Assert.That(finalSources.Count(source => source.loop),
                Is.EqualTo(service.AmbienceVoiceCount));
            Assert.That(finalSources.Select(source => source.GetInstanceID()),
                Is.EquivalentTo(initialSources.Select(source => source.GetInstanceID())));
        }

        [Test]
        public void MixedEventsRemainIndependentAndServiceStaysSubscribed()
        {
            GameObject root = NewObject("M16 B1 Mixed Runtime Events");
            CircuitJuiceCoordinator coordinator = root.AddComponent<CircuitJuiceCoordinator>();
            coordinator.Initialize(juice);
            NeonGridAudioService service = root.AddComponent<NeonGridAudioService>();
            service.Initialize(audio, coordinator);
            CircuitJuiceEventType[] sequence =
            {
                CircuitJuiceEventType.RotationAccepted,
                CircuitJuiceEventType.InteractionRejected,
                CircuitJuiceEventType.PowerActivated,
                CircuitJuiceEventType.SwitchChanged,
                CircuitJuiceEventType.GateActivated,
                CircuitJuiceEventType.ObjectiveActivated,
                CircuitJuiceEventType.HintTargeted,
                CircuitJuiceEventType.CompletionTriggered
            };
            foreach (CircuitJuiceEventType eventType in sequence)
                coordinator.PublishPresentationEvent(eventType);
            Assert.That(service.SuccessfulPlaybackCount, Is.EqualTo(sequence.Length));
            Assert.That(service.enabled, Is.True);
            Assert.That(service.gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void ReusedVoicesRemainActiveEnabledUnmutedAndValidAfterPoolWrap()
        {
            NeonGridAudioService service = BuildService();
            var voiceIndices = new List<int>();
            for (int index = 0; index < service.VoiceCount * 4; index++)
            {
                Assert.That(service.TryPlay(NeonGridAudioEvent.TileRotateAccepted,
                    index + 1f), Is.True);
                voiceIndices.Add(service.LastPlayback.Value.VoiceIndex);
            }
            AudioSource[] sources = service.GetComponentsInChildren<AudioSource>(true);
            AudioSource[] sfxSources = sources.Where(source => !source.loop).ToArray();
            AudioSource[] ambienceSources = sources.Where(source => source.loop).ToArray();
            Assert.That(service.VoiceCount, Is.EqualTo(6));
            Assert.That(service.AmbienceVoiceCount, Is.EqualTo(2));
            Assert.That(sources, Has.Length.EqualTo(8));
            Assert.That(sfxSources, Has.Length.EqualTo(service.VoiceCount));
            Assert.That(ambienceSources, Has.Length.EqualTo(service.AmbienceVoiceCount));
            Assert.That(sfxSources.All(source => source != null && source.enabled &&
                source.gameObject.activeInHierarchy && !source.mute &&
                source.volume > 0f && !source.loop), Is.True);
            Assert.That(ambienceSources.All(source => source != null && source.enabled &&
                source.gameObject.activeInHierarchy && !source.mute && source.loop), Is.True);
            Assert.That(service.LastPlayback.Value.VoiceIndex,
                Is.InRange(0, service.VoiceCount - 1));
            Assert.That(voiceIndices.Take(service.VoiceCount + 1), Is.EqualTo(
                Enumerable.Range(0, service.VoiceCount).Concat(new[] { 0 })));
        }

        [Test]
        public void PrototypeAudioEnsuresOneActiveListenerWithoutDuplicates()
        {
            foreach (AudioListener existing in Object.FindObjectsByType<AudioListener>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(existing);
            GameObject cameraObject = NewObject("M16 B1 Prototype Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            AudioListener first =
                GameplayVisualPrototypeController.EnsurePrototypeAudioListener(camera);
            AudioListener second =
                GameplayVisualPrototypeController.EnsurePrototypeAudioListener(camera);
            Assert.That(first, Is.SameAs(second));
            Assert.That(first.enabled, Is.True);
            Assert.That(first.gameObject.activeInHierarchy, Is.True);
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void CooldownRejectsPathologicalSpamWithoutDelayedQueue()
        {
            NeonGridAudioService service = BuildService();
            audio.TryGetCue(NeonGridAudioEvent.TileRotateAccepted,
                out NeonGridAudioCue cue);
            Assert.That(service.TryPlay(NeonGridAudioEvent.TileRotateAccepted, 10f), Is.True);
            Assert.That(service.TryPlay(NeonGridAudioEvent.TileRotateAccepted,
                10f + cue.CooldownSeconds * .5f), Is.False);
            Assert.That(service.TryPlay(NeonGridAudioEvent.TileRotateAccepted,
                10f + cue.CooldownSeconds + .001f), Is.True);
            Assert.That(service.SuccessfulPlaybackCount, Is.EqualTo(2));
        }

        [Test]
        public void DeterministicPitchVariationStaysWithinAuthoredBounds()
        {
            NeonGridAudioService service = BuildService();
            audio.TryGetCue(NeonGridAudioEvent.TileRotateAccepted,
                out NeonGridAudioCue cue);
            var observed = new HashSet<float>();
            for (int index = 0; index < 12; index++)
            {
                service.TryPlay(NeonGridAudioEvent.TileRotateAccepted, index + 1f);
                float pitch = service.LastPlayback.Value.Pitch;
                Assert.That(pitch, Is.InRange(cue.MinimumPitch, cue.MaximumPitch));
                observed.Add(pitch);
            }
            Assert.That(observed.Count, Is.GreaterThan(1));
        }

        [Test]
        public void AudioRuntimeCreatesNoRenderingResources()
        {
            NeonGridAudioService service = BuildService();
            Assert.That(service.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(service.GetComponentsInChildren<CanvasRenderer>(true), Is.Empty);
            Assert.That(service.GetComponentsInChildren<Camera>(true), Is.Empty);
        }

        [Test]
        public void SaveSchemaContainsNoAudioState()
        {
            Assert.That(typeof(CampaignSaveData).GetFields().Any(field =>
                field.Name.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.Name.IndexOf("volume", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(CampaignSaveStore.CurrentVersion, Is.GreaterThan(0));
        }

        [Test]
        public void GeneratorUsesStandardLibraryAndNoExternalAudioInputs()
        {
            string source = File.ReadAllText(ProjectPath(
                "Tools/AudioGeneration/generate_audio.py"));
            Assert.That(source, Does.Not.Contain("requests"));
            Assert.That(source, Does.Not.Contain("urllib"));
            Assert.That(source, Does.Not.Contain("socket"));
            Assert.That(source, Does.Not.Contain("subprocess"));
            Assert.That(source, Does.Not.Match(@"open\([^\n]+\.(wav|mp3|ogg)"));
        }

        [Test]
        public void ManifestDeclaresLocalProceduralOriginAndNoExternalSamples()
        {
            AudioManifest manifest = LoadManifest();
            Assert.That(manifest.generator_version,
                Is.EqualTo("neon-grid-procedural-sfx-1.0"));
            Assert.That(manifest.origin, Is.EqualTo("procedural_local"));
            Assert.That(manifest.external_samples, Is.False);
            Assert.That(SfxSounds(manifest), Has.Length.EqualTo(11));
            Assert.That(SfxSounds(manifest).All(sound => sound.seed > 0), Is.True);
        }

        [Test]
        public void RequiredGeneratedWavFilesExistAndHaveMobileSafePcmFormat()
        {
            AudioManifest manifest = LoadManifest();
            string[] required =
            {
                "tile_rotate", "locked_reject", "power_activate", "power_deactivate",
                "switch_toggle", "gate_activate", "objective_activate", "hint",
                "ui_button", "completion"
            };
            Assert.That(manifest.sounds.Select(sound => sound.id),
                Is.SupersetOf(required));
            foreach (AudioSound sound in SfxSounds(manifest))
            {
                string path = WavPath(sound);
                Assert.That(File.Exists(path), Is.True, sound.id);
                WavInfo info = ReadWav(path);
                Assert.That(info.AudioFormat, Is.EqualTo(1), sound.id);
                Assert.That(info.Channels, Is.EqualTo(1), sound.id);
                Assert.That(info.SampleRate, Is.EqualTo(48000), sound.id);
                Assert.That(info.BitsPerSample, Is.EqualTo(16), sound.id);
                Assert.That(info.DurationSeconds, Is.EqualTo(sound.duration).Within(0.001),
                    sound.id);
            }
        }

        [Test]
        public void GeneratedWavHashesMatchManifestAndProvenanceBaseline()
        {
            AudioManifest manifest = LoadManifest();
            string provenance = File.ReadAllText(ProjectPath(
                "Assets/NeonGrid/Documentation/M16_Audio_Provenance.md"));
            foreach (AudioSound sound in SfxSounds(manifest))
            {
                string hash = Sha256(WavPath(sound));
                Assert.That(hash, Is.EqualTo(sound.sha256), sound.id);
                Assert.That(provenance, Does.Contain(hash), sound.id);
            }
        }

        [Test]
        public void GeneratorReproducesSamePcmForSameManifestAndSeeds()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "Tools/AudioGeneration/generate_audio.py --verify --kind sfx",
                    WorkingDirectory = ProjectPath(string.Empty),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero, error);
            Assert.That(output, Does.Contain("Verified 11 deterministic procedural audio files"));
        }

        [Test]
        public void GenerationIsNeverAutomaticAndRuntimeHasNoPythonDependency()
        {
            string[] runtimeFiles = Directory.GetFiles(ProjectPath(
                "Assets/NeonGrid/Scripts"), "*.cs", SearchOption.AllDirectories);
            string[] editorFiles = Directory.GetFiles(ProjectPath(
                "Assets/NeonGrid/Editor"), "*.cs", SearchOption.AllDirectories);
            string[] generatorReferences =
            {
                "generate_audio.py",
                "Tools/AudioGeneration",
                "python.exe",
                "python3"
            };
            string[] automaticLifecycleMarkers =
            {
                "InitializeOnLoad",
                "InitializeOnLoadMethod",
                "DidReloadScripts",
                "RuntimeInitializeOnLoadMethod",
                "InitializeOnEnterPlayMode",
                "playModeStateChanged",
                "AssetPostprocessor",
                "IPreprocessBuild",
                "IPostprocessBuild",
                "PostProcessBuild"
            };

            foreach (string file in runtimeFiles)
            {
                string source = File.ReadAllText(file);
                foreach (string generatorReference in generatorReferences)
                    Assert.That(source, Does.Not.Contain(generatorReference), file);
                Assert.That(source, Does.Not.Contain("System.Diagnostics.Process"), file);
                Assert.That(source, Does.Not.Contain("Process.Start("), file);
            }

            foreach (string file in editorFiles)
            {
                string source = File.ReadAllText(file);
                foreach (string generatorReference in generatorReferences)
                    Assert.That(source, Does.Not.Contain(generatorReference), file);

                if (!automaticLifecycleMarkers.Any(source.Contains)) continue;
                Assert.That(source, Does.Not.Contain("System.Diagnostics.Process"), file);
                Assert.That(source, Does.Not.Contain("Process.Start("), file);
                Assert.That(source, Does.Not.Contain("ProcessStartInfo"), file);
            }

            string verificationRunner = File.ReadAllText(ProjectPath(
                "Assets/NeonGrid/Editor/Verification/NeonGridVerificationRunner.cs"));
            Assert.That(verificationRunner, Does.Contain("InitializeOnLoad"));
            foreach (string generatorReference in generatorReferences)
                Assert.That(verificationRunner, Does.Not.Contain(generatorReference));
            Assert.That(verificationRunner, Does.Not.Contain("Process.Start("));
            Assert.That(verificationRunner, Does.Not.Contain("ProcessStartInfo"));
        }

        [Test]
        public void ImportedClipsUseLocalShortSfxPolicy()
        {
            foreach (AudioSound sound in SfxSounds(LoadManifest()))
            {
                string assetPath = $"Assets/NeonGrid/Audio/SFX/{sound.category}/{sound.filename}";
                var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                Assert.That(importer.forceToMono, Is.True, sound.id);
                Assert.That(importer.loadInBackground, Is.False, sound.id);
                Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad),
                    sound.id);
                Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM),
                    sound.id);
                Assert.That(settings.sampleRateSetting,
                    Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate), sound.id);
                Assert.That(settings.preloadAudioData, Is.True, sound.id);
            }
        }

        [Test]
        public void AcceptedA1A2ActionProducesIdenticalAuthoritativeBoardWithAudio()
        {
            LevelDefinition level = PowerTransitionLevel();
            BoardController audible = BuildController(level, audio);
            var reference = new GameplaySession(level, 8);
            PuzzleAction action = new PuzzleAction(new GridPosition(1, 0),
                PuzzleActionType.RotateClockwise);
            Assert.That(audible.PerformPlayerAction(action.Position), Is.True);
            Assert.That(reference.PerformAction(action), Is.True);
            AssertBoardsEqual(reference.Board, audible.Session.Board);
            Assert.That(audible.Session.IsCompleted, Is.EqualTo(reference.IsCompleted));
            Assert.That(audible.Session.MoveCount, Is.EqualTo(reference.MoveCount));
            reference.Dispose();
        }

        [Test]
        public void RestartStopsCompletionAndClearsPlaybackStateWithoutVoiceLeak()
        {
            BoardController controller = BuildController(PowerTransitionLevel(), audio);
            int voices = controller.AudioService.VoiceCount;
            controller.AudioService.TryPlay(NeonGridAudioEvent.CompletionTriggered, 1f);
            Assert.That(controller.AudioService.LastPlayback.HasValue, Is.True);
            controller.Restart();
            Assert.That(controller.AudioService.LastPlayback.HasValue, Is.False);
            Assert.That(controller.AudioService.VoiceCount, Is.EqualTo(voices));
            Assert.That(controller.AudioService.GetComponentsInChildren<AudioSource>(true)
                .Any(source => source.isPlaying), Is.False);
        }

        [Test]
        public void HintAudioOccursForPresentedTargetNotTimerAvailability()
        {
            BoardController controller = BuildController(RotationLevel(), audio);
            controller.Session.AdvanceTime(GameplaySession.HintUnlockSeconds);
            Assert.That(controller.AudioService.LastPlayback.HasValue, Is.False);
            controller.BoardView.HighlightHint(new GridPosition(0, 0));
            Assert.That(controller.AudioService.LastPlayback.Value.EventType,
                Is.EqualTo(NeonGridAudioEvent.HintActivated));
        }

        private BoardController BuildController(LevelDefinition level,
            NeonGridAudioDefinition definition)
        {
            GameObject root = NewObject("M16 B1 Board Controller");
            var controller = root.AddComponent<BoardController>();
            controller.Initialize(new GameplaySession(level, 8), null, null, null, theme,
                definition == null ? null : juice, definition);
            return controller;
        }

        private NeonGridAudioService BuildService()
        {
            GameObject root = NewObject("M16 B1 Audio Service");
            NeonGridAudioService service = root.AddComponent<NeonGridAudioService>();
            service.Initialize(audio);
            return service;
        }

        private GameObject NewObject(string name)
        {
            var result = new GameObject(name);
            cleanup.Add(result);
            return result;
        }

        private LevelDefinition RotationLevel()
        {
            return Level(2, 1,
                new TileDefinition(new GridPosition(0, 0), TileType.StraightWire, 0, true),
                new TileDefinition(new GridPosition(1, 0), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition PowerTransitionLevel()
        {
            return Level(3, 1,
                new TileDefinition(new GridPosition(0, 0), TileType.PowerSource, 0, false),
                new TileDefinition(new GridPosition(1, 0), TileType.StraightWire, 0, true),
                new TileDefinition(new GridPosition(2, 0), TileType.OutputLamp, 0, false));
        }

        private LevelDefinition Level(int width, int height, params TileDefinition[] tiles)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.SetData(width, height, tiles);
            cleanup.Add(level);
            return level;
        }

        private static object[] Snapshot(BoardState board) => board.AllTiles().Select(tile =>
            (object)(tile.Position, tile.TileType, tile.Rotation, tile.IsSwitchOn,
                tile.IsPowered, tile.EnergizedInputSides, tile.ActiveOutputSides)).ToArray();

        private static void AssertBoardsEqual(BoardState expected, BoardState actual)
        {
            CollectionAssert.AreEqual(Snapshot(expected), Snapshot(actual));
        }

        private static AudioManifest LoadManifest()
        {
            return JsonUtility.FromJson<AudioManifest>(File.ReadAllText(
                ProjectPath(ManifestPath)));
        }

        private static AudioSound[] SfxSounds(AudioManifest manifest)
        {
            return manifest.sounds.Where(sound => sound.asset_kind != "ambience").ToArray();
        }

        private static string ProjectPath(string relative)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string WavPath(AudioSound sound) => ProjectPath(
            $"Assets/NeonGrid/Audio/SFX/{sound.category}/{sound.filename}");

        private static string Sha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }

        private static WavInfo ReadWav(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"));
                reader.ReadUInt32();
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("WAVE"));
                ushort format = 0, channels = 0, bits = 0;
                uint rate = 0, dataBytes = 0;
                while (stream.Position + 8 <= stream.Length)
                {
                    string id = new string(reader.ReadChars(4));
                    uint size = reader.ReadUInt32();
                    long next = stream.Position + size + (size & 1);
                    if (id == "fmt ")
                    {
                        format = reader.ReadUInt16();
                        channels = reader.ReadUInt16();
                        rate = reader.ReadUInt32();
                        reader.ReadUInt32();
                        reader.ReadUInt16();
                        bits = reader.ReadUInt16();
                    }
                    else if (id == "data") dataBytes = size;
                    stream.Position = next;
                }
                double duration = dataBytes / (double)(rate * channels * (bits / 8));
                return new WavInfo(format, channels, rate, bits, duration);
            }
        }

        [Serializable]
        private sealed class AudioManifest
        {
            public string generator_version;
            public string origin;
            public bool external_samples;
            public AudioSound[] sounds;
        }

        [Serializable]
        private sealed class AudioSound
        {
            public string id;
            public string filename;
            public string category;
            public string asset_kind;
            public int seed;
            public double duration;
            public string sha256;
        }

        private readonly struct WavInfo
        {
            public ushort AudioFormat { get; }
            public ushort Channels { get; }
            public uint SampleRate { get; }
            public ushort BitsPerSample { get; }
            public double DurationSeconds { get; }

            public WavInfo(ushort audioFormat, ushort channels, uint sampleRate,
                ushort bitsPerSample, double durationSeconds)
            {
                AudioFormat = audioFormat;
                Channels = channels;
                SampleRate = sampleRate;
                BitsPerSample = bitsPerSample;
                DurationSeconds = durationSeconds;
            }
        }
    }
}

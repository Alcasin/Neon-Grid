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
    public sealed class M16AmbientAudioFoundationTests
    {
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
            Assert.That(audio.IsAmbienceConfigured, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void ProductionRemainsAmbienceUnboundAndPrototypeExcludedFromBuild()
        {
            CampaignDefinition campaign = Resources.Load<CampaignDefinition>(
                "Campaigns/NeonGrid_Main");
            Assert.That(campaign, Is.Not.Null);
            Assert.That(typeof(CampaignDefinition).GetFields(BindingFlags.Instance |
                BindingFlags.NonPublic).Any(field =>
                field.FieldType == typeof(NeonGridAudioDefinition)), Is.False);
            BoardController production = BuildController(Level(), null);
            Assert.That(production.AudioService, Is.Null);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo("Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity"));
        }

        [TestCase(NeonGridAmbienceMode.Gameplay, "AMB_Gameplay_01")]
        [TestCase(NeonGridAmbienceMode.City, "AMB_City_01")]
        public void ModeMapsToAuthoredLoop(NeonGridAmbienceMode mode, string clipName)
        {
            Assert.That(audio.GetAmbienceClip(mode).name, Is.EqualTo(clipName));
            NeonGridAudioService service = BuildService(audio);
            Assert.That(service.RequestAmbience(mode), Is.True);
            service.AdvanceAmbience(audio.AmbienceFadeDuration);
            AudioSource active = AmbienceSources(service).Single(source => source.clip != null);
            Assert.That(active.clip.name, Is.EqualTo(clipName));
            Assert.That(active.loop, Is.True);
            Assert.That(active.volume, Is.EqualTo(audio.GetAmbienceGain(mode) *
                audio.AmbienceVolume)
                .Within(.0001f));
        }

        [Test]
        public void AcceptedListeningQaUsesModeSpecificDefaultGains()
        {
            Assert.That(audio.GameplayAmbienceGain, Is.EqualTo(.153f).Within(.0001f));
            Assert.That(audio.CityAmbienceGain, Is.EqualTo(.18f).Within(.0001f));
            Assert.That(audio.TryGetCue(NeonGridAudioEvent.ObjectiveActivated,
                out NeonGridAudioCue objective), Is.True);
            Assert.That(objective.Gain, Is.EqualTo(.612f).Within(.0001f));
        }

        [Test]
        public void NoneFadesAndStopsAmbienceSafely()
        {
            NeonGridAudioService service = BuildService(audio);
            service.RequestAmbience(NeonGridAmbienceMode.Gameplay);
            service.AdvanceAmbience(audio.AmbienceFadeDuration);
            Assert.That(service.RequestAmbience(NeonGridAmbienceMode.None), Is.True);
            Assert.That(service.IsAmbienceTransitioning, Is.True);
            service.AdvanceAmbience(audio.AmbienceFadeDuration);
            Assert.That(service.IsAmbienceTransitioning, Is.False);
            Assert.That(AmbienceSources(service).All(source =>
                source.clip == null && !source.isPlaying && source.volume == 0f), Is.True);
        }

        [Test]
        public void SameModeIsIdempotentAndNeverStacksPlayback()
        {
            NeonGridAudioService service = BuildService(audio);
            Assert.That(service.RequestAmbience(NeonGridAmbienceMode.Gameplay), Is.True);
            int starts = service.AmbiencePlaybackStartCount;
            for (int index = 0; index < 20; index++)
                Assert.That(service.RequestAmbience(NeonGridAmbienceMode.Gameplay), Is.False);
            Assert.That(service.AmbiencePlaybackStartCount, Is.EqualTo(starts));
            Assert.That(AmbienceSources(service).Count(source => source.clip != null),
                Is.EqualTo(1));
        }

        [Test]
        public void DedicatedSourcesAreBoundedAndSeparateFromSfxPool()
        {
            NeonGridAudioService service = BuildService(audio);
            Assert.That(service.VoiceCount, Is.EqualTo(6));
            Assert.That(service.AmbienceVoiceCount, Is.EqualTo(2));
            Assert.That(service.GetComponentsInChildren<AudioSource>(true), Has.Length.EqualTo(8));
            Assert.That(AmbienceSources(service).All(source => source.loop), Is.True);
            Assert.That(service.transform.Cast<Transform>().Count(child =>
                child.name.StartsWith("SFX Voice", StringComparison.Ordinal)), Is.EqualTo(6));
        }

        [Test]
        public void RepeatedModeTransitionsDoNotGrowHierarchy()
        {
            NeonGridAudioService service = BuildService(audio);
            int initial = service.GetComponentsInChildren<Transform>(true).Length;
            for (int index = 0; index < 40; index++)
            {
                service.RequestAmbience(index % 3 == 0 ? NeonGridAmbienceMode.Gameplay :
                    index % 3 == 1 ? NeonGridAmbienceMode.City : NeonGridAmbienceMode.None);
                service.AdvanceAmbience(audio.AmbienceFadeDuration);
            }
            Assert.That(service.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(initial));
            Assert.That(service.AmbienceVoiceCount, Is.EqualTo(2));
        }

        [Test]
        public void ZeroAmbienceVolumeIsSilentStableAndDoesNotRestart()
        {
            NeonGridAudioDefinition muted = CloneDefinition(0f);
            NeonGridAudioService service = BuildService(muted);
            Assert.That(service.RequestAmbience(NeonGridAmbienceMode.Gameplay), Is.True);
            service.AdvanceAmbience(muted.AmbienceFadeDuration);
            Assert.That(AmbienceSources(service).All(source => source.volume == 0f), Is.True);
            int starts = service.AmbiencePlaybackStartCount;
            Assert.That(service.RequestAmbience(NeonGridAmbienceMode.Gameplay), Is.False);
            Assert.That(service.AmbiencePlaybackStartCount, Is.EqualTo(starts));
        }

        [Test]
        public void AmbienceTransitionDoesNotAlterGameplayState()
        {
            BoardController controller = BuildController(Level(), audio);
            object[] before = Snapshot(controller.Session.Board);
            int moves = controller.Session.MoveCount;
            controller.AudioService.RequestAmbience(NeonGridAmbienceMode.Gameplay);
            controller.AudioService.AdvanceAmbience(audio.AmbienceFadeDuration);
            controller.AudioService.RequestAmbience(NeonGridAmbienceMode.City);
            controller.AudioService.AdvanceAmbience(audio.AmbienceFadeDuration);
            CollectionAssert.AreEqual(before, Snapshot(controller.Session.Board));
            Assert.That(controller.Session.MoveCount, Is.EqualTo(moves));
        }

        [Test]
        public void SfxAndCompletionRemainFunctionalAndIndependentDuringAmbience()
        {
            NeonGridAudioService service = BuildService(audio);
            service.RequestAmbience(NeonGridAmbienceMode.Gameplay);
            service.AdvanceAmbience(audio.AmbienceFadeDuration);
            Assert.That(service.TryPlay(NeonGridAudioEvent.TileRotateAccepted, 1f), Is.True);
            Assert.That(service.TryPlay(NeonGridAudioEvent.CompletionTriggered, 2f), Is.True);
            Assert.That(service.LastPlayback.Value.EventType,
                Is.EqualTo(NeonGridAudioEvent.CompletionTriggered));
            Assert.That(AmbienceSources(service).Single(source => source.clip != null).loop,
                Is.True);
            Assert.That(service.VoiceCount, Is.EqualTo(6));
        }

        [Test]
        public void RestartPreservesOneAmbienceInstanceWithoutDuplication()
        {
            BoardController controller = BuildController(Level(), audio);
            NeonGridAudioService service = controller.AudioService;
            service.RequestAmbience(NeonGridAmbienceMode.Gameplay);
            service.AdvanceAmbience(audio.AmbienceFadeDuration);
            int starts = service.AmbiencePlaybackStartCount;
            int hierarchy = service.GetComponentsInChildren<Transform>(true).Length;
            controller.Restart();
            Assert.That(service.RequestedAmbienceMode,
                Is.EqualTo(NeonGridAmbienceMode.Gameplay));
            Assert.That(service.AmbiencePlaybackStartCount, Is.EqualTo(starts));
            Assert.That(service.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(hierarchy));
        }

        [Test]
        public void RepeatedInitializationIsIdempotent()
        {
            GameObject root = NewObject("M16 B2 Idempotent Service");
            NeonGridAudioService service = root.AddComponent<NeonGridAudioService>();
            service.Initialize(audio);
            int hierarchy = service.GetComponentsInChildren<Transform>(true).Length;
            service.Initialize(audio);
            service.Initialize(audio);
            Assert.That(service.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(hierarchy));
            Assert.That(service.VoiceCount, Is.EqualTo(6));
            Assert.That(service.AmbienceVoiceCount, Is.EqualTo(2));
        }

        [Test]
        public void AmbienceImportSettingsUseMobileMemoryTradeoff()
        {
            foreach (string filename in new[] { "AMB_Gameplay_01.wav", "AMB_City_01.wav" })
            {
                string path = $"Assets/NeonGrid/Audio/Ambience/{filename}";
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                Assert.That(importer.forceToMono, Is.False, filename);
                Assert.That(settings.loadType,
                    Is.EqualTo(AudioClipLoadType.CompressedInMemory), filename);
                Assert.That(settings.compressionFormat,
                    Is.EqualTo(AudioCompressionFormat.Vorbis), filename);
                Assert.That(settings.quality, Is.EqualTo(.55f).Within(.001f), filename);
                Assert.That(settings.sampleRateSetting,
                    Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate), filename);
                Assert.That(settings.preloadAudioData, Is.True, filename);
            }
        }

        [Test]
        public void ManifestAndProvenanceDescribeProceduralLoopSafeAmbience()
        {
            AudioManifest manifest = LoadManifest();
            AudioSound[] ambience = AmbienceSounds(manifest);
            Assert.That(ambience, Has.Length.EqualTo(2));
            Assert.That(ambience.All(sound => sound.loop_safe &&
                sound.origin == "procedural_local" && !sound.external_samples &&
                sound.generator_version == "neon-grid-procedural-ambience-1.0"), Is.True);
            string provenance = File.ReadAllText(ProjectPath(
                "Assets/NeonGrid/Documentation/M16_Audio_Provenance.md"));
            foreach (AudioSound sound in ambience)
                Assert.That(provenance, Does.Contain(sound.sha256));
        }

        [Test]
        public void AmbienceWavFormatHashesBoundaryAndDownmixAreSafe()
        {
            foreach (AudioSound sound in AmbienceSounds(LoadManifest()))
            {
                string path = AmbiencePath(sound);
                Assert.That(File.Exists(path), Is.True, sound.id);
                WavData wav = ReadWav(path);
                Assert.That(wav.AudioFormat, Is.EqualTo(1), sound.id);
                Assert.That(wav.Channels, Is.EqualTo(2), sound.id);
                Assert.That(wav.SampleRate, Is.EqualTo(48000), sound.id);
                Assert.That(wav.BitsPerSample, Is.EqualTo(16), sound.id);
                Assert.That(wav.Duration, Is.EqualTo(sound.duration).Within(.001), sound.id);
                Assert.That(wav.MaximumBoundaryDelta, Is.LessThan(128), sound.id);
                Assert.That(wav.MonoRms, Is.GreaterThan(wav.SideRms * 8.0), sound.id);
                Assert.That(Sha256(path), Is.EqualTo(sound.sha256), sound.id);
            }
        }

        [Test]
        public void AmbienceGeneratorIsDeterministicAndB1HashesRemainAccepted()
        {
            ProcessResult result = RunPython(
                "Tools/AudioGeneration/generate_audio.py --verify --kind ambience");
            Assert.That(result.ExitCode, Is.Zero, result.Error);
            Assert.That(result.Output, Does.Contain("Verified 2 deterministic procedural audio files"));
            var expected = new Dictionary<string, string>
            {
                ["tile_rotate"] = "4699F1FC5D4AAC538ACDE4E21DCA01A3A5AC70D5BC876D4BBA9679D323146FE6",
                ["locked_reject"] = "BDBDDC4E1D2C9B917EA06958B372770FE0E414EC1CBAB0A7AB3FB712C907FC28",
                ["power_activate"] = "14E4B9D97FDE426FC85B07C6BCCB98AC583146A94D08C434E447093960A65ED7",
                ["power_deactivate"] = "1DDB7565FF0C1CBFD2E894D27EAA24C16B941AA5E6A9DD31E0906892B5D3863E",
                ["source_pulse"] = "B0B7EBFBDE7956B974233D5FCDF5FCF4DFB9D718DEAD7D22AC9DF332D1941E7C",
                ["switch_toggle"] = "4A957B16C15344C5B2B666A34834A2FA8D4EB5E3CD65D8C8E4717F45B7E49E4C",
                ["gate_activate"] = "2208C6DA36892FD4FD2DD261972C0D7481A1A7076D9DF1ACCFCDCB056C51E91A",
                ["objective_activate"] = "2EC093080A55E12C4E94763C362941EDC567FEA9E597B1C535A0FE5040F81090",
                ["hint"] = "A788A7CC973F784428436445A0D91C53FE1FA1BC446EE4D1077EC76B0092325F",
                ["ui_button"] = "1D85F88719EF633BE7EDFBD92584740A4123C60A341F6A78127040BB8F5B4CEA",
                ["completion"] = "ACEC17CFE0BEDD1D13E3EE26F8196D542916ED2C13868AE56BEF5D5A7E8D8DEB"
            };
            foreach (AudioSound sound in LoadManifest().sounds.Where(item =>
                         item.asset_kind != "ambience"))
            {
                string path = ProjectPath(
                    $"Assets/NeonGrid/Audio/SFX/{sound.category}/{sound.filename}");
                Assert.That(Sha256(path), Is.EqualTo(expected[sound.id]), sound.id);
            }
        }

        [Test]
        public void RuntimeHasNoGeneratorDependencyAndSaveSchemaIsUnchanged()
        {
            string runtime = string.Join("\n", Directory.GetFiles(ProjectPath(
                    "Assets/NeonGrid/Scripts"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
            string editor = string.Join("\n", Directory.GetFiles(ProjectPath(
                    "Assets/NeonGrid/Editor"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
            Assert.That(runtime, Does.Not.Contain("generate_audio.py"));
            Assert.That(runtime, Does.Not.Contain("System.Diagnostics.Process"));
            Assert.That(editor, Does.Not.Contain("generate_audio.py"));
            Assert.That(typeof(CampaignSaveData).GetFields().Any(field =>
                field.Name.IndexOf("ambience", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        private BoardController BuildController(LevelDefinition level,
            NeonGridAudioDefinition definition)
        {
            GameObject root = NewObject("M16 B2 Board Controller");
            var controller = root.AddComponent<BoardController>();
            controller.Initialize(new GameplaySession(level, 8), null, null, null, theme,
                definition == null ? null : juice, definition);
            return controller;
        }

        private NeonGridAudioService BuildService(NeonGridAudioDefinition definition)
        {
            GameObject root = NewObject("M16 B2 Audio Service");
            NeonGridAudioService service = root.AddComponent<NeonGridAudioService>();
            service.Initialize(definition);
            return service;
        }

        private NeonGridAudioDefinition CloneDefinition(float ambienceVolume)
        {
            NeonGridAudioDefinition result = ScriptableObject.CreateInstance<NeonGridAudioDefinition>();
            result.SetData(audio.Cues, audio.SourcePoolSize, audio.MasterSfxMultiplier,
                audio.SfxVolume, audio.GameplayAmbience, audio.CityAmbience, ambienceVolume,
                audio.AmbienceGain, audio.AmbienceFadeDuration,
                audio.GameplayAmbienceGain);
            cleanup.Add(result);
            return result;
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

        private static AudioSource[] AmbienceSources(NeonGridAudioService service) =>
            service.GetComponentsInChildren<AudioSource>(true)
                .Where(source => source.gameObject.name.StartsWith("Ambience Voice",
                    StringComparison.Ordinal)).ToArray();

        private static object[] Snapshot(BoardState board) => board.AllTiles().Select(tile =>
            (object)(tile.Position, tile.TileType, tile.Rotation, tile.IsSwitchOn,
                tile.IsPowered, tile.EnergizedInputSides, tile.ActiveOutputSides)).ToArray();

        private static AudioManifest LoadManifest() => JsonUtility.FromJson<AudioManifest>(
            File.ReadAllText(ProjectPath("Tools/AudioGeneration/audio_manifest.json")));

        private static AudioSound[] AmbienceSounds(AudioManifest manifest) =>
            manifest.sounds.Where(sound => sound.asset_kind == "ambience").ToArray();

        private static string AmbiencePath(AudioSound sound) => ProjectPath(
            $"Assets/NeonGrid/Audio/Ambience/{sound.filename}");

        private static string ProjectPath(string relative)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string Sha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }

        private static ProcessResult RunPython(string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python", Arguments = arguments,
                    WorkingDirectory = ProjectPath(string.Empty), UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new ProcessResult(process.ExitCode, output, error);
        }

        private static WavData ReadWav(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"));
                reader.ReadUInt32();
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("WAVE"));
                ushort format = 0, channels = 0, bits = 0;
                uint rate = 0;
                byte[] data = null;
                while (stream.Position + 8 <= stream.Length)
                {
                    string id = new string(reader.ReadChars(4));
                    uint size = reader.ReadUInt32();
                    long next = stream.Position + size + (size & 1);
                    if (id == "fmt ")
                    {
                        format = reader.ReadUInt16(); channels = reader.ReadUInt16();
                        rate = reader.ReadUInt32(); reader.ReadUInt32(); reader.ReadUInt16();
                        bits = reader.ReadUInt16();
                    }
                    else if (id == "data") data = reader.ReadBytes((int)size);
                    stream.Position = next;
                }
                short[] samples = new short[data.Length / 2];
                Buffer.BlockCopy(data, 0, samples, 0, data.Length);
                int frames = samples.Length / channels;
                int boundary = 0;
                double monoSquares = 0, sideSquares = 0;
                int measured = 0;
                for (int channel = 0; channel < channels; channel++)
                    boundary = Math.Max(boundary, Math.Abs(samples[channel] -
                        samples[(frames - 1) * channels + channel]));
                for (int frame = 0; frame < frames; frame += 97)
                {
                    double left = samples[frame * channels];
                    double right = samples[frame * channels + 1];
                    monoSquares += Math.Pow((left + right) * .5, 2);
                    sideSquares += Math.Pow((left - right) * .5, 2);
                    measured++;
                }
                return new WavData(format, channels, rate, bits,
                    frames / (double)rate, boundary,
                    Math.Sqrt(monoSquares / measured), Math.Sqrt(sideSquares / measured));
            }
        }

        [Serializable]
        private sealed class AudioManifest { public AudioSound[] sounds; }

        [Serializable]
        private sealed class AudioSound
        {
            public string id, filename, category, asset_kind, generator_version, origin, sha256;
            public int seed;
            public double duration;
            public bool loop_safe, external_samples;
        }

        private readonly struct ProcessResult
        {
            public int ExitCode { get; }
            public string Output { get; }
            public string Error { get; }
            public ProcessResult(int exitCode, string output, string error)
            { ExitCode = exitCode; Output = output; Error = error; }
        }

        private readonly struct WavData
        {
            public ushort AudioFormat { get; }
            public ushort Channels { get; }
            public uint SampleRate { get; }
            public ushort BitsPerSample { get; }
            public double Duration { get; }
            public int MaximumBoundaryDelta { get; }
            public double MonoRms { get; }
            public double SideRms { get; }
            public WavData(ushort format, ushort channels, uint sampleRate, ushort bits,
                double duration, int boundary, double monoRms, double sideRms)
            { AudioFormat = format; Channels = channels; SampleRate = sampleRate;
              BitsPerSample = bits; Duration = duration; MaximumBoundaryDelta = boundary;
              MonoRms = monoRms; SideRms = sideRms; }
        }
    }
}

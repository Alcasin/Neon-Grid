using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class M16ProductionGameplayFeedbackTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private CampaignDefinition campaign;
        private ProductionGameplayFeedbackDefinition feedback;

        [SetUp]
        public void SetUp()
        {
            campaign = Resources.Load<CampaignDefinition>("Campaigns/NeonGrid_Main");
            feedback = AssetDatabase.LoadAssetAtPath<ProductionGameplayFeedbackDefinition>(
                M16ProductionGameplayFeedbackBuilder.DefinitionPath);
            Assert.That(campaign, Is.Not.Null);
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.IsConfigured, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            NeonGridHapticsSettings.HapticsEnabled = true;
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void ProductionCompositionReferencesAcceptedDefinitionsWithoutPrototypeContainer()
        {
            Assert.That(feedback.CircuitJuice, Is.SameAs(
                AssetDatabase.LoadAssetAtPath<CircuitJuiceDefinition>(
                    M16CircuitJuicePrototypeBuilder.JuicePath)));
            Assert.That(feedback.AudioDefinition, Is.SameAs(
                AssetDatabase.LoadAssetAtPath<NeonGridAudioDefinition>(
                    M16AudioPrototypeBuilder.DefinitionPath)));
            Assert.That(feedback.HapticsDefinition, Is.SameAs(
                AssetDatabase.LoadAssetAtPath<NeonGridHapticsDefinition>(
                    M16HapticsPrototypeBuilder.DefinitionPath)));
            Assert.That(feedback.AudioDefinition.SourcePoolSize, Is.EqualTo(6));
            Assert.That(feedback.AudioDefinition.GameplayAmbienceGain,
                Is.EqualTo(.153f).Within(.0001f));
            Assert.That(feedback.AudioDefinition.AmbienceFadeDuration,
                Is.EqualTo(.8f).Within(.0001f));
        }

        [Test]
        public void MainCampaignAloneOptsIntoProductionFeedback()
        {
            Assert.That(campaign.GameplayFeedback, Is.SameAs(feedback));
            foreach (string resource in new[]
                     {
                         "PowerStation_VerticalSlice", "Substation_VerticalSlice",
                         "ControlCenter_VerticalSlice", "AutomationPlant_VerticalSlice",
                         "CentralGrid_VerticalSlice"
                     })
            {
                CampaignDefinition slice = Resources.Load<CampaignDefinition>(
                    "Campaigns/" + resource);
                Assert.That(slice, Is.Not.Null, resource);
                Assert.That(slice.GameplayFeedback, Is.Null, resource);
            }
        }

        [Test]
        public void ProductionRuntimeBindsOneServicePairAndGameplayAmbienceOnly()
        {
            CampaignRuntimeController runtime = BuildRuntime();
            Assert.That(runtime.OpenChapter("power_station"), Is.True);
            Assert.That(runtime.StartLevel("power_01"), Is.True);

            ProductionGameplayFeedbackController composition = runtime.GameplayFeedback;
            BoardController board = runtime.ActiveBoard;
            Assert.That(composition, Is.Not.Null);
            Assert.That(board, Is.Not.Null);
            Assert.That(board.BoardView.UsesCircuitJuice, Is.True);
            Assert.That(board.AudioService, Is.SameAs(composition.AudioService));
            Assert.That(board.HapticsService, Is.SameAs(composition.HapticsService));
            Assert.That(runtime.GetComponents<NeonGridAudioService>(), Has.Length.EqualTo(1));
            Assert.That(runtime.GetComponents<NeonGridHapticsService>(), Has.Length.EqualTo(1));
            Assert.That(composition.AudioService.VoiceCount, Is.EqualTo(6));
            Assert.That(composition.AudioService.AmbienceVoiceCount, Is.EqualTo(2));
            Assert.That(composition.AudioService.RequestedAmbienceMode,
                Is.EqualTo(NeonGridAmbienceMode.Gameplay));
            Assert.That(composition.AudioService.RequestedAmbienceMode,
                Is.Not.EqualTo(NeonGridAmbienceMode.City));
            Assert.That(Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void ExitAndReentryReuseServicesAndDoNotGrowFeedbackHierarchy()
        {
            CampaignRuntimeController runtime = BuildRuntime();
            Assert.That(runtime.OpenChapter("power_station"), Is.True);
            Assert.That(runtime.StartLevel("power_01"), Is.True);
            ProductionGameplayFeedbackController composition = runtime.GameplayFeedback;
            NeonGridAudioService audio = composition.AudioService;
            NeonGridHapticsService haptics = composition.HapticsService;
            int[] sourceIds = composition.GetComponentsInChildren<AudioSource>(true)
                .Select(source => source.GetInstanceID()).ToArray();

            runtime.ShowCurrentChapter();
            Assert.That(composition.ActiveBoard, Is.Null);
            Assert.That(audio.RequestedAmbienceMode, Is.EqualTo(NeonGridAmbienceMode.None));
            Assert.That(runtime.StartLevel("power_01"), Is.True);
            Assert.That(composition.AudioService, Is.SameAs(audio));
            Assert.That(composition.HapticsService, Is.SameAs(haptics));
            Assert.That(composition.GetComponentsInChildren<AudioSource>(true)
                    .Select(source => source.GetInstanceID()), Is.EquivalentTo(sourceIds));
            Assert.That(runtime.GetComponents<NeonGridAudioService>(), Has.Length.EqualTo(1));
            Assert.That(runtime.GetComponents<NeonGridHapticsService>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void ProductionFeedbackDoesNotChangeGameplaySemantics()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(
                "Levels/PowerStation/PS_01");
            var reference = new GameplaySession(level);
            CampaignRuntimeController runtime = BuildRuntime();
            Assert.That(runtime.OpenChapter("power_station"), Is.True);
            Assert.That(runtime.StartLevel("power_01"), Is.True);
            BoardController board = runtime.ActiveBoard;
            var action = new PuzzleAction(new GridPosition(1, 0),
                PuzzleActionType.RotateClockwise);

            Assert.That(board.PerformPlayerAction(action.Position),
                Is.EqualTo(reference.PerformAction(action)));
            Assert.That(board.Session.MoveCount, Is.EqualTo(reference.MoveCount));
            Assert.That(board.Session.IsCompleted, Is.EqualTo(reference.IsCompleted));
            Assert.That(board.Session.Board.AllTiles().Select(tile =>
                    (tile.Position, tile.Rotation, tile.IsPowered)),
                Is.EqualTo(reference.Board.AllTiles().Select(tile =>
                    (tile.Position, tile.Rotation, tile.IsPowered))));
            reference.Dispose();
        }

        [Test]
        public void ProductionCompletionResultFadesOnceWithoutDelayingAuthoritativeCompletion()
        {
            CampaignRuntimeController runtime = BuildRuntime();
            Assert.That(runtime.OpenChapter("power_station"), Is.True);
            Assert.That(runtime.StartLevel("power_01"), Is.True);
            BoardController board = runtime.ActiveBoard;
            GameplayHudView hud = board.GetComponent<GameplayHudView>();

            Assert.That(board.PerformPlayerAction(new GridPosition(1, 0)), Is.True);
            Assert.That(board.Session.IsCompleted, Is.True,
                "Authoritative completion must remain immediate.");
            Assert.That(board.Session.CompletionResult, Is.Not.Null);
            Assert.That(board.IsCompletionPanelVisible, Is.True);
            Assert.That(hud.CompletionFadeDuration, Is.EqualTo(.32f).Within(.0001f));
            Assert.That(hud.CompletionPanelAlpha, Is.Zero.Within(.0001f));
            Assert.That(hud.IsCompletionFadeActive, Is.True);
            Assert.That(hud.IsCompletionFadeFramePending, Is.True);

            hud.AdvanceCompletionFade(.16f);
            Assert.That(hud.CompletionPanelAlpha, Is.Zero.Within(.0001f),
                "The activation update must preserve a truly transparent first frame.");
            Assert.That(hud.IsCompletionFadeFramePending, Is.False);
            hud.AdvanceCompletionFade(.16f);
            float halfAlpha = hud.CompletionPanelAlpha;
            Assert.That(halfAlpha, Is.EqualTo(.5f).Within(.001f));
            hud.Refresh(board.Session);
            Assert.That(hud.CompletionPanelAlpha, Is.EqualTo(halfAlpha).Within(.001f),
                "Repeated refresh must not restart or stack the fade.");
            hud.AdvanceCompletionFade(.16f);
            Assert.That(hud.CompletionPanelAlpha, Is.EqualTo(1f).Within(.0001f));
            Assert.That(hud.IsCompletionFadeActive, Is.False);

            board.Restart();
            Assert.That(board.Session.IsCompleted, Is.False);
            Assert.That(board.IsCompletionPanelVisible, Is.False);
            Assert.That(hud.CompletionPanelAlpha, Is.Zero.Within(.0001f));
            Assert.That(hud.IsCompletionFadeActive, Is.False);
            Assert.That(hud.IsCompletionFadeFramePending, Is.False);
        }

        [Test]
        public void ProductionHasNoPrototypeQaControllerOrControls()
        {
            CampaignRuntimeController runtime = BuildRuntime();
            Assert.That(runtime.OpenChapter("power_station"), Is.True);
            Assert.That(runtime.StartLevel("power_01"), Is.True);
            Assert.That(runtime.GetComponent<GameplayVisualPrototypeController>(), Is.Null);
            string[] names = runtime.GetComponentsInChildren<Transform>(true)
                .Select(item => item.name).ToArray();
            Assert.That(names, Has.None.EqualTo("M15 Prototype Selector"));
            Assert.That(names, Has.None.EqualTo("Haptics ON"));
            Assert.That(names, Has.None.EqualTo("Haptics OFF"));
            Assert.That(names, Has.None.EqualTo("Play Gameplay Ambience"));
            Assert.That(names, Has.None.EqualTo("Play City Ambience"));
        }

        [Test]
        public void IsolatedPrototypeRetainsAcceptedBindings()
        {
            GameplayVisualPrototypeDefinition prototype = GameplayVisualPrototypeCatalog.Load();
            Assert.That(prototype, Is.Not.Null);
            Assert.That(prototype.CircuitJuice, Is.SameAs(feedback.CircuitJuice));
            Assert.That(prototype.AudioDefinition, Is.SameAs(feedback.AudioDefinition));
            Assert.That(prototype.HapticsDefinition, Is.SameAs(feedback.HapticsDefinition));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path),
                Has.None.EqualTo("Assets/NeonGrid/Scenes/M15_GameplayVisualPrototype.unity"));
        }

        private CampaignRuntimeController BuildRuntime()
        {
            GameObject cameraObject = NewObject("M16 D1 Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>().orthographic = true;
            GameObject root = NewObject("M16 D1 Production Runtime");
            CampaignRuntimeController runtime = root.AddComponent<CampaignRuntimeController>();
            runtime.Initialize(campaign, new MemoryStore(campaign));
            return runtime;
        }

        private GameObject NewObject(string name)
        {
            var value = new GameObject(name);
            cleanup.Add(value);
            return value;
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            private readonly CampaignDefinition campaign;
            public string SavePath => "memory://m16-d1";

            public MemoryStore(CampaignDefinition campaign)
            {
                this.campaign = campaign;
            }

            public CampaignLoadResult Load(CampaignDefinition definition) =>
                new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(campaign), Array.Empty<string>());
            public CampaignSaveResult Save(CampaignProgressService progress) =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            public CampaignSaveResult Delete() =>
                new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
        }
    }
}

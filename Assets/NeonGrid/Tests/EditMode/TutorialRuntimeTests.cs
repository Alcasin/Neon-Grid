using NeonGrid.Campaign;
using NeonGrid.Data;
using NeonGrid.Session;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class TutorialRuntimeTests
    {
        private CampaignDefinition campaign;

        [SetUp]
        public void SetUp()
        {
            campaign = Resources.Load<CampaignDefinition>("Campaigns/PowerStation_VerticalSlice");
            Assert.That(campaign, Is.Not.Null);
        }

        [TestCase(0, "Tap a wire to rotate it.", TileType.StraightWire, 1, 0)]
        [TestCase(1, "Corner wires redirect the current.", TileType.CornerWire, 1, 0)]
        [TestCase(5, "T-junctions split power into multiple paths.", TileType.TJunction, 2, 2)]
        [TestCase(8, "Diodes only allow power in one direction.", TileType.Diode, 2, 2)]
        public void ProductionTutorial_ExposesExpectedStepAndCompletesOnlyForTargetAction(
            int levelIndex, string message, TileType targetType, int targetX, int targetY)
        {
            CampaignLevelEntry entry = campaign.Chapters[0].Levels[levelIndex];
            var tutorial = new TutorialRuntime(entry.Tutorial);
            var session = new GameplaySession(entry.LevelDefinition);
            GridPosition target = new GridPosition(targetX, targetY);
            int startingRotation = session.Board.GetTile(target).Rotation;

            Assert.That(tutorial.IsActive, Is.True);
            Assert.That(tutorial.CurrentStep.Message, Is.EqualTo(message));
            Assert.That(tutorial.CurrentStep.TargetPosition, Is.EqualTo(target));
            Assert.That(session.Board.GetTile(target).TileType, Is.EqualTo(targetType));
            Assert.That(session.Board.GetTile(target).Rotation, Is.EqualTo(startingRotation),
                "Creating the tutorial must not execute its action.");
            Assert.That(tutorial.ObserveSuccessfulAction(new PuzzleAction(
                new GridPosition(9, 9), PuzzleActionType.RotateClockwise)), Is.False);
            Assert.That(tutorial.IsActive, Is.True);

            Assert.That(session.Board.TryGetPlayerAction(target, out PuzzleAction action), Is.True);
            Assert.That(session.PerformAction(action), Is.True);
            Assert.That(tutorial.ObserveSuccessfulAction(action), Is.True);

            Assert.That(tutorial.IsActive, Is.False);
            Assert.That(session.MoveCount, Is.EqualTo(1));
            Assert.That(session.HintsUsed, Is.False);
            if (targetType == TileType.Diode)
                Assert.That(session.IsCompleted, Is.False,
                    "The PS_09 tutorial rotation must not auto-solve the level.");
        }

        [Test]
        public void TutorialSequence_AdvancesInOrderWithoutBranching()
        {
            var first = new TutorialStepDefinition("First", new GridPosition(1, 0),
                TutorialCompletionCondition.RotateClockwise);
            var second = new TutorialStepDefinition("Second", new GridPosition(2, 0),
                TutorialCompletionCondition.RotateClockwise);
            var tutorial = new TutorialRuntime(new LevelTutorialDefinition(new[] { first, second }));

            Assert.That(tutorial.ObserveSuccessfulAction(new PuzzleAction(second.TargetPosition,
                PuzzleActionType.RotateClockwise)), Is.False);
            Assert.That(tutorial.CurrentStep, Is.SameAs(first));
            Assert.That(tutorial.ObserveSuccessfulAction(new PuzzleAction(first.TargetPosition,
                PuzzleActionType.RotateClockwise)), Is.True);
            Assert.That(tutorial.CurrentStep, Is.SameAs(second));
            Assert.That(tutorial.ObserveSuccessfulAction(new PuzzleAction(second.TargetPosition,
                PuzzleActionType.RotateClockwise)), Is.True);
            Assert.That(tutorial.IsActive, Is.False);
        }

        [Test]
        public void TransferLevels_HaveNoTutorialRuntime()
        {
            foreach (int levelIndex in new[] { 2, 3, 4, 6, 7, 9 })
            {
                CampaignLevelEntry entry = campaign.Chapters[0].Levels[levelIndex];
                Assert.That(entry.Tutorial, Is.Null, $"{entry.LevelId} must not have a tutorial.");
                Assert.That(new TutorialRuntime(entry.Tutorial).IsActive, Is.False);
            }
        }

        [Test]
        public void Ps09CompletedReplaySkipsTutorialWhileFreshUnlockedProgressShowsIt()
        {
            var progress = new CampaignProgressService(campaign);
            CompleteThrough(progress, 8);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore());
            Assert.That(flow.OpenChapter("power_station"), Is.True);
            Assert.That(flow.StartLevel("power_09"), Is.True);
            Assert.That(flow.ActiveTutorial, Is.Not.Null);

            PuzzleSolverResult solution = new PuzzleSolver().Solve(flow.ActiveSession.Board);
            Assert.That(solution.Status, Is.EqualTo(PuzzleSolverStatus.Solved));
            foreach (PuzzleAction action in solution.Solution)
                Assert.That(flow.ActiveSession.PerformAction(action), Is.True);
            Assert.That(progress.GetLevelProgress("power_09").Completed, Is.True);
            Assert.That(flow.Retry(), Is.True);
            Assert.That(flow.ActiveTutorial, Is.Null);

            var freshProgress = new CampaignProgressService(campaign);
            CompleteThrough(freshProgress, 8);
            var freshFlow = new CampaignFlowCoordinator(campaign, freshProgress, new MemoryStore());
            freshFlow.OpenChapter("power_station");
            freshFlow.StartLevel("power_09");
            Assert.That(freshFlow.ActiveTutorial, Is.Not.Null);
        }

        [Test]
        public void UndoDoesNotReopenCompletedStepAndRestartResetsIncompleteAttemptTutorial()
        {
            LevelDefinition ps03 = campaign.Chapters[0].Levels[2].LevelDefinition;
            LevelTutorialDefinition definition = campaign.Chapters[0].Levels[0].Tutorial;
            var tutorial = new TutorialRuntime(definition);
            var session = new GameplaySession(ps03);
            GridPosition target = tutorial.CurrentStep.TargetPosition;

            Assert.That(session.Board.TryGetPlayerAction(target, out PuzzleAction action), Is.True);
            Assert.That(session.PerformAction(action), Is.True);
            Assert.That(tutorial.ObserveSuccessfulAction(action), Is.True);
            Assert.That(session.MoveCount, Is.EqualTo(1));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.MoveCount, Is.EqualTo(1),
                "Undo preserves the accepted cumulative move semantics.");
            Assert.That(tutorial.IsActive, Is.False,
                "Tutorial progress is monotonic within one attempt.");

            tutorial.Restart();
            session.Restart();
            Assert.That(tutorial.IsActive, Is.True);
            Assert.That(session.MoveCount, Is.Zero);
        }

        [Test]
        public void HintOperationsDoNotAdvanceTutorialOrImplicitlyMarkHintUsage()
        {
            CampaignLevelEntry entry = campaign.Chapters[0].Levels[0];
            var tutorial = new TutorialRuntime(entry.Tutorial);
            var session = new GameplaySession(entry.LevelDefinition);

            Assert.That(session.HintsUsed, Is.False);
            Assert.That(session.RequestHint().Status, Is.EqualTo(HintStatus.HintLocked));
            Assert.That(session.HintsUsed, Is.False);
            Assert.That(tutorial.IsActive, Is.True);
        }

        [Test]
        public void CampaignCompletionHistoryGatesEachAttemptWithoutTutorialPersistence()
        {
            var progress = new CampaignProgressService(campaign);
            var flow = new CampaignFlowCoordinator(campaign, progress, new MemoryStore());
            Assert.That(flow.OpenChapter("power_station"), Is.True);
            Assert.That(flow.StartLevel("power_01"), Is.True);
            Assert.That(flow.ActiveTutorial, Is.Not.Null);

            Assert.That(flow.ReturnToLevelSelection(), Is.True);
            Assert.That(flow.StartLevel("power_01"), Is.True);
            Assert.That(flow.ActiveTutorial, Is.Not.Null,
                "Leaving an incomplete level starts its tutorial again on re-entry.");

            CampaignTestFixture.Solve(flow.ActiveSession);
            Assert.That(progress.GetLevelProgress("power_01").Completed, Is.True);
            Assert.That(flow.Retry(), Is.True);
            Assert.That(flow.ActiveTutorial, Is.Null,
                "A completed PS_01 replay skips onboarding.");

            Assert.That(flow.ReturnToLevelSelection(), Is.True);
            Assert.That(flow.StartLevel("power_02"), Is.True);
            Assert.That(flow.ActiveTutorial, Is.Not.Null);
            CampaignTestFixture.Solve(flow.ActiveSession);
            Assert.That(flow.Retry(), Is.True);
            Assert.That(flow.ActiveTutorial, Is.Null,
                "A completed PS_02 replay skips onboarding.");

            var freshFlow = new CampaignFlowCoordinator(campaign,
                new CampaignProgressService(campaign), new MemoryStore());
            freshFlow.OpenChapter("power_station");
            freshFlow.StartLevel("power_01");
            Assert.That(freshFlow.ActiveTutorial, Is.Not.Null,
                "Fresh/reset campaign progress naturally restores onboarding.");
        }

        private void CompleteThrough(CampaignProgressService progress, int levelCount)
        {
            for (int index = 0; index < levelCount; index++)
            {
                CampaignLevelEntry entry = campaign.Chapters[0].Levels[index];
                CampaignProgressUpdate update = progress.RecordCompletion(entry.LevelId,
                    CampaignTestFixture.Result(entry.LevelDefinition, 1, 1f, 1));
                Assert.That(update.Accepted, Is.True);
            }
        }

        private sealed class MemoryStore : ICampaignProgressStore
        {
            public string SavePath => "memory://tutorial";

            public CampaignLoadResult Load(CampaignDefinition definition)
            {
                return new CampaignLoadResult(CampaignLoadStatus.NoSaveFound,
                    new CampaignProgressService(definition), new string[0]);
            }

            public CampaignSaveResult Save(CampaignProgressService progress)
            {
                return new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }

            public CampaignSaveResult Delete()
            {
                return new CampaignSaveResult(CampaignSaveStatus.Saved, SavePath);
            }
        }
    }
}

using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Session;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class GameplaySessionTests
    {
        private readonly List<LevelDefinition> temporaryLevels = new List<LevelDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (LevelDefinition level in temporaryLevels)
                Object.DestroyImmediate(level);
            temporaryLevels.Clear();
        }

        [Test]
        public void SuccessfulRotation_IncrementsMovesExactlyOnce()
        {
            GameplaySession session = Session("Levels/M3_Test_02");

            Assert.That(session.InteractWithTile(new GridPosition(1, 0)), Is.True);

            Assert.That(session.MoveCount, Is.EqualTo(1));
            Assert.That(session.Board.GetTile(new GridPosition(1, 0)).Rotation, Is.EqualTo(1));
        }

        [Test]
        public void SuccessfulSwitchToggle_IncrementsMovesExactlyOnce()
        {
            GameplaySession session = Session("Levels/M2_Test_01");

            Assert.That(session.InteractWithTile(new GridPosition(1, 0)), Is.True);

            Assert.That(session.MoveCount, Is.EqualTo(1));
            Assert.That(session.Board.GetTile(new GridPosition(1, 0)).IsSwitchOn, Is.True);
        }

        [Test]
        public void InvalidOrLockedInteraction_DoesNotIncrementMoves()
        {
            GameplaySession session = Session("Levels/M3_Test_02");

            Assert.That(session.InteractWithTile(new GridPosition(0, 0)), Is.False, "Fixed source is not actionable.");
            Assert.That(session.InteractWithTile(new GridPosition(3, 0)), Is.False, "Fixed lamp is not actionable.");
            Assert.That(session.PerformAction(new PuzzleAction(new GridPosition(9, 9),
                PuzzleActionType.RotateClockwise)), Is.False);

            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void RotationUndo_RestoresRotationWithoutChangingCumulativeMoves()
        {
            GameplaySession session = Session("Levels/M3_Test_02");
            session.InteractWithTile(new GridPosition(1, 0));

            Assert.That(session.Undo(), Is.True);

            Assert.That(session.Board.GetTile(new GridPosition(1, 0)).Rotation, Is.Zero);
            Assert.That(session.MoveCount, Is.EqualTo(1));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void SwitchUndo_RestoresPreviousSwitchState()
        {
            GameplaySession session = Session("Levels/M3_Test_02");
            session.InteractWithTile(new GridPosition(2, 0));

            Assert.That(session.Undo(), Is.True);

            Assert.That(session.Board.GetTile(new GridPosition(2, 0)).IsSwitchOn, Is.False);
            Assert.That(session.MoveCount, Is.EqualTo(1));
        }

        [Test]
        public void MultipleUndos_WalkPersistentHistoryInReverseOrder()
        {
            GameplaySession session = Session("Levels/M3_Test_02");
            GridPosition wire = new GridPosition(1, 0);
            GridPosition switchPosition = new GridPosition(2, 0);
            session.InteractWithTile(wire);
            session.InteractWithTile(wire);
            session.InteractWithTile(switchPosition);

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Board.GetTile(switchPosition).IsSwitchOn, Is.False);
            Assert.That(session.Board.GetTile(wire).Rotation, Is.EqualTo(2));

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Board.GetTile(wire).Rotation, Is.EqualTo(1));

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Board.GetTile(wire).Rotation, Is.Zero);
            Assert.That(session.MoveCount, Is.EqualTo(3), "Undo does not erase attempted moves or add new moves.");
        }

        [Test]
        public void UndoWithEmptyHistory_IsSafe()
        {
            GameplaySession session = Session("Levels/M3_Test_02");

            Assert.That(session.Undo(), Is.False);
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.IsCompleted, Is.False);
        }

        [Test]
        public void Undo_RecalculatesTransientPowerAndCompletion()
        {
            GameplaySession session = Session("Levels/M3_Test_02");
            GridPosition wire = new GridPosition(1, 0);
            session.InteractWithTile(wire);
            Assert.That(session.Board.GetTile(wire).IsPowered, Is.True);
            session.InteractWithTile(wire);
            Assert.That(session.Board.GetTile(wire).IsPowered, Is.False);

            session.Undo();

            Assert.That(session.Board.GetTile(wire).IsPowered, Is.True);
            Assert.That(session.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.False);
            Assert.That(session.IsCompleted, Is.False);
            Assert.That(session.CompletionResult, Is.Null);
        }

        [Test]
        public void Completion_DisablesUndoAndFurtherBoardActions()
        {
            GameplaySession session = Session("Levels/M3_Test_01");
            session.InteractWithTile(new GridPosition(1, 0));

            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.Undo(), Is.False);
            Assert.That(session.InteractWithTile(new GridPosition(1, 0)), Is.False);
            Assert.That(session.MoveCount, Is.EqualTo(1));
        }

        [Test]
        public void Restart_RestoresDefinitionStateAndClearsAttemptState()
        {
            GameplaySession session = Session("Levels/M3_Test_02");
            session.AdvanceTime(42f);
            session.InteractWithTile(new GridPosition(1, 0));
            session.InteractWithTile(new GridPosition(2, 0));
            Assert.That(session.IsCompleted, Is.True);

            session.Restart();

            Assert.That(session.Board.GetTile(new GridPosition(1, 0)).Rotation, Is.Zero);
            Assert.That(session.Board.GetTile(new GridPosition(2, 0)).IsSwitchOn, Is.False);
            Assert.That(session.Board.GetTile(new GridPosition(3, 0)).IsPowered, Is.False);
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.ElapsedSeconds, Is.Zero);
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.IsCompleted, Is.False);
            Assert.That(session.CompletionResult, Is.Null);
            Assert.That(session.HintAvailability, Is.EqualTo(HintStatus.HintLocked));
        }

        [Test]
        public void Restart_DoesNotMutateLevelDefinitionAsset()
        {
            LevelDefinition level = Load("Levels/M3_Test_02");
            GameplaySession session = new GameplaySession(level);
            session.InteractWithTile(new GridPosition(1, 0));
            session.InteractWithTile(new GridPosition(2, 0));
            session.Restart();

            Assert.That(level.Tiles[1].startingRotation, Is.Zero);
            Assert.That(level.Tiles[2].startingSwitchOn, Is.False);
        }

        [Test]
        public void HintUnlocksAtExactlyThreeMinutesWithoutRealTimeWaiting()
        {
            GameplaySession session = Session("Levels/M3_Test_02");

            session.AdvanceTime(179.999f);
            Assert.That(session.HintAvailability, Is.EqualTo(HintStatus.HintLocked));
            Assert.That(session.RequestHint().Status, Is.EqualTo(HintStatus.HintLocked));

            session.AdvanceTime(0.001f);
            Assert.That(session.HintAvailability, Is.EqualTo(HintStatus.HintAvailable));
            Assert.That(session.LastHint.Status, Is.EqualTo(HintStatus.HintAvailable));
        }

        [Test]
        public void CompletedSession_StopsAdvancingTime()
        {
            GameplaySession session = Session("Levels/M3_Test_01");
            session.AdvanceTime(12.5f);
            session.InteractWithTile(new GridPosition(1, 0));

            session.AdvanceTime(100f);

            Assert.That(session.ElapsedSeconds, Is.EqualTo(12.5f));
            Assert.That(session.CompletionResult.ElapsedSeconds, Is.EqualTo(12.5f));
        }

        [Test]
        public void Hint_UsesCurrentBoardAndReturnsOnlyNextActionWithoutExecutingIt()
        {
            GameplaySession session = Session("Levels/M3_Test_02");
            session.InteractWithTile(new GridPosition(1, 0));
            session.AdvanceTime(GameplaySession.HintUnlockSeconds);

            HintResult hint = session.RequestHint();

            Assert.That(hint.Status, Is.EqualTo(HintStatus.HintAvailable));
            Assert.That(hint.SuggestedAction, Is.EqualTo(
                new PuzzleAction(new GridPosition(2, 0), PuzzleActionType.ToggleSwitch)));
            Assert.That(session.Board.GetTile(new GridPosition(2, 0)).IsSwitchOn, Is.False,
                "A hint must not execute its suggested action.");
            Assert.That(session.MoveCount, Is.EqualTo(1), "Requesting a hint is not a move.");
            Assert.That(session.HintsUsed, Is.True);
        }

        [Test]
        public void SearchLimitReached_IsNotReportedAsAvailableOrUnsolvable()
        {
            GameplaySession session = Session("Levels/M3_Test_02");
            session.AdvanceTime(GameplaySession.HintUnlockSeconds);

            HintResult hint = session.RequestHint(new PuzzleSolverOptions
            {
                MaximumExploredStates = 1,
                MaximumDepth = 64
            });

            Assert.That(hint.Status, Is.EqualTo(HintStatus.SolverLimitReached));
            Assert.That(hint.SuggestedAction, Is.Null);
            Assert.That(session.HintsUsed, Is.False);
            Assert.That(session.MoveCount, Is.Zero);
        }

        [Test]
        public void AlreadySolvedSession_ReturnsNoHintAction()
        {
            LevelDefinition solved = CreateLevel(2, 1,
                Tile(0, TileType.PowerSource, 0, false),
                Tile(1, TileType.OutputLamp, 0, false));
            var session = new GameplaySession(solved);

            HintResult hint = session.RequestHint();

            Assert.That(session.IsCompleted, Is.True);
            Assert.That(hint.Status, Is.EqualTo(HintStatus.NoHintNeeded));
            Assert.That(hint.SuggestedAction, Is.Null);
        }

        [Test]
        public void CompletionResult_ReportsActualAttemptAndExactInitialBaseline()
        {
            LevelDefinition level = Load("Levels/M3_Test_02");
            var session = new GameplaySession(level);
            session.AdvanceTime(19.25f);
            session.InteractWithTile(new GridPosition(1, 0));
            session.InteractWithTile(new GridPosition(2, 0));

            SessionCompletionResult result = session.CompletionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.CompletedLevel, Is.SameAs(level));
            Assert.That(result.TotalMoves, Is.EqualTo(2));
            Assert.That(result.ElapsedSeconds, Is.EqualTo(19.25f));
            Assert.That(result.OptimalMoves, Is.EqualTo(2));
            Assert.That(result.HintsUsed, Is.False);
            Assert.That(result.StarRating.Status, Is.EqualTo(StarEvaluationStatus.Rated));
            Assert.That(result.StarRating.Stars, Is.EqualTo(3));
        }

        [Test]
        public void CumulativeMovesIncludeUndoneActionsInCompletionResult()
        {
            GameplaySession session = Session("Levels/M3_Test_02");
            GridPosition wire = new GridPosition(1, 0);
            session.InteractWithTile(wire);
            session.Undo();
            session.InteractWithTile(wire);
            session.InteractWithTile(new GridPosition(2, 0));

            Assert.That(session.CompletionResult.TotalMoves, Is.EqualTo(3));
            Assert.That(session.CompletionResult.StarRating.Stars, Is.EqualTo(2));
        }

        [Test]
        public void SolverLimitedBaseline_RemainsExplicitlyUnknownAtCompletion()
        {
            var session = new GameplaySession(Load("Levels/M3_Test_02"), new PuzzleSolverOptions
            {
                MaximumExploredStates = 1,
                MaximumDepth = 64
            });
            session.InteractWithTile(new GridPosition(1, 0));
            session.InteractWithTile(new GridPosition(2, 0));

            Assert.That(session.OptimalSolverStatus, Is.EqualTo(PuzzleSolverStatus.SearchLimitReached));
            Assert.That(session.CompletionResult.OptimalMoves, Is.Null);
            Assert.That(session.CompletionResult.StarRating.Status,
                Is.EqualTo(StarEvaluationStatus.OptimalMovesUnknown));
            Assert.That(session.CompletionResult.StarRating.Stars, Is.Zero);
        }

        [TestCase(1, 1, 3)]
        [TestCase(1, 3, 2)]
        [TestCase(1, 4, 1)]
        [TestCase(2, 2, 3)]
        [TestCase(2, 4, 2)]
        [TestCase(2, 5, 1)]
        [TestCase(10, 12, 3)]
        [TestCase(10, 15, 2)]
        [TestCase(10, 16, 1)]
        public void StarEvaluator_UsesProvisionalThresholds(int optimal, int moves, int expectedStars)
        {
            StarEvaluationResult result = new StarEvaluator().Evaluate(true, moves, optimal);

            Assert.That(result.Status, Is.EqualTo(StarEvaluationStatus.Rated));
            Assert.That(result.Stars, Is.EqualTo(expectedStars));
        }

        [Test]
        public void StarEvaluator_ExposesUnknownAndIncompleteStates()
        {
            StarEvaluationResult unknown = new StarEvaluator().Evaluate(true, 4, null);
            StarEvaluationResult incomplete = new StarEvaluator().Evaluate(false, 0, 1);

            Assert.That(unknown.Status, Is.EqualTo(StarEvaluationStatus.OptimalMovesUnknown));
            Assert.That(unknown.Stars, Is.Zero);
            Assert.That(incomplete.Status, Is.EqualTo(StarEvaluationStatus.LevelNotCompleted));
            Assert.That(incomplete.Stars, Is.Zero);
        }

        [Test]
        public void StarEvaluatorSettings_AreConfigurableWithoutSimulationChanges()
        {
            var evaluator = new StarEvaluator(new StarEvaluationSettings(1d, 2d, 0));

            Assert.That(evaluator.Evaluate(true, 4, 2).Stars, Is.EqualTo(3));
            Assert.That(evaluator.Evaluate(true, 6, 2).Stars, Is.EqualTo(2));
            Assert.That(evaluator.Evaluate(true, 7, 2).Stars, Is.EqualTo(1));
        }

        private static GameplaySession Session(string resourcePath)
        {
            return new GameplaySession(Load(resourcePath));
        }

        private static LevelDefinition Load(string resourcePath)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(resourcePath);
            Assert.That(level, Is.Not.Null, $"Missing level data at {resourcePath}.");
            return level;
        }

        private LevelDefinition CreateLevel(int width, int height, params TileDefinition[] tiles)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.SetData(width, height, tiles);
            temporaryLevels.Add(level);
            return level;
        }

        private static TileDefinition Tile(int x, TileType tileType, int rotation, bool rotatable,
            bool switchOn = false)
        {
            return new TileDefinition(new GridPosition(x, 0), tileType, rotation, rotatable, switchOn);
        }
    }
}

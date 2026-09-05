using System;
using NeonGrid.Data;

namespace NeonGrid.Session
{
    public sealed class SessionCompletionResult
    {
        public LevelDefinition CompletedLevel { get; }
        public int TotalMoves { get; }
        public float ElapsedSeconds { get; }
        public int? OptimalMoves { get; }
        public bool HintsUsed { get; }
        public StarEvaluationResult StarRating { get; }

        internal SessionCompletionResult(LevelDefinition completedLevel, int totalMoves, float elapsedSeconds,
            int? optimalMoves, bool hintsUsed, StarEvaluationResult starRating)
        {
            CompletedLevel = completedLevel ?? throw new ArgumentNullException(nameof(completedLevel));
            TotalMoves = totalMoves;
            ElapsedSeconds = elapsedSeconds;
            OptimalMoves = optimalMoves;
            HintsUsed = hintsUsed;
            StarRating = starRating;
        }
    }
}

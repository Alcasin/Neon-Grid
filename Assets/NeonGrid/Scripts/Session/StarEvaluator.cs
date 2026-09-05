using System;

namespace NeonGrid.Session
{
    public enum StarEvaluationStatus
    {
        Rated,
        LevelNotCompleted,
        OptimalMovesUnknown
    }

    public readonly struct StarEvaluationResult
    {
        public StarEvaluationStatus Status { get; }
        public int Stars { get; }

        internal StarEvaluationResult(StarEvaluationStatus status, int stars)
        {
            Status = status;
            Stars = stars;
        }
    }

    public sealed class StarEvaluationSettings
    {
        public double ThreeStarAllowanceRatio { get; }
        public double TwoStarAllowanceRatio { get; }
        public int TwoStarMinimumAllowance { get; }

        public StarEvaluationSettings(double threeStarAllowanceRatio = 0.20d,
            double twoStarAllowanceRatio = 0.50d, int twoStarMinimumAllowance = 2)
        {
            if (threeStarAllowanceRatio < 0d)
                throw new ArgumentOutOfRangeException(nameof(threeStarAllowanceRatio));
            if (twoStarAllowanceRatio < 0d)
                throw new ArgumentOutOfRangeException(nameof(twoStarAllowanceRatio));
            if (twoStarMinimumAllowance < 0)
                throw new ArgumentOutOfRangeException(nameof(twoStarMinimumAllowance));

            ThreeStarAllowanceRatio = threeStarAllowanceRatio;
            TwoStarAllowanceRatio = twoStarAllowanceRatio;
            TwoStarMinimumAllowance = twoStarMinimumAllowance;
        }
    }

    public sealed class StarEvaluator
    {
        private readonly StarEvaluationSettings settings;

        public StarEvaluator(StarEvaluationSettings settings = null)
        {
            this.settings = settings ?? new StarEvaluationSettings();
        }

        public StarEvaluationResult Evaluate(bool levelCompleted, int moves, int? optimalMoves)
        {
            if (moves < 0) throw new ArgumentOutOfRangeException(nameof(moves));
            if (!levelCompleted)
                return new StarEvaluationResult(StarEvaluationStatus.LevelNotCompleted, 0);
            if (!optimalMoves.HasValue)
                return new StarEvaluationResult(StarEvaluationStatus.OptimalMovesUnknown, 0);
            if (optimalMoves.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(optimalMoves));

            int threeStarThreshold = optimalMoves.Value +
                                     (int)Math.Floor(optimalMoves.Value * settings.ThreeStarAllowanceRatio);
            if (moves <= threeStarThreshold)
                return new StarEvaluationResult(StarEvaluationStatus.Rated, 3);

            int twoStarAllowance = Math.Max(settings.TwoStarMinimumAllowance,
                (int)Math.Ceiling(optimalMoves.Value * settings.TwoStarAllowanceRatio));
            int twoStarThreshold = optimalMoves.Value + twoStarAllowance;
            return new StarEvaluationResult(StarEvaluationStatus.Rated, moves <= twoStarThreshold ? 2 : 1);
        }
    }
}

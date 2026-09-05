using NeonGrid.Simulation;

namespace NeonGrid.Session
{
    public enum HintStatus
    {
        HintLocked,
        HintAvailable,
        NoHintNeeded,
        SolverLimitReached,
        UnsolvableOrInvalid
    }

    public sealed class HintResult
    {
        public HintStatus Status { get; }
        public PuzzleAction? SuggestedAction { get; }

        private HintResult(HintStatus status, PuzzleAction? suggestedAction)
        {
            Status = status;
            SuggestedAction = suggestedAction;
        }

        public static HintResult WithoutAction(HintStatus status)
        {
            return new HintResult(status, null);
        }

        public static HintResult Available(PuzzleAction action)
        {
            return new HintResult(HintStatus.HintAvailable, action);
        }
    }
}

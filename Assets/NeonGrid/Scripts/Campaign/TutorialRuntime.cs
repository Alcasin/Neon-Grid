using NeonGrid.Data;
using NeonGrid.Simulation;

namespace NeonGrid.Campaign
{
    /// <summary>
    /// Attempt-local, monotonic progress through a campaign-authored tutorial sequence.
    /// It observes successful puzzle actions but never changes the puzzle itself.
    /// </summary>
    public sealed class TutorialRuntime
    {
        private readonly LevelTutorialDefinition definition;
        private int currentStepIndex;

        public bool IsActive => definition?.Steps != null &&
                                currentStepIndex < definition.Steps.Count;
        public TutorialStepDefinition CurrentStep => IsActive
            ? definition.Steps[currentStepIndex]
            : null;
        public int CurrentStepIndex => currentStepIndex;

        public TutorialRuntime(LevelTutorialDefinition definition)
        {
            this.definition = definition;
        }

        public bool ObserveSuccessfulAction(PuzzleAction action)
        {
            TutorialStepDefinition step = CurrentStep;
            if (step == null || !step.TargetPosition.Equals(action.Position) ||
                !Matches(step.CompletionCondition, action.ActionType))
                return false;

            currentStepIndex++;
            return true;
        }

        public void Restart()
        {
            currentStepIndex = 0;
        }

        private static bool Matches(TutorialCompletionCondition condition,
            PuzzleActionType actionType)
        {
            switch (condition)
            {
                case TutorialCompletionCondition.RotateClockwise:
                    return actionType == PuzzleActionType.RotateClockwise;
                case TutorialCompletionCondition.ToggleSwitch:
                    return actionType == PuzzleActionType.ToggleSwitch;
                default:
                    return false;
            }
        }
    }
}

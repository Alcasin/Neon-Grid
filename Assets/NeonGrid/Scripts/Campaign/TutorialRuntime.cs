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
            if (step == null || !Matches(step, action))
                return false;

            currentStepIndex++;
            return true;
        }

        public void Restart()
        {
            currentStepIndex = 0;
        }

        private static bool Matches(TutorialStepDefinition step, PuzzleAction action)
        {
            switch (step.CompletionCondition)
            {
                case TutorialCompletionCondition.RotateClockwise:
                    return step.TargetPosition.Equals(action.Position) &&
                           action.ActionType == PuzzleActionType.RotateClockwise;
                case TutorialCompletionCondition.ToggleSwitch:
                    return step.TargetPosition.Equals(action.Position) &&
                           action.ActionType == PuzzleActionType.ToggleSwitch;
                case TutorialCompletionCondition.AnyAcceptedAction:
                    return true;
                default:
                    return false;
            }
        }
    }
}

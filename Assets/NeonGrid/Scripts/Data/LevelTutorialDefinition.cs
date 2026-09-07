using System;
using System.Collections.Generic;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Data
{
    public enum TutorialCompletionCondition
    {
        RotateClockwise,
        ToggleSwitch
    }

    [Serializable]
    public sealed class LevelTutorialDefinition
    {
        [SerializeField] private List<TutorialStepDefinition> steps =
            new List<TutorialStepDefinition>();

        public IReadOnlyList<TutorialStepDefinition> Steps => steps;

        public LevelTutorialDefinition()
        {
        }

        public LevelTutorialDefinition(IEnumerable<TutorialStepDefinition> steps)
        {
            this.steps = steps == null ? null : new List<TutorialStepDefinition>(steps);
        }
    }

    [Serializable]
    public sealed class TutorialStepDefinition
    {
        [SerializeField] private string message;
        [SerializeField] private GridPosition targetPosition;
        [SerializeField] private TutorialCompletionCondition completionCondition;

        public string Message => message;
        public GridPosition TargetPosition => targetPosition;
        public TutorialCompletionCondition CompletionCondition => completionCondition;

        public TutorialStepDefinition(string message, GridPosition targetPosition,
            TutorialCompletionCondition completionCondition)
        {
            this.message = message;
            this.targetPosition = targetPosition;
            this.completionCondition = completionCondition;
        }
    }
}

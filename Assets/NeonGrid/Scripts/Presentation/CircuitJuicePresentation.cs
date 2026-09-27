using NeonGrid.Simulation;

namespace NeonGrid.Presentation
{
    public enum CircuitJuiceEventType
    {
        None,
        TilePressed,
        RotationAccepted,
        InteractionRejected,
        PowerActivated,
        PowerDeactivated,
        SourcePulse,
        ObjectiveActivated,
        SwitchChanged,
        GateActivated,
        GateDeactivated,
        HintTargeted,
        CompletionTriggered
    }

    public enum CircuitJuiceRefreshKind
    {
        Synchronize,
        PlayerAction,
        Undo,
        Restart
    }

    public readonly struct CircuitJuiceTransition
    {
        public static CircuitJuiceTransition Synchronize =>
            new CircuitJuiceTransition(CircuitJuiceRefreshKind.Synchronize, null);
        public static CircuitJuiceTransition Undo =>
            new CircuitJuiceTransition(CircuitJuiceRefreshKind.Undo, null);
        public static CircuitJuiceTransition Restart =>
            new CircuitJuiceTransition(CircuitJuiceRefreshKind.Restart, null);

        public CircuitJuiceRefreshKind Kind { get; }
        public PuzzleAction? Action { get; }

        private CircuitJuiceTransition(CircuitJuiceRefreshKind kind, PuzzleAction? action)
        {
            Kind = kind;
            Action = action;
        }

        public static CircuitJuiceTransition PlayerAction(PuzzleAction action)
        {
            return new CircuitJuiceTransition(CircuitJuiceRefreshKind.PlayerAction, action);
        }
    }
}

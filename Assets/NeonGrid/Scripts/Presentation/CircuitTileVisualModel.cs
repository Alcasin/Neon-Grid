using NeonGrid.Data;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public enum CircuitFunctionalSymbol
    {
        None,
        Source,
        LedObjectiveOff,
        LedObjectiveOn,
        Diode,
        SwitchOpen,
        SwitchClosed,
        AndGate,
        OrGate
    }

    public readonly struct CircuitTileVisualModel
    {
        public CircuitFunctionalSymbol Symbol { get; }
        public CardinalDirection Connections { get; }
        public CardinalDirection EnergizedSides { get; }
        public bool IsPowered { get; }
        public bool IsLocked { get; }
        public bool IsHinted { get; }
        public bool IsTutorialTarget { get; }
        public Color FunctionalColor { get; }

        internal CircuitTileVisualModel(CircuitFunctionalSymbol symbol,
            CardinalDirection connections, CardinalDirection energizedSides, bool isPowered,
            bool isLocked, bool isHinted, bool isTutorialTarget, Color functionalColor)
        {
            Symbol = symbol;
            Connections = connections;
            EnergizedSides = energizedSides;
            IsPowered = isPowered;
            IsLocked = isLocked;
            IsHinted = isHinted;
            IsTutorialTarget = isTutorialTarget;
            FunctionalColor = functionalColor;
        }

        public bool IsSideEnergized(CardinalDirection side) =>
            (EnergizedSides & side) != 0;
    }

    public static class CircuitTileVisualResolver
    {
        public static CircuitTileVisualModel Resolve(CircuitTileState state,
            CircuitVisualThemeDefinition theme, TileHighlightReason highlights)
        {
            CircuitFunctionalSymbol symbol = ResolveSymbol(state);
            CardinalDirection energized = ResolveEnergizedSides(state);
            Color functional = ResolveFunctionalColor(state, theme);
            return new CircuitTileVisualModel(symbol, state.Connections, energized,
                state.IsPowered, IsLockable(state.TileType) && !state.IsRotatable,
                (highlights & TileHighlightReason.Hint) != 0,
                (highlights & TileHighlightReason.Tutorial) != 0, functional);
        }

        private static CircuitFunctionalSymbol ResolveSymbol(CircuitTileState state)
        {
            switch (state.TileType)
            {
                case TileType.PowerSource: return CircuitFunctionalSymbol.Source;
                case TileType.OutputLamp:
                    return state.IsPowered
                        ? CircuitFunctionalSymbol.LedObjectiveOn
                        : CircuitFunctionalSymbol.LedObjectiveOff;
                case TileType.Diode: return CircuitFunctionalSymbol.Diode;
                case TileType.Switch:
                    return state.IsSwitchOn
                        ? CircuitFunctionalSymbol.SwitchClosed
                        : CircuitFunctionalSymbol.SwitchOpen;
                case TileType.AndGate: return CircuitFunctionalSymbol.AndGate;
                case TileType.OrGate: return CircuitFunctionalSymbol.OrGate;
                default: return CircuitFunctionalSymbol.None;
            }
        }

        private static CardinalDirection ResolveEnergizedSides(CircuitTileState state)
        {
            if (state.TileType == TileType.PowerSource) return state.ActiveOutputSides;
            if (state.TileType == TileType.OutputLamp)
                return state.IsPowered ? state.Connections : CardinalDirection.None;
            if (state.TileType == TileType.Diode || state.TileType == TileType.Switch ||
                state.TileType == TileType.AndGate || state.TileType == TileType.OrGate)
                return state.EnergizedInputSides | state.ActiveOutputSides;
            return state.IsPowered ? state.Connections : CardinalDirection.None;
        }

        private static Color ResolveFunctionalColor(CircuitTileState state,
            CircuitVisualThemeDefinition theme)
        {
            switch (state.TileType)
            {
                case TileType.PowerSource: return theme.PowerSource;
                case TileType.OutputLamp:
                    Color led = theme.LedObjective;
                    led.a = state.IsPowered ? 1f : 0.35f;
                    return led;
                case TileType.Diode: return theme.DirectionalAccent;
                case TileType.AndGate: return theme.SecondaryBlue;
                case TileType.OrGate: return theme.DirectionalAccent;
                default: return state.IsPowered ? theme.PoweredEnergy : theme.InactiveConductor;
            }
        }

        private static bool IsLockable(TileType tileType)
        {
            return tileType == TileType.StraightWire || tileType == TileType.CornerWire ||
                   tileType == TileType.TJunction || tileType == TileType.CrossJunction ||
                   tileType == TileType.Diode || tileType == TileType.AndGate ||
                   tileType == TileType.OrGate;
        }
    }
}

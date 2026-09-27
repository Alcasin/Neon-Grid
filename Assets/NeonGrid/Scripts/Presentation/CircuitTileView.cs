using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class CircuitTileView : MonoBehaviour
    {
        private static readonly Color TileBackground = new Color(0.035f, 0.045f, 0.09f);
        private static readonly Color InactiveWire = new Color(0.22f, 0.17f, 0.38f);
        private static readonly Color PoweredWire = new Color(0.05f, 0.95f, 1f);
        private static readonly Color SourceColor = new Color(1f, 0.15f, 0.75f);
        private static readonly Color InactiveLamp = new Color(0.35f, 0.20f, 0.06f);
        private static readonly Color PoweredLamp = new Color(1f, 0.9f, 0.15f);
        private static readonly Color DirectionMarker = new Color(1f, 0.75f, 0.1f);
        private static readonly Color LockMarker = new Color(0.7f, 0.75f, 0.85f);
        private static readonly Color SwitchOnColor = new Color(0.15f, 0.9f, 0.35f);
        private static readonly Color SwitchOffColor = new Color(0.55f, 0.12f, 0.18f);
        private static readonly Color AndGateColor = new Color(0.15f, 0.35f, 0.85f);
        private static readonly Color OrGateColor = new Color(0.85f, 0.35f, 0.12f);
        private static readonly Color HintHighlight = new Color(0.95f, 0.25f, 1f);
        private static readonly Color TutorialHighlight = new Color(0.1f, 1f, 0.55f);

        private readonly List<SpriteRenderer> arms = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
        private SpriteRenderer center;
        private SpriteRenderer background;
        private TextMesh label;
        private TileHighlightReason highlightReasons;
        private CircuitVisualThemeDefinition visualTheme;
        private TechnicalNeonTileRenderer themedRenderer;
        private CircuitTileJuiceView juiceView;
        private PresentationSnapshot presentationSnapshot;
        private bool hasPresentationSnapshot;

        public Color CurrentCircuitColor => themedRenderer != null
            ? themedRenderer.CurrentCircuitColor
            : center != null ? center.color : Color.clear;
        public string CurrentLabel => themedRenderer != null
            ? themedRenderer.CurrentLabel
            : label != null ? label.text : string.Empty;
        public bool IsHintHighlighted => (highlightReasons & TileHighlightReason.Hint) != 0;
        public bool IsTutorialHighlighted => (highlightReasons & TileHighlightReason.Tutorial) != 0;
        public TileHighlightReason HighlightReasons => highlightReasons;
        public bool IsUsingVisualTheme => themedRenderer != null;
        public CircuitVisualThemeDefinition VisualTheme => visualTheme;
        public CircuitFunctionalSymbol CurrentFunctionalSymbol => themedRenderer != null
            ? themedRenderer.CurrentModel.Symbol
            : CircuitFunctionalSymbol.None;
        public bool IsVisualLocked => themedRenderer != null &&
                                      themedRenderer.CurrentModel.IsLocked;
        public bool IsUnderlyingPowered => themedRenderer != null &&
                                           themedRenderer.CurrentModel.IsPowered;
        public float RotatingContentDegrees => themedRenderer?.RotatingContentDegrees ?? 0f;
        public CircuitTileJuiceView JuiceView => juiceView;
        public TileType CurrentTileType => hasPresentationSnapshot
            ? presentationSnapshot.TileType
            : TileType.Empty;

        public void Build(Sprite squareSprite)
        {
            Build(squareSprite, null);
        }

        public void Build(Sprite squareSprite, CircuitVisualThemeDefinition theme)
        {
            Build(squareSprite, theme, null, null);
        }

        public void Build(Sprite squareSprite, CircuitVisualThemeDefinition theme,
            CircuitJuiceDefinition juiceDefinition, CircuitJuiceCoordinator juiceCoordinator)
        {
            visualTheme = theme;
            if (visualTheme != null && visualTheme.IsConfigured)
            {
                Transform visualRoot = transform;
                bool useJuice = juiceDefinition != null && juiceDefinition.IsConfigured &&
                                juiceCoordinator != null;
                if (useJuice)
                {
                    var juiceObject = new GameObject("Juice Visual Root");
                    juiceObject.transform.SetParent(transform, false);
                    visualRoot = juiceObject.transform;
                }
                themedRenderer = new TechnicalNeonTileRenderer(visualRoot, squareSprite,
                    visualTheme);
                if (useJuice)
                    juiceView = juiceCoordinator.Register(visualRoot, themedRenderer);
                return;
            }

            background = CreatePart("Background", squareSprite, Vector3.zero, new Vector3(0.9f, 0.9f, 1f), 0);
            center = CreatePart("Center", squareSprite, Vector3.zero, new Vector3(0.30f, 0.30f, 1f), 2);
            var labelObject = new GameObject("Component Label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 32;
            label.characterSize = 0.045f;
            label.color = Color.white;
            labelObject.GetComponent<MeshRenderer>().sortingOrder = 4;
        }

        public void Refresh(CircuitTileState state, Sprite squareSprite)
        {
            Refresh(state, squareSprite, CircuitJuiceTransition.Synchronize);
        }

        public void Refresh(CircuitTileState state, Sprite squareSprite,
            CircuitJuiceTransition transition)
        {
            if (themedRenderer != null)
            {
                bool rotationChanged = hasPresentationSnapshot &&
                                       presentationSnapshot.Rotation != state.Rotation;
                bool animateRotation = juiceView != null && rotationChanged &&
                                       transition.Kind != CircuitJuiceRefreshKind.Synchronize &&
                                       transition.Kind != CircuitJuiceRefreshKind.Restart;
                themedRenderer.Refresh(state, !animateRotation);
                PresentJuiceTransition(state, transition, rotationChanged);
                presentationSnapshot = PresentationSnapshot.Capture(state);
                hasPresentationSnapshot = true;
                return;
            }

            background.color = TileBackground;
            center.gameObject.SetActive(state.TileType != TileType.Empty);

            foreach (SpriteRenderer arm in arms) DestroyPart(arm.gameObject);
            arms.Clear();
            foreach (SpriteRenderer marker in markers) DestroyPart(marker.gameObject);
            markers.Clear();

            Color circuitColor = GetCircuitColor(state);
            center.color = circuitColor;
            UpdateLabel(state);

            foreach (CardinalDirection direction in DirectionUtility.CardinalDirections)
            {
                if ((state.Connections & direction) == 0) continue;
                CreateArm(direction, squareSprite, GetPortColor(state, direction));
            }

            if (state.TileType == TileType.Diode)
                CreateDiodeOutputMarker(state, squareSprite);

            if (!state.IsRotatable && IsLockableCircuitTile(state.TileType))
                CreateLockMarker(squareSprite);

            if (state.TileType == TileType.PowerSource)
                center.transform.localScale = new Vector3(0.48f, 0.48f, 1f);
            else if (state.TileType == TileType.OutputLamp)
                center.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            else if (state.TileType == TileType.Switch || state.TileType == TileType.AndGate ||
                     state.TileType == TileType.OrGate)
                center.transform.localScale = new Vector3(0.55f, 0.38f, 1f);
            else
                center.transform.localScale = new Vector3(0.30f, 0.30f, 1f);

            presentationSnapshot = PresentationSnapshot.Capture(state);
            hasPresentationSnapshot = true;
        }

        public void PresentPressed()
        {
            juiceView?.PresentPress();
        }

        public void PresentRejected()
        {
            if (IsVisualLocked) juiceView?.PresentRejected();
        }

        public void PresentSourcePulse()
        {
            if (juiceView == null || CurrentTileType != TileType.PowerSource ||
                !juiceView.Definition.ComponentFeedback) return;
            juiceView.PresentPulse(CircuitJuiceEventType.SourcePulse,
                juiceView.Definition.PowerActivationDuration,
                juiceView.Definition.SourcePulseScale);
        }

        public void PresentHintTargeted()
        {
            if (juiceView == null || !juiceView.Definition.HintFeedback) return;
            juiceView.PresentPulse(CircuitJuiceEventType.HintTargeted,
                juiceView.Definition.HintEmphasisDuration,
                juiceView.Definition.HintPulseScale);
        }

        public void PresentCompletion()
        {
            juiceView?.MarkCompletion();
        }

        private void PresentJuiceTransition(CircuitTileState state,
            CircuitJuiceTransition transition, bool rotationChanged)
        {
            if (juiceView == null)
                return;
            if (!hasPresentationSnapshot)
            {
                juiceView.SetInitialRotation(state.Rotation);
                return;
            }
            if (transition.Kind == CircuitJuiceRefreshKind.Restart)
            {
                juiceView.CancelAndSnap(state.Rotation);
                return;
            }

            if (rotationChanged)
            {
                bool clockwise = transition.Kind == CircuitJuiceRefreshKind.PlayerAction &&
                                 transition.Action.HasValue &&
                                 transition.Action.Value.ActionType ==
                                 PuzzleActionType.RotateClockwise;
                juiceView.RetargetRotation(state.Rotation, clockwise);
            }

            CircuitJuiceDefinition definition = juiceView.Definition;
            if (definition.PowerFeedback && presentationSnapshot.IsPowered != state.IsPowered)
                juiceView.PresentPulse(state.IsPowered
                        ? CircuitJuiceEventType.PowerActivated
                        : CircuitJuiceEventType.PowerDeactivated,
                    state.IsPowered ? definition.PowerActivationDuration :
                    definition.PowerDeactivationDuration,
                    state.IsPowered ? definition.PowerActivationScale :
                    definition.PowerDeactivationScale);

            if (!definition.ComponentFeedback) return;
            if (state.TileType == TileType.OutputLamp &&
                !presentationSnapshot.IsPowered && state.IsPowered)
                juiceView.PresentPulse(CircuitJuiceEventType.ObjectiveActivated,
                    definition.ObjectiveActivationDuration, definition.ObjectivePulseScale);
            else if (state.TileType == TileType.Switch &&
                     presentationSnapshot.IsSwitchOn != state.IsSwitchOn)
                juiceView.PresentPulse(CircuitJuiceEventType.SwitchChanged,
                    definition.PowerActivationDuration, definition.ComponentPulseScale);
            else if (state.TileType == TileType.AndGate || state.TileType == TileType.OrGate)
            {
                bool active = state.ActiveOutputSides != CardinalDirection.None;
                if (presentationSnapshot.HasActiveOutput != active)
                    juiceView.PresentPulse(active
                            ? CircuitJuiceEventType.GateActivated
                            : CircuitJuiceEventType.GateDeactivated,
                        active ? definition.PowerActivationDuration :
                        definition.PowerDeactivationDuration,
                        active ? definition.ComponentPulseScale :
                        definition.PowerDeactivationScale);
            }
        }

        public void SetHintHighlighted(bool highlighted)
        {
            SetHighlightReasons(highlighted
                ? highlightReasons | TileHighlightReason.Hint
                : highlightReasons & ~TileHighlightReason.Hint);
        }

        public void SetHighlightReasons(TileHighlightReason reasons)
        {
            highlightReasons = reasons;
            if (themedRenderer != null)
            {
                themedRenderer.SetHighlights(reasons);
                return;
            }
            if (highlightReasons == TileHighlightReason.None && background != null)
                background.color = TileBackground;
        }

        private void Update()
        {
            if (themedRenderer != null)
            {
                themedRenderer.Tick(Time.unscaledTime);
                return;
            }
            if (highlightReasons == TileHighlightReason.None || background == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            // Tutorial takes visual priority if tutorial and hint target the same tile.
            Color highlight = (highlightReasons & TileHighlightReason.Tutorial) != 0
                ? TutorialHighlight
                : HintHighlight;
            background.color = Color.Lerp(TileBackground, highlight, 0.3f + pulse * 0.5f);
        }

        private void UpdateLabel(CircuitTileState state)
        {
            switch (state.TileType)
            {
                case TileType.Switch:
                    label.text = state.IsSwitchOn ? "ON" : "OFF";
                    break;
                case TileType.AndGate:
                    label.text = "AND";
                    break;
                case TileType.OrGate:
                    label.text = "OR";
                    break;
                default:
                    label.text = string.Empty;
                    break;
            }
        }

        private Color GetPortColor(CircuitTileState state, CardinalDirection direction)
        {
            if (state.TileType == TileType.PowerSource) return SourceColor;
            if (state.TileType == TileType.OutputLamp) return state.IsPowered ? PoweredLamp : InactiveLamp;
            if (state.TileType == TileType.Switch && !state.IsSwitchOn) return InactiveWire;

            bool inputEnergized = (state.EnergizedInputSides & direction) != 0;
            bool outputActive = (state.ActiveOutputSides & direction) != 0;
            if (state.TileType == TileType.Diode || state.TileType == TileType.Switch ||
                state.TileType == TileType.AndGate || state.TileType == TileType.OrGate)
                return inputEnergized || outputActive ? PoweredWire : InactiveWire;

            return state.IsPowered ? PoweredWire : InactiveWire;
        }

        private void CreateDiodeOutputMarker(CircuitTileState state, Sprite sprite)
        {
            CardinalDirection output = TilePowerFlow.GetOutputSides(state.TileType, state.Rotation);
            Vector3 position;
            switch (output)
            {
                case CardinalDirection.Up: position = new Vector3(0f, 0.23f, 0f); break;
                case CardinalDirection.Right: position = new Vector3(0.23f, 0f, 0f); break;
                case CardinalDirection.Down: position = new Vector3(0f, -0.23f, 0f); break;
                default: position = new Vector3(-0.23f, 0f, 0f); break;
            }

            SpriteRenderer marker = CreatePart("Diode Output", sprite, position, new Vector3(0.16f, 0.16f, 1f), 3);
            marker.color = DirectionMarker;
            markers.Add(marker);
        }

        private void CreateLockMarker(Sprite sprite)
        {
            SpriteRenderer marker = CreatePart("Lock Indicator", sprite,
                new Vector3(-0.32f, 0.32f, 0f), new Vector3(0.13f, 0.13f, 1f), 3);
            marker.color = LockMarker;
            markers.Add(marker);
        }

        private static bool IsLockableCircuitTile(TileType tileType)
        {
            return tileType == TileType.StraightWire || tileType == TileType.CornerWire ||
                   tileType == TileType.TJunction || tileType == TileType.CrossJunction ||
                   tileType == TileType.Diode || tileType == TileType.AndGate ||
                   tileType == TileType.OrGate;
        }

        private readonly struct PresentationSnapshot
        {
            public TileType TileType { get; }
            public int Rotation { get; }
            public bool IsPowered { get; }
            public bool IsSwitchOn { get; }
            public bool HasActiveOutput { get; }

            private PresentationSnapshot(TileType tileType, int rotation, bool isPowered,
                bool isSwitchOn, bool hasActiveOutput)
            {
                TileType = tileType;
                Rotation = rotation;
                IsPowered = isPowered;
                IsSwitchOn = isSwitchOn;
                HasActiveOutput = hasActiveOutput;
            }

            public static PresentationSnapshot Capture(CircuitTileState state)
            {
                return new PresentationSnapshot(state.TileType, state.Rotation,
                    state.IsPowered, state.IsSwitchOn,
                    state.ActiveOutputSides != CardinalDirection.None);
            }
        }

        private Color GetCircuitColor(CircuitTileState state)
        {
            if (state.TileType == TileType.PowerSource) return SourceColor;
            if (state.TileType == TileType.OutputLamp) return state.IsPowered ? PoweredLamp : InactiveLamp;
            if (state.TileType == TileType.Switch) return state.IsSwitchOn ? SwitchOnColor : SwitchOffColor;
            if (state.TileType == TileType.AndGate) return AndGateColor;
            if (state.TileType == TileType.OrGate) return OrGateColor;
            return state.IsPowered ? PoweredWire : InactiveWire;
        }

        private void CreateArm(CardinalDirection direction, Sprite sprite, Color color)
        {
            bool vertical = direction == CardinalDirection.Up || direction == CardinalDirection.Down;
            float x = direction == CardinalDirection.Right ? 0.3f : direction == CardinalDirection.Left ? -0.3f : 0f;
            float y = direction == CardinalDirection.Up ? 0.3f : direction == CardinalDirection.Down ? -0.3f : 0f;
            Vector3 scale = vertical ? new Vector3(0.16f, 0.6f, 1f) : new Vector3(0.6f, 0.16f, 1f);
            SpriteRenderer arm = CreatePart("Connection", sprite, new Vector3(x, y, 0f), scale, 1);
            arm.color = color;
            arms.Add(arm);
        }

        private SpriteRenderer CreatePart(string objectName, Sprite sprite, Vector3 localPosition, Vector3 localScale, int order)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void DestroyPart(GameObject part)
        {
            if (Application.isPlaying)
                Destroy(part);
            else
                DestroyImmediate(part);
        }
    }
}

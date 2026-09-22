using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    internal sealed class TechnicalNeonTileRenderer
    {
        private readonly Transform root;
        private readonly Sprite squareSprite;
        private readonly CircuitVisualThemeDefinition theme;
        private readonly Dictionary<CardinalDirection, PortLayers> ports =
            new Dictionary<CardinalDirection, PortLayers>();
        private readonly List<SpriteRenderer> hintRim = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> tutorialRim = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> hotCenterParts = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> ledEmitters = new List<SpriteRenderer>();

        private Transform rotatingContent;
        private SpriteRenderer circuitCenter;
        private SpriteRenderer functionalCore;
        private SpriteRenderer ledGlow;
        private GameObject switchOpenGeometry;
        private GameObject switchClosedGeometry;
        private TextMesh label;
        private bool isBuilt;
        private TileHighlightReason highlights;

        public CircuitTileVisualModel CurrentModel { get; private set; }
        public Color CurrentCircuitColor => circuitCenter != null
            ? circuitCenter.color
            : Color.clear;
        public string CurrentLabel => label != null ? label.text : string.Empty;
        public float RotatingContentDegrees => rotatingContent != null
            ? rotatingContent.localEulerAngles.z
            : 0f;

        public TechnicalNeonTileRenderer(Transform root, Sprite squareSprite,
            CircuitVisualThemeDefinition theme)
        {
            this.root = root;
            this.squareSprite = squareSprite;
            this.theme = theme;
            BuildStableHousing();
        }

        public void Refresh(CircuitTileState state)
        {
            if (!isBuilt) BuildCircuit(state);
            CurrentModel = CircuitTileVisualResolver.Resolve(state, theme, highlights);
            rotatingContent.localRotation = Quaternion.Euler(0f, 0f, -90f * state.Rotation);

            foreach (KeyValuePair<CardinalDirection, PortLayers> pair in ports)
            {
                CardinalDirection worldDirection = pair.Key.RotateClockwise(state.Rotation);
                bool energized = CurrentModel.IsSideEnergized(worldDirection);
                pair.Value.SetEnergized(energized, theme);
            }

            circuitCenter.color = state.TileType == TileType.PowerSource ||
                                  state.TileType == TileType.OutputLamp ||
                                  state.TileType == TileType.Diode ||
                                  state.TileType == TileType.AndGate ||
                                  state.TileType == TileType.OrGate
                ? CurrentModel.FunctionalColor
                : state.IsPowered ? theme.PoweredEnergy : theme.InactiveConductor;
            foreach (SpriteRenderer hot in hotCenterParts)
                hot.gameObject.SetActive(state.IsPowered && state.TileType != TileType.OutputLamp);

            if (functionalCore != null)
                functionalCore.color = CurrentModel.FunctionalColor;
            if (state.TileType == TileType.OutputLamp)
                UpdateLedObjective(state.IsPowered);
            if (switchOpenGeometry != null)
                switchOpenGeometry.SetActive(!state.IsSwitchOn);
            if (switchClosedGeometry != null)
                switchClosedGeometry.SetActive(state.IsSwitchOn);
            UpdateLabel(state);
            ApplyOverlayVisibility();
        }

        public void SetHighlights(TileHighlightReason reasons)
        {
            highlights = reasons;
            ApplyOverlayVisibility();
        }

        public void Tick(float unscaledTime)
        {
            if (highlights == TileHighlightReason.None) return;
            float pulse = 0.55f + 0.25f * (0.5f + 0.5f * Mathf.Sin(unscaledTime * 6f));
            UpdateRimAlpha(hintRim, theme.Hint, pulse);
            UpdateRimAlpha(tutorialRim, theme.Success, pulse);
        }

        private void BuildStableHousing()
        {
            Sprite housing = theme.HousingSprite != null ? theme.HousingSprite : squareSprite;
            CreatePart(root, "Housing", housing, Vector3.zero, new Vector3(0.92f, 0.92f, 1f),
                theme.Board, 0);
            Color railColor = theme.SecondaryBlue;
            railColor.a = 0.28f;
            CreateBorder(root, "Housing Rail", 0.88f, 0.025f, railColor, 1, null);
            CreateBorder(root, "Hint Rim", 0.96f, 0.028f, theme.Hint, 8, hintRim);
            CreateBorder(root, "Tutorial Rim", 0.96f, 0.028f, theme.Success, 9, tutorialRim);
            ApplyOverlayVisibility();
        }

        private void BuildCircuit(CircuitTileState state)
        {
            isBuilt = true;
            var circuitObject = new GameObject("Rotating Circuit");
            circuitObject.transform.SetParent(root, false);
            rotatingContent = circuitObject.transform;

            if (state.TileType == TileType.Empty)
            {
                circuitCenter = CreatePart(rotatingContent, "Empty Center", squareSprite,
                    Vector3.zero, Vector3.zero, Color.clear, 2);
                return;
            }

            CardinalDirection baseConnections = TileConnections.GetBaseConnections(state.TileType);
            foreach (CardinalDirection direction in DirectionUtility.CardinalDirections)
                if ((baseConnections & direction) != 0)
                    ports.Add(direction, BuildPort(direction,
                        state.TileType == TileType.Switch));

            float hubSize = theme.ToTileUnits(theme.ConduitWidth * 1.35f);
            circuitCenter = CreatePart(rotatingContent, "Circuit Base Hub", squareSprite,
                Vector3.zero, new Vector3(hubSize, hubSize, 1f), theme.InactiveConductor, 3);
            float hotSize = theme.ToTileUnits(theme.PoweredCoreWidth * 1.25f);
            SpriteRenderer hotCenter = CreatePart(rotatingContent, "Powered Hot Hub", squareSprite,
                Vector3.zero, new Vector3(hotSize, hotSize, 1f), theme.PoweredHotCore, 6);
            hotCenterParts.Add(hotCenter);

            switch (state.TileType)
            {
                case TileType.PowerSource:
                    BuildSource();
                    break;
                case TileType.OutputLamp:
                    BuildLedObjective();
                    break;
                case TileType.Diode:
                    BuildDiode();
                    break;
                case TileType.Switch:
                    BuildSwitch();
                    break;
                case TileType.AndGate:
                    BuildAndGate();
                    break;
                case TileType.OrGate:
                    BuildOrGate();
                    break;
            }

            if (!state.IsRotatable && IsLockable(state.TileType)) BuildCornerClamps();
        }

        private PortLayers BuildPort(CardinalDirection direction, bool leaveSwitchGap)
        {
            bool vertical = direction == CardinalDirection.Up ||
                            direction == CardinalDirection.Down;
            Vector2 outward = CircuitVisualGeometry.GetConnectorCenterNormalized(direction).normalized;
            float length = leaveSwitchGap ? 0.36f : 0.5f;
            float centerDistance = leaveSwitchGap ? 0.32f : 0.25f;
            Vector3 position = outward * centerDistance;
            float conduit = theme.ToTileUnits(theme.ConduitWidth);
            float energy = theme.ToTileUnits(
                (theme.ConduitWidth + theme.PoweredCoreWidth) * 0.5f);
            float core = theme.ToTileUnits(theme.PoweredCoreWidth);
            float halo = theme.ToTileUnits(theme.ConduitWidth + theme.HaloWidth);
            Vector3 Scale(float width) => vertical
                ? new Vector3(width, length, 1f)
                : new Vector3(length, width, 1f);

            Sprite conductor = theme.ConductorSprite != null
                ? theme.ConductorSprite
                : squareSprite;
            SpriteRenderer glow = CreatePart(rotatingContent, "Energy Halo", conductor,
                position, Scale(halo), WithAlpha(theme.PoweredEnergy, 0.18f), 2);
            SpriteRenderer baseLayer = CreatePart(rotatingContent, "Circuit Base", conductor,
                position, Scale(conduit), theme.InactiveConductor, 3);
            SpriteRenderer energyLayer = CreatePart(rotatingContent, "Powered Energy", conductor,
                position, Scale(energy), theme.PoweredEnergy, 4);
            SpriteRenderer hotLayer = CreatePart(rotatingContent, "Powered Hot Core", conductor,
                position, Scale(core), theme.PoweredHotCore, 5);
            return new PortLayers(baseLayer, glow, energyLayer, hotLayer);
        }

        private void BuildSource()
        {
            float core = 0.36f;
            functionalCore = CreatePart(rotatingContent, "Source Magenta Core", squareSprite,
                Vector3.zero, new Vector3(core, core, 1f), theme.PowerSource, 6);
            Color reinforcement = theme.SecondaryBlue;
            reinforcement.a = 0.55f;
            CreateBorder(rotatingContent, "Source Reinforcement", 0.58f, 0.055f,
                reinforcement, 5, null);
            CreatePart(rotatingContent, "Source Energy Glyph Upper", squareSprite,
                new Vector3(0.035f, 0.08f, 0f), new Vector3(0.075f, 0.20f, 1f),
                theme.PoweredHotCore, 7, -22f);
            CreatePart(rotatingContent, "Source Energy Glyph Lower", squareSprite,
                new Vector3(-0.035f, -0.08f, 0f), new Vector3(0.075f, 0.20f, 1f),
                theme.PoweredHotCore, 7, -22f);
        }

        private void BuildLedObjective()
        {
            ledGlow = CreatePart(rotatingContent, "LED Objective Halo", squareSprite,
                Vector3.zero, new Vector3(0.68f, 0.58f, 1f),
                WithAlpha(theme.LedObjective, 0.16f), 5);
            functionalCore = CreatePart(rotatingContent, "LED Diffuser", squareSprite,
                Vector3.zero, new Vector3(0.52f, 0.42f, 1f), theme.LedObjective, 6);
            Color frame = theme.InactiveConductor;
            CreateBorder(rotatingContent, "LED Technical Frame", 0.66f, 0.05f, frame, 7, null);
            for (int index = 0; index < 4; index++)
            {
                float x = -0.15f + index * 0.10f;
                ledEmitters.Add(CreatePart(rotatingContent, $"LED Emitter {index + 1}",
                    squareSprite, new Vector3(x, 0f, 0f), new Vector3(0.055f, 0.18f, 1f),
                    theme.LedObjective, 8));
            }
        }

        private void BuildDiode()
        {
            CreatePart(rotatingContent, "Diode Chevron Upper", squareSprite,
                new Vector3(0.025f, 0.075f, 0f), new Vector3(0.24f, 0.045f, 1f),
                theme.DirectionalAccent, 7, -35f);
            CreatePart(rotatingContent, "Diode Chevron Lower", squareSprite,
                new Vector3(0.025f, -0.075f, 0f), new Vector3(0.24f, 0.045f, 1f),
                theme.DirectionalAccent, 7, 35f);
            CreatePart(rotatingContent, "Diode Blocking Bar", squareSprite,
                new Vector3(0.15f, 0f, 0f), new Vector3(0.045f, 0.32f, 1f),
                theme.DirectionalAccent, 7);
        }

        private void BuildSwitch()
        {
            CreatePart(rotatingContent, "Switch Left Contact", squareSprite,
                new Vector3(-0.20f, 0f, 0f), new Vector3(0.11f, 0.11f, 1f),
                theme.SecondaryBlue, 7);
            CreatePart(rotatingContent, "Switch Right Contact", squareSprite,
                new Vector3(0.20f, 0f, 0f), new Vector3(0.11f, 0.11f, 1f),
                theme.SecondaryBlue, 7);
            switchOpenGeometry = CreatePart(rotatingContent, "Switch Open Contact", squareSprite,
                new Vector3(0f, 0.10f, 0f), new Vector3(0.43f, 0.07f, 1f),
                theme.InactiveConductor, 7, 30f).gameObject;
            switchClosedGeometry = CreatePart(rotatingContent, "Switch Closed Contact", squareSprite,
                Vector3.zero, new Vector3(0.44f, 0.07f, 1f),
                theme.PoweredEnergy, 7).gameObject;
        }

        private void BuildAndGate()
        {
            Color color = theme.SecondaryBlue;
            Color body = color;
            body.a = 0.58f;
            CreatePart(rotatingContent, "AND Gate Body", squareSprite,
                new Vector3(0f, -0.03f, 0f), new Vector3(0.48f, 0.46f, 1f), body, 6);
            CreatePart(rotatingContent, "AND Gate Flat Input", squareSprite,
                new Vector3(0f, -0.25f, 0f), new Vector3(0.50f, 0.075f, 1f), color, 7);
            CreatePart(rotatingContent, "AND Gate Shoulder", squareSprite,
                new Vector3(-0.20f, -0.02f, 0f), new Vector3(0.075f, 0.42f, 1f), color, 7);
            CreatePart(rotatingContent, "AND Gate Rounded Output", squareSprite,
                new Vector3(0f, 0.18f, 0f), new Vector3(0.40f, 0.16f, 1f), color, 7);
            CreatePart(rotatingContent, "AND Gate Lower Edge", squareSprite,
                new Vector3(0.20f, -0.02f, 0f), new Vector3(0.075f, 0.42f, 1f), color, 7);
        }

        private void BuildOrGate()
        {
            Color color = theme.DirectionalAccent;
            Color body = color;
            body.a = 0.50f;
            CreatePart(rotatingContent, "OR Gate Body", squareSprite,
                new Vector3(0f, -0.01f, 0f), new Vector3(0.46f, 0.46f, 1f), body, 6);
            CreatePart(rotatingContent, "OR Gate Curved Input", squareSprite,
                new Vector3(0f, -0.21f, 0f), new Vector3(0.50f, 0.085f, 1f), color, 7);
            CreatePart(rotatingContent, "OR Gate Upper Wing", squareSprite,
                new Vector3(-0.13f, 0.04f, 0f), new Vector3(0.075f, 0.50f, 1f), color, 7, -28f);
            CreatePart(rotatingContent, "OR Gate Lower Wing", squareSprite,
                new Vector3(0.13f, 0.04f, 0f), new Vector3(0.075f, 0.50f, 1f), color, 7, 28f);
            CreatePart(rotatingContent, "OR Gate Point", squareSprite,
                new Vector3(0f, 0.25f, 0f), new Vector3(0.12f, 0.12f, 1f), color, 7, 45f);
        }

        private void UpdateLedObjective(bool isPowered)
        {
            if (ledGlow != null) ledGlow.gameObject.SetActive(isPowered);
            Color emitterColor = isPowered
                ? theme.PoweredHotCore
                : WithAlpha(theme.LedObjective, 0.52f);
            foreach (SpriteRenderer emitter in ledEmitters)
                emitter.color = emitterColor;
        }

        private void BuildCornerClamps()
        {
            Color clamp = theme.InactiveConductor;
            CreatePart(root, "Lock Clamp Top Left", squareSprite,
                new Vector3(-0.37f, 0.37f, 0f), new Vector3(0.16f, 0.055f, 1f), clamp, 7);
            CreatePart(root, "Lock Clamp Top Left Bolt", squareSprite,
                new Vector3(-0.37f, 0.34f, 0f), new Vector3(0.05f, 0.05f, 1f),
                theme.SecondaryBlue, 8);
            CreatePart(root, "Lock Clamp Bottom Right", squareSprite,
                new Vector3(0.37f, -0.37f, 0f), new Vector3(0.16f, 0.055f, 1f), clamp, 7);
            CreatePart(root, "Lock Clamp Bottom Right Bolt", squareSprite,
                new Vector3(0.37f, -0.34f, 0f), new Vector3(0.05f, 0.05f, 1f),
                theme.SecondaryBlue, 8);
        }

        private void CreateLabel(string value, float characterSize, int sortingOrder)
        {
            var labelObject = new GameObject($"{value} Label");
            labelObject.transform.SetParent(rotatingContent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 32;
            label.characterSize = characterSize;
            label.color = theme.PoweredHotCore;
            label.text = value;
            labelObject.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
        }

        private void UpdateLabel(CircuitTileState state)
        {
            if (label == null) return;
            if (state.TileType == TileType.OutputLamp) label.text = "LED";
            else if (state.TileType == TileType.AndGate) label.text = "AND";
            else if (state.TileType == TileType.OrGate) label.text = "OR";
        }

        private void ApplyOverlayVisibility()
        {
            bool hint = (highlights & TileHighlightReason.Hint) != 0;
            bool tutorial = (highlights & TileHighlightReason.Tutorial) != 0;
            foreach (SpriteRenderer part in hintRim) part.gameObject.SetActive(hint && !tutorial);
            foreach (SpriteRenderer part in tutorialRim) part.gameObject.SetActive(tutorial);
        }

        private static void UpdateRimAlpha(IReadOnlyList<SpriteRenderer> rim, Color color,
            float alpha)
        {
            color.a = alpha;
            foreach (SpriteRenderer part in rim)
                if (part.gameObject.activeSelf) part.color = color;
        }

        private void CreateBorder(Transform parent, string name, float extent, float thickness,
            Color color, int order, ICollection<SpriteRenderer> collection)
        {
            float offset = extent * 0.5f - thickness * 0.5f;
            SpriteRenderer top = CreatePart(parent, $"{name} Top", squareSprite,
                new Vector3(0f, offset, 0f), new Vector3(extent, thickness, 1f), color, order);
            SpriteRenderer bottom = CreatePart(parent, $"{name} Bottom", squareSprite,
                new Vector3(0f, -offset, 0f), new Vector3(extent, thickness, 1f), color, order);
            SpriteRenderer left = CreatePart(parent, $"{name} Left", squareSprite,
                new Vector3(-offset, 0f, 0f), new Vector3(thickness, extent, 1f), color, order);
            SpriteRenderer right = CreatePart(parent, $"{name} Right", squareSprite,
                new Vector3(offset, 0f, 0f), new Vector3(thickness, extent, 1f), color, order);
            collection?.Add(top);
            collection?.Add(bottom);
            collection?.Add(left);
            collection?.Add(right);
        }

        private static SpriteRenderer CreatePart(Transform parent, string name, Sprite sprite,
            Vector3 position, Vector3 scale, Color color, int order, float rotation = 0f)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            child.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static bool IsLockable(TileType tileType)
        {
            return tileType == TileType.StraightWire || tileType == TileType.CornerWire ||
                   tileType == TileType.TJunction || tileType == TileType.CrossJunction ||
                   tileType == TileType.Diode || tileType == TileType.AndGate ||
                   tileType == TileType.OrGate;
        }

        private readonly struct PortLayers
        {
            private readonly SpriteRenderer baseLayer;
            private readonly SpriteRenderer glow;
            private readonly SpriteRenderer energy;
            private readonly SpriteRenderer hotCore;

            public PortLayers(SpriteRenderer baseLayer, SpriteRenderer glow,
                SpriteRenderer energy, SpriteRenderer hotCore)
            {
                this.baseLayer = baseLayer;
                this.glow = glow;
                this.energy = energy;
                this.hotCore = hotCore;
            }

            public void SetEnergized(bool energized, CircuitVisualThemeDefinition theme)
            {
                baseLayer.color = theme.InactiveConductor;
                glow.gameObject.SetActive(energized);
                energy.gameObject.SetActive(energized);
                hotCore.gameObject.SetActive(energized);
            }
        }
    }
}

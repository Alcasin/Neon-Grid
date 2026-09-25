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
        private bool UsesProductionTreatment => theme.UsesProductionTreatment;

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
            if (UsesProductionTreatment)
            {
                Color socket = Color.Lerp(theme.Board, theme.Background, 0.72f);
                Color lip = theme.InactiveConductor;
                lip.a = 0.58f;
                Color recess = Color.Lerp(theme.Board, theme.Background, 0.36f);
                Color edge = theme.SecondaryBlue;
                edge.a = 0.22f;
                CreatePart(root, "Cell Socket", squareSprite, Vector3.zero,
                    new Vector3(0.98f, 0.98f, 1f), socket, -2);
                CreatePart(root, "Socket Inner Lip", squareSprite,
                    new Vector3(0f, -0.012f, 0f), new Vector3(0.94f, 0.94f, 1f), lip, -1);
                CreatePart(root, "Housing", housing, Vector3.zero,
                    new Vector3(0.90f, 0.90f, 1f), theme.Board, 0);
                CreatePart(root, "Recessed Module Surface", squareSprite,
                    new Vector3(0f, -0.01f, 0f), new Vector3(0.80f, 0.80f, 1f), recess, 1);
                CreateBorder(root, "Housing Bevel", 0.90f, 0.022f, edge, 2, null);
                CreateHousingFasteners();
            }
            else
            {
            CreatePart(root, "Housing", housing, Vector3.zero, new Vector3(0.92f, 0.92f, 1f),
                theme.Board, 0);
            Color railColor = theme.SecondaryBlue;
            railColor.a = 0.28f;
            CreateBorder(root, "Housing Rail", 0.88f, 0.025f, railColor, 1, null);
            }
            CreateBorder(root, "Hint Rim", 0.96f, 0.028f, theme.Hint, 12, hintRim);
            CreateBorder(root, "Tutorial Rim", 0.96f, 0.028f, theme.Success, 13, tutorialRim);
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
            if (UsesProductionTreatment)
                CreatePart(rotatingContent, "Circuit Hub Material Edge", squareSprite,
                    Vector3.zero, new Vector3(hubSize + 0.04f, hubSize + 0.04f, 1f),
                    WithAlpha(theme.SecondaryBlue, 0.20f), 4);
            circuitCenter = CreatePart(rotatingContent, "Circuit Base Hub", squareSprite,
                Vector3.zero, new Vector3(hubSize, hubSize, 1f), theme.InactiveConductor,
                UsesProductionTreatment ? 5 : 3);
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
            if (UsesProductionTreatment)
            {
                Vector3 collarPosition = outward * 0.45f;
                Vector3 collarScale = vertical
                    ? new Vector3(0.18f, 0.10f, 1f)
                    : new Vector3(0.10f, 0.18f, 1f);
                Color collar = theme.InactiveConductor;
                Color collarEdge = theme.SecondaryBlue;
                collarEdge.a = 0.28f;
                CreatePart(rotatingContent, "Connector Port Collar", squareSprite,
                    collarPosition, collarScale, collarEdge, 2);
                CreatePart(rotatingContent, "Connector Port Recess", squareSprite,
                    collarPosition, collarScale * 0.72f, collar, 3);
                float edgeWidth = theme.ToTileUnits(theme.ConduitWidth + 7f);
                CreatePart(rotatingContent, "Conduit Material Edge", conductor,
                    position, Scale(edgeWidth), WithAlpha(theme.SecondaryBlue, 0.20f), 3);
            }
            SpriteRenderer glow = CreatePart(rotatingContent, "Energy Halo", conductor,
                position, Scale(halo), WithAlpha(theme.PoweredEnergy, 0.18f),
                UsesProductionTreatment ? 4 : 2);
            SpriteRenderer baseLayer = CreatePart(rotatingContent, "Circuit Base", conductor,
                position, Scale(conduit), theme.InactiveConductor,
                UsesProductionTreatment ? 5 : 3);
            SpriteRenderer energyLayer = CreatePart(rotatingContent, "Powered Energy", conductor,
                position, Scale(energy), theme.PoweredEnergy,
                UsesProductionTreatment ? 6 : 4);
            SpriteRenderer hotLayer = CreatePart(rotatingContent, "Powered Hot Core", conductor,
                position, Scale(core), theme.PoweredHotCore,
                UsesProductionTreatment ? 7 : 5);
            return new PortLayers(baseLayer, glow, energyLayer, hotLayer);
        }

        private void BuildSource()
        {
            float core = 0.36f;
            if (UsesProductionTreatment)
            {
                CreatePart(rotatingContent, "Source Chamber Shadow", squareSprite,
                    new Vector3(0.018f, -0.025f, 0f), new Vector3(0.58f, 0.58f, 1f),
                    WithAlpha(Color.black, 0.52f), 5);
                CreateBorder(rotatingContent, "Source Chamber Frame", 0.58f, 0.052f,
                    WithAlpha(theme.SecondaryBlue, 0.52f), 7, null);
                CreatePart(rotatingContent, "Source Inner Emission", squareSprite,
                    Vector3.zero, new Vector3(0.46f, 0.46f, 1f),
                    WithAlpha(theme.PowerSource, 0.22f), 6, 45f);
            }
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
            if (UsesProductionTreatment)
            {
                CreatePart(rotatingContent, "LED Receiver Backplate", squareSprite,
                    new Vector3(0.018f, -0.025f, 0f), new Vector3(0.68f, 0.56f, 1f),
                    WithAlpha(Color.black, 0.48f), 5);
                CreatePart(rotatingContent, "LED Diffuser Edge", squareSprite,
                    Vector3.zero, new Vector3(0.58f, 0.48f, 1f),
                    WithAlpha(theme.LedObjective, 0.24f), 6);
            }
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
            if (UsesProductionTreatment)
                CreatePart(rotatingContent, "Diode Mechanism Plate", squareSprite,
                    Vector3.zero, new Vector3(0.50f, 0.44f, 1f),
                    WithAlpha(theme.InactiveConductor, 0.88f), 6);
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
            if (UsesProductionTreatment)
            {
                CreatePart(rotatingContent, "Switch Mechanism Bed", squareSprite,
                    Vector3.zero, new Vector3(0.58f, 0.42f, 1f),
                    WithAlpha(theme.InactiveConductor, 0.72f), 6);
                CreatePart(rotatingContent, "Switch Left Terminal Collar", squareSprite,
                    new Vector3(-0.20f, 0f, 0f), new Vector3(0.16f, 0.16f, 1f),
                    WithAlpha(theme.PoweredHotCore, 0.38f), 6);
                CreatePart(rotatingContent, "Switch Right Terminal Collar", squareSprite,
                    new Vector3(0.20f, 0f, 0f), new Vector3(0.16f, 0.16f, 1f),
                    WithAlpha(theme.PoweredHotCore, 0.38f), 6);
            }
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
            if (UsesProductionTreatment) BuildGateMount("AND");
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
            if (UsesProductionTreatment) BuildGateMount("OR");
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

        private void BuildGateMount(string gateName)
        {
            Color mount = theme.InactiveConductor;
            Color keyline = theme.SecondaryBlue;
            keyline.a = 0.24f;
            CreatePart(rotatingContent, $"{gateName} Gate Module Shadow", squareSprite,
                new Vector3(0.018f, -0.025f, 0f), new Vector3(0.64f, 0.62f, 1f),
                WithAlpha(Color.black, 0.44f), 5);
            CreatePart(rotatingContent, $"{gateName} Gate Module Bed", squareSprite,
                Vector3.zero, new Vector3(0.60f, 0.58f, 1f),
                WithAlpha(mount, 0.82f), 6);
            CreateBorder(rotatingContent, $"{gateName} Gate Module Keyline", 0.60f,
                0.024f, keyline, 7, null);
        }

        private void CreateHousingFasteners()
        {
            Color collar = theme.InactiveConductor;
            Color bolt = theme.SecondaryBlue;
            bolt.a = 0.48f;
            Vector3[] positions =
            {
                new Vector3(-0.37f, 0.37f, 0f),
                new Vector3(0.37f, 0.37f, 0f),
                new Vector3(-0.37f, -0.37f, 0f),
                new Vector3(0.37f, -0.37f, 0f)
            };
            for (int index = 0; index < positions.Length; index++)
            {
                CreatePart(root, $"Housing Fastener Collar {index + 1}", squareSprite,
                    positions[index], new Vector3(0.075f, 0.075f, 1f), collar, 3);
                CreatePart(root, $"Housing Fastener Bolt {index + 1}", squareSprite,
                    positions[index], new Vector3(0.032f, 0.032f, 1f), bolt, 4, 45f);
            }
        }

        private void BuildCornerClamps()
        {
            Color clamp = UsesProductionTreatment
                ? Color.Lerp(theme.InactiveConductor, theme.SecondaryBlue, 0.38f)
                : theme.InactiveConductor;
            float clampWidth = UsesProductionTreatment ? 0.24f : 0.16f;
            float clampHeight = UsesProductionTreatment ? 0.075f : 0.055f;
            float boltSize = UsesProductionTreatment ? 0.075f : 0.05f;
            CreatePart(root, "Lock Clamp Top Left", squareSprite,
                new Vector3(-0.37f, 0.37f, 0f),
                new Vector3(clampWidth, clampHeight, 1f), clamp, 9);
            if (UsesProductionTreatment)
                CreatePart(root, "Lock Clamp Top Left Vertical", squareSprite,
                    new Vector3(-0.43f, 0.31f, 0f),
                    new Vector3(clampHeight, clampWidth, 1f), clamp, 9);
            CreatePart(root, "Lock Clamp Top Left Bolt", squareSprite,
                new Vector3(-0.37f, 0.34f, 0f), new Vector3(boltSize, boltSize, 1f),
                theme.SecondaryBlue, 10, UsesProductionTreatment ? 45f : 0f);
            CreatePart(root, "Lock Clamp Bottom Right", squareSprite,
                new Vector3(0.37f, -0.37f, 0f),
                new Vector3(clampWidth, clampHeight, 1f), clamp, 9);
            if (UsesProductionTreatment)
                CreatePart(root, "Lock Clamp Bottom Right Vertical", squareSprite,
                    new Vector3(0.43f, -0.31f, 0f),
                    new Vector3(clampHeight, clampWidth, 1f), clamp, 9);
            CreatePart(root, "Lock Clamp Bottom Right Bolt", squareSprite,
                new Vector3(0.37f, -0.34f, 0f), new Vector3(boltSize, boltSize, 1f),
                theme.SecondaryBlue, 10, UsesProductionTreatment ? 45f : 0f);
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

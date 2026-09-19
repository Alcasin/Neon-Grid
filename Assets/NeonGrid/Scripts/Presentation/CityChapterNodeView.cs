using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class CityChapterNodeView : MonoBehaviour
    {
        private static readonly Color LockedColor = new Color(0.08f, 0.10f, 0.15f, 1f);
        private static readonly Color[] ProgressColors =
        {
            new Color(0.08f, 0.28f, 0.40f, 1f),
            new Color(0.07f, 0.38f, 0.48f, 1f),
            new Color(0.06f, 0.48f, 0.52f, 1f)
        };
        private static readonly Color RestoredColor = new Color(0.05f, 0.78f, 0.60f, 1f);

        private Button button;
        private Text label;
        private Image glow;
        private Image[] buildingParts;
        private RectTransform visualRoot;
        private Vector3 baseVisualScale;
        private bool pulse;
        private float pulseStartedAt;
        private Color glowColor;

        public string ChapterId { get; private set; }
        public ChapterMapVisualState VisualState { get; private set; }
        public bool IsPulsing => pulse;
        public RectTransform HitArea => (RectTransform)transform;
        public Text Label => label;
        public Button Button => button;
        public Vector3 BaseVisualScale => baseVisualScale;
        public Vector3 VisualScale => visualRoot != null ? visualRoot.localScale : Vector3.one;
        public float ArrivalCueStrength { get; private set; }
        public float NetworkPulseStrength { get; private set; }

        public void Initialize(string chapterId, Button nodeButton, Text nodeLabel, Image nodeGlow,
            Image[] parts)
        {
            ChapterId = chapterId;
            button = nodeButton;
            label = nodeLabel;
            glow = nodeGlow;
            buildingParts = parts;
            visualRoot = parts != null && parts.Length > 0
                ? parts[0].transform.parent as RectTransform
                : null;
            baseVisualScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        }

        public void Present(ChapterMapVisualState state, string text)
        {
            VisualState = state;
            label.text = text;
            button.interactable = state != ChapterMapVisualState.Locked;
            SetPulse(state != ChapterMapVisualState.Locked &&
                     state != ChapterMapVisualState.Restored);
            ArrivalCueStrength = 0f;
            NetworkPulseStrength = 0f;

            Color color = state == ChapterMapVisualState.Locked
                ? LockedColor
                : state == ChapterMapVisualState.Restored
                    ? RestoredColor
                    : ProgressColors[(int)state - (int)ChapterMapVisualState.ProgressStage1];
            foreach (Image part in buildingParts) part.color = color;
            glowColor = color;
            glowColor.a = state == ChapterMapVisualState.Locked ? 0.03f :
                state == ChapterMapVisualState.Restored ? 0.30f : 0.16f;
            glow.color = glowColor;
            glow.rectTransform.localScale = Vector3.one;
            ResetVisualScale();
        }

        public void ApplyFocus(float progress)
        {
            float normalized = Mathf.Clamp01(progress);
            float emphasis = Mathf.Sin(normalized * Mathf.PI);
            SetPulse(false);
            SetVisualScale(1f + emphasis * 0.018f);
            Color color = glowColor;
            color.a = 0.16f + emphasis * 0.05f;
            glow.color = color;
        }

        public void ApplyPowerUp(float progress)
        {
            float normalized = Mathf.Clamp01(progress);
            float initialSurge = Mathf.Sin(Mathf.Clamp01(normalized / 0.24f) * Mathf.PI) *
                                 (1f - CityRestorationEasing.SmoothStep(normalized / 0.38f));
            float flicker = Mathf.Sin(normalized * Mathf.PI * 6f) *
                            (1f - normalized) * 0.08f;
            Color color = Color.Lerp(ProgressColors[2], RestoredColor, normalized);
            color = Color.Lerp(color, Color.white, Mathf.Max(0f, flicker));
            foreach (Image part in buildingParts) part.color = color;

            VisualState = normalized >= 1f
                ? ChapterMapVisualState.Restored
                : ChapterMapVisualState.ProgressStage3;
            SetPulse(false);
            glowColor = Color.Lerp(ProgressColors[2], RestoredColor, normalized);
            glowColor.a = Mathf.Lerp(0.16f, 0.30f, normalized) + initialSurge * 0.14f;
            glow.color = glowColor;
            glow.rectTransform.localScale = Vector3.one * (1f + initialSurge * 0.06f);
            SetVisualScale(1f + Mathf.Sin(normalized * Mathf.PI) * 0.045f);
            if (normalized >= 1f)
            {
                glowColor = RestoredColor;
                glowColor.a = 0.30f;
                glow.color = glowColor;
                glow.rectTransform.localScale = Vector3.one;
                ResetVisualScale();
            }
        }

        public void ApplyAvailableReveal(float progress)
        {
            float normalized = Mathf.Clamp01(progress);
            float arrival = Mathf.Sin(normalized * Mathf.PI);
            Color color = Color.Lerp(LockedColor, ProgressColors[0], normalized);
            foreach (Image part in buildingParts) part.color = color;

            VisualState = normalized >= 1f
                ? ChapterMapVisualState.ProgressStage1
                : ChapterMapVisualState.Locked;
            button.interactable = normalized >= 1f;
            SetPulse(normalized >= 1f);
            ArrivalCueStrength = arrival;
            glowColor = color;
            glowColor.a = Mathf.Lerp(0.03f, 0.16f, normalized) + arrival * 0.10f;
            glow.color = glowColor;
            glow.rectTransform.localScale = Vector3.one * (1f + arrival * 0.05f);
            SetVisualScale(Mathf.Lerp(0.985f, 1f, normalized) + arrival * 0.025f);
            if (normalized >= 1f)
            {
                ArrivalCueStrength = 0f;
                glowColor = ProgressColors[0];
                glowColor.a = 0.16f;
                glow.color = glowColor;
                glow.rectTransform.localScale = Vector3.one;
                ResetVisualScale();
            }
        }

        public void ApplyNetworkPulse(float progress)
        {
            float normalized = Mathf.Clamp01(progress);
            NetworkPulseStrength = Mathf.Sin(normalized * Mathf.PI);
            VisualState = ChapterMapVisualState.Restored;
            SetPulse(false);
            Color color = Color.Lerp(RestoredColor, Color.white,
                NetworkPulseStrength * 0.12f);
            foreach (Image part in buildingParts) part.color = color;
            glowColor = RestoredColor;
            glowColor.a = 0.30f + NetworkPulseStrength * 0.16f;
            glow.color = glowColor;
            glow.rectTransform.localScale = Vector3.one *
                                             (1f + NetworkPulseStrength * 0.04f);
            SetVisualScale(1f + NetworkPulseStrength * 0.025f);
            if (normalized >= 1f)
            {
                NetworkPulseStrength = 0f;
                glow.rectTransform.localScale = Vector3.one;
                ResetVisualScale();
            }
        }

        private void SetPulse(bool enabled)
        {
            if (enabled && !pulse) pulseStartedAt = Time.unscaledTime;
            pulse = enabled;
        }

        private void SetVisualScale(float multiplier)
        {
            if (visualRoot != null) visualRoot.localScale = baseVisualScale * multiplier;
        }

        private void ResetVisualScale()
        {
            if (visualRoot != null) visualRoot.localScale = baseVisualScale;
        }

        private void Update()
        {
            if (!pulse || glow == null) return;
            Color color = glowColor;
            float frequency = Mathf.PI * 2f / ProgrammerUiMetrics.CityAvailablePulseSeconds;
            float pulseTime = Time.unscaledTime - pulseStartedAt;
            color.a *= 0.78f + 0.22f *
                (Mathf.Sin(pulseTime * frequency - Mathf.PI * 0.5f) * 0.5f + 0.5f);
            glow.color = color;
        }
    }
}

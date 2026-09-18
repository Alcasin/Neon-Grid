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
        private bool pulse;
        private Color glowColor;

        public string ChapterId { get; private set; }
        public ChapterMapVisualState VisualState { get; private set; }
        public bool IsPulsing => pulse;
        public RectTransform HitArea => (RectTransform)transform;
        public Text Label => label;
        public Button Button => button;

        public void Initialize(string chapterId, Button nodeButton, Text nodeLabel, Image nodeGlow,
            Image[] parts)
        {
            ChapterId = chapterId;
            button = nodeButton;
            label = nodeLabel;
            glow = nodeGlow;
            buildingParts = parts;
        }

        public void Present(ChapterMapVisualState state, string text)
        {
            VisualState = state;
            label.text = text;
            button.interactable = state != ChapterMapVisualState.Locked;
            pulse = state != ChapterMapVisualState.Locked && state != ChapterMapVisualState.Restored;

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
        }

        public void ApplyPowerUp(float progress)
        {
            float normalized = Mathf.Clamp01(progress);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            float flicker = Mathf.Sin(normalized * Mathf.PI * 8f) * (1f - normalized) * 0.12f;
            Color color = Color.Lerp(ProgressColors[2], RestoredColor, eased);
            color = Color.Lerp(color, Color.white, Mathf.Max(0f, flicker));
            foreach (Image part in buildingParts) part.color = color;

            VisualState = normalized >= 1f
                ? ChapterMapVisualState.Restored
                : ChapterMapVisualState.ProgressStage3;
            pulse = false;
            glowColor = Color.Lerp(ProgressColors[2], RestoredColor, eased);
            glowColor.a = Mathf.Lerp(0.16f, 0.30f, eased);
            glow.color = glowColor;
            glow.rectTransform.localScale = Vector3.one *
                                             (1f + Mathf.Sin(normalized * Mathf.PI) * 0.08f);
        }

        public void ApplyAvailableReveal(float progress)
        {
            float normalized = Mathf.Clamp01(progress);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            Color color = Color.Lerp(LockedColor, ProgressColors[0], eased);
            foreach (Image part in buildingParts) part.color = color;

            VisualState = normalized >= 1f
                ? ChapterMapVisualState.ProgressStage1
                : ChapterMapVisualState.Locked;
            button.interactable = normalized >= 1f;
            pulse = normalized >= 1f;
            glowColor = color;
            glowColor.a = Mathf.Lerp(0.03f, 0.16f, eased);
            glow.color = glowColor;
            glow.rectTransform.localScale = Vector3.one *
                                             Mathf.Lerp(0.92f, 1f, eased);
        }

        private void Update()
        {
            if (!pulse || glow == null) return;
            Color color = glowColor;
            float frequency = Mathf.PI * 2f / ProgrammerUiMetrics.CityAvailablePulseSeconds;
            color.a *= 0.78f + 0.22f *
                (Mathf.Sin(Time.unscaledTime * frequency) * 0.5f + 0.5f);
            glow.color = color;
        }
    }
}

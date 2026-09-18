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

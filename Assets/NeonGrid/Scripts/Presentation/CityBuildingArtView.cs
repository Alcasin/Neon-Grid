using System;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    // Opt-in only: production nodes do not create this component.
    public sealed class CityBuildingArtView : MonoBehaviour
    {
        private readonly Image[] layers = new Image[4];
        private Image[] fallback;
        private bool[] fallbackEnabled;
        private RectTransform artRoot;
        private Vector3 basePosition;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private bool initialized;
        public CityBuildingArtDefinition Definition { get; private set; }
        public ChapterMapVisualState VisualState { get; private set; }
        public bool UsesArt { get; private set; }
        public RectTransform ArtRoot => artRoot;
        public const float MaximumEmphasis = 1.045f;

        public void Initialize(CityBuildingArtDefinition definition, string chapterId,
            RectTransform visualRegion, Image[] placeholderParts)
        {
            if (initialized) throw new InvalidOperationException("Art view is already initialized.");
            if (visualRegion == null) throw new ArgumentNullException(nameof(visualRegion));
            initialized = true;
            Definition = definition;
            fallback = placeholderParts ?? Array.Empty<Image>();
            fallbackEnabled = new bool[fallback.Length];
            for (int index = 0; index < fallback.Length; index++)
                fallbackEnabled[index] = fallback[index] != null && fallback[index].enabled;
            UsesArt = definition != null && definition.IsConfigured &&
                      definition.ChapterId == chapterId;
            if (!UsesArt) return;

            var root = new GameObject("Authored Building Art", typeof(RectTransform));
            artRoot = root.GetComponent<RectTransform>();
            artRoot.SetParent(visualRegion, false);
            artRoot.sizeDelta = Vector2.Scale(visualRegion.sizeDelta, definition.FootprintFraction);
            artRoot.anchoredPosition = definition.LocalOffset;
            artRoot.localScale = Vector3.one * definition.LocalScale;
            basePosition = artRoot.localPosition;
            baseScale = artRoot.localScale;
            baseRotation = artRoot.localRotation;
            string[] names = { "Base Architecture", "Warm Facility Lights", "Electrical Energy", "Restored Core" };
            for (int index = 0; index < layers.Length; index++)
            {
                var child = new GameObject(names[index], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                child.transform.SetParent(artRoot, false);
                Image image = child.GetComponent<Image>();
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.sizeDelta = Vector2.zero;
                image.sprite = definition.GetSprite(index);
                image.raycastTarget = false;
                // Registered layers share a canvas; never derive dimensions from source pixels.
                image.type = Image.Type.Simple;
                layers[index] = image;
            }
            SetFallback(false);
            Present(ChapterMapVisualState.Locked);
        }

        public void Present(CampaignChapterState chapterState, int completed, int total)
        {
            Present(CityMapPresentationModel.GetChapterVisualState(chapterState, completed, total));
        }

        public void Present(ChapterMapVisualState state)
        {
            if (!Enum.IsDefined(typeof(ChapterMapVisualState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            VisualState = state;
            if (!UsesArt) return;
            if (Definition == null || !Definition.IsConfigured)
            {
                UsesArt = false;
                artRoot.gameObject.SetActive(false);
                SetFallback(true);
                return;
            }
            ResetEmphasis();
            Vector4 opacity = Definition.GetOpacity(state);
            for (int index = 0; index < layers.Length; index++)
            {
                Image image = layers[index];
                image.sprite = Definition.GetSprite(index);
                Color color = Definition.GetTint(index);
                color.a *= opacity[index];
                image.color = color;
                image.enabled = image.sprite != null && color.a > 0f;
            }
        }

        // Timeline/coroutine caller owns timing; no decorative Update or accumulated multipliers.
        public void ApplyEmphasis(float normalizedStrength)
        {
            if (!UsesArt) return;
            artRoot.localScale = baseScale * Mathf.Lerp(1f, MaximumEmphasis,
                Mathf.Clamp01(normalizedStrength));
        }

        public void ResetEmphasis()
        {
            if (artRoot == null) return;
            artRoot.localPosition = basePosition;
            artRoot.localRotation = baseRotation;
            artRoot.localScale = baseScale;
        }

        private void SetFallback(bool restore)
        {
            for (int index = 0; index < fallback.Length; index++)
                if (fallback[index] != null)
                    fallback[index].enabled = restore && fallbackEnabled[index];
        }

        private void OnDisable()
        {
            ResetEmphasis();
            if (artRoot != null) artRoot.gameObject.SetActive(false);
            if (initialized) SetFallback(true);
        }

        private void OnEnable()
        {
            if (!UsesArt || artRoot == null) return;
            artRoot.gameObject.SetActive(true);
            SetFallback(false);
            Present(VisualState);
        }

        private void OnDestroy()
        {
            if (artRoot == null) return;
            if (Application.isPlaying) Destroy(artRoot.gameObject);
            else DestroyImmediate(artRoot.gameObject);
        }
    }
}

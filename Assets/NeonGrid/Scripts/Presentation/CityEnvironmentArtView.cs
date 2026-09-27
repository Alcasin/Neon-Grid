using System;
using System.Collections.Generic;
using System.Linq;
using NeonGrid.Campaign;
using NeonGrid.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class CityEnvironmentArtView : MonoBehaviour
    {
        private readonly Dictionary<string, Image> districtImages =
            new Dictionary<string, Image>(StringComparer.Ordinal);
        private Image baseImage;
        private Image finalAccentImage;
        private RectTransform artRoot;
        private bool initialized;

        public CityEnvironmentArtDefinition Definition { get; private set; }
        public RectTransform ArtRoot => artRoot;
        public int LayerCount => artRoot == null ? 0 : artRoot.childCount;
        public int ActiveDistrictCount => districtImages.Values.Count(image => image.enabled);
        public bool IsVisible => artRoot != null && artRoot.gameObject.activeSelf;

        public void Initialize(CityEnvironmentArtDefinition definition, RectTransform host,
            int siblingIndex)
        {
            if (initialized) throw new InvalidOperationException(
                "City environment art is already initialized.");
            if (definition == null || !definition.IsConfigured)
                throw new ArgumentException("A valid city environment definition is required.",
                    nameof(definition));
            if (host == null) throw new ArgumentNullException(nameof(host));

            initialized = true;
            Definition = definition;
            var root = new GameObject("City Environment Art", typeof(RectTransform));
            artRoot = root.GetComponent<RectTransform>();
            artRoot.SetParent(host, false);
            artRoot.anchorMin = artRoot.anchorMax = new Vector2(0.5f, 0.5f);
            artRoot.sizeDelta = Vector2.Scale(host.sizeDelta, definition.FootprintFraction);
            artRoot.anchoredPosition = definition.LocalOffset;
            artRoot.localScale = Vector3.one;
            artRoot.localRotation = Quaternion.identity;
            artRoot.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, host.childCount - 1));

            baseImage = CreateLayer("Base City", definition.BaseCity);
            foreach (CityEnvironmentDistrictLayer district in definition.Districts)
                districtImages.Add(district.ChapterId,
                    CreateLayer("District — " + district.ChapterId, district.Overlay));
            if (definition.FinalAccent != null)
                finalAccentImage = CreateLayer("Final City Accent", definition.FinalAccent);
            PresentRestoredDistricts(Array.Empty<string>());
        }

        public void Present(CampaignDefinition campaign, CampaignProgressService progress)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            PresentRestoredDistricts(campaign.Chapters
                .Where(chapter => progress.GetChapterState(chapter.ChapterId) ==
                                  CampaignChapterState.Restored)
                .Select(chapter => chapter.ChapterId));
        }

        public void PresentRestoredDistricts(IEnumerable<string> restoredChapterIds)
        {
            if (!initialized) throw new InvalidOperationException(
                "City environment art is not initialized.");
            var restored = new HashSet<string>(restoredChapterIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, Image> district in districtImages)
                district.Value.enabled = restored.Contains(district.Key);
            if (finalAccentImage != null)
                finalAccentImage.enabled = districtImages.Count > 0 &&
                                           districtImages.Keys.All(restored.Contains);
            baseImage.enabled = true;
        }

        public bool IsDistrictActive(string chapterId) =>
            chapterId != null && districtImages.TryGetValue(chapterId, out Image image) &&
            image.enabled;

        public void SetVisible(bool visible)
        {
            if (artRoot != null) artRoot.gameObject.SetActive(visible);
        }

        private Image CreateLayer(string name, Sprite sprite)
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            layer.transform.SetParent(artRoot, false);
            Image image = layer.GetComponent<Image>();
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.sizeDelta = Vector2.zero;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private void OnDestroy()
        {
            if (artRoot == null) return;
            if (Application.isPlaying) Destroy(artRoot.gameObject);
            else DestroyImmediate(artRoot.gameObject);
        }
    }
}

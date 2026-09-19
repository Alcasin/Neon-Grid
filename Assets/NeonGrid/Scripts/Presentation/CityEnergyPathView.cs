using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class CityEnergyPathView : MonoBehaviour
    {
        private static readonly Color Locked = new Color(0.07f, 0.10f, 0.15f, 0.9f);
        private static readonly Color Frontier = new Color(0.05f, 0.48f, 0.58f, 0.95f);
        private static readonly Color Restored = new Color(0.04f, 0.85f, 0.65f, 1f);
        private static readonly Color LeadingEdge = new Color(0.55f, 1f, 0.88f, 1f);
        private Image[] segments;
        private Image frontierMarker;

        public int FromIndex { get; private set; }
        public int ToIndex { get; private set; }
        public CityEnergyPathState State { get; private set; }
        public bool IsEnergyTraveling { get; private set; }
        public float TravelProgress { get; private set; }
        public bool IsFrontierVisible => frontierMarker != null && frontierMarker.gameObject.activeSelf;
        public Vector2 FrontierPosition => frontierMarker != null
            ? frontierMarker.rectTransform.anchoredPosition
            : Vector2.zero;
        public float NetworkPulseStrength { get; private set; }

        public void Initialize(int fromIndex, int toIndex, Image[] pathSegments)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            segments = pathSegments;
            var markerObject = new GameObject("Energy Frontier", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            markerObject.transform.SetParent(transform, false);
            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(28f, 28f);
            frontierMarker = markerObject.GetComponent<Image>();
            frontierMarker.color = LeadingEdge;
            frontierMarker.raycastTarget = false;
            frontierMarker.gameObject.SetActive(false);
        }

        public void Present(CityEnergyPathState state)
        {
            State = state;
            IsEnergyTraveling = false;
            TravelProgress = 0f;
            NetworkPulseStrength = 0f;
            if (frontierMarker != null) frontierMarker.gameObject.SetActive(false);
            Color color = state == CityEnergyPathState.Restored
                ? Restored
                : state == CityEnergyPathState.Frontier ? Frontier : Locked;
            foreach (Image segment in segments) segment.color = color;
        }

        public void ApplyEnergyTravel(float progress)
        {
            TravelProgress = Mathf.Clamp01(progress);
            IsEnergyTraveling = TravelProgress < 1f;
            State = CityEnergyPathState.Frontier;
            NetworkPulseStrength = 0f;

            float totalLength = GetTotalLength();
            float illuminatedLength = totalLength * TravelProgress;
            float traversed = 0f;
            foreach (Image segment in segments)
            {
                float length = segment.rectTransform.sizeDelta.x;
                float segmentProgress = length <= 0f
                    ? 1f
                    : Mathf.Clamp01((illuminatedLength - traversed) / length);
                segment.color = segmentProgress >= 1f
                    ? Restored
                    : segmentProgress > 0f
                        ? Color.Lerp(Frontier, Restored, segmentProgress)
                        : Locked;
                traversed += length;
            }
            UpdateFrontier(illuminatedLength, totalLength);
        }

        public void ApplyNetworkPulse(float progress)
        {
            float normalized = Mathf.Clamp01(progress);
            NetworkPulseStrength = Mathf.Sin(normalized * Mathf.PI);
            State = CityEnergyPathState.Restored;
            IsEnergyTraveling = false;
            TravelProgress = 0f;
            frontierMarker.gameObject.SetActive(false);
            Color color = Color.Lerp(Restored, LeadingEdge,
                NetworkPulseStrength * 0.45f);
            foreach (Image segment in segments) segment.color = color;
            if (normalized >= 1f) NetworkPulseStrength = 0f;
        }

        internal Color GetSegmentColor(int index)
        {
            return segments[index].color;
        }

        private float GetTotalLength()
        {
            float total = 0f;
            foreach (Image segment in segments)
                total += segment.rectTransform.sizeDelta.x;
            return total;
        }

        private void UpdateFrontier(float illuminatedLength, float totalLength)
        {
            bool visible = totalLength > 0f && TravelProgress < 1f;
            frontierMarker.gameObject.SetActive(visible);
            if (!visible) return;

            float traversed = 0f;
            foreach (Image segment in segments)
            {
                RectTransform rect = segment.rectTransform;
                float length = rect.sizeDelta.x;
                if (illuminatedLength <= traversed + length ||
                    segment == segments[segments.Length - 1])
                {
                    float localProgress = length <= 0f
                        ? 1f
                        : Mathf.Clamp01((illuminatedLength - traversed) / length);
                    Vector2 direction = rect.localRotation * Vector2.right;
                    Vector2 start = rect.anchoredPosition - direction * (length * 0.5f);
                    frontierMarker.rectTransform.anchoredPosition =
                        start + direction * (length * localProgress);
                    return;
                }
                traversed += length;
            }
        }
    }
}

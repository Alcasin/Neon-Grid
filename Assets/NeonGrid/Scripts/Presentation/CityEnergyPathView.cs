using UnityEngine;
using UnityEngine.UI;

namespace NeonGrid.Presentation
{
    public sealed class CityEnergyPathView : MonoBehaviour
    {
        private static readonly Color Locked = new Color(0.07f, 0.10f, 0.15f, 0.9f);
        private static readonly Color Frontier = new Color(0.05f, 0.48f, 0.58f, 0.95f);
        private static readonly Color Restored = new Color(0.04f, 0.85f, 0.65f, 1f);
        private Image[] segments;

        public int FromIndex { get; private set; }
        public int ToIndex { get; private set; }
        public CityEnergyPathState State { get; private set; }
        public bool IsEnergyTraveling { get; private set; }
        public float TravelProgress { get; private set; }

        public void Initialize(int fromIndex, int toIndex, Image[] pathSegments)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            segments = pathSegments;
        }

        public void Present(CityEnergyPathState state)
        {
            State = state;
            IsEnergyTraveling = false;
            TravelProgress = 0f;
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

            float totalLength = 0f;
            foreach (Image segment in segments)
                totalLength += segment.rectTransform.sizeDelta.x;
            float illuminatedLength = totalLength * TravelProgress;
            float traversed = 0f;
            foreach (Image segment in segments)
            {
                float length = segment.rectTransform.sizeDelta.x;
                float segmentProgress = length <= 0f
                    ? 1f
                    : Mathf.Clamp01((illuminatedLength - traversed) / length);
                segment.color = Color.Lerp(Locked, Restored, segmentProgress);
                traversed += length;
            }
        }
    }
}

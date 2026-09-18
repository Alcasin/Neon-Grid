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

        public void Initialize(int fromIndex, int toIndex, Image[] pathSegments)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            segments = pathSegments;
        }

        public void Present(CityEnergyPathState state)
        {
            State = state;
            Color color = state == CityEnergyPathState.Restored
                ? Restored
                : state == CityEnergyPathState.Frontier ? Frontier : Locked;
            foreach (Image segment in segments) segment.color = color;
        }
    }
}

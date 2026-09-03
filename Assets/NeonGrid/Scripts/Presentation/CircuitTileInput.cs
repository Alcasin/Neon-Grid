using System;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CircuitTileInput : MonoBehaviour
    {
        private GridPosition position;
        private Action<GridPosition> onTapped;

        public void Initialize(GridPosition tilePosition, Action<GridPosition> tapped)
        {
            position = tilePosition;
            onTapped = tapped;
        }

        public void HandleTap()
        {
            onTapped?.Invoke(position);
        }
    }
}

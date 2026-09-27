using System;
using System.Collections.Generic;
using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class CircuitJuiceCoordinator : MonoBehaviour
    {
        private readonly List<CircuitTileJuiceView> active =
            new List<CircuitTileJuiceView>();

        public CircuitJuiceDefinition Definition { get; private set; }
        public int RegisteredTileCount { get; private set; }
        public int ActiveAnimationCount => active.Count;

        public void Initialize(CircuitJuiceDefinition definition)
        {
            Definition = definition != null && definition.IsConfigured ? definition : null;
        }

        internal CircuitTileJuiceView Register(Transform visualRoot,
            TechnicalNeonTileRenderer renderer)
        {
            if (Definition == null) return null;
            var view = visualRoot.parent.gameObject.AddComponent<CircuitTileJuiceView>();
            view.Initialize(Definition, this, visualRoot, renderer);
            RegisteredTileCount++;
            return view;
        }

        internal void Activate(CircuitTileJuiceView view)
        {
            if (view == null || view.IsQueued) return;
            view.IsQueued = true;
            active.Add(view);
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            for (int index = active.Count - 1; index >= 0; index--)
            {
                CircuitTileJuiceView view = active[index];
                if (view != null && view.Advance(deltaSeconds)) continue;
                if (view != null) view.IsQueued = false;
                active.RemoveAt(index);
            }
        }

        public void CancelAll()
        {
            for (int index = active.Count - 1; index >= 0; index--)
            {
                CircuitTileJuiceView view = active[index];
                if (view != null)
                {
                    view.CancelAndRestore();
                    view.IsQueued = false;
                }
            }
            active.Clear();
        }
    }
}

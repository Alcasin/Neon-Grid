using System;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class CircuitJuiceCoordinator : MonoBehaviour
    {
        private readonly List<CircuitTileJuiceView> active =
            new List<CircuitTileJuiceView>();
        private readonly List<ScheduledStep> scheduledSteps = new List<ScheduledStep>();
        private readonly List<CircuitTileJuiceView> completionTargets =
            new List<CircuitTileJuiceView>();
        private readonly List<CircuitTileJuiceView> registered =
            new List<CircuitTileJuiceView>();
        private float scheduleElapsed;
        private float completionTriggerTime;
        private bool completionRequested;
        private bool completionScheduled;

        public CircuitJuiceDefinition Definition { get; private set; }
        public int RegisteredTileCount { get; private set; }
        public int ActiveAnimationCount => active.Count;
        public int PendingPropagationStepCount => scheduledSteps.Count;
        public int PendingPropagationTileCount { get; private set; }
        public bool HasPendingCompletion => completionRequested || completionScheduled;
        public bool CompletionPulseStarted { get; private set; }
        public PropagationPresentationPlan LastPropagationPlan { get; private set; }
        public float MaximumScheduledDelay { get; private set; }
        public event Action<CircuitJuiceEventType> PresentationEvent;

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
            registered.Add(view);
            RegisteredTileCount++;
            return view;
        }

        internal void Activate(CircuitTileJuiceView view)
        {
            if (view == null || view.IsQueued) return;
            view.IsQueued = true;
            active.Add(view);
        }

        internal void PublishPresentationEvent(CircuitJuiceEventType eventType)
        {
            PresentationEvent?.Invoke(eventType);
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            DispatchDuePresentation();
            float remaining = deltaSeconds;
            while (remaining > 0f)
            {
                float nextEvent = NextScheduledTime();
                if (float.IsPositiveInfinity(nextEvent))
                {
                    AdvanceActive(remaining);
                    remaining = 0f;
                    continue;
                }

                float untilEvent = Mathf.Max(0f, nextEvent - scheduleElapsed);
                float slice = Mathf.Min(remaining, untilEvent);
                if (slice > 0f)
                {
                    AdvanceActive(slice);
                    scheduleElapsed += slice;
                    remaining -= slice;
                }
                DispatchDuePresentation();
                if (slice <= 0f && Mathf.Approximately(nextEvent, scheduleElapsed) &&
                    (scheduledSteps.Count > 0 || completionScheduled))
                    continue;
            }
            if (deltaSeconds <= 0f) AdvanceActive(0f);
        }

        private void AdvanceActive(float deltaSeconds)
        {
            for (int index = active.Count - 1; index >= 0; index--)
            {
                CircuitTileJuiceView view = active[index];
                if (view != null && view.Advance(deltaSeconds)) continue;
                if (view != null) view.IsQueued = false;
                active.RemoveAt(index);
            }
        }

        private float NextScheduledTime()
        {
            float result = completionScheduled
                ? completionTriggerTime
                : float.PositiveInfinity;
            foreach (ScheduledStep scheduled in scheduledSteps)
                result = Mathf.Min(result, scheduled.Step.DelaySeconds);
            return result;
        }

        public void RequestCompletion()
        {
            completionRequested = true;
        }

        public void SchedulePowerPresentation(BoardState board,
            ISet<GridPosition> newlyPowered, ISet<GridPosition> newlyUnpowered,
            IReadOnlyDictionary<GridPosition, CircuitTileView> tileViews)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (newlyPowered == null) throw new ArgumentNullException(nameof(newlyPowered));
            if (newlyUnpowered == null) throw new ArgumentNullException(nameof(newlyUnpowered));
            if (tileViews == null) throw new ArgumentNullException(nameof(tileViews));
            CancelScheduledPresentation();
            LastPropagationPlan = PropagationPresentationPlan.Create(board, newlyPowered,
                Definition);
            PendingPropagationTileCount = LastPropagationPlan.TileCount;
            MaximumScheduledDelay = LastPropagationPlan.MaximumDelaySeconds;
            scheduleElapsed = 0f;

            foreach (GridPosition position in newlyUnpowered)
                if (tileViews.TryGetValue(position, out CircuitTileView tile))
                    tile.JuiceView?.PresentPowerDeactivation();
            if (newlyUnpowered.Count > 0)
                PublishPresentationEvent(CircuitJuiceEventType.PowerDeactivated);

            if (Definition.PropagationFeedback && LastPropagationPlan.TileCount > 0)
            {
                PublishPresentationEvent(CircuitJuiceEventType.PowerActivated);
                foreach (GridPosition source in LastPropagationPlan.Sources)
                    if (tileViews.TryGetValue(source, out CircuitTileView sourceView))
                        sourceView.PresentSourcePulse();
                foreach (PropagationPresentationStep step in LastPropagationPlan.Steps)
                {
                    scheduledSteps.Add(new ScheduledStep(step, tileViews));
                    foreach (GridPosition position in step.Positions)
                        if (tileViews.TryGetValue(position, out CircuitTileView tile))
                            tile.JuiceView?.PreparePowerActivation(
                                EventForActivation(tile));
                }
            }
            else
            {
                foreach (PropagationPresentationStep step in LastPropagationPlan.Steps)
                    foreach (GridPosition position in step.Positions)
                        if (tileViews.TryGetValue(position, out CircuitTileView tile))
                            tile.JuiceView?.BeginPowerActivation(EventForActivation(tile));
            }

            if (completionRequested)
            {
                completionRequested = false;
                completionScheduled = true;
                CompletionPulseStarted = false;
                completionTriggerTime = LastPropagationPlan.MaximumDelaySeconds +
                                        Definition.PropagationActivationDuration +
                                        Definition.CompletionSettleDelay;
                MaximumScheduledDelay = Mathf.Max(MaximumScheduledDelay,
                    completionTriggerTime);
                completionTargets.Clear();
                foreach (CircuitTileState state in board.AllTiles())
                    if (state.IsPowered && tileViews.TryGetValue(state.Position,
                            out CircuitTileView tile) && tile.JuiceView != null)
                        completionTargets.Add(tile.JuiceView);
            }
            DispatchDuePresentation();
        }

        public void CancelScheduledPresentation()
        {
            foreach (CircuitTileJuiceView view in registered)
                if (view != null) view.CancelScheduledChannels();
            scheduledSteps.Clear();
            completionTargets.Clear();
            PendingPropagationTileCount = 0;
            completionScheduled = false;
            CompletionPulseStarted = false;
            MaximumScheduledDelay = 0f;
            scheduleElapsed = 0f;
        }

        private void DispatchDuePresentation()
        {
            for (int index = scheduledSteps.Count - 1; index >= 0; index--)
            {
                ScheduledStep scheduled = scheduledSteps[index];
                if (scheduleElapsed + 0.000001f < scheduled.Step.DelaySeconds) continue;
                bool objectiveActivated = false;
                foreach (CircuitTileView tile in scheduled.Tiles)
                {
                    tile.JuiceView?.BeginPowerActivation(EventForActivation(tile));
                    objectiveActivated |= tile.CurrentTileType == TileType.OutputLamp;
                }
                if (objectiveActivated)
                    PublishPresentationEvent(CircuitJuiceEventType.ObjectiveActivated);
                scheduledSteps.RemoveAt(index);
            }

            if (!completionScheduled || scheduleElapsed + 0.000001f < completionTriggerTime)
                return;
            completionScheduled = false;
            CompletionPulseStarted = true;
            PublishPresentationEvent(CircuitJuiceEventType.CompletionTriggered);
            foreach (CircuitTileJuiceView target in completionTargets)
                target.BeginCompletionPulse();
            completionTargets.Clear();
        }

        private static CircuitJuiceEventType EventForActivation(CircuitTileView tile)
        {
            if (tile.CurrentTileType == TileType.OutputLamp)
                return CircuitJuiceEventType.ObjectiveActivated;
            if ((tile.CurrentTileType == TileType.AndGate ||
                 tile.CurrentTileType == TileType.OrGate) && tile.HasActiveOutput)
                return CircuitJuiceEventType.GateActivated;
            return CircuitJuiceEventType.PowerActivated;
        }

        public void CancelAll()
        {
            completionRequested = false;
            CancelScheduledPresentation();
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

        private sealed class ScheduledStep
        {
            private readonly List<CircuitTileView> tiles = new List<CircuitTileView>();

            public PropagationPresentationStep Step { get; }
            public IReadOnlyList<CircuitTileView> Tiles => tiles;

            public ScheduledStep(PropagationPresentationStep step,
                IReadOnlyDictionary<GridPosition, CircuitTileView> tileViews)
            {
                Step = step;
                foreach (GridPosition position in step.Positions)
                    if (tileViews.TryGetValue(position, out CircuitTileView tile))
                        tiles.Add(tile);
            }
        }
    }
}

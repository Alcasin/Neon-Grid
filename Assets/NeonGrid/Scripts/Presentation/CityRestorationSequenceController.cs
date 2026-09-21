using System;
using System.Collections;
using NeonGrid.Campaign;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class CityRestorationSequenceController : MonoBehaviour
    {
        private CampaignRuntimeView view;
        private CampaignFlowCoordinator flow;
        private Func<bool> tryShowFinalCompletion;
        private Coroutine routine;
        private Coroutine statusTailRoutine;

        public bool IsRunning { get; private set; }
        public CityRestorationSequencePhase Phase { get; private set; }
        public CityRestorationSequencePlan CurrentPlan { get; private set; }
        public float DurationMultiplier { get; set; } = 1f;
        public bool IsStatusTailRunning { get; private set; }
        internal float StatusTailHoldSeconds => ProgrammerUiMetrics.RestorationStatusHoldSeconds;
        internal float StatusTailFadeSeconds => ProgrammerUiMetrics.RestorationStatusFadeSeconds;

        public void Initialize(CampaignRuntimeView runtimeView,
            CampaignFlowCoordinator flowCoordinator)
        {
            Initialize(runtimeView, flowCoordinator, null);
        }

        public void Initialize(CampaignRuntimeView runtimeView,
            CampaignFlowCoordinator flowCoordinator, Func<bool> onFinalRestorationCompleted)
        {
            view = runtimeView;
            flow = flowCoordinator;
            tryShowFinalCompletion = onFinalRestorationCompleted;
        }

        public void EnterMap()
        {
            if (IsRunning) return;
            CancelStatusTail();
            if (!PreparePendingRestoration()) return;
            routine = StartCoroutine(RunPreparedSequence());
        }

        internal bool PreparePendingRestoration()
        {
            if (view == null || flow == null) return false;
            if (IsStatusTailRunning) CancelStatusTail();

            ChapterRestorationEvent pending = flow.PeekPendingRestoration();
            if (pending == null)
            {
                view.ShowMap();
                return false;
            }

            if (!view.UsesCityMap)
            {
                view.ShowMap();
                if (!flow.TryConsumePendingRestoration(out _))
                    Debug.LogWarning("Could not persist the fallback-map restoration event " +
                                     "consumption. It remains pending.", this);
                return false;
            }

            if (!view.TryPrepareRestoration(pending, out CityRestorationSequencePlan plan))
            {
                view.ShowMap();
                Debug.LogWarning("The pending restoration event does not match the current city " +
                                 "map. The event remains pending.", this);
                return false;
            }

            CurrentPlan = plan;
            Phase = CityRestorationSequencePhase.Focus;
            IsRunning = true;
            return true;
        }

        internal void ApplyPhase(CityRestorationSequencePhase phase, float progress)
        {
            if (!IsRunning || CurrentPlan == null) return;
            Phase = phase;
            float normalized = Mathf.Clamp01(progress);
            switch (phase)
            {
                case CityRestorationSequencePhase.Focus:
                    view.ApplyRestoredNodeFocus(CurrentPlan,
                        CityRestorationEasing.SmoothStep(normalized));
                    break;
                case CityRestorationSequencePhase.BuildingPowerUp:
                    view.ApplyRestoredNodePowerUp(CurrentPlan,
                        CityRestorationEasing.EaseOutCubic(normalized));
                    if (normalized >= 1f)
                        view.ShowRestorationStatus(CurrentPlan.RestoredChapterId);
                    else
                        view.HideRestorationStatus();
                    break;
                case CityRestorationSequencePhase.EnergyTravel:
                    if (CurrentPlan.HasNextChapter)
                        view.ApplyEnergyTravel(CurrentPlan,
                            CityRestorationEasing.EaseInOutCubic(normalized));
                    break;
                case CityRestorationSequencePhase.NextChapterReveal:
                    if (CurrentPlan.HasNextChapter)
                        view.ApplyNextChapterReveal(CurrentPlan,
                            CityRestorationEasing.SmoothStep(normalized));
                    break;
                case CityRestorationSequencePhase.FinalNetworkPulse:
                    if (CurrentPlan.IncludesFinalNetworkPulse)
                        view.ApplyFinalNetworkPulse(
                            CityRestorationEasing.SmoothStep(normalized));
                    break;
            }
        }

        internal bool CompletePreparedSequence()
        {
            if (!IsRunning || CurrentPlan == null) return false;

            Phase = CityRestorationSequencePhase.Settle;
            string restoredChapterId = CurrentPlan.RestoredChapterId;
            bool hadVisibleStatus = view.RestorationStatus != null &&
                                    view.RestorationStatus.IsVisible;
            view.RestoreAuthoritativeMap(false);
            if (hadVisibleStatus)
                view.ShowRestorationStatus(restoredChapterId);
            bool consumed = flow.TryConsumePendingRestoration(
                out ChapterRestorationEvent consumedEvent);
            bool consumedExpectedEvent = consumed &&
                                         consumedEvent.RestoredChapterId == restoredChapterId;
            bool wasFinalRestoration = CurrentPlan.IncludesFinalNetworkPulse;
            if (!consumedExpectedEvent)
            {
                Debug.LogWarning("The restoration sequence completed, but its pending event " +
                                 "could not be durably consumed. It may replay on a later map visit.",
                    this);
                view.HideRestorationStatus();
            }

            ResetSequenceState();
            bool endingOwnsPresentation = consumedExpectedEvent && wasFinalRestoration &&
                                           tryShowFinalCompletion != null &&
                                           tryShowFinalCompletion();
            view.SetMapInteractionEnabled(!endingOwnsPresentation);
            if (consumedExpectedEvent && hadVisibleStatus)
                BeginStatusTail();
            return consumed;
        }

        internal void CancelPreparedSequence()
        {
            if (!IsRunning) return;
            if (view != null)
            {
                view.RestoreAuthoritativeMap(false);
                view.SetMapInteractionEnabled(true);
            }
            ResetSequenceState();
        }

        internal void CancelStatusTail()
        {
            if (statusTailRoutine != null)
                StopCoroutine(statusTailRoutine);
            statusTailRoutine = null;
            IsStatusTailRunning = false;
            view?.HideRestorationStatus();
        }

        internal void ApplyStatusTailFade(float progress)
        {
            if (!IsStatusTailRunning) return;
            float normalized = Mathf.Clamp01(progress);
            view.RestorationStatus?.SetOpacity(1f - normalized);
        }

        private IEnumerator RunPreparedSequence()
        {
            yield return AnimatePhase(CityRestorationSequencePhase.Focus,
                ProgrammerUiMetrics.CityRestorationFocusSeconds);
            yield return AnimatePhase(CityRestorationSequencePhase.BuildingPowerUp,
                ProgrammerUiMetrics.CityRestorationPowerUpSeconds);
            if (CurrentPlan.HasNextChapter)
            {
                yield return AnimatePhase(CityRestorationSequencePhase.EnergyTravel,
                    ProgrammerUiMetrics.CityRestorationEnergyTravelSeconds);
                yield return AnimatePhase(CityRestorationSequencePhase.NextChapterReveal,
                    ProgrammerUiMetrics.CityRestorationRevealSeconds);
            }
            else if (CurrentPlan.IncludesFinalNetworkPulse)
            {
                yield return WaitPhase(CityRestorationSequencePhase.PreNetworkSettle,
                    ProgrammerUiMetrics.CityRestorationFinalPrePulseSettleSeconds);
                yield return AnimatePhase(CityRestorationSequencePhase.FinalNetworkPulse,
                    ProgrammerUiMetrics.CityRestorationFinalNetworkPulseSeconds);
            }
            yield return WaitPhase(CityRestorationSequencePhase.Settle,
                ProgrammerUiMetrics.CityRestorationSettleSeconds);
            routine = null;
            CompletePreparedSequence();
        }

        private void BeginStatusTail()
        {
            if (statusTailRoutine != null)
                StopCoroutine(statusTailRoutine);
            IsStatusTailRunning = true;
            statusTailRoutine = StartCoroutine(RunStatusTail());
        }

        private IEnumerator RunStatusTail()
        {
            float elapsed = 0f;
            while (elapsed < ProgrammerUiMetrics.RestorationStatusHoldSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            float fadeDuration = ProgrammerUiMetrics.RestorationStatusFadeSeconds;
            if (fadeDuration <= 0f)
            {
                ApplyStatusTailFade(1f);
                view.HideRestorationStatus();
                IsStatusTailRunning = false;
                statusTailRoutine = null;
                yield break;
            }
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyStatusTailFade(elapsed / fadeDuration);
                yield return null;
            }
            ApplyStatusTailFade(1f);
            view.HideRestorationStatus();
            IsStatusTailRunning = false;
            statusTailRoutine = null;
        }

        private IEnumerator WaitPhase(CityRestorationSequencePhase phase, float duration)
        {
            Phase = phase;
            duration *= Mathf.Max(0f, DurationMultiplier);
            if (duration <= 0f) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator AnimatePhase(CityRestorationSequencePhase phase, float duration)
        {
            duration *= Mathf.Max(0f, DurationMultiplier);
            if (duration <= 0f)
            {
                ApplyPhase(phase, 1f);
                yield break;
            }
            float elapsed = 0f;
            ApplyPhase(phase, 0f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyPhase(phase, elapsed / duration);
                yield return null;
            }
            ApplyPhase(phase, 1f);
        }

        private void OnDisable()
        {
            if (IsRunning)
            {
                if (routine != null) StopCoroutine(routine);
                routine = null;
                CancelPreparedSequence();
            }
            CancelStatusTail();
        }

        private void ResetSequenceState()
        {
            IsRunning = false;
            Phase = CityRestorationSequencePhase.None;
            CurrentPlan = null;
        }
    }
}

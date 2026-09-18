using System.Collections;
using NeonGrid.Campaign;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class CityRestorationSequenceController : MonoBehaviour
    {
        private CampaignRuntimeView view;
        private CampaignFlowCoordinator flow;
        private Coroutine routine;

        public bool IsRunning { get; private set; }
        public CityRestorationSequencePhase Phase { get; private set; }
        public CityRestorationSequencePlan CurrentPlan { get; private set; }

        public void Initialize(CampaignRuntimeView runtimeView,
            CampaignFlowCoordinator flowCoordinator)
        {
            view = runtimeView;
            flow = flowCoordinator;
        }

        public void EnterMap()
        {
            if (IsRunning) return;
            if (!PreparePendingRestoration()) return;
            routine = StartCoroutine(RunPreparedSequence());
        }

        internal bool PreparePendingRestoration()
        {
            if (view == null || flow == null) return false;

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
                case CityRestorationSequencePhase.BuildingPowerUp:
                    view.ApplyRestoredNodePowerUp(CurrentPlan, normalized);
                    break;
                case CityRestorationSequencePhase.EnergyTravel:
                    if (CurrentPlan.HasNextChapter)
                        view.ApplyEnergyTravel(CurrentPlan, normalized);
                    break;
                case CityRestorationSequencePhase.NextChapterReveal:
                    if (CurrentPlan.HasNextChapter)
                        view.ApplyNextChapterReveal(CurrentPlan, normalized);
                    break;
            }
        }

        internal bool CompletePreparedSequence()
        {
            if (!IsRunning || CurrentPlan == null) return false;

            Phase = CityRestorationSequencePhase.Settle;
            view.RestoreAuthoritativeMap(false);
            bool consumed = flow.TryConsumePendingRestoration(
                out ChapterRestorationEvent consumedEvent);
            if (!consumed || consumedEvent.RestoredChapterId != CurrentPlan.RestoredChapterId)
            {
                Debug.LogWarning("The restoration sequence completed, but its pending event " +
                                 "could not be durably consumed. It may replay on a later map visit.",
                    this);
            }

            view.SetMapInteractionEnabled(true);
            ResetSequenceState();
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

        private IEnumerator RunPreparedSequence()
        {
            yield return WaitPhase(CityRestorationSequencePhase.Focus,
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
            yield return WaitPhase(CityRestorationSequencePhase.Settle,
                ProgrammerUiMetrics.CityRestorationSettleSeconds);
            routine = null;
            CompletePreparedSequence();
        }

        private IEnumerator WaitPhase(CityRestorationSequencePhase phase, float duration)
        {
            Phase = phase;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator AnimatePhase(CityRestorationSequencePhase phase, float duration)
        {
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
            if (!IsRunning) return;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            CancelPreparedSequence();
        }

        private void ResetSequenceState()
        {
            IsRunning = false;
            Phase = CityRestorationSequencePhase.None;
            CurrentPlan = null;
        }
    }
}

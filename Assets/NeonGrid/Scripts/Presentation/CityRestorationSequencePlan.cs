using System;
using System.Collections.Generic;
using NeonGrid.Campaign;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public enum CityRestorationSequencePhase
    {
        None,
        Focus,
        BuildingPowerUp,
        EnergyTravel,
        NextChapterReveal,
        PreNetworkSettle,
        FinalNetworkPulse,
        Settle
    }

    public sealed class CityRestorationSequencePlan
    {
        public ChapterRestorationEvent RestorationEvent { get; }
        public string RestoredChapterId => RestorationEvent.RestoredChapterId;
        public int RestoredChapterIndex => RestorationEvent.RestoredChapterIndex;
        public bool HasNextChapter => RestorationEvent.HasNextChapter;
        public string NextChapterId => RestorationEvent.NextChapterId;
        public int EnergyPathIndex { get; }
        public ChapterMapVisualState PreRestorationState { get; }
        public bool IncludesFinalNetworkPulse =>
            !HasNextChapter && RestorationEvent.IsCampaignComplete;
        public IReadOnlyList<CityRestorationSequencePhase> Phases { get; }
        public float TotalDuration => IncludesFinalNetworkPulse
            ? ProgrammerUiMetrics.CityRestorationFocusSeconds +
              ProgrammerUiMetrics.CityRestorationPowerUpSeconds +
              ProgrammerUiMetrics.CityRestorationFinalPrePulseSettleSeconds +
              ProgrammerUiMetrics.CityRestorationFinalNetworkPulseSeconds +
              ProgrammerUiMetrics.CityRestorationSettleSeconds
            : ProgrammerUiMetrics.CityRestorationFocusSeconds +
              ProgrammerUiMetrics.CityRestorationPowerUpSeconds +
              (HasNextChapter
                  ? ProgrammerUiMetrics.CityRestorationEnergyTravelSeconds +
                    ProgrammerUiMetrics.CityRestorationRevealSeconds
                  : 0f) +
              ProgrammerUiMetrics.CityRestorationSettleSeconds;

        internal CityRestorationSequencePlan(ChapterRestorationEvent restorationEvent,
            int energyPathIndex, ChapterMapVisualState preRestorationState)
        {
            RestorationEvent = restorationEvent ??
                throw new ArgumentNullException(nameof(restorationEvent));
            EnergyPathIndex = energyPathIndex;
            PreRestorationState = preRestorationState;
            Phases = IncludesFinalNetworkPulse
                ? new[]
                {
                    CityRestorationSequencePhase.Focus,
                    CityRestorationSequencePhase.BuildingPowerUp,
                    CityRestorationSequencePhase.PreNetworkSettle,
                    CityRestorationSequencePhase.FinalNetworkPulse,
                    CityRestorationSequencePhase.Settle
                }
                : HasNextChapter
                    ? new[]
                    {
                        CityRestorationSequencePhase.Focus,
                        CityRestorationSequencePhase.BuildingPowerUp,
                        CityRestorationSequencePhase.EnergyTravel,
                        CityRestorationSequencePhase.NextChapterReveal,
                        CityRestorationSequencePhase.Settle
                    }
                    : new[]
                    {
                        CityRestorationSequencePhase.Focus,
                        CityRestorationSequencePhase.BuildingPowerUp,
                        CityRestorationSequencePhase.Settle
                    };
        }
    }

    internal static class CityRestorationEasing
    {
        internal static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        internal static float EaseInOutCubic(float value)
        {
            float normalized = Mathf.Clamp01(value);
            return normalized < 0.5f
                ? 4f * normalized * normalized * normalized
                : 1f - Mathf.Pow(-2f * normalized + 2f, 3f) * 0.5f;
        }

        internal static float SmoothStep(float value)
        {
            float normalized = Mathf.Clamp01(value);
            return normalized * normalized * (3f - 2f * normalized);
        }
    }
}

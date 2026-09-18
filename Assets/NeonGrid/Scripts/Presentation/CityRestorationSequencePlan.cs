using System;
using NeonGrid.Campaign;

namespace NeonGrid.Presentation
{
    public enum CityRestorationSequencePhase
    {
        None,
        Focus,
        BuildingPowerUp,
        EnergyTravel,
        NextChapterReveal,
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

        internal CityRestorationSequencePlan(ChapterRestorationEvent restorationEvent,
            int energyPathIndex, ChapterMapVisualState preRestorationState)
        {
            RestorationEvent = restorationEvent ??
                throw new ArgumentNullException(nameof(restorationEvent));
            EnergyPathIndex = energyPathIndex;
            PreRestorationState = preRestorationState;
        }
    }
}

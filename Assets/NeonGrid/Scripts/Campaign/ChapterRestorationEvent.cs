using System;

namespace NeonGrid.Campaign
{
    public sealed class ChapterRestorationEvent
    {
        public string RestoredChapterId { get; }
        public int RestoredChapterIndex { get; }
        public string NextChapterId { get; }
        public bool HasNextChapter => NextChapterId != null;
        public bool IsCampaignComplete { get; }

        internal ChapterRestorationEvent(string restoredChapterId, int restoredChapterIndex,
            string nextChapterId, bool isCampaignComplete)
        {
            if (string.IsNullOrWhiteSpace(restoredChapterId))
                throw new ArgumentException("A restored chapter ID is required.",
                    nameof(restoredChapterId));
            if (restoredChapterIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(restoredChapterIndex));

            RestoredChapterId = restoredChapterId;
            RestoredChapterIndex = restoredChapterIndex;
            NextChapterId = string.IsNullOrWhiteSpace(nextChapterId) ? null : nextChapterId;
            IsCampaignComplete = isCampaignComplete;
        }
    }
}

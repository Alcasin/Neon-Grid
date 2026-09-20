using System;
using NeonGrid.Data;

namespace NeonGrid.Presentation
{
    public sealed class CampaignIntroSequence
    {
        private readonly CampaignNarrativeDefinition narrative;

        public int CurrentPageIndex { get; private set; }
        public int PageCount => narrative.IntroPages.Count;
        public CampaignIntroPage CurrentPage => narrative.IntroPages[CurrentPageIndex];
        public bool IsFinalPage => CurrentPageIndex == PageCount - 1;

        public CampaignIntroSequence(CampaignNarrativeDefinition narrative)
        {
            this.narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
            if (narrative.IntroPages == null || narrative.IntroPages.Count == 0)
                throw new ArgumentException("Intro narrative requires at least one page.",
                    nameof(narrative));
        }

        public bool MoveNext()
        {
            if (IsFinalPage) return false;
            CurrentPageIndex++;
            return true;
        }
    }
}

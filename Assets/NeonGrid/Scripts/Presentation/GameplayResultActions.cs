using System;
using NeonGrid.Campaign;

namespace NeonGrid.Presentation
{
    public sealed class GameplayResultActions
    {
        public Action Retry { get; }
        public Action Levels { get; }
        public Action Map { get; }
        public Action Next { get; }
        public Action Leave { get; }
        public Func<CampaignResultNavigationState> GetNavigation { get; }

        public GameplayResultActions(Action retry, Action levels, Action map, Action next,
            Action leave, Func<CampaignResultNavigationState> getNavigation)
        {
            Retry = retry;
            Levels = levels;
            Map = map;
            Next = next;
            Leave = leave;
            GetNavigation = getNavigation;
        }
    }
}

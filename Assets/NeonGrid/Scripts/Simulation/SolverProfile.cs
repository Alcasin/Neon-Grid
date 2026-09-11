using System.Diagnostics;

namespace NeonGrid.Simulation
{
    // Per-call counters used only by the explicit Editor benchmark.
    internal sealed class SolverProfile
    {
        internal long CopyTicks, PowerTicks, KeyTicks, ActionTicks;
        internal long Copies, Powers, Keys, ActionLists;
        public override string ToString()
        {
            double scale = 1000d / Stopwatch.Frequency;
            return $"copies={Copies} copyMs={CopyTicks * scale:F2} " +
                $"powers={Powers} powerMs={PowerTicks * scale:F2} " +
                $"keys={Keys} keyMs={KeyTicks * scale:F2} " +
                $"actionLists={ActionLists} actionMs={ActionTicks * scale:F2}";
        }
    }
}

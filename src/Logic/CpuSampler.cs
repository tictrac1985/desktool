using System;

namespace DeskMate.Logic
{
    /// <summary>CPU busy-fraction math from GetSystemTimes deltas. Pure — the Win32
    /// sampling lives in the feature; this converts tick deltas to a percentage.</summary>
    public static class CpuSampler
    {
        /// <summary>kernelDelta includes idle time on Windows, so
        /// busy = (kernel + user - idle) / (kernel + user).</summary>
        public static double Fraction(ulong idleDelta, ulong kernelDelta, ulong userDelta)
        {
            ulong total = kernelDelta + userDelta;
            if (total == 0) return 0;
            double busy = (double)(total - Math.Min(idleDelta, total)) / total;
            return busy < 0 ? 0 : busy > 1 ? 1 : busy;
        }

        public static double Percent(ulong idleDelta, ulong kernelDelta, ulong userDelta)
        {
            return Math.Round(Fraction(idleDelta, kernelDelta, userDelta) * 1000) / 10;
        }
    }
}

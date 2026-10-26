using System;

namespace DS4Windows.InputDevices
{
    internal sealed class Vader4ProReportClock
    {
        private long startTicks;
        private long previousTicks;

        public double ElapsedSeconds { get; private set; }
        public ulong TotalMicroseconds { get; private set; }
        public ushort Timestamp { get; private set; }

        public void Reset(long nowTicks)
        {
            startTicks = previousTicks = nowTicks;
            ElapsedSeconds = 0;
            TotalMicroseconds = 0;
            Timestamp = 0;
        }

        public void Advance(long nowTicks, long frequency)
        {
            ElapsedSeconds = Math.Max(0, nowTicks - previousTicks) / (double)frequency;
            previousTicks = nowTicks;
            double totalSeconds = Math.Max(0, nowTicks - startTicks) / (double)frequency;
            // Convert absolute time so fractional microseconds are not lost on
            // every report. DS4 ticks are 16/3 us, wrapping modulo 65536.
            TotalMicroseconds = (ulong)(totalSeconds * 1_000_000);
            Timestamp = unchecked((ushort)(ulong)(totalSeconds * 187_500));
        }
    }
}

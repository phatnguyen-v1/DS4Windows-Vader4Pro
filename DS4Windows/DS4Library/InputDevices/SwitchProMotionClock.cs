using System;

namespace DS4Windows.InputDevices
{
    // Paces motion time from the report timer byte instead of host arrival time.
    // Over Bluetooth, reports the controller sends every 10 ms can arrive 8 or
    // 16 ms apart; timing motion by arrival makes steady turns hitch. The timer
    // counts at a fixed rate, so ticks give the real spacing between samples.
    internal sealed class SwitchProMotionClock
    {
        // Typical tick length; refined at runtime from host time so the long-run
        // rate matches the wall clock on any controller.
        public const double DefaultTickSeconds = 0.005;
        private const double MinTickSeconds = 0.001;
        private const double MaxTickSeconds = 0.02;
        private const double EstimateWindowSeconds = 2.0;
        // Larger gaps (reconnect, stall, timer reset) fall back to host time
        private const int MaxTickGap = 40;
        private const double MaxHostGapSeconds = 0.25;

        private bool started;
        private byte previousTimer;
        // Exponentially decayed host time and timer ticks; their ratio is the tick length
        private double hostSum;
        private double tickSum;
        private double totalSeconds;

        public double TickSeconds { get; private set; } = DefaultTickSeconds;
        public double ElapsedSeconds { get; private set; }
        public ulong TotalMicroseconds { get; private set; }
        // DS4 timestamp ticks are 16/3 us, wrapping modulo 65536
        public ushort Timestamp { get; private set; }

        public void Reset()
        {
            started = false;
            hostSum = tickSum = 0;
            totalSeconds = 0;
            TickSeconds = DefaultTickSeconds;
            ElapsedSeconds = 0;
            TotalMicroseconds = 0;
            Timestamp = 0;
        }

        public void Advance(byte timer, double hostElapsedSeconds)
        {
            if (!started)
            {
                started = true;
                previousTimer = timer;
                ElapsedSeconds = 0;
                Publish();
                return;
            }

            int ticks = (byte)(timer - previousTimer);
            previousTimer = timer;
            bool hostValid = double.IsFinite(hostElapsedSeconds) && hostElapsedSeconds > 0 &&
                hostElapsedSeconds <= MaxHostGapSeconds;
            if (ticks == 0 || ticks > MaxTickGap || !hostValid)
            {
                ElapsedSeconds = hostValid ? hostElapsedSeconds : 0;
            }
            else
            {
                // Ratio of totals over the last few seconds: arrival jitter averages
                // out, and the first reports already give a plain mean.
                double decay = Math.Exp(-hostElapsedSeconds / EstimateWindowSeconds);
                hostSum = hostSum * decay + hostElapsedSeconds;
                tickSum = tickSum * decay + ticks;
                TickSeconds = Math.Clamp(hostSum / tickSum, MinTickSeconds, MaxTickSeconds);
                ElapsedSeconds = ticks * TickSeconds;
            }

            totalSeconds += ElapsedSeconds;
            Publish();
        }

        private void Publish()
        {
            TotalMicroseconds = (ulong)(totalSeconds * 1_000_000);
            Timestamp = unchecked((ushort)(ulong)(totalSeconds * 187_500));
        }
    }
}

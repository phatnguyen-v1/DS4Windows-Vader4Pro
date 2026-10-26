using System;

namespace DS4Windows.InputDevices
{
    // Reconstructs continuous output from the firmware's held gyro samples.
    // Values and speed thresholds use DS4 units (16 units per degree/second).
    internal sealed class Vader4ProGyroFilter
    {
        private const double SlowSpeed = 8 * 16;
        private const double FastSpeed = 80 * 16;
        private const double ResumeGapSeconds = 0.1;
        private double value;
        private bool initialized;

        public void Reset() => initialized = false;

        public double Filter(double input, double elapsedSeconds, int smoothingMilliseconds)
        {
            if (!initialized || !double.IsFinite(elapsedSeconds) ||
                elapsedSeconds <= 0 || elapsedSeconds > ResumeGapSeconds || smoothingMilliseconds <= 0)
            {
                initialized = true;
                return value = input;
            }

            // Keep a constant response around zero: changing the coefficient on every
            // 0/1 quantization step would change the average speed of fine aiming.
            double speed = Math.Max(Math.Abs(input), Math.Abs(value));
            double blend = Math.Clamp((speed - SlowSpeed) / (FastSpeed - SlowSpeed), 0, 1);
            blend = blend * blend * (3 - 2 * blend);
            double slowTime = Math.Clamp(smoothingMilliseconds, 0, 40) * 0.001;
            double timeConstant = slowTime + (Math.Min(slowTime, 0.002) - slowTime) * blend;

            // Repeated raw values represent velocity held over elapsed time.
            // Exponential time weighting gives the same response at 500 and 1000 Hz.
            double alpha = 1 - Math.Exp(-elapsedSeconds / timeConstant);
            value += alpha * (input - value);

            // Remove the tail only below half an output unit. A raw zero alone
            // is not evidence that the controller stopped moving.
            if (input == 0 && Math.Abs(value) < 0.5)
                value = 0;
            return value;
        }

        public static short ToInt16(double value) =>
            (short)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), short.MinValue, short.MaxValue);
    }
}

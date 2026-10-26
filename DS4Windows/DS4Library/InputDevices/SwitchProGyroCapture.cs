using System.Globalization;

namespace DS4Windows.InputDevices
{
    // One CSV row per IMU sample; Switch Pro reports carry three samples each
    internal static class SwitchProGyroCapture
    {
        public const string Header =
            "seq,host_us,utc_ms,dt_us,motion_dt_us,skipped_reports,timer,imu_idx," +
            "yaw_raw,pitch_raw,roll_raw,accel_x_raw,accel_y_raw,accel_z_raw,used";

        public static GyroCaptureWriter<Sample> Start(string directory, string deviceId) =>
            new GyroCaptureWriter<Sample>(directory, "switchpro-gyro", deviceId, Header, "Switch Pro");

        public struct Sample : IGyroCaptureSample
        {
            public ulong HostMicroseconds;
            public long UtcMilliseconds;
            public double ElapsedSeconds;
            // Timer-paced interval DS4Windows uses for motion
            public double MotionElapsedSeconds;
            // Reports dropped by the input loop's 5 ms poll filter before this one
            public int SkippedReports;
            public byte Timer;
            public byte ImuIndex;
            public short YawRaw, PitchRaw, RollRaw;
            public short AccelXRaw, AccelYRaw, AccelZRaw;
            // Whether DS4Windows forwards this sample (it uses only the newest)
            public bool Used;

            public string ToCsv()
            {
                CultureInfo inv = CultureInfo.InvariantCulture;
                return string.Join(",",
                    HostMicroseconds.ToString(inv),
                    UtcMilliseconds.ToString(inv),
                    System.Math.Round(ElapsedSeconds * 1_000_000).ToString(inv),
                    System.Math.Round(MotionElapsedSeconds * 1_000_000).ToString(inv),
                    SkippedReports.ToString(inv),
                    Timer.ToString(inv),
                    ImuIndex.ToString(inv),
                    YawRaw.ToString(inv), PitchRaw.ToString(inv), RollRaw.ToString(inv),
                    AccelXRaw.ToString(inv), AccelYRaw.ToString(inv), AccelZRaw.ToString(inv),
                    Used ? "1" : "0");
            }
        }
    }
}

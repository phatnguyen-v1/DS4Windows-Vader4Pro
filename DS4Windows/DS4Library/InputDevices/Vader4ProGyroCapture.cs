using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DS4Windows.InputDevices
{
    // One CSV row per valid Vader report
    internal static class Vader4ProGyroCapture
    {
        public const string Header =
            "seq,host_us,utc_ms,dt_us,yaw_raw,pitch_raw,roll_raw,accel_x_raw,accel_y_raw,accel_z_raw," +
            "yaw_out,pitch_out,roll_out,smoothing_ms,report_hex";

        public static GyroCaptureWriter<Sample> Start(string directory, string deviceId) =>
            new GyroCaptureWriter<Sample>(directory, "vader4pro-gyro", deviceId, Header, "Vader 4 Pro");

        public struct Sample : IGyroCaptureSample
        {
            public ulong HostMicroseconds;
            public long UtcMilliseconds;
            public double ElapsedSeconds;
            public short YawRaw, PitchRaw, RollRaw;
            public short AccelXRaw, AccelYRaw, AccelZRaw;
            public short YawOut, PitchOut, RollOut;
            public int SmoothingMilliseconds;
            // Full 32-byte report, kept so unknown fields can be analyzed later
            public ulong Report0, Report1, Report2, Report3;

            public void SetReport(ReadOnlySpan<byte> report)
            {
                Report0 = MemoryMarshal.Read<ulong>(report.Slice(0, 8));
                Report1 = MemoryMarshal.Read<ulong>(report.Slice(8, 8));
                Report2 = MemoryMarshal.Read<ulong>(report.Slice(16, 8));
                Report3 = MemoryMarshal.Read<ulong>(report.Slice(24, 8));
            }

            public string ToCsv()
            {
                Span<ulong> words = stackalloc ulong[] { Report0, Report1, Report2, Report3 };
                CultureInfo inv = CultureInfo.InvariantCulture;
                return string.Join(",",
                    HostMicroseconds.ToString(inv),
                    UtcMilliseconds.ToString(inv),
                    Math.Round(ElapsedSeconds * 1_000_000).ToString(inv),
                    YawRaw.ToString(inv), PitchRaw.ToString(inv), RollRaw.ToString(inv),
                    AccelXRaw.ToString(inv), AccelYRaw.ToString(inv), AccelZRaw.ToString(inv),
                    YawOut.ToString(inv), PitchOut.ToString(inv), RollOut.ToString(inv),
                    SmoothingMilliseconds.ToString(inv),
                    Convert.ToHexString(MemoryMarshal.AsBytes(words)));
            }
        }
    }
}

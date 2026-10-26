using DS4Windows;
using DS4Windows.InputDevices;
using System.Xml;

namespace DS4WindowsTests;

[TestClass]
public class Vader4ProGyroTests
{
    [TestMethod]
    public void RepeatedSamplesConvergeInsteadOfFreezingAtPartialSpeed()
    {
        var filter = new Vader4ProGyroFilter();
        filter.Filter(0, 0.001, 16);
        double first = filter.Filter(62.5, 0.001, 16);
        double output = first;
        for (int i = 0; i < 100; i++)
            output = filter.Filter(62.5, 0.001, 16);
        Assert.IsTrue(first > 0 && first < 62.5);
        Assert.AreEqual(62.5, output, 0.2);
    }

    [TestMethod]
    public void FineAimingResponseIsIndependentOfReportRateAndTimingJitter()
    {
        var oneKhz = new Vader4ProGyroFilter();
        var fiveHundredHz = new Vader4ProGyroFilter();
        var irregular = new Vader4ProGyroFilter();
        oneKhz.Filter(0, 0.001, 16);
        fiveHundredHz.Filter(0, 0.001, 16);
        irregular.Filter(0, 0.001, 16);
        for (int sample = 0; sample < 100; sample++)
        {
            double raw = sample % 3 * 62.5;
            double a = 0, b = 0;
            for (int report = 0; report < 10; report++)
                a = oneKhz.Filter(raw, 0.001, 16);
            for (int report = 0; report < 5; report++)
                b = fiveHundredHz.Filter(raw, 0.002, 16);
            irregular.Filter(raw, 0.0035, 16);
            double c = irregular.Filter(raw, 0.0065, 16);
            Assert.AreEqual(a, b, 0.000001);
            Assert.AreEqual(a, c, 0.000001);
        }
    }

    [TestMethod]
    public void ZeroCrossingQuantizationIsSmoothedWithoutLosingAverageAimSpeed()
    {
        var filter = new Vader4ProGyroFilter();
        double min = double.MaxValue, max = double.MinValue, sum = 0;
        int count = 0;
        // A 100 Hz gyro alternating raw 0/1, carried by a 1000 Hz HID stream.
        for (int i = 0; i < 2000; i++)
        {
            double input = i / 10 % 2 * 62.5;
            double output = filter.Filter(input, 0.001, 16);
            if (i < 1000) continue;
            min = Math.Min(min, output);
            max = Math.Max(max, output);
            sum += output;
            count++;
        }
        Assert.IsTrue(max - min < 62.5 * 0.35, $"Ripple: {max - min}");
        Assert.AreEqual(31.25, sum / count, 0.01);
    }

    [TestMethod]
    public void FastTurnsReachNinetyFivePercentWithinSixMilliseconds()
    {
        var filter = new Vader4ProGyroFilter();
        filter.Filter(0, 0.001, 16);
        double output = 0;
        for (int i = 0; i < 6; i++)
            output = filter.Filter(2000, 0.001, 16);
        Assert.IsTrue(output >= 1900 && output <= 2000);
        for (int i = 0; i < 6; i++)
            output = filter.Filter(-2000, 0.001, 16);
        Assert.IsTrue(output < -1800 && output >= -2000);
    }

    [TestMethod]
    public void StopDecaysSmoothlyThenReachesExactZero()
    {
        var filter = new Vader4ProGyroFilter();
        filter.Filter(62.5, 0.001, 16);
        double previous = filter.Filter(0, 0.001, 16);
        Assert.IsTrue(previous > 0 && previous < 62.5);
        for (int i = 0; i < 80; i++)
        {
            double output = filter.Filter(0, 0.001, 16);
            Assert.IsTrue(output >= 0 && output <= previous);
            previous = output;
        }
        Assert.AreEqual(0, previous);
    }

    [TestMethod]
    public void ResetPauseAndDisabledSmoothingDiscardOldMotion()
    {
        var filter = new Vader4ProGyroFilter();
        filter.Filter(100, 0.001, 16);
        Assert.AreEqual(0, filter.Filter(0, 0.2, 16));
        Assert.AreEqual(-62.5, filter.Filter(-62.5, 0.001, 0));
        filter.Reset();
        Assert.AreEqual(125, filter.Filter(125, 0.001, 16));
        Assert.AreEqual(0, filter.Filter(0, double.NaN, 16));
    }

    [TestMethod]
    public void ConversionRoundsSymmetricallyAndSaturatesInsteadOfWrapping()
    {
        Assert.AreEqual((short)63, Vader4ProGyroFilter.ToInt16(62.5));
        Assert.AreEqual((short)-63, Vader4ProGyroFilter.ToInt16(-62.5));
        Assert.AreEqual(short.MaxValue, Vader4ProGyroFilter.ToInt16(100000));
        Assert.AreEqual(short.MinValue, Vader4ProGyroFilter.ToInt16(-100000));
    }

    [TestMethod]
    public void ClockUsesCurrentIntervalAndPreservesFractionalMicroseconds()
    {
        var clock = new Vader4ProReportClock();
        const long start = 1000000000000;
        clock.Reset(start);
        clock.Advance(start + 10009, 10000000);
        Assert.AreEqual(0.0010009, clock.ElapsedSeconds, 0.000000001);
        for (int i = 2; i <= 1000; i++)
            clock.Advance(start + i * 10009L, 10000000);
        Assert.AreEqual(1000900.0, (double)clock.TotalMicroseconds, 1.0);
        clock.Advance(start + 1000 * 10009L + 20000, 10000000);
        Assert.AreEqual(0.002, clock.ElapsedSeconds, 0.000000001);
    }

    [TestMethod]
    public void ClockWrapsAt65536AndRestartsCleanly()
    {
        var clock = new Vader4ProReportClock();
        clock.Reset(0);
        clock.Advance(65535, 187500);
        Assert.AreEqual(ushort.MaxValue, clock.Timestamp);
        clock.Advance(65536, 187500);
        Assert.AreEqual((ushort)0, clock.Timestamp);
        clock.Advance(65537, 187500);
        Assert.AreEqual((ushort)1, clock.Timestamp);
        clock.Reset(100000);
        clock.Advance(100001, 187500);
        Assert.AreEqual((ushort)1, clock.Timestamp);
        Assert.AreEqual(1.0 / 187500, clock.ElapsedSeconds, 0.000000001);
    }

    [TestMethod]
    public void SmoothingSettingsRoundTripAndOldConfigsUseDefault()
    {
        var options = new Vader4ProControllerOptions(InputDeviceType.Vader4Pro);
        var xml = new XmlDocument();
        xml.LoadXml("<Controller><Vader4ProSupportSettings><EnableHomeLED>True</EnableHomeLED></Vader4ProSupportSettings></Controller>");
        options.LoadSettings(xml, xml.DocumentElement);
        Assert.AreEqual(16, options.GyroSmoothingMilliseconds);
        foreach (int setting in new[] { 0, 24, 40 })
        {
            options.GyroSmoothingMilliseconds = setting;
            options.PersistSettings(xml, xml.DocumentElement);
            var loaded = new Vader4ProControllerOptions(InputDeviceType.Vader4Pro);
            loaded.LoadSettings(xml, xml.DocumentElement);
            Assert.AreEqual(setting, loaded.GyroSmoothingMilliseconds);
        }
        options.GyroSmoothingMilliseconds = -1;
        Assert.AreEqual(0, options.GyroSmoothingMilliseconds);
        options.GyroSmoothingMilliseconds = 100;
        Assert.AreEqual(40, options.GyroSmoothingMilliseconds);
    }

    [TestMethod]
    public void GyroCaptureWritesEveryReportAndIsNotPersisted()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vader-capture-" + Guid.NewGuid().ToString("N"));
        try
        {
            byte[] report = new byte[32];
            for (int i = 0; i < report.Length; i++)
                report[i] = (byte)i;
            string path;
            using (var capture = Vader4ProGyroCapture.Start(dir, "AA:BB:CC:DD:EE:FF"))
            {
                path = capture.FilePath;
                for (short i = 0; i < 3; i++)
                {
                    var sample = new Vader4ProGyroCapture.Sample
                    {
                        HostMicroseconds = 1000UL * (ulong)i,
                        UtcMilliseconds = 1_700_000_000_000L + i,
                        ElapsedSeconds = 0.001,
                        YawRaw = (short)(i - 1),
                        PitchRaw = 512,
                        RollRaw = short.MinValue,
                        YawOut = (short)(-62 * i),
                        SmoothingMilliseconds = 16,
                    };
                    sample.SetReport(report);
                    capture.Record(sample);
                }
            }

            string[] lines = File.ReadAllLines(path);
            Assert.AreEqual(Vader4ProGyroCapture.Header, lines[0]);
            Assert.AreEqual(4, lines.Length);
            string[] last = lines[3].Split(',');
            Assert.AreEqual(Vader4ProGyroCapture.Header.Split(',').Length, last.Length);
            Assert.AreEqual("2", last[0]);
            Assert.AreEqual("2000", last[1]);
            Assert.AreEqual("1700000000002", last[2]);
            Assert.AreEqual("1000", last[3]);
            Assert.AreEqual("1", last[4]);
            Assert.AreEqual("512", last[5]);
            Assert.AreEqual("-32768", last[6]);
            Assert.AreEqual("-124", last[10]);
            Assert.AreEqual(Convert.ToHexString(report), last[14]);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        var options = new Vader4ProControllerOptions(InputDeviceType.Vader4Pro) { CaptureGyroLog = true };
        var xml = new XmlDocument();
        xml.LoadXml("<Controller />");
        options.PersistSettings(xml, xml.DocumentElement);
        var loaded = new Vader4ProControllerOptions(InputDeviceType.Vader4Pro);
        loaded.LoadSettings(xml, xml.DocumentElement);
        Assert.IsFalse(loaded.CaptureGyroLog);
    }
}

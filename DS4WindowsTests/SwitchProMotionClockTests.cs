using DS4Windows.InputDevices;

namespace DS4WindowsTests;

[TestClass]
public class SwitchProMotionClockTests
{
    // Arrival pattern measured from a Vader 4 Pro in Switch mode over Bluetooth:
    // the timer advances 2 per report but reports land 8 or 16 ms apart.
    private static readonly double[] JitteredArrivals = { 0.008, 0.008, 0.008, 0.016 };

    [TestMethod]
    public void BluetoothArrivalJitterIsRemovedFromMotionTiming()
    {
        var clock = new SwitchProMotionClock();
        byte timer = 0;
        clock.Advance(timer, 0);
        double hostTotal = 0;
        for (int i = 0; i < 2000; i++)
        {
            timer += 2;
            double host = JitteredArrivals[i % JitteredArrivals.Length];
            hostTotal += host;
            clock.Advance(timer, host);
            if (i >= 400)
                Assert.AreEqual(0.010, clock.ElapsedSeconds, 0.0003, $"report {i}");
        }
        // Paced time stays on the wall clock over the long run
        Assert.AreEqual(hostTotal, clock.TotalMicroseconds / 1e6, hostTotal * 0.01);
    }

    [TestMethod]
    public void TickLengthIsLearnedForOtherReportRates()
    {
        // Genuine Switch Pro style: three ticks per ~15 ms report
        var clock = new SwitchProMotionClock();
        byte timer = 10;
        clock.Advance(timer, 0);
        for (int i = 0; i < 50; i++)
        {
            timer += 3;
            clock.Advance(timer, i % 2 == 0 ? 0.012 : 0.018);
        }
        Assert.AreEqual(0.015, clock.ElapsedSeconds, 0.001);
    }

    [TestMethod]
    public void TimerWrapCountsForwardTicks()
    {
        var clock = new SwitchProMotionClock();
        clock.Advance(254, 0);
        clock.Advance(0, 0.010);
        Assert.AreEqual(2 * 0.005, clock.ElapsedSeconds, 0.0001);
    }

    [TestMethod]
    public void UnreliableTimerFallsBackToHostTime()
    {
        var clock = new SwitchProMotionClock();
        clock.Advance(0, 0);
        clock.Advance(0, 0.010);          // timer did not move
        Assert.AreEqual(0.010, clock.ElapsedSeconds, 1e-9);
        clock.Advance(100, 0.120);        // implausible tick jump
        Assert.AreEqual(0.120, clock.ElapsedSeconds, 1e-9);
        clock.Advance(102, 1.5);          // long stall: do not integrate stale motion
        Assert.AreEqual(0, clock.ElapsedSeconds);
        clock.Advance(104, double.NaN);
        Assert.AreEqual(0, clock.ElapsedSeconds);
    }

    [TestMethod]
    public void TimestampUsesDs4UnitsAndWraps()
    {
        var clock = new SwitchProMotionClock();
        byte timer = 0;
        clock.Advance(timer, 0);
        Assert.AreEqual((ushort)0, clock.Timestamp);
        // 100 reports x 2 ticks x 5 ms = 1 s = 187500 DS4 ticks, wrapped
        for (int i = 0; i < 100; i++)
        {
            timer += 2;
            clock.Advance(timer, 0.010);
        }
        Assert.AreEqual(1_000_000.0, clock.TotalMicroseconds, 10.0);
        Assert.AreEqual(187_500 % 65_536, (double)clock.Timestamp, 2.0);
        clock.Reset();
        Assert.AreEqual((ushort)0, clock.Timestamp);
        Assert.AreEqual(SwitchProMotionClock.DefaultTickSeconds, clock.TickSeconds);
    }
}

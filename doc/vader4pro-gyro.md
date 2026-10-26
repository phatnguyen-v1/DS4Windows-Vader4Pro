# Vader 4 Pro gyro smoothing

For a first in-game comparison in Horizon Forbidden West over the dongle, keep
the same game sensitivity and DS4 passthrough profile. In DS4Windows Settings,
open Device Options and select the Vader in Detected Controllers. The Gyro
smoothing slider applies immediately and saves per controller when the window closes.

- **16 ms (default):** starting point for fine aiming.
- **24 ms:** try if slow aiming is still uneven; adds more delay.
- **8 ms:** try if 16 feels too heavy.
- **0 ms:** raw output for comparison.

These values are filter time constants, not a fixed input delay. The response
becomes faster between 8 and 80 degrees/second, reaching a 2 ms time constant
at fast speeds. Roll retains its existing passthrough behavior.

Compare holding still, slow horizontal/vertical tracking, small diagonal
corrections, fast reversals, and stopping. Check with and without game vibration:
a smoothing filter cannot distinguish intentional rotation from physical vibration.
The default is a candidate for play testing, not a hardware-validated optimum.

## Changes

The old filter updated only when a raw value changed, so a repeated value could
leave output stuck at a fraction of the actual speed. It also snapped to zero on
every zero sample, distorting slow movement that alternated between zero and the
first nonzero reading. Its adaptive coefficient used the difference from filtered
output rather than angular speed.

The replacement uses `alpha = 1 - exp(-elapsed / timeConstant)` on every valid
report, including held samples. Fine movement uses a constant coefficient across
small quantization steps, without an input dead zone. A zero sample decays normally;
the tail is cleared only below half a DS4 output unit. A pause over 100 ms resets
filter history. Conversion rounds symmetrically and saturates instead of wrapping.

Timing now starts from the input thread's clock origin, uses the current interval
before motion processing, retains fractional time across reports, and wraps the
DS4 timestamp at 65536. Command reports do not publish stale motion.

## Verification and limits

`Vader4ProGyroTests` covers convergence on repeated samples, timing variation,
500/1000 Hz agreement during fine aiming, zero crossings, stop tails, fast turns,
reset/bypass, numeric limits, timestamp rollover, and settings persistence.

For synthetic 100 Hz samples alternating 0 and 62.5 DS4 units, the default filter
reduces peak-to-peak ripple by more than 65% while preserving mean velocity. A
synthetic 125 degree/second turn reaches 95% within 6 ms. These are software tests,
not measurements of game latency or this controller's sensor.

The existing parser scale is retained. The upstream
[Vader4ProReader](https://github.com/dantmnf/Vader4ProReader#known-issues) warns that
IMU ranges differ across models and firmware. The often-cited 100 Hz measurement
is from [Vader 3 Pro](https://github.com/fishchev/vader-3-pro-gyro), not a measurement
of this Vader 4 Pro unit. Smoothing cannot recover movement omitted by firmware.
If tuning still falls short, capture raw reports with firmware version and compare
integrated gyro output during known-angle rotations before changing scale factors
or assuming the sensor itself is the limiting factor.

## Capturing raw gyro data

In Device Options → Vader 4 Pro, tick **Capture raw gyro to CSV**. DS4Windows
records every report until you untick it or restart. The log shows the file path,
which is under `VaderGyroCapture` in the DS4Windows settings folder (about 10 MB per minute).
Columns: report sequence, host time and interval (µs), raw yaw/pitch/roll and
accelerometer, filtered gyro sent to DS4 output, the smoothing setting, and the
full 32-byte report in hex.

Useful recordings:
- 10 s held still on a table (noise and bias)
- slow, steady yaw turns of about 1–5 °/s (quantization and dead zone)
- a 90° yaw turn on a flat surface between two marks (scale check)
- fast flicks (update rate and saturation)

Summarize a capture with:

    python tools/vader_gyro_analyze.py <capture.csv>

It reports the HID report rate, each axis's range and smallest raw step, how
often each raw axis changes (the gyro update rate while moving), and which report
bytes change on most reports (possible IMU sample counters). For the scale check,
the sum of `yaw_raw × 62.5 / 16 × dt_us / 1e6` over a 90° turn should be close to 90.

### Switch mode comparison

With the Vader paired in Switch mode, DS4Windows sees it as a Switch Pro
controller. The Switch Pro panel in Device Options has the same capture checkbox;
files go to `SwitchProGyroCapture`. Each report carries three IMU samples, so the
CSV has one row per sample (`imu_idx` 0–2). `used` marks the one sample DS4Windows
currently forwards, and `skipped_reports` counts reports its 5 ms poll filter
dropped. The same analysis script detects the format.

### Switch mode timing fix

Measured over Bluetooth: one new IMU sample per report at 100 Hz (each report
repeats the previous two samples), with ~0.07 deg/s resolution. Reports arrive
8 or 16 ms apart even though the controller timer (report byte 1) advances
exactly 2 ticks (~5 ms each) per report. The Switch Pro driver also never set the
virtual DS4 motion timestamp.

`SwitchProMotionClock` now paces motion time from the timer byte: tick length is
the ratio of decayed host time to decayed ticks over ~2 s, so it stays on the
wall clock and adapts to other controllers. It sets `elapsedTime`,
`totalMicroSec` and `ds4Timestamp`. Implausible timer steps fall back to host
time; stalls over 250 ms report zero elapsed time. Replaying a 25k-report capture,
motion intervals went from 10.0 ± 3.5 ms (arrival) to 10.00 ± 0.01 ms (paced).
The capture's `motion_dt_us` column records the paced interval.

"""Summarize a gyro capture CSV written by DS4Windows (Vader 4 Pro or Switch Pro).

Usage: python tools/vader_gyro_analyze.py <capture.csv>
"""
import collections
import csv
import math
import statistics
import sys
from functools import reduce

AXES = ("yaw", "pitch", "roll")


def pct(values, p):
    values = sorted(values)
    return values[min(len(values) - 1, int(p / 100 * len(values)))]


def smallest_step(raw):
    steps = [abs(b - a) for a, b in zip(raw, raw[1:]) if b != a]
    return reduce(math.gcd, steps) if steps else 0


def report_timing(t, dt):
    duration = (t[-1] - t[0]) / 1e6
    print(f"reports: {len(t)} over {duration:.2f} s = {(len(t) - 1) / duration:.0f} Hz")
    print(f"report interval us: median {statistics.median(dt)}, p99 {pct(dt, 99)}, max {max(dt)}")
    return duration


def analyze_vader(rows):
    t = [int(r["host_us"]) for r in rows]
    report_timing(t, [int(r["dt_us"]) for r in rows[1:]])
    for axis in AXES:
        raw = [int(r[f"{axis}_raw"]) for r in rows]
        # Update rate: time between reports where the raw value changed.
        changes = [t[i] for i in range(1, len(raw)) if raw[i] != raw[i - 1]]
        gaps = [b - a for a, b in zip(changes, changes[1:])]
        rate = f"{1e6 / statistics.median(gaps):.0f} Hz (median gap {statistics.median(gaps)} us)" if gaps else "n/a"
        print(f"{axis}: range [{min(raw)}, {max(raw)}], {len(set(raw))} distinct, "
              f"smallest step {smallest_step(raw)}, change rate {rate}")

    # Bytes that change on most reports may be an IMU sample counter.
    reports = [bytes.fromhex(r["report_hex"]) for r in rows]
    print("report bytes that change in >50% of reports:")
    for i in range(len(reports[0])):
        changed = sum(1 for a, b in zip(reports, reports[1:]) if a[i] != b[i])
        if changed > len(reports) / 2:
            print(f"  byte {i}: changes in {100 * changed / (len(reports) - 1):.0f}% of reports")


def analyze_switch(rows):
    first = [r for r in rows if r["imu_idx"] == "0"]
    t = [int(r["host_us"]) for r in first]
    duration = report_timing(t, [int(r["dt_us"]) for r in first[1:]])
    skipped = sum(int(r["skipped_reports"]) for r in first)
    print(f"reports dropped by DS4Windows' 5 ms poll filter: {skipped}")
    if "motion_dt_us" in rows[0]:
        motion = [int(r["motion_dt_us"]) for r in first[1:]]
        print(f"timer-paced motion interval us: median {statistics.median(motion)}, "
              f"stdev {statistics.pstdev(motion):.0f} (arrival stdev {statistics.pstdev(int(r['dt_us']) for r in first[1:]):.0f})")
    fields = ("yaw_raw", "pitch_raw", "roll_raw", "accel_x_raw", "accel_y_raw", "accel_z_raw")
    reports = {}
    for r in rows:
        reports.setdefault(r["host_us"], []).append(tuple(int(r[k]) for k in fields))
    reports = [v for v in reports.values() if len(v) == 3]
    # Reports may repeat earlier samples: count how many leading samples of each
    # report were already the trailing samples of the previous one.
    new_total = 0
    for a, b in zip(reports, reports[1:]):
        new_total += next((k for k in (1, 2) if b[:3 - k] == a[k:]), 3)
    per_report = new_total / (len(reports) - 1)
    print(f"IMU samples: 3 per report, {per_report:.2f} new per report "
          f"-> ~{per_report * (len(first) - 1) / duration:.0f} Hz real IMU updates; "
          "DS4Windows forwards the newest")
    for axis in AXES:
        raw = [int(r[f"{axis}_raw"]) for r in rows]
        print(f"{axis}: range [{min(raw)}, {max(raw)}], {len(set(raw))} distinct, "
              f"smallest step {smallest_step(raw)}")
    gaps = collections.Counter(round((b - a) / 1000) for a, b in zip(t, t[1:]))
    print("report arrival gaps (ms: count):", dict(sorted(gaps.items())))


def main(path):
    rows = list(csv.DictReader(open(path, newline="")))
    if len(rows) < 6:
        sys.exit("capture has too few rows")
    if "imu_idx" in rows[0]:
        analyze_switch(rows)
    else:
        analyze_vader(rows)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])

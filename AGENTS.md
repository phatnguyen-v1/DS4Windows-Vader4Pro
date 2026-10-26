# AGENTS.md

## Project Overview

DS4Windows is a WPF application that maps DualShock 4, DualSense, Switch Pro, and JoyCon controllers to virtual Xbox 360 controllers on Windows. Built with .NET 8.0 and WPF.

## Build

```powershell
dotnet build DS4WindowsWPF.sln -c Debug -p:Platform=x64
dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64
```

Supported platforms: `x64`, `x86`.

## Test

```powershell
dotnet test DS4WindowsTests\DS4WindowsTests.csproj -c Debug
```

Uses MSTest framework (MSTest.TestAdapter + MSTest.TestFramework).

## Project Structure

- `DS4Windows/` — Main WPF application (DS4WinWPF.csproj)
- `DS4WindowsTests/` — Unit tests (MSTest)
- `DS4Windows/DS4Control/` — Controller device handling (HID input, output, etc.)
- `DS4Windows/HidLibrary/` — HID device library
- `DS4Windows/DS4Forms/` — WPF UI forms and controls
- `DS4Windows/DS4Library/` — Core library logic
- `DS4Windows/VJoyFeeder/` — vJoy virtual joystick integration
- `DS4Windows/Translations/` — Localized string resources (Strings.resx + language variants)
- `DS4Windows/Resources/` — Icons and images
- `libs/` — External native DLLs (ViGEm, FakerInput, SharpOSC)

## Key Configuration

- Target framework: `net8.0-windows`
- Allow unsafe blocks for some platform-specific code
- Server GC enabled
- Solution: Visual Studio 17 (2022)

## Feature Notes

### Touchpad Swipe Mapping

Allows any controller button to be mapped to a touchpad swipe gesture (Up/Down/Left/Right) via the binding UI.

**Key files:**
- `DS4Windows/DS4Control/FakeSwipeInjector.cs` — Per-device state machine that injects fake touchpad touch data into `DS4State`
- `DS4Windows/DS4Control/ScpUtil.cs` — `X360Controls` enum contains `SwipeTouchUp/Down/Left/Right` (placed after `Unbound`)
- `DS4Windows/DS4Control/Mapping.cs` — Routes swipe button presses to `FakeSwipeInjector.SetSwipeState()`
- `DS4Windows/DS4Control/ControlService.cs` — Calls `FakeSwipeInjector.ApplyToState()` after touch data copy in `On_Report`
- `DS4Windows/DS4Forms/BindingWindow.xaml(.cs)` — Swipe buttons in the Abs Mouse tab

**How it works:** Three-phase approach:
1. Center hold (3 frames) — finger at center (960, 471) to establish contact
2. Move (24 frames) — finger drifts toward endpoint (±400px) at ~17 units/frame
3. Release — finger lifts past endpoint (+40px) so the game sees clear velocity vector

Total ~27 active frames (~68ms at 400Hz). The finger never parks stationary — it keeps drifting past the endpoint (clamped to touchpad edges) so velocity is always present at lift, even after a long button hold. Presses arriving during an active swipe are dropped (no queue, no conflicts).

### Vader 4 Pro Gyro (PC/dongle mode)

Measured (see `doc/vader4pro-gyro.md`): yaw/pitch arrive as raw [-512, 512] at 3.9 deg/s
per step, the IMU updates at 100 Hz on all axes, and HID reports arrive at 500–1000 Hz.
The 2000 dps scale in `Vader4ProReport.cs` is correct (90° turns integrate to 88–90°).

**Key files:**
- `DS4Windows/DS4Library/InputDevices/Vader4ProGyroFilter.cs` — Time-based, speed-adaptive EMA on yaw/pitch. Time constant comes from the per-controller **Gyro smoothing** slider (0–40 ms, default 16) in Device Options; fast turns shrink it toward 2 ms. Roll is passthrough.
- `DS4Windows/DS4Library/InputDevices/Vader4ProReportClock.cs` — Host-clock timing and DS4 timestamp for motion.
- `DS4Windows/DS4Library/InputDevices/Vader4ProGyroCapture.cs` — Optional CSV capture (Device Options checkbox, runtime only, never persisted).

### Switch Pro Motion Timing

Over Bluetooth or USB, Switch-protocol reports can arrive 8 or 16 ms apart while the
controller timer (report byte 1) advances at a fixed rate.

**Key files:**
- `DS4Windows/DS4Library/InputDevices/SwitchProMotionClock.cs` — Paces `elapsedTime`, `totalMicroSec` and `ds4Timestamp` from timer ticks. Tick length is the ratio of decayed host time to decayed ticks (~2 s window), so it stays on the wall clock. Implausible timer steps fall back to host time; stalls over 250 ms add no time.
- `DS4Windows/DS4Library/InputDevices/SwitchProGyroCapture.cs` — Optional CSV capture, one row per IMU sample.
- `DS4Windows/DS4Library/InputDevices/GyroCaptureWriter.cs` — Shared background CSV writer for both captures.
- `tools/vader_gyro_analyze.py` — Summarizes either capture format.

Before this fork the Switch Pro driver never set `ds4Timestamp`, so the virtual DS4 sent a constant motion timestamp.

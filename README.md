# DS4Windows (Vader 4 Pro fork)

A continuation of DS4Windows with native support for the **Flydigi Vader 4 Pro** and
better motion (gyro) handling for Switch Pro protocol controllers.

DS4Windows is an extract anywhere program that lets you use a DualShock 4, DualSense,
Switch Pro, JoyCon, and now a Vader 4 Pro on PC by emulating a DualShock 4 or Xbox 360
controller.

![DS4Windows Preview](https://raw.githubusercontent.com/Ryochan7/DS4Windows/jay/ds4winwpf_screen_20200412.png)

## About this fork

This is a fork of [schmaldeo/DS4Windows](https://github.com/schmaldeo/DS4Windows), which itself
continued the work of Jays2Kings and Ryochan7. schmaldeo
[discontinued that project](https://github.com/schmaldeo/DS4Windows#discontinuation) in March 2026;
this fork contains all of its code changes.

Inherited from schmaldeo: switch [debouncing](https://www.ganssle.com/debouncing.pdf), a stick
drift helper, and DS3 pitch/roll simulation from the accelerometer (work of
[sunnyqeen](https://github.com/sunnyqeen)).

Added in this fork:

- **Flydigi Vader 4 Pro (PC/dongle mode):** native input including back paddles and C/Z,
  analog triggers, rumble and LED; gyro smoothing adjustable per controller in Device Options.
- **Switch Pro protocol motion timing:** gyro time is paced from the controller's own timer
  instead of Bluetooth/USB arrival time, and the virtual DS4 now receives a real motion
  timestamp. This also benefits a Vader 4 Pro used in Switch mode.
- **Touchpad swipe mapping:** any button can perform a touchpad swipe (Up/Down/Left/Right).
- **Gyro capture:** optional CSV recording of raw gyro for Vader 4 Pro and Switch Pro
  controllers, with `tools/vader_gyro_analyze.py` to summarize it.

Measurements and setup notes for the Vader are in [doc/vader4pro-gyro.md](doc/vader4pro-gyro.md).
In short: PC mode gives analog triggers and every button but coarse 3.9 deg/s gyro steps;
Switch mode gives fine gyro but on/off triggers and only standard buttons. In Switch mode, hide
the raw controller with HidHide (allowing DS4Windows) so Windows does not react to it directly.

## License

DS4Windows is licensed under the terms of the GNU General Public License version 3.
You can find a copy of the terms and conditions of that license at
[https://www.gnu.org/licenses/gpl-3.0.txt](https://www.gnu.org/licenses/gpl-3.0.txt). The license is also
available in this source code from the COPYING file.

## Downloads

This fork has no prebuilt releases yet. Build it from source:

```powershell
dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64
```

The app is written to `DS4Windows\bin\x64\Release\net8.0-windows\DS4Windows.exe`.

Upstream builds without this fork's changes remain at
[schmaldeo/DS4Windows releases](https://github.com/schmaldeo/DS4Windows/releases).

## Requirements

- Windows 10 or newer (Thanks Microsoft)
- Microsoft .NET 8.0 Desktop Runtime. [x64](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.0-windows-x64-installer) or [x86](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.0-windows-x86-installer)
- Visual C++ 2015-2022 Redistributable. [x64](https://aka.ms/vs/17/release/vc_redist.x64.exe) or [x86](https://aka.ms/vs/17/release/vc_redist.x86.exe)
- [ViGEmBus](https://vigem.org/) driver (DS4Windows will install it for you)
- **Sony** DualShock 4 or other supported controller
- Connection method:
  - Micro USB cable
  - [Sony Wireless Adapter](https://www.amazon.com/gp/product/B01KYVLKG2)
  - Bluetooth 4.0 (via an
  [adapter like this](https://www.newegg.com/Product/Product.aspx?Item=N82E16833166126)
  or built in pc). Only use of Microsoft BT stack is supported. CSR BT stack is
  confirmed to not work with the DS4 even though some CSR adapters work fine
  using Microsoft BT stack. Toshiba's adapters currently do not work.
  *Disabling 'Enable output data' in the controller profile settings might help with latency issues, but will disable lightbar and rumble support.*
- Disable **PlayStation Configuration Support** and
**Xbox Configuration Support** options in Steam

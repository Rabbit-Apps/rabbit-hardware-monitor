# Rabbit Hardware Monitor

A compact Windows hardware dashboard by Rabbit Apps, using LibreHardwareMonitor and optional Intel PresentMon FPS capture.

## Why I built it

Tools like LibreHardwareMonitor and HWMonitor are fantastic for detailed hardware monitoring and gathering sensor data. I created Rabbit Hardware Monitor to make the readings that matter while gaming easy to see at a glance, with a compact, stylish interface that I hope gamers and hardware enthusiasts will enjoy.

The inspiration came after I switched to an NVIDIA GPU and couldn't find a replacement that gave me the same monitoring dashboard experience I enjoyed in AMD Adrenalin. Rabbit is my take on that experience: the key readings, clearly presented and easy to keep an eye on while gaming.

## Run
[Download the 0.2.1 installer with PresentMon 2.6.0](https://github.com/Rabbit-Apps/rabbit-hardware-monitor/releases/tag/v0.2.1). Version 0.2.1 includes an x64 Setup installer. It installs into Program Files and offers **Start at sign-in** for the administrator account running Setup. Startup uses a delayed interactive scheduled task, not a system service. If you supply another administrator's credentials, the startup option belongs to that account. Rerun Setup to change the option, or disable the named task in Task Scheduler. Uninstall removes the task and retains your dashboard settings. Close portable copies before installing. See [security notes](SECURITY.md) and [release validation](RELEASE-VALIDATION.md).

Download the Windows x64 portable ZIP from [Releases](https://github.com/Rabbit-Apps/rabbit-hardware-monitor/releases).

Extract the entire portable ZIP and double-click HardwareMonitor.exe. The app automatically requests administrator access; approve the Windows permission prompt to continue. Install the official PawnIO driver separately from https://pawnio.eu/. Visual Studio is not needed to run the portable build. Settings remain in the per-user HardwareMonitor local application-data folder.

## Build
Use Windows, .NET 10 SDK and the Visual Studio Windows/WinUI build tools. From this folder:

    dotnet publish App/HardwareMonitor.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o dist/Rabbit
    dotnet run --project FpsTests/FpsTests.csproj -c Release -- --sensors FpsTests/ryzen-sensors.json
    ./package-portable.ps1 -PublishDirectory dist/Rabbit -ZipPath dist/Rabbit.zip

The local package feed contains the exact privacy-rebuilt library. Directory.Build.props maps build paths and disables release debug symbols. Runtime privacy scans should be repeated after rebuilding on another machine.

To build the installer, install Inno Setup 7.1 or later from its official source, then run:

    ./Installer/Build.ps1 -Compiler 'C:\path\to\Inno Setup 7\ISCC.exe'

The script runs tests, publishes the self-contained app, and writes Setup plus SHA-256 to `dist/Installer`. Build artifacts are excluded from Git. Installer startup integration must also be validated on Windows with administrator access; unit tests alone do not verify it.

## Settings and limitations
Edit dashboard controls visibility, sensor mapping, order, warnings and FPS detection. Automatic GPU selection prefers a known dedicated adapter. Sensor availability varies with firmware, drivers and hardware; verify fan mappings against your own system. FPS detection is heuristic and supports a manual application selection.

Diagnostics generates local reports that can include hardware identifiers and system information. Inspect reports before sharing. No diagnostic captures are included here.

### FPS statistics

Current average and 1% low appear on the left, with the previous measured period on the right. Narrow panels stack the figures. Each measured reset replaces the previous figures; an empty reset preserves them. Changing game or render stream clears the history.

### E-core preference

Edit dashboard includes **Prefer E-cores (restart required)**, enabled by default. The monitor and its PresentMon helper select all available processors in the lower of two Windows efficiency classes using process-default CPU sets. On the tested i7-14700K this selects logical processors 16–27. Sensor threads may still temporarily use other processors for hardware reads; this is not a hard affinity guarantee or a demonstrated FPS improvement. No game affinity, priority, or power setting is changed.

Unsupported topology falls back without guessing; existing conflicting CPU-set selections are retained. Disable the setting, save and restart to return to normal scheduling. Readback status is saved in `%LOCALAPPDATA%\HardwareMonitor\cpu-placement-monitor.json` and `cpu-placement-presentmon.json`; check their timestamps and PIDs. The helper log refreshes when FPS detection starts. Live gaming overhead remains to be measured.

CPU-set selection/parser tests run with the regular suite. On a supported hybrid Windows machine, add `--native-cpu-placement` to run the process/helper assignment and restoration checks:

    dotnet run --project FpsTests/FpsTests.csproj -c Release -- --native-cpu-placement

### FPS calculation details

Dashboard settings have independent switches for live FPS, average FPS and 1% low FPS. FPS detection controls the PresentMon helper; the display switches only control which figures are visible. Statistics can reset manually or every 30 seconds, 1 minute, 5 minutes or 10 minutes. Reset now takes effect immediately; other settings apply when saved.

Live FPS uses the existing approximately one-second window. Average FPS is frame count divided by total frame duration since reset. The 1% low is the reciprocal of the mean duration of the slowest 1% of captured frame intervals (rounded up to a whole frame). These are application present timings, not displayed-frame timings, and may differ from other tools' definitions. Statistics start collecting after game selection and appear after at least 100 frames and two seconds of frame time.

The statistics use a fixed-size histogram to avoid retaining a growing history or sorting frames continuously. Average FPS uses unrounded durations. The 1% low is approximate at the cutoff: intervals are grouped in 0.1 ms buckets, with intervals over one second grouped together. A partial cutoff bucket uses that bucket's mean. Long intervals are included, so pauses/loading can affect results; reset before a comparison run. A game/process or selected render-stream change resets statistics. Brief pauses retain the session but hide figures until frames resume.

## Licence and release status
Rabbit's original code uses the Rabbit Apps Free Use and Sharing Licence in LICENSE.md. Free personal/business use and free sharing are allowed; selling requires permission. Dependencies retain their own licences, including MPL and LGPL; see Legal and Tools/PresentMon under App.

This is an early test release. The application presents Rabbit and applicable Microsoft component terms on first run. Dependency notices and the identified native runtime attributions are included in Legal, along with the pinned dependency sources. The licence review is an engineering check, not legal certification.

Hardware coverage and gaming overhead are still being evaluated. The modified PawnIO-module loading route has not been runtime-tested in an isolated development environment. Report sensor issues with your hardware model and app version; inspect diagnostic reports for personal information before attaching them.

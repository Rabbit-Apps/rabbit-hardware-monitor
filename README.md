# Rabbit Hardware Monitor

A compact Windows hardware dashboard by Rabbit Apps, using LibreHardwareMonitor and optional Intel PresentMon FPS capture.

## Why I built it

Tools like LibreHardwareMonitor and HWMonitor are fantastic for detailed hardware monitoring and gathering sensor data. I created Rabbit Hardware Monitor to make the readings that matter while gaming easy to see at a glance, with a compact, stylish interface that I hope gamers and hardware enthusiasts will enjoy.

The inspiration came after I switched to an NVIDIA GPU and couldn't find a replacement that gave me the same monitoring dashboard experience I enjoyed in AMD Adrenalin. Rabbit is my take on that experience: the key readings, clearly presented and easy to keep an eye on while gaming.

## Run
Download the Windows x64 portable ZIP from [Releases](https://github.com/Rabbit-Apps/rabbit-hardware-monitor/releases).

Extract the entire portable ZIP and run HardwareMonitor.exe as administrator. Install the official PawnIO driver separately from https://pawnio.eu/. Visual Studio is not needed to run the portable build. Settings remain in the per-user HardwareMonitor local application-data folder.

## Build
Use Windows, .NET 10 SDK and the Visual Studio Windows/WinUI build tools. From this folder:

    dotnet publish App/HardwareMonitor.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o dist/Rabbit
    dotnet run --project FpsTests/FpsTests.csproj -c Release -- --sensors FpsTests/ryzen-sensors.json
    ./package-portable.ps1 -PublishDirectory dist/Rabbit -ZipPath dist/Rabbit.zip

The local package feed contains the exact privacy-rebuilt library. Directory.Build.props maps build paths and disables release debug symbols. Runtime privacy scans should be repeated after rebuilding on another machine.

## Settings and limitations
Edit dashboard controls visibility, sensor mapping, order, warnings and FPS detection. Automatic GPU selection prefers a known dedicated adapter. Sensor availability varies with firmware, drivers and hardware; verify fan mappings against your own system. FPS detection is heuristic and supports a manual application selection.

Diagnostics generates local reports that can include hardware identifiers and system information. Inspect reports before sharing. No diagnostic captures are included here.

## Licence and release status
Rabbit's original code uses the Rabbit Apps Free Use and Sharing Licence in LICENSE.md. Free personal/business use and free sharing are allowed; selling requires permission. Dependencies retain their own licences, including MPL and LGPL; see Legal and Tools/PresentMon under App.

This is an early test release. The application presents Rabbit and applicable Microsoft component terms on first run. Dependency notices and the identified native runtime attributions are included in Legal, along with the pinned dependency sources. The licence review is an engineering check, not legal certification.

Hardware coverage and gaming overhead are still being evaluated. The modified PawnIO-module loading route has not been runtime-tested in an isolated development environment. Report sensor issues with your hardware model and app version; inspect diagnostic reports for personal information before attaching them.

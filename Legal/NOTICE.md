# Rabbit Hardware Monitor — third-party software and disclaimer

Rabbit Hardware Monitor is an independent project provided free of charge. It uses third-party software whose authors retain their respective rights. Good-faith efforts are made to identify these components, preserve their notices, and meet their licence requirements. If you identify a licensing or attribution concern, please report it to the project maintainer through the repository's issue tracker so it can be investigated and corrected.

This statement does not replace any applicable licence, excuse non-compliance, or limit rights granted by third-party licences. No affiliation with or endorsement by the authors or vendors of these components is implied. Product names and trademarks belong to their respective owners.

To the extent permitted by applicable law, Rabbit Hardware Monitor is provided "as is", without warranties of accuracy, completeness, fitness for a particular purpose, or uninterrupted operation. Sensor availability and readings depend on hardware, firmware, drivers, and third-party components. The application is an informational monitoring tool and is not a substitute for hardware protection mechanisms. Nothing in this notice excludes rights or liabilities that cannot lawfully be excluded.

## LibreHardwareMonitorLib

Local package: 0.9.6-hotspot.7faa1af.anon1, built from unmodified upstream source at commit 7faa1af1fb3c2c307186c60162035d42bf3b5a48. The local package version differs from upstream.

The covered source is available under the Mozilla Public License 2.0 at:
https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/7faa1af1fb3c2c307186c60162035d42bf3b5a48

See LibreHardwareMonitor-LICENSE.txt and LibreHardwareMonitor-THIRD-PARTY-NOTICES.txt in this directory. These notices include the LGPL 2.1 text for embedded PawnIO.Modules.

## PawnIO.Modules and the installed PawnIO driver

LibreHardwareMonitor embeds PawnIO module binaries. Its resource README identifies these as release 0.2.11:
https://github.com/namazso/PawnIO.Modules/releases/tag/0.2.11

Module source repository: https://github.com/namazso/PawnIO.Modules

The modules and the separately installed driver are distinct components. The official PawnIO driver is obtained and installed separately from https://pawnio.eu/ under its own terms; this application does not bundle its installer or driver. Installing it separately does not remove obligations for modules embedded in the application dependency.

## Intel PresentMon

PresentMon 2.5.1 provides frame-rate monitoring. Its MIT licence, copyright notice, third-party notices, and binary provenance are distributed in Tools/PresentMon alongside PresentMon.exe.

Source: https://github.com/GameTechDev/PresentMon/tree/v2.5.1

## Other dependencies

The application also ships .NET and Windows App SDK runtime components and transitive dependencies. Their applicable upstream licence terms continue to apply. This notice is not a substitute for their licence texts.


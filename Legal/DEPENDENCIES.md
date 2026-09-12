# Dependency source and notice index

The `Dependencies` directory preserves package-provided notices and NuGet metadata. `dependency-inventory.json` lists 42 resolved packages, including build-only and non-Windows packages; it is not a claim that all are distributed runtime components. The .NET 10.0.12 runtime notices are included separately in that directory.

The following MPL-covered source archives are included under `Sources`, under MPL 2.0 with their original notices:

| Component | Version | Source revision |
| --- | --- | --- |
| BlackSharp.Core | 1.2.0 | https://github.com/Blacktempel/BlackSharp/tree/e3383d014620777a561c939efe3f27ed4f72bc04 |
| DiskInfoToolkit | 2.1.4 | https://github.com/Blacktempel/DiskInfoToolkit/tree/a6ea726b6118d4469d1228e19b5db0f3437864dc |
| RAMSPDToolkit-NDD | 1.6.1 | https://github.com/Blacktempel/RAMSPDToolkit/tree/0ea51855144eef82787f983004fb8ce7346f8227 |
| LibreHardwareMonitorLib | 0.9.6-hotspot.7faa1af | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/7faa1af1fb3c2c307186c60162035d42bf3b5a48 |

The first three source revisions come from their restored package metadata. Original copyright information is retained in the archives and package metadata. The common MPL text is `LibreHardwareMonitor-LICENSE.txt`.

HidSharp 2.6.4 carries its Apache 2.0 licence and copyright in its dependency directory. PresentMon's notices remain beside its executable in `Tools/PresentMon`.

Microsoft's distributed Windows App SDK packages carry Microsoft Software License Terms, which differ from the MIT licence on the public source repository. The package terms permit redistribution of files placed with the app by the SDK, subject to their conditions. Those conditions include end-user/distributor terms, attribution restrictions, and an indemnity provision with an exception for claims based solely on unmodified distributable code. This distribution must not be presented as entirely MIT licensed.

Mono.Posix.NETStandard 1.0.0 is retained unchanged. Its dependency directory now includes the package-linked licence, the original Mono 5.8.0.127 licence, 132 source notice headers (including native support, eglib and zlib/minizip), MinGW runtime notices, GCC runtime licence/exception, and PROVENANCE.md linking the original version 1.0.0 build recipe. The native attribution follow-up identified embedded MinGW-w64 5.0.3 and GCC 6.4.0 runtime code and preserved their notices. Original build reproduction is not claimed.



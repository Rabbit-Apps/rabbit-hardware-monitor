# Pinned library

LibreHardwareMonitor source commit: 7faa1af1fb3c2c307186c60162035d42bf3b5a48
Source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/7faa1af1fb3c2c307186c60162035d42bf3b5a48
Package: 0.9.6-hotspot.7faa1af.anon1
SHA256: 022F8D003824C95BFCBAD26EFA1A2A4AD999C1B40155431D26F72B4048C33694

Upstream source is unchanged. Recompiled with anonymous source-path mapping and no release debug symbols. Source archive and MPL notices are under Legal.

To rebuild, extract the source archive and run dotnet pack on LibreHardwareMonitorLib/LibreHardwareMonitorLib.csproj with:
-c Release -p:Platform=x64 -p:TargetFrameworks=net10.0 -p:GeneratePackageOnBuild=false -p:PackageVersion=0.9.6-hotspot.7faa1af.anon1 -p:DebugType=none -p:DebugSymbols=false -p:PathMap=ABSOLUTE_SOURCE_ROOT=/_/LibreHardwareMonitor/ -o packages

Replace ABSOLUTE_SOURCE_ROOT with the actual extracted source directory. Use a fresh local package version if changing source or build options to avoid NuGet cache reuse.

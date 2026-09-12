# PawnIO module source and replacement notes

Verified 2026-09-11 against the official release assets. All 14 .bin resources in our pinned LibreHardwareMonitor source match the official PawnIO.Modules 0.2.11 release by SHA-256. This verifies release provenance; it is not a reproducible-build verification of the signed binaries.

Module source revision: `52a7e536dff3e53c96917a28caac5e0fa6510696`.

Source archive included: `Sources/PawnIO.Modules-0.2.11-source.zip`. It preserves the upstream source headers, COPYING, includes, bundled compiler material and GitHub build workflow. Per-file notices identify LGPL-2.1-or-later where applicable; this distribution relies on the included LGPL 2.1 terms. See `PawnIO.Modules-LGPL-2.1.txt`.

Sources and evidence:

- https://github.com/namazso/PawnIO.Modules/tree/52a7e536dff3e53c96917a28caac5e0fa6510696
- https://github.com/namazso/PawnIO.Modules/releases/tag/0.2.11
- `Sources/PawnIO-module-hashes.json`

## Documented developer replacement route — not yet exercised end to end

1. Extract the included source archive and modify the relevant `.p` source. Preserve the module's exported interface for compatibility with LibreHardwareMonitor.
2. Follow the included `.github/workflows` build recipe. It installs the supplied Pawn RPM in Oracle Linux 9 and compiles each module with `pawncc "$f" '-iinclude' '-C64' '-;+' '-(+' '-p'`. Compilation produces `.amx` bytecode, not an officially signed release blob.
3. Work from LibreHardwareMonitor commit `7faa1af1fb3c2c307186c60162035d42bf3b5a48`. Its module loader sends the embedded resource bytes directly to the installed PawnIO driver. For a development rebuild, replace the corresponding `LibreHardwareMonitorLib/Resources/PawnIo/<Module>.bin` resource with the compiled module bytes, retaining that resource filename. This substitution is a proposed developer procedure inferred from the loader; it has not been runtime-tested here.
4. Rebuild LibreHardwareMonitorLib with the .NET 10 target using the package build instructions in this project's `packages/README.md`. Replace the library in a separate application build, or rebuild Rabbit Hardware Monitor against that local package. Keep a separate output directory and preserve the normal installation.
5. Upstream's development guide says unsigned modules use the unrestricted PawnIO edition and Windows test signing. These are developer-environment requirements, not requirements for normal Rabbit Hardware Monitor users. This review does not install that driver or change Windows security settings.

Upstream developer guide: https://github.com/namazso/PawnIO.Modules/wiki/Getting-started-with-PawnIO

The normal signed driver does not provide a documented route for arbitrary unsigned replacement modules. The unrestricted driver offers an upstream-documented development route, but the full resource replacement and application run above remains untested. Do not treat these notes as a certification that all LGPL distribution requirements have been discharged.

Rabbit Hardware Monitor does not impose a restriction on modifying these LGPL-covered components or reverse engineering for debugging such modifications. This statement preserves applicable LGPL rights; it does not grant rights over unrelated third-party components.

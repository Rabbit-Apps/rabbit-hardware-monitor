# Mono.Posix.NETStandard 1.0.0

Rabbit retains this unmodified transitive LibreHardwareMonitor dependency.

The original [XamarinComponents build recipe](https://github.com/xamarin/XamarinComponents/blob/b3999764b5de09a8e0a735a91b153c59cf525979/XPlat/Mono.Posix/Makefile) explicitly names NuGet version 1.0.0, Mono version 5.8.0.127, the Mono repository tag `mono-5.8.0.127`, and patch PR-7024. Native helpers are fetched from Mono's build-artifact service, build 46. This is source/build provenance evidence, not a reproduced-build attestation.

Included notices:

- `LICENSE`: destination of the installed package's licence URL when retrieved.
- `LICENSE-5.8.0.127.txt`: original licence index from the source release named by the recipe.
- `SOURCE-NOTICES.txt`: 132 original leading comment blocks from that release's managed component, native support and eglib sources, including zlib/minizip attribution and terms. Platform-dependent native notices are included conservatively. Tests, remoting components, and unrelated support libraries are excluded.
- The package nuspec retains Microsoft's package-level copyright notice.

The Windows distribution includes `Mono.Posix.NETStandard.dll`, `MonoPosixHelper.dll`, and `libMonoPosixHelper.dll`. The managed source project selects Assembly, Mono.Posix, Mono.Unix, Mono.Unix.Native and shared Locale.cs; it excludes CdeclFunction.cs. Native source selection is documented in `support/Makefile.am`.

Original sources: https://github.com/mono/mono/tree/mono-5.8.0.127

Original package: https://www.nuget.org/packages/Mono.Posix.NETStandard/1.0.0

The general Mono licence index describes components beyond this package. Its inclusion does not assert that every Mono component or licence listed there applies to Rabbit. Rabbit's own restrictions do not replace these third-party permissions.

Follow-up binary attribution: the shipped libMonoPosixHelper.dll contains zlib 1.2.5 and minizip 1.01 copyright strings, Mono eglib source paths, MinGW-w64 runtime 5.0.3-1 source paths, and GCC 6.4.0-2 source paths. The two helpers import only Windows system/CRT DLLs. Preserved eglib headers, MinGW v5.0.3 COPYING and AUTHORS, identified runtime source headers, GCC 6.4.0 GPL3 and Runtime Library Exception 3.1. The GCC exception is included with the GPL text; the compiler runtime material is not being treated as a requirement to relicense Rabbit under GPL. No separate GNU GLib or libiconv DLL is imported; the binary identifies Mono eglib implementation code. This closes the identified native-attribution notice gap. It is not a reproducible-build claim, a proof about every possible binary fragment, or legal certification.

Runtime source references:
- https://github.com/mingw-w64/mingw-w64/tree/v5.0.3
- https://github.com/gcc-mirror/gcc/tree/releases/gcc-6.4.0
- https://github.com/mono/mono/tree/mono-5.8.0.127/mono/eglib


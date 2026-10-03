# Changes

## 0.2.1 — 3 October 2026

- Bundled Intel-signed PresentMon Console 2.6.0 in the Windows installer.
- Retained existing FPS metrics, settings and sign-in startup behavior.
- Added an optional compatibility test for CSV captured by the rendering test.

## 0.2.0 — 27 September 2026

- Added a Windows x64 installer, Start menu entry, uninstall entry, optional desktop shortcut, and optional startup at sign-in for the installing administrator account.
- Added E-core preference for the monitor and PresentMon, selected from Windows topology and reversible in Edit dashboard after restart.
- Retained previous measured average FPS and 1% low across resets; display them to the right of current values, stacking on narrow panels.
- Prevented duplicate monitor instances within the same account/session.
- Batched and bounded FPS input reading, handled duplicate headers and invalid PIDs, bounded settings parsing, and verified CPU-set restoration after failure.
- Retained existing bounded statistics/history and cached UI brushes; no unmeasured game-FPS improvement is claimed.
- Added security/release validation documentation and reproducible installer source/build instructions.

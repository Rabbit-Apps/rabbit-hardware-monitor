# Security review and reporting

## Scope of the 0.2.0 review (27 September 2026)

Reviewed first-party process launching, settings persistence, FPS input, bounded statistics storage, CPU placement, application lifetime, and installer/startup privilege boundaries. This is an engineering review, not a penetration-test certification or a guarantee of no vulnerabilities.

Changes made:

- Helper frame input is read in bounded batches, with a 64 KiB line limit. Stderr was already capped at 8 KiB. Oversized input stops capture with an error rather than growing an unbounded line buffer.
- Duplicate case-insensitive FPS headers are rejected without a dictionary exception; invalid nonpositive process IDs are ignored.
- Settings reads are bounded to 1 MiB and 32 JSON levels, preserve UTF-8 BOM compatibility, and fall back to the previous settings backup on malformed input.
- CPU-set rollback now verifies the restored selection.
- A per-account, per-session single-instance mutex prevents a manual launch from duplicating the sign-in instance.
- The installer fixes the destination to Program Files and refuses arbitrary `/DIR` overrides. It grants no ordinary-user write permission to application files.
- Optional startup uses an interactive logon task for the installing administrator's SID, with a 15-second delay. No SYSTEM account, stored password, service, or background driver installation is used. Only Administrators and SYSTEM may modify the task. Its action is the absolute installed executable path, with a fixed working directory and no interpolated command.
- Startup configuration is performed by a packaged script in the protected installation directory, invoked by absolute Windows PowerShell path with no profile. This is not the scheduled task action. Configuration failure is visible and returns installer exit code 10. Uninstall removes the task before removing its executable and preserves per-user settings.

## Checks and limits

The NuGet vulnerability query, including transitive dependencies, returned no reported vulnerable packages on the review date. This does not independently audit the custom LibreHardwareMonitor package, embedded PawnIO modules, native PresentMon executable, or every Microsoft runtime component. The self-contained publish includes .NET 10.0.12. Keep dependencies and the runtime serviced; rebuilding is required to update the packaged runtime.

The existing 61 FPS/settings/sensor assertions pass, alongside CPU-set parser/native/helper tests and the new oversized-input, chunk-boundary, malformed-header, settings-depth and backup-recovery tests. Local installer lifecycle and runtime verification are recorded in RELEASE-VALIDATION.md.

The published-file privacy check found no known personal build-account/path markers. Raw sensor inventories, ETL traces, developer logs and installation-test reports are excluded from Git. This targeted check is not an exhaustive secret-discovery guarantee.

The app still runs elevated because its current sensor/tracing architecture requires it. A separate least-privilege UI and narrow broker would be a larger future redesign. Do not enable unattended elevated startup from a user-writable portable directory; use the installer. Do not grant write access to Program Files application files. The app has no first-party update downloader or network listener in the reviewed source; dependency behavior was not exhaustively network-audited.

This release is unsigned; no code-signing certificate was configured. SHA-256 files detect accidental changes only when obtained through a trusted channel; they do not replace publisher authentication. Microsoft Defender was inactive on the validation machine, so no Defender clean-scan claim is made and its settings were not changed.

Report suspected vulnerabilities privately through the repository's GitHub Security reporting option if available. If unavailable, open an issue requesting a private contact without posting exploit details, credentials, or personal diagnostic captures. A later review should reassess newly published advisories and unreviewed dependency/driver internals.

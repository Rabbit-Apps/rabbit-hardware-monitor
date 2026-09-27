# 0.2.0 validation — 27 September 2026

Validated on Windows 11 x64 with an Intel i7-14700K and NVIDIA RTX 5080.

- Release tests passed: all 61 existing FPS/settings/sensor assertions, CPU topology selection and malformed-input tests, native process/helper CPU-set assignment and restoration, and new bounded stream/settings and malformed-header cases.
- Self-contained x64 publish succeeded. Inno Setup 7.1.0 x64 compiled the installer. The compiler download's Authenticode signature was verified against its published Pyrsys B.V. publisher.
- Installer and in-place upgrade completed successfully in Program Files.
- Startup checkbox enabled the account-specific task; clearing it on reinstallation removed the task; enabling it again recreated the task.
- Startup task principal was checked against the installing account SID. Interactive logon and highest available run level were verified, along with absolute installed executable/working paths. Task modification ACL was Administrators and SYSTEM only. Program Files application ACL did not grant ordinary users write access.
- Invoking the task launched the monitor with one PresentMon child. A second app launch exited rather than creating a second dashboard.
- UI Automation found a responsive dashboard with populated CPU/GPU temperatures, usage, clocks, power and cooling values, and zero unavailable readings in the observed snapshot. No game was running; FPS collection throughput during gameplay was not benchmarked.
- Uninstall removed the executable, Start menu shortcut and startup task. Saved dashboard settings remained byte-for-byte unchanged. A final install was performed with startup enabled.
- Published-file privacy scan checked 654 files and found no known personal build-account/path markers. Developer logs, raw inventories and captures remain outside the repository.
- NuGet vulnerability query with transitive dependencies reported no vulnerable packages. See SECURITY.md for coverage limits.

Not verified: actual reboot/sign-out/sign-in trigger execution, all supported Windows versions/accounts, malicious reparse-point or hostile dependency attacks, exhaustive driver/native-component vulnerability analysis, or comparative in-game overhead. The task's manual launch was tested instead of interrupting the user's session. The build is unsigned, and Defender was inactive, so no antivirus clean-scan assertion is made.

The E-core preference selects all available E-cores on this machine; it does not guarantee exclusive residency or an FPS improvement. CPU-specific sensor reads may temporarily use other processors. The installer does not install, update or uninstall the PawnIO driver.

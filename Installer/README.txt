Rabbit Hardware Monitor 0.2.1

Installs into Program Files with Start menu and uninstall entries.
Close any portable Rabbit copy before installing or upgrading.

Start at sign-in is optional. It creates an elevated interactive scheduled task
for the administrator account running this installer, delayed by 15 seconds.
It does not run as SYSTEM, store a password, or run before sign-in. If you supply
another administrator's credentials, startup belongs to that administrator.
Use Task Scheduler to disable the task, or rerun Setup and clear the startup
checkbox. Uninstall removes the task. Your dashboard settings are retained.

Rabbit requires administrator access for hardware sensors and FPS tracing.
The official PawnIO driver is a separate prerequisite: https://pawnio.eu/
This installer does not download or silently install a kernel driver.

E-core preference remains optional in Edit dashboard (restart required).
Normal startup shows the dashboard. First-run component terms still require
acceptance. Automatic startup does not bypass those terms.

This build is not code-signed; Windows may show an unknown-publisher warning.
Only install from a trusted Rabbit Apps source and check the supplied SHA-256.

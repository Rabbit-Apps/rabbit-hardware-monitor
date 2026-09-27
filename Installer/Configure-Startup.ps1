param([Parameter(Mandatory)][ValidateSet('Enable', 'Disable')][string]$Mode)
$ErrorActionPreference = 'Stop'
$taskName = 'Rabbit Hardware Monitor Startup'
$installRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
# Explicit 64-bit registry view also works if an administrator invokes this from
# a 32-bit PowerShell host. Do not trust inherited ProgramFiles environment text.
$machine = [Microsoft.Win32.RegistryKey]::OpenBaseKey('LocalMachine', 'Registry64')
try {
    $windows = $machine.OpenSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion')
    try { $programFiles = [string]$windows.GetValue('ProgramFilesDir') } finally { $windows.Dispose() }
} finally { $machine.Dispose() }
$expectedRoot = Join-Path $programFiles 'Rabbit Hardware Monitor'
try {
    if (-not $installRoot.Equals($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Startup must target the protected Program Files installation.'
    }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator access required.' }
    $service = New-Object -ComObject Schedule.Service
    $service.Connect()
    $root = $service.GetFolder('\')
    if ($Mode -eq 'Disable') {
        $existing = @($root.GetTasks(1) | Where-Object Name -eq $taskName)
        if ($existing.Count -gt 0) { $root.DeleteTask($taskName, 0) }
    } else {
        $exe = Join-Path $installRoot 'HardwareMonitor.exe'
        if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Installed monitor is missing.' }
        $sid = $identity.User.Value
        $definition = $service.NewTask(0)
        $definition.RegistrationInfo.Description = 'Start Rabbit Hardware Monitor for the installing account at sign-in.'
        $definition.RegistrationInfo.Author = 'Rabbit Apps'
        $definition.Principal.UserId = $sid
        $definition.Principal.LogonType = 3
        $definition.Principal.RunLevel = 1
        $definition.Settings.MultipleInstances = 2
        $definition.Settings.DisallowStartIfOnBatteries = $false
        $definition.Settings.StopIfGoingOnBatteries = $false
        $definition.Settings.ExecutionTimeLimit = 'PT0S'
        $definition.Settings.AllowDemandStart = $true
        $trigger = $definition.Triggers.Create(9)
        $trigger.UserId = $sid
        $trigger.Delay = 'PT15S'
        $action = $definition.Actions.Create(0)
        $action.Path = $exe
        $action.WorkingDirectory = $installRoot
        # Interactive account, never SYSTEM; no password. Only admins/SYSTEM may modify.
        $registered = $root.RegisterTaskDefinition($taskName, $definition, 22, $sid, $null, 3, 'D:P(A;;FA;;;SY)(A;;FA;;;BA)')
        if ($registered.Definition.Actions.Item(1).Path -ne $exe) { throw 'Startup action readback mismatch.' }
    }
    "Startup $Mode succeeded at $([DateTimeOffset]::Now)" | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'startup-result.log')
    exit 0
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'startup-result.log')
    exit 1
}

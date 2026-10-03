param([Parameter(Mandatory)][string]$Compiler)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $repo 'dist/Rabbit'
Push-Location $repo
try {
    & dotnet run --project FpsTests/FpsTests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & dotnet publish App/HardwareMonitor.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    & $Compiler /Q (Join-Path $PSScriptRoot 'Rabbit.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $installer = Join-Path $repo 'dist/Installer/RabbitHardwareMonitor-0.2.1-Setup.exe'
    $digest = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
    "$digest  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath ($installer + '.sha256.txt')
    Write-Output $installer
} finally { Pop-Location }

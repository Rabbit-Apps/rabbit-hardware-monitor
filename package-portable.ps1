param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$ZipPath
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = (Resolve-Path -LiteralPath $PublishDirectory).Path
if (Test-Path -LiteralPath $ZipPath) { throw 'Output already exists; choose a new ZIP name.' }
$archive = [IO.Compression.ZipFile]::Open([IO.Path]::GetFullPath($ZipPath), [IO.Compression.ZipArchiveMode]::Create)
$index = [Collections.Generic.List[string]]::new()
$index.Add('# Packaged dependency notices')
$index.Add('')
$index.Add('Files are given short names for Windows Explorer extraction. Contents are unchanged. Original paths below identify the package and upstream notice filename.')
$index.Add('')
$index.Add('| Packaged file | Original dependency path |')
$index.Add('| --- | --- |')
$number = 0
try {
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File | Sort-Object FullName) {
        $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\','/')
        if ($relative.StartsWith('Legal/Dependencies/')) {
            $number++
            $short = 'n{0:D3}{1}' -f $number, $file.Extension
            $index.Add("| [$short]($short) | $($relative.Substring(19)) |")
            $relative = 'Legal/Dependencies/' + $short
        }
        if ($relative.Length -gt 120) { throw "Archive path exceeds 120 characters: $relative" }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $entry = $archive.CreateEntry('Legal/Dependencies/INDEX.md')
    $writer = [IO.StreamWriter]::new($entry.Open())
    try { $writer.Write(($index -join "`n")) } finally { $writer.Dispose() }
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $ZipPath).Hash
($hash + '  ' + [IO.Path]::GetFileName($ZipPath)) | Set-Content -LiteralPath ($ZipPath + '.sha256.txt')

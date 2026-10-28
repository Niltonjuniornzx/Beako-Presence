param([Parameter(Mandatory = $true)][string]$OutputPath)

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($PSScriptRoot)
$stage = Join-Path $env:TEMP ('BeakoPresence-payload-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    Get-ChildItem -LiteralPath $source -File -Recurse | Where-Object {
        $_.FullName.Substring($source.Length + 1) -ne 'BeakoPresence.exe'
    } | ForEach-Object {
        $relative = $_.FullName.Substring($source.Length + 1)
        $destination = Join-Path $stage $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $OutputPath) { Remove-Item -LiteralPath $OutputPath -Force }
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $OutputPath, [IO.Compression.CompressionLevel]::Optimal, $false)
} finally {
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}


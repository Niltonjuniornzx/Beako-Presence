param(
    [Parameter(Mandatory = $true)][string]$DownloadUrl,
    [Parameter(Mandatory = $true)][string]$Sha256,
    [Parameter(Mandatory = $true)][string]$ZipName,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$InstallPath,
    [Parameter(Mandatory = $true)][int]$WaitForProcessId
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

function Show-Error([string]$Message) {
    [System.Windows.Forms.MessageBox]::Show($Message, 'Atualização do Beako Presence', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
}

$expectedInstallPath = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\BeakoPresence'))
$InstallPath = [IO.Path]::GetFullPath($InstallPath)
$uri = $null
if (-not $InstallPath.Equals($expectedInstallPath, [StringComparison]::OrdinalIgnoreCase) -or
    $Sha256 -notmatch '^[a-fA-F0-9]{64}$' -or
    $ZipName -notmatch '^Beako-Presence-Windows-\d+(?:\.\d+){1,3}\.zip$' -or
    -not [Uri]::TryCreate($DownloadUrl, [UriKind]::Absolute, [ref]$uri) -or
    $uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com') {
    Show-Error 'Os dados da atualização não passaram na validação. Nenhum arquivo foi alterado.'
    exit 2
}

$stage = Join-Path $env:TEMP ('BeakoPresence-update-' + [Guid]::NewGuid().ToString('N'))
$zipPath = Join-Path $stage $ZipName
$packagePath = Join-Path $stage 'package'
try {
    New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $zipPath -UseBasicParsing -TimeoutSec 120
    $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    if (-not $actualHash.Equals($Sha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'O SHA-256 do pacote não corresponde ao valor publicado pelo GitHub.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $stagePrefix = [IO.Path]::GetFullPath($packagePath).TrimEnd('\') + '\'
        foreach ($entry in $archive.Entries) {
            $destination = [IO.Path]::GetFullPath((Join-Path $packagePath $entry.FullName))
            if (-not $destination.StartsWith($stagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'O pacote contém um caminho de arquivo inválido.'
            }
        }
    } finally { $archive.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $packagePath)

    foreach ($required in @('BeakoPresence.App.exe', 'BeakoPresence.exe', 'INSTALAR.ps1', 'DESINSTALAR.ps1', 'ATUALIZAR.ps1', 'catalog.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packagePath $required) -PathType Leaf)) { throw "O pacote atualizado está incompleto: $required" }
    }
    $packageVersion = (Get-Item -LiteralPath (Join-Path $packagePath 'BeakoPresence.App.exe')).VersionInfo.FileVersion
    if ($packageVersion -notlike ($Version + '.*') -and $packageVersion -ne $Version) {
        throw "A versão do executável ($packageVersion) não corresponde à versão publicada ($Version)."
    }

    try { Wait-Process -Id $WaitForProcessId -Timeout 90 } catch {
        if (Get-Process -Id $WaitForProcessId -ErrorAction SilentlyContinue) { throw 'O app não fechou no tempo esperado; nenhum arquivo foi alterado.' }
    }
    $installer = Join-Path $packagePath 'INSTALAR.ps1'
    $powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $arguments = '-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $installer + '" -InstallPath "' + $InstallPath + '" -StartApp'
    $installProcess = Start-Process -FilePath $powershell -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($installProcess.ExitCode -ne 0) { throw 'O instalador não concluiu a atualização. Execute INSTALAR.bat do ZIP para tentar novamente.' }
} catch {
    Show-Error ('A atualização não foi aplicada. O app instalado permanece intacto.' + [Environment]::NewLine + $_.Exception.Message)
    exit 1
} finally {
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}


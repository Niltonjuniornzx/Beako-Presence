param(
    [string]$InstallPath,
    [string]$LogPath,
    [switch]$StartApp,
    [bool]$CreateDesktopShortcut = $true,
    [bool]$CreateStartMenuShortcut = $true
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

function Show-Message([string]$Message, [string]$Title, [System.Windows.Forms.MessageBoxIcon]$Icon) {
    [System.Windows.Forms.MessageBox]::Show($Message, $Title, [System.Windows.Forms.MessageBoxButtons]::OK, $Icon) | Out-Null
}
function Write-InstallLog([string]$Message) {
    if ([string]::IsNullOrWhiteSpace($LogPath)) { return }
    try { [IO.File]::AppendAllText($LogPath, ('[' + [DateTime]::Now.ToString('s') + '] ' + $Message + [Environment]::NewLine), [Text.UTF8Encoding]::new($false)) } catch { }
}

$sourcePath = [IO.Path]::GetFullPath($PSScriptRoot)
Write-InstallLog 'Instalador iniciado.'
$defaultInstallPath = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\BeakoPresence'))
if ([string]::IsNullOrWhiteSpace($InstallPath)) { $InstallPath = $defaultInstallPath }
$InstallPath = [IO.Path]::GetFullPath($InstallPath)
if (-not $InstallPath.Equals($defaultInstallPath, [StringComparison]::OrdinalIgnoreCase)) {
    Write-InstallLog ('Caminho de instalação recusado: ' + $InstallPath)
    Show-Message 'O instalador só pode atualizar a instalação do usuário em LocalAppData\Programs\BeakoPresence.' 'Beako Presence' Error
    exit 2
}

$appSourceName = if (Test-Path -LiteralPath (Join-Path $sourcePath 'BeakoPresence.App.exe') -PathType Leaf) { 'BeakoPresence.App.exe' } else { 'BeakoPresence.exe' }
$required = @(
    $appSourceName, 'BeakoPresence.ico', 'BeakoPresence-Avatar.png',
    'catalog.json', 'community-paths.json', 'icon-index.json',
    'INSTALAR.bat', 'INSTALAR.ps1', 'DESINSTALAR.bat', 'DESINSTALAR.ps1', 'ATUALIZAR.ps1'
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourcePath $name) -PathType Leaf)) {
        Write-InstallLog ('Arquivo obrigatório ausente: ' + $name)
        Show-Message "Arquivo obrigatório ausente: $name`nExtraia o ZIP completo antes de instalar." 'Beako Presence' Error
        exit 2
    }
}
$exeBytes = [IO.File]::ReadAllBytes((Join-Path $sourcePath $appSourceName))
if ($exeBytes.Length -lt 2 -or $exeBytes[0] -ne 0x4D -or $exeBytes[1] -ne 0x5A) {
    Write-InstallLog 'O executável não passou na validação MZ.'
    Show-Message 'O executável do pacote não passou na validação.' 'Beako Presence' Error
    exit 2
}

if (Test-Path -LiteralPath (Join-Path $InstallPath 'BeakoPresence.exe')) {
    $running = Get-Process -Name BeakoPresence -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -and [IO.Path]::GetFullPath($_.Path).Equals((Join-Path $InstallPath 'BeakoPresence.exe'), [StringComparison]::OrdinalIgnoreCase) } catch { $false }
    }
    if ($running) {
        $answer = [System.Windows.Forms.MessageBox]::Show('O Beako Presence está aberto. Deseja fechá-lo agora para continuar a instalação? O aplicativo será encerrado normalmente, e suas configurações serão preservadas.', 'Beako Presence em execução', [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question)
        if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) { exit 3 }
        foreach ($process in $running) {
            try {
                if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $null = $process.CloseMainWindow() }
                if (-not $process.WaitForExit(15000)) {
                    Show-Message 'O aplicativo ainda está aberto. Feche-o manualmente e clique em Instalar novamente.' 'Beako Presence em execução' Warning
                    exit 3
                }
            } catch {
                Show-Message 'Não foi possível fechar o aplicativo automaticamente. Feche-o e clique em Instalar novamente.' 'Beako Presence em execução' Warning
                exit 3
            }
        }
    }
}

$installFiles = New-Object System.Collections.Generic.List[object]
foreach ($relative in $required | Where-Object { $_ -ne $appSourceName }) {
    $installFiles.Add([pscustomobject]@{ Source = $relative; Target = $relative })
}
$installFiles.Add([pscustomobject]@{ Source = $appSourceName; Target = 'BeakoPresence.exe' })
$sourceIcons = Join-Path $sourcePath 'icons'
if (Test-Path -LiteralPath $sourceIcons -PathType Container) {
    Get-ChildItem -LiteralPath $sourceIcons -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($sourcePath.Length + 1)
        $installFiles.Add([pscustomobject]@{ Source = $relative; Target = $relative })
    }
}
$transaction = Join-Path $env:TEMP ('BeakoPresence-install-' + [Guid]::NewGuid().ToString('N'))
$backupRoot = Join-Path $transaction 'backup'
$changed = New-Object System.Collections.Generic.List[object]

try {
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    foreach ($file in $installFiles) {
        $source = Join-Path $sourcePath $file.Source
        $target = Join-Path $InstallPath $file.Target
        $backup = Join-Path $backupRoot $file.Target
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
        Write-InstallLog ('Copiando ' + $file.Source + ' para ' + $target)
        $parent = Split-Path -Parent $target
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
        $hadOriginal = Test-Path -LiteralPath $target -PathType Leaf
        if ($hadOriginal) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
            Copy-Item -LiteralPath $target -Destination $backup -Force
        }
        $changed.Add([pscustomobject]@{ Target = $target; Backup = $backup; HadOriginal = $hadOriginal })
        $newFile = $target + '.beako-new'
        Copy-Item -LiteralPath $source -Destination $newFile -Force
        Move-Item -LiteralPath $newFile -Destination $target -Force
    }

    $shell = New-Object -ComObject WScript.Shell
    if ($CreateStartMenuShortcut) {
        $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
        New-Item -ItemType Directory -Path $startMenu -Force | Out-Null
        $shortcut = $shell.CreateShortcut((Join-Path $startMenu 'Beako Presence.lnk'))
        $shortcut.TargetPath = Join-Path $InstallPath 'BeakoPresence.exe'
        $shortcut.WorkingDirectory = $InstallPath
        $shortcut.IconLocation = Join-Path $InstallPath 'BeakoPresence.ico'
        $shortcut.Description = 'Gerenciador de atividades para Discord'
        $shortcut.Save()
    }

    if ($CreateDesktopShortcut) {
        $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
        $desktopShortcut = $shell.CreateShortcut((Join-Path $desktop 'Beako Presence.lnk'))
        $desktopShortcut.TargetPath = Join-Path $InstallPath 'BeakoPresence.exe'
        $desktopShortcut.WorkingDirectory = $InstallPath
        $desktopShortcut.IconLocation = Join-Path $InstallPath 'BeakoPresence.ico'
        $desktopShortcut.Description = 'Gerenciador de atividades para Discord'
        $desktopShortcut.Save()
    }

    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BeakoPresence'
    New-Item -Path $uninstallKey -Force | Out-Null
    $appVersion = (Get-Item -LiteralPath (Join-Path $InstallPath 'BeakoPresence.exe')).VersionInfo.FileVersion
    New-ItemProperty -Path $uninstallKey -Name DisplayName -Value 'Beako Presence' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value $appVersion -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'Beako' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $InstallPath -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value (Join-Path $InstallPath 'BeakoPresence.exe') -PropertyType String -Force | Out-Null
    $uninstaller = Join-Path $InstallPath 'DESINSTALAR.ps1'
    $uninstallCommand = 'powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "' + $uninstaller + '"'
    New-ItemProperty -Path $uninstallKey -Name UninstallString -Value $uninstallCommand -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

    if ($StartApp) {
        Start-Process -FilePath (Join-Path $InstallPath 'BeakoPresence.exe') -WorkingDirectory $InstallPath
    } else {
        $shortcutNames = @()
        if ($CreateDesktopShortcut) { $shortcutNames += 'Área de Trabalho' }
        if ($CreateStartMenuShortcut) { $shortcutNames += 'menu Iniciar' }
        $shortcutSummary = if ($shortcutNames.Count) { 'Atalhos criados: ' + ($shortcutNames -join ' e ') + '.' } else { 'Nenhum atalho foi criado.' }
        Show-Message ('Instalação concluída em:' + [Environment]::NewLine + $InstallPath + [Environment]::NewLine + [Environment]::NewLine + $shortcutSummary + ' Configurações e dados existentes foram preservados.') 'Beako Presence' Information
    }
    Remove-Item -LiteralPath $transaction -Recurse -Force -ErrorAction SilentlyContinue
    exit 0
} catch {
    Write-InstallLog ('ERRO: ' + $_.Exception.ToString())
    $changedItems = $changed.ToArray()
    [Array]::Reverse($changedItems)
    foreach ($item in $changedItems) {
        try {
            Remove-Item -LiteralPath ($item.Target + '.beako-new') -Force -ErrorAction SilentlyContinue
            if ($item.HadOriginal -and (Test-Path -LiteralPath $item.Backup -PathType Leaf)) {
                Copy-Item -LiteralPath $item.Backup -Destination $item.Target -Force
            } elseif (-not $item.HadOriginal -and (Test-Path -LiteralPath $item.Target -PathType Leaf)) {
                Remove-Item -LiteralPath $item.Target -Force
            }
        } catch { }
    }
    Remove-Item -LiteralPath $transaction -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

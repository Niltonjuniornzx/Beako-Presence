$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

function Show-Message([string]$Message, [string]$Title, [System.Windows.Forms.MessageBoxIcon]$Icon) {
    [System.Windows.Forms.MessageBox]::Show($Message, $Title, [System.Windows.Forms.MessageBoxButtons]::OK, $Icon) | Out-Null
}

$installPath = [IO.Path]::GetFullPath($PSScriptRoot)
$expectedPath = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\BeakoPresence'))
if (-not $installPath.Equals($expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
    Show-Message 'Este desinstalador não está no caminho de instalação esperado. Nenhum arquivo foi removido.' 'Beako Presence' Error
    exit 2
}

$running = Get-Process -Name BeakoPresence -ErrorAction SilentlyContinue | Where-Object {
    try { $_.Path -and [IO.Path]::GetFullPath($_.Path).Equals((Join-Path $installPath 'BeakoPresence.exe'), [StringComparison]::OrdinalIgnoreCase) } catch { $false }
}
if ($running) {
    Show-Message 'Feche o Beako Presence e execute a desinstalação novamente. Nenhum arquivo foi removido.' 'Beako Presence' Information
    exit 3
}

$confirm = [System.Windows.Forms.MessageBox]::Show('Remover o Beako Presence deste usuário? Configurações, cache e logs serão preservados por padrão.', 'Desinstalar Beako Presence', [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question)
if ($confirm -ne [System.Windows.Forms.DialogResult]::Yes) { exit 0 }

$appData = Join-Path $env:LOCALAPPDATA 'BeakoPresence'
$appDataMarker = Join-Path $appData 'steamfix-root.txt'
$removeData = $false
if (Test-Path -LiteralPath $appData -PathType Container) {
    $answer = [System.Windows.Forms.MessageBox]::Show('Também apagar configurações, cache, ícones e logs deste usuário? Escolha Não para preservá-los.', 'Dados do usuário', [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question, [System.Windows.Forms.MessageBoxDefaultButton]::Button2)
    $removeData = $answer -eq [System.Windows.Forms.DialogResult]::Yes
}

$exe = Join-Path $installPath 'BeakoPresence.exe'
if (Test-Path -LiteralPath $exe -PathType Leaf) {
    try { Start-Process -FilePath $exe -ArgumentList '--cleanup-fc27' -Wait -WindowStyle Hidden } catch { }
}
if ($removeData -and (Test-Path -LiteralPath $appDataMarker -PathType Leaf)) {
    Show-Message 'Há um marcador da correção Steam do FC 27 que ainda precisa ser preservado para evitar deixar alterações órfãs na Steam. Os arquivos do app serão removidos, mas os dados locais serão mantidos.' 'Beako Presence' Information
    $removeData = $false
}

$startMenuShortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Beako Presence.lnk'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)) 'Beako Presence.lnk'
Remove-Item -LiteralPath $startMenuShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BeakoPresence' -Recurse -Force -ErrorAction SilentlyContinue

$cleanupPath = Join-Path $env:TEMP ('BeakoPresence-uninstall-' + [Guid]::NewGuid().ToString('N') + '.ps1')
$cleanupScript = @'
param([string]$InstallPath, [string]$AppData, [int]$WaitForProcessId, [bool]$RemoveData, [string]$SelfPath)
$ErrorActionPreference = 'SilentlyContinue'
try { Wait-Process -Id $WaitForProcessId -Timeout 90 } catch { }
$expected = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\BeakoPresence'))
$candidate = [IO.Path]::GetFullPath($InstallPath)
if ($candidate.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $candidate -Recurse -Force }
if ($RemoveData) {
    $expectedData = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'BeakoPresence'))
    if ([IO.Path]::GetFullPath($AppData).Equals($expectedData, [StringComparison]::OrdinalIgnoreCase) -and -not (Test-Path -LiteralPath (Join-Path $expectedData 'steamfix-root.txt'))) {
        Remove-Item -LiteralPath $expectedData -Recurse -Force
    }
}
Remove-Item -LiteralPath $SelfPath -Force
'@
[IO.File]::WriteAllText($cleanupPath, $cleanupScript, [System.Text.UTF8Encoding]::new($false))
$quotedCleanup = '"' + $cleanupPath + '"'
$quotedInstall = '"' + $installPath + '"'
$quotedData = '"' + $appData + '"'
$removeFlag = if ($removeData) { '$true' } else { '$false' }
$arguments = '-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File ' + $quotedCleanup + ' -InstallPath ' + $quotedInstall + ' -AppData ' + $quotedData + ' -WaitForProcessId ' + $PID + ' -RemoveData ' + $removeFlag + ' -SelfPath ' + $quotedCleanup
Start-Process -FilePath (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList $arguments -WindowStyle Hidden
Show-Message 'Os atalhos e o registro de desinstalação foram removidos. A pasta do app será apagada em instantes; os dados locais seguem a opção escolhida.' 'Beako Presence' Information


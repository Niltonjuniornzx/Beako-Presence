# FC27-Steam-Setup.ps1
# Prepara a Steam para reconhecer "EA SPORTS FC 27" (appid 4080220) como instalado,
# no mesmo esquema do metodo da r/DiscordQuests (appmanifest + pasta + FC27.exe).
#
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\FC27-Steam-Setup.ps1              # instala
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\FC27-Steam-Setup.ps1 -Iniciar     # instala e abre o FC27.exe
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\FC27-Steam-Setup.ps1 -Remover     # desfaz tudo
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\FC27-Steam-Setup.ps1 -ReiniciarSteam   # encerra/reabre a Steam depois de instalar
#
# Opcional: -Exe "C:\caminho\para\arquivo.exe" usa o SEU executavel no lugar do stub.
# Aviso: simular atividade de jogo pode violar os termos do Discord/Steam; use por sua conta e risco.

param(
    [string]$Exe = "",
    [switch]$Iniciar,
    [switch]$Remover,
    [switch]$ReiniciarSteam
)

$ErrorActionPreference = 'Stop'
$AppId      = '4080220'
$InstallDir = 'EA SPORTS FC 27'
$ExeName    = 'FC27.exe'
$GameName   = 'EA SPORTS FC' + [char]0x2122 + ' 27'   # nome com (TM) como aparece na Steam

function Get-SteamPath {
    $p = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if ($p) { return ($p -replace '/', '\') }
    $p = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam' -ErrorAction SilentlyContinue).InstallPath
    if ($p) { return $p.TrimEnd('\') }
    return 'C:\Program Files (x86)\Steam'
}

$steam        = Get-SteamPath
$steamapps    = Join-Path $steam 'steamapps'
$common       = Join-Path $steamapps 'common'
$gameFolder   = Join-Path $common $InstallDir
$manifestPath = Join-Path $steamapps ("appmanifest_" + $AppId + ".acf")
$targetExe    = Join-Path $gameFolder $ExeName

function Stop-Fc27 {
    Get-Process FC27 -ErrorAction SilentlyContinue | ForEach-Object {
        try { $_.Kill(); $_.WaitForExit(3000) } catch { }
    }
}

if ($Remover) {
    Write-Host "Removendo simulacao do FC 27..." -ForegroundColor Yellow
    Stop-Fc27
    if (Test-Path $manifestPath) {
        $conteudo = Get-Content $manifestPath -Raw -ErrorAction SilentlyContinue
        if ($conteudo -match $AppId) {
            Remove-Item $manifestPath -Force
            Write-Host "  appmanifest_$AppId.acf removido." -ForegroundColor Green
        } else {
            Write-Host "  appmanifest_$AppId.acf existe mas nao e o nosso; mantido." -ForegroundColor Yellow
        }
    }
    if (Test-Path $gameFolder) {
        $soNossa = -not (Get-ChildItem $gameFolder -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne $ExeName })
        if ($soNossa) {
            Remove-Item $gameFolder -Recurse -Force
            Write-Host "  Pasta '$InstallDir' removida." -ForegroundColor Green
        } else {
            Write-Host "  Pasta '$InstallDir' tem outros arquivos; mantida. Remova manualmente se quiser:" -ForegroundColor Yellow
            Write-Host "  $gameFolder" -ForegroundColor Yellow
        }
    }
    Write-Host "Pronto. Reinicie a Steam se ela estiver aberta." -ForegroundColor Green
    exit 0
}

# --- Instalacao ---

if (-not (Test-Path $steamapps)) { throw "Pasta steamapps nao encontrada em: $steam" }
Write-Host "Steam encontrada em: $steam"

# Se ja existe um manifest REAL desse appid (jogo comprado), nao mexe.
if (Test-Path $manifestPath) {
    $conteudo = Get-Content $manifestPath -Raw -ErrorAction SilentlyContinue
    if ($conteudo -match 'InstalledDepots|MountedDependencies|buildid') {
        Write-Host "appmanifest_$AppId.acf real ja existe (jogo instalado pela Steam). Nada a fazer." -ForegroundColor Yellow
        exit 0
    }
}

# 1) appmanifest_4080220.acf (conteudo minimo do metodo da comunidade)
$manifest = @"
"AppState"
{
    "appid"             "$AppId"
    "name"              "$GameName"
    "StateFlags"        "4"
    "installdir"        "$InstallDir"
}
"@
[System.IO.File]::WriteAllText($manifestPath, $manifest, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "  Criado: $manifestPath" -ForegroundColor Green

# 2) steamapps\common\EA SPORTS FC 27\
New-Item -ItemType Directory -Force -Path $gameFolder | Out-Null
Write-Host "  Criada:  $gameFolder" -ForegroundColor Green

# 3) FC27.exe dentro da pasta
if ($Exe) {
    if (-not (Test-Path $Exe)) { throw "Executavel informado nao encontrado: $Exe" }
    Copy-Item $Exe $targetExe -Force
    Write-Host "  Copiado seu executavel como: $targetExe" -ForegroundColor Green
} else {
    # Compila um stub minimo (janela "EA SPORTS FC 27") com o csc do .NET Framework
    $stubSource = @'
using System;
using System.Drawing;
using System.Windows.Forms;

static class Program {
    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Form f = new Form();
        f.Text = "EA SPORTS FC 27";
        f.Size = new Size(380, 190);
        f.FormBorderStyle = FormBorderStyle.FixedSingle;
        f.MaximizeBox = false;
        f.StartPosition = FormStartPosition.CenterScreen;
        Label l = new Label();
        l.Text = "Simulacao de atividade\nEA SPORTS FC 27 (appid 4080220)\n\nDeixe esta janela aberta.\nFeche para encerrar.";
        l.Dock = DockStyle.Fill;
        l.TextAlign = ContentAlignment.MiddleCenter;
        f.Controls.Add(l);
        Application.Run(f);
    }
}
'@
    $csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (-not (Test-Path $csc)) { throw "csc.exe nao encontrado; use -Exe para informar um executavel." }
    $srcPath = Join-Path $env:TEMP 'fc27_stub.cs'
    $tmpExe  = Join-Path $env:TEMP 'fc27_stub.exe'
    [System.IO.File]::WriteAllText($srcPath, $stubSource, (New-Object System.Text.UTF8Encoding($false)))
    & $csc /nologo /target:winexe /out:"$tmpExe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "$srcPath" | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $tmpExe)) { throw "Falha ao compilar o FC27.exe stub." }
    try {
        Copy-Item $tmpExe $targetExe -Force
    } catch {
        Stop-Fc27
        Copy-Item $tmpExe $targetExe -Force
    }
    Remove-Item $tmpExe, $srcPath -Force -ErrorAction SilentlyContinue
    Write-Host "  Gerado:  $targetExe (stub de atividade)" -ForegroundColor Green
}

# 4) Reiniciar a Steam (opcional) para ela reler a steamapps
if ($ReiniciarSteam) {
    if (Get-Process steam -ErrorAction SilentlyContinue) {
        Write-Host "Encerrando a Steam (shutdown suave)..."
        Start-Process (Join-Path $steam 'steam.exe') -ArgumentList '-shutdown'
        (Get-Process steam -ErrorAction SilentlyContinue) | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    Start-Process (Join-Path $steam 'steam.exe')
    Write-Host "Steam reiniciada." -ForegroundColor Green
} elseif (Get-Process steam -ErrorAction SilentlyContinue) {
    Write-Host "Steam esta aberta; reinicie-a para ela reler a pasta steamapps (-ReiniciarSteam)." -ForegroundColor Yellow
}

# 5) Iniciar o FC27.exe (passo final do metodo)
if ($Iniciar) {
    if (Get-Process FC27 -ErrorAction SilentlyContinue) {
        Write-Host "FC27.exe ja esta em execucao; nao iniciei outro." -ForegroundColor Yellow
    } else {
        Start-Process $targetExe -WorkingDirectory $gameFolder
        Write-Host "FC27.exe em execucao." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Concluido. Proximo passo: abra a Steam e veja 'EA SPORTS FC 27' na biblioteca;" -ForegroundColor Cyan
Write-Host "com o FC27.exe rodando e o Discord aberto, acompanhe o progresso da quest." -ForegroundColor Cyan


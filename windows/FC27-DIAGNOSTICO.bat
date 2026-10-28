@echo off
rem Diagnostico rapido do FC 27 no PC (rodar onde a quest nao avanca)
title Diagnostico FC 27
set STEAM=C:\Program Files (x86)\Steam
echo === Diagnostico FC 27 (appid 4080220) ===
echo.

if exist "%STEAM%\steamapps\appmanifest_4080220.acf" (echo [OK]    appmanifest_4080220.acf) else (echo [FALTA] appmanifest_4080220.acf - clique Iniciar no Beako)
if exist "%STEAM%\steamapps\common\EA SPORTS FC 27\FC27.exe" (echo [OK]    FC27.exe na pasta da Steam) else (echo [FALTA] FC27.exe - clique Iniciar no Beako)

tasklist /fi "imagename eq FC27.exe" 2>nul | find /i "FC27.exe" >nul
if not errorlevel 1 (echo [OK]    FC27.exe em execucao) else (echo [PARADO] FC27.exe nao esta rodando - clique Iniciar no Beako e deixe aberto)

tasklist /fi "imagename eq Discord.exe" 2>nul | find /i "Discord.exe" >nul
if not errorlevel 1 (echo [OK]    Discord aberto) else (echo [FECHADO] Discord nao esta rodando - abra o Discord DESKTOP)

reg query "HKCU\Software\Valve\Steam" /v SteamPath >nul 2>&1
if not errorlevel 1 (echo [OK]    registro SteamPath apontando pra Steam) else (echo [FALTA] registro SteamPath - a Steam precisa estar instalada)

echo.
echo Se tudo acima esta [OK] e a quest nao avanca:
echo   1) ACEITE a missao no Discord (botao Missao Aceita)
echo   2) SAIA do Discord de verdade: icone na bandeja perto do relogio,
echo      botao direito - Sair/Quit (fechar a janela NAO sai, fica oculto)
echo   3) Abra o Discord de novo com o FC27.exe ainda rodando
echo   4) Espere 1 a 2 minutos e olhe a quest de novo
echo.
pause


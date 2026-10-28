@echo off
rem Instala a simulacao do EA SPORTS FC 27 (appmanifest + pasta + FC27.exe) e abre o FC27.exe
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0FC27-Steam-Setup.ps1" -Iniciar %*
pause


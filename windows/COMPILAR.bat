@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "PAYLOAD=%TEMP%\BeakoPresence-SetupPayload-%RANDOM%-%RANDOM%.zip"

"%CSC%" /nologo /target:winexe /out:BeakoPresence.App.exe /win32icon:BeakoPresence.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll BeakoPresence.cs
if errorlevel 1 goto :failed

powershell.exe -NoProfile -ExecutionPolicy Bypass -File BuildPayload.ps1 -OutputPath "%PAYLOAD%"
if errorlevel 1 goto :failed

"%CSC%" /nologo /target:winexe /out:BeakoPresence.exe /win32icon:BeakoPresence.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /resource:"%PAYLOAD%,BeakoPresence.SetupPayload.zip" BeakoSetup.cs
if errorlevel 1 goto :failed

del /q "%PAYLOAD%" >nul 2>&1
echo App e instalador autocontido compilados.
pause
exit /b 0

:failed
del /q "%PAYLOAD%" >nul 2>&1
echo Falha ao compilar o app ou instalador.
pause
exit /b 1


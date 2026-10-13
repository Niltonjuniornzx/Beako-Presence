@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
echo BEAKO PRESENCE 3.0 - Windows 10
echo Feche qualquer instancia antiga antes de continuar.
if not exist "BeakoPresence.ico" (echo ERRO: BeakoPresence.ico nao encontrado. & pause & exit /b 1)
if exist "BeakoPresenceAnime.exe" (
  del /f /q "BeakoPresenceAnime.exe" 2>nul
  if exist "BeakoPresenceAnime.exe" (echo ERRO: feche o BeakoPresence antes de recompilar. & pause & exit /b 1)
)
set "FX=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
call :compile
if not errorlevel 1 if exist "BeakoPresenceAnime.exe" goto run
set "FX=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
call :compile
if not errorlevel 1 if exist "BeakoPresenceAnime.exe" goto run
echo.
echo Nao foi possivel compilar. O erro foi salvo em compilacao.log.
echo Envie o conteudo desse arquivo para verificar o problema.
pause
exit /b 1
:run
echo Novo EXE compilado com o icone da personagem.
echo Se o Explorer ainda mostrar o icone antigo, renomeie o EXE ou reinicie o Explorer.
start "" "%~dp0BeakoPresenceAnime.exe"
exit /b 0
:compile
if not exist "%FX%\csc.exe" exit /b 1
echo Compilando com %FX%...
rem Ignora csc.rsp: ele inclui Microsoft.CSharp.dll, que o app nao usa.
rem Todas as referencias sao explicitas, inclusive mscorlib.
"%FX%\csc.exe" /noconfig /nostdlib+ /nologo /codepage:65001 /utf8output /target:winexe /optimize+ /platform:anycpu /win32icon:BeakoPresence.ico /out:BeakoPresenceAnime.exe /reference:"%FX%\mscorlib.dll" /reference:"%FX%\System.dll" /reference:"%FX%\System.Core.dll" /reference:"%FX%\System.Windows.Forms.dll" /reference:"%FX%\System.Drawing.dll" /reference:"%FX%\System.Web.Extensions.dll" BeakoPresence.cs > compilacao.log 2>&1
set "BUILD_RESULT=%ERRORLEVEL%"
type compilacao.log
exit /b %BUILD_RESULT%

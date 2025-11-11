@echo off
REM olmez Agent Helpers - User session'da başlatılır (Startup klasörüne kopyalanır)

set AGENT_PATH=C:\Program Files\olmez\Agent
if not exist "%AGENT_PATH%" set AGENT_PATH=%~dp0AgentHost\bin\Debug\net8.0-windows

echo Starting olmez Agent Helpers...

REM DesktopHelper'ı başlat
if exist "%AGENT_PATH%\DesktopHelper.exe" (
    echo Starting DesktopHelper...
    start "" "%AGENT_PATH%\DesktopHelper.exe"
) else (
    echo WARNING: DesktopHelper.exe not found at %AGENT_PATH%
)

REM NotificationHelper'ı başlat
if exist "%AGENT_PATH%\NotificationHelper.exe" (
    echo Starting NotificationHelper...
    start "" "%AGENT_PATH%\NotificationHelper.exe"
) else (
    echo WARNING: NotificationHelper.exe not found at %AGENT_PATH%
)

echo.
echo Helpers started. Check logs:
echo - DesktopHelper: %%TEMP%%\DesktopHelper.log
echo - NotificationHelper: %%TEMP%%\NotificationHelper.log
echo.
timeout /t 3

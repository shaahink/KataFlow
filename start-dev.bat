@echo off
echo Starting KataFlow Development Environment...
echo.

start "KataFlow API" cmd /k "cd /d %~dp0src\KataFlow.Api && dotnet run"
timeout /t 8 >nul

start "KataFlow Web" cmd /k "cd /d %~dp0src\KataFlow.Web && npx ng serve --proxy-config proxy.conf.json --port 4200"

echo.
echo ========================================
echo  API:      http://localhost:5100
echo  Web App:  http://localhost:4200
echo ========================================
echo.
echo Close the windows to stop.
echo.
pause

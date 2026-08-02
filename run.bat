@echo off
chcp 65001 > nul
cd /d "%~dp0"
echo Project RouteX アプリケーションを起動しています...
dotnet run --launch-profile https
pause

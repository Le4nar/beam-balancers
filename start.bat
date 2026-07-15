@echo off
chcp 65001 >nul
title Lampac NextGen Core

cd /d "%~dp0"

echo [Lampac] Starting Core on http://*:9118 ...
echo.

"C:\Program Files\dotnet\dotnet.exe" Core.dll

echo.
echo [Lampac] Core stopped.
pause

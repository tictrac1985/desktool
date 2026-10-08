@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo === DeskMate build %DATE% %TIME% ===> build-log.txt
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" >> build-log.txt 2>&1
set RC=%ERRORLEVEL%
echo.>> build-log.txt
echo === EXIT CODE: %RC% ===>> build-log.txt
echo.
if "%RC%"=="0" (echo BUILD OK) else (echo BUILD FAILED - exit code %RC%)
echo Full log: %~dp0build-log.txt
echo.
pause

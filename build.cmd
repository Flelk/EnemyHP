@echo off
rem Double-click to build, package and install into your game for testing.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Install %*
pause

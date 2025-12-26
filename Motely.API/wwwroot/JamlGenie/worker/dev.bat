@echo off
REM Quick dev script for Windows

echo 🧞 Starting JamlGenie Worker locally...
echo.
echo Make sure you've run: wrangler login
echo.

cd /d "%~dp0"
wrangler dev

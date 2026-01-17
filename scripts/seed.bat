@echo off
echo Starting seeding...
cd /d "%~dp0"
node manual-seed.js
echo Done!
pause

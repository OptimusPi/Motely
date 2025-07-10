@echo off
echo Building Motely...
dotnet build -c Release
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo Running test...

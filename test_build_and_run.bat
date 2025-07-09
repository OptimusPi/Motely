@echo off
echo Building Motely...
cd /d X:\Motely
dotnet build
if %errorlevel% neq 0 (
    echo Build failed!
    exit /b %errorlevel%
)
echo Build successful!
echo.
echo Running simple test...
dotnet run -- --config simple_test --threads 2 --batchSize 1 --startBatch 0 --endBatch 10
pause

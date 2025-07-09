@echo off
echo Testing PerkeoObservatoryFilter vs OuijaJsonFilter
echo.
echo ========================================
echo Test 1: PerkeoObservatoryFilter
echo ========================================
dotnet run -- --config test --threads 2 --batchSize 4 --startBatch 0 --endBatch 100
echo.
echo Press any key to test OuijaJsonFilter...
pause

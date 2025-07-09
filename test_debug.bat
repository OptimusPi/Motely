@echo off
echo Testing different configs with debug output...
echo.
echo ========================================
echo Test 1: debug_test (simple common joker)
echo ========================================
dotnet run -- --config debug_test --threads 2 --batchSize 2 --startBatch 0 --endBatch 10 --debug
echo.
echo ========================================
echo Test 2: simple_wants_only (no needs)
echo ========================================
dotnet run -- --config simple_wants_only --threads 2 --batchSize 2 --startBatch 0 --endBatch 10 --debug
echo.
echo ========================================
echo Test 3: test (Perkeo as need)
echo ========================================
dotnet run -- --config test --threads 2 --batchSize 2 --startBatch 0 --endBatch 5 --debug
pause

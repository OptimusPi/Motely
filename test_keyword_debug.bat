@echo off
echo Testing keyword seed generation...
echo.

REM Test with a simple keyword
Motely.exe --config simple_test --keyword TEST --threads 4 --debug

echo.
echo Checking seed generation only...
echo.
pause
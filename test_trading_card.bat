@echo off
echo Building Motely...
dotnet build -c Release
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo Testing Trading Card search for seed D9FUJ7VX:
bin\Release\net8.0\Motely.exe --config trading_card_test.ouija.json --debug --seed D9FUJ7VX

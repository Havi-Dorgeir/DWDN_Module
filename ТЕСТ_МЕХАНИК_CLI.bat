@echo off
chcp 65001 > nul
echo ============================================================================
echo   БЫСТРЫЙ CLI-ТЕСТ МЕХАНИК DWDN (ДЛЯ ИИ И СКРИПТОВ)
echo ============================================================================
dotnet run --project "%~dp0MechanicsTester\MechanicsTester.csproj" -c Release -- --cli
pause

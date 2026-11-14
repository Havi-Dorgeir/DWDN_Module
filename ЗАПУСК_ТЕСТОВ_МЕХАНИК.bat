@echo off
chcp 65001 > nul
title DWDN Pre-Flight Mechanics Tester v1.0
echo ============================================================================
echo   ЗАПУСК АВТОНОМНОГО ПОЛИГОНА ТЕСТИРОВАНИЯ МЕХАНИК И ЛОГИКИ DWDN
echo ============================================================================
if not exist "%~dp0MechanicsTester\bin\Release\net8.0-windows\MechanicsTester.exe" (
    echo [Сборка тестового полигона MechanicsTester...]
    dotnet build "%~dp0MechanicsTester\MechanicsTester.csproj" -c Release
)
start "" "%~dp0MechanicsTester\bin\Release\net8.0-windows\MechanicsTester.exe"

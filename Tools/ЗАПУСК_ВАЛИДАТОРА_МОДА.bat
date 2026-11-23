@echo off
chcp 65001 > nul
title DWDN Pre-Flight Mod Validator v1.0
echo ============================================================================
echo   ЗАПУСК МГНОВЕННОГО ПРЕДПУСКОВОГО ВАЛИДАТОРА И АВТО-ФИКСЕРА МОДА (DWDN)
echo ============================================================================
if not exist "%~dp0ModValidator\bin\Release\net8.0-windows\ModValidator.exe" (
    echo [Сборка ModValidator...]
    dotnet build "%~dp0ModValidator\ModValidator.csproj" -c Release
)
start "" "%~dp0ModValidator\bin\Release\net8.0-windows\ModValidator.exe"

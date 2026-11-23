@echo off
chcp 65001 > nul
title DWDN Weapon Calibrator
if not exist "%~dp0WeaponCalibrator\bin\Release\net8.0-windows\WeaponCalibrator.exe" (
    echo [Сборка WeaponCalibrator...]
    dotnet build "%~dp0WeaponCalibrator\WeaponCalibrator.csproj" -c Release
)
start "" "%~dp0WeaponCalibrator\bin\Release\net8.0-windows\WeaponCalibrator.exe"

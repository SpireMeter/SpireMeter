@echo off
rem Runs install.ps1 without changing the PowerShell script policy on this PC.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*

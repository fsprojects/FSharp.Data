@echo off

dotnet tool restore
if errorlevel 1 (
  exit /b %errorlevel%
)
dotnet paket restore
if errorlevel 1 (
  exit /b %errorlevel%
)

dotnet fake build -t Build

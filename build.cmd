@echo off

.paket\paket.exe restore
if errorlevel 1 (
  exit /b %errorlevel%
)

set VisualStudioVersion=14.0
packages\FAKE\tools\FAKE.exe build.fsx %*

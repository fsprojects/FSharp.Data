@echo off

dotnet tool restore
dotnet paket restore
dotnet fake build -t Build

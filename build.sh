#!/usr/bin/env bash
dotnet tool restore
dotnet paket restore
dotnet run --project build/build.fsproj -t Build

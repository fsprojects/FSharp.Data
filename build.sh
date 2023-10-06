#!/usr/bin/env bash
dotnet tool restore
dotnet paket restore
dotnet run --project build/_build.fsproj -t Build

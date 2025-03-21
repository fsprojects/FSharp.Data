#!/bin/bash
set -e

dotnet tool restore
dotnet paket restore
dotnet restore


# Exec the container's main process (what's set as CMD in the Dockerfile).
exec "$@"
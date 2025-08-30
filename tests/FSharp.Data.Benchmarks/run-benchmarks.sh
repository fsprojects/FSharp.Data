#!/bin/bash

set -e

cd "$(dirname "$0")"

echo "FSharp.Data Performance Benchmarks"
echo "=================================="
echo ""

# Build in Release mode
echo "Building benchmarks in Release mode..."
dotnet build -c Release
echo ""

# Check arguments
if [ "$1" = "quick" ]; then
    echo "Running quick dry-run benchmark (ParseSimpleJson only)..."
    dotnet run -c Release -- --job dry --filter "*ParseSimpleJson*"
elif [ "$1" = "json" ]; then
    echo "Running JSON parsing benchmarks..."
    dotnet run -c Release -- json
elif [ "$1" = "conversions" ]; then
    echo "Running JSON conversion benchmarks..."
    dotnet run -c Release -- conversions
elif [ "$1" = "simple" ]; then
    echo "Running simple JSON benchmarks only..."
    dotnet run -c Release -- --filter "*ParseSimpleJson*|*ParseNestedJson*"
elif [ "$#" -eq 0 ]; then
    echo "Running all benchmarks..."
    echo "This may take several minutes..."
    dotnet run -c Release
else
    echo "Running custom benchmark filter: $*"
    dotnet run -c Release -- "$@"
fi

echo ""
echo "Benchmark run complete!"
echo "Results saved to BenchmarkDotNet.Artifacts/"
# FSharp.Data Benchmarks

This project contains performance benchmarks for the FSharp.Data library using BenchmarkDotNet.

## Available Benchmarks

### JSON Benchmarks (`JsonBenchmarks`)
- `ParseSimpleJson` - Parse a small simple JSON document
- `ParseNestedJson` - Parse a nested JSON document
- `ParseGitHubJson` - Parse GitHub API response (~75KB)
- `ParseTwitterJson` - Parse Twitter API response (~74KB) 
- `ParseWorldBankJson` - Parse World Bank API response (~20KB)
- `ToStringGitHubJson` - JSON parsing + ToString()
- `ToStringTwitterJson` - JSON parsing + ToString()

### JSON Conversion Benchmarks (`JsonConversionBenchmarks`)
- `AccessProperties` - Access JSON object properties
- `GetArrayElements` - Extract array elements from JSON

## Running Benchmarks

### Prerequisites
1. Build the project in Release mode:
   ```bash
   dotnet build -c Release
   ```

### Run All Benchmarks
```bash
dotnet run -c Release
```

### Run Specific Benchmark Categories
```bash
# JSON parsing benchmarks only
dotnet run -c Release -- json

# JSON conversion benchmarks only
dotnet run -c Release -- conversions
```

### Run Specific Benchmarks
```bash
# Run only simple JSON parsing
dotnet run -c Release -- --filter "*ParseSimpleJson*"

# Run only GitHub JSON benchmarks
dotnet run -c Release -- --filter "*GitHub*"
```

### Quick Dry Run (for testing)
```bash
dotnet run -c Release -- --job dry --filter "*ParseSimpleJson*"
```

## Understanding Results

BenchmarkDotNet will show:
- **Mean**: Average execution time per operation
- **StdErr**: Standard error of the mean
- **Min/Max**: Fastest and slowest execution times
- **Memory**: Memory allocations (when `[MemoryDiagnoser]` is used)

Results are saved to `BenchmarkDotNet.Artifacts/` folder.

## Performance Testing Strategy

These benchmarks provide baseline measurements for:

1. **JSON Parsing Performance**: How fast can we parse different sizes/types of JSON?
2. **Memory Allocation**: How much memory is allocated during operations?
3. **Regression Testing**: Detect performance regressions in changes
4. **Optimization Validation**: Measure impact of performance improvements

## Adding New Benchmarks

1. Add new benchmark methods to existing classes or create new benchmark classes
2. Mark methods with `[<Benchmark>]` attribute
3. Add `[<GlobalSetup>]` for initialization if needed
4. Use `[<Params>]` for parameterized benchmarks
5. Add appropriate diagnostics like `[<MemoryDiagnoser>]`

Example:
```fsharp
[<Benchmark>]
member this.MyNewBenchmark() =
    // Your code to benchmark here
    SomeOperation()
```
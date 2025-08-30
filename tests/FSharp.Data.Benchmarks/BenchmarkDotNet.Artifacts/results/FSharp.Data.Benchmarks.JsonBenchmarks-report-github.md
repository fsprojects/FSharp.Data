```

BenchmarkDotNet v0.15.2, Linux Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.413
  [Host]     : .NET 8.0.19 (8.0.1925.36514), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 8.0.19 (8.0.1925.36514), X64 RyuJIT AVX2


```
| Method              | Mean         | Error       | StdDev      | Gen0    | Gen1    | Gen2    | Allocated |
|-------------------- |-------------:|------------:|------------:|--------:|--------:|--------:|----------:|
| ParseSimpleJson     |     737.7 ns |     2.39 ns |     1.99 ns |  0.0696 |       - |       - |   1.15 KB |
| ParseNestedJson     |     917.2 ns |     1.63 ns |     1.36 ns |  0.0868 |       - |       - |   1.43 KB |
| ParseGitHubJson     | 333,775.1 ns | 1,210.55 ns | 1,010.86 ns | 24.9023 | 12.2070 |       - |  409.2 KB |
| ParseTwitterJson    |           NA |          NA |          NA |      NA |      NA |      NA |        NA |
| ParseWorldBankJson  |  99,754.1 ns |   276.91 ns |   231.24 ns |  7.9346 |  1.7090 |       - | 131.02 KB |
| ToStringGitHubJson  | 731,842.3 ns | 6,551.90 ns | 5,808.09 ns | 46.8750 | 46.8750 | 46.8750 |  771.7 KB |
| ToStringTwitterJson |           NA |          NA |          NA |      NA |      NA |      NA |        NA |

Benchmarks with issues:
  JsonBenchmarks.ParseTwitterJson: DefaultJob
  JsonBenchmarks.ToStringTwitterJson: DefaultJob

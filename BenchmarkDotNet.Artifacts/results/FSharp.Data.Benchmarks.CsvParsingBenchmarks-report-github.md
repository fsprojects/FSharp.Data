```

BenchmarkDotNet v0.15.2, Linux Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.413
  [Host] : .NET 8.0.19 (8.0.1925.36514), X64 RyuJIT AVX2 DEBUG


```
| Method             | Mean | Error |
|------------------- |-----:|------:|
| ParseSimpleCsv     |   NA |    NA |
| ParseMediumCsv     |   NA |    NA |
| ParseLargeCsv      |   NA |    NA |
| ParseQuotedCsv     |   NA |    NA |
| ParseTitanicCsv    |   NA |    NA |
| ParseAndAccessData |   NA |    NA |

Benchmarks with issues:
  CsvParsingBenchmarks.ParseSimpleCsv: DefaultJob
  CsvParsingBenchmarks.ParseMediumCsv: DefaultJob
  CsvParsingBenchmarks.ParseLargeCsv: DefaultJob
  CsvParsingBenchmarks.ParseQuotedCsv: DefaultJob
  CsvParsingBenchmarks.ParseTitanicCsv: DefaultJob
  CsvParsingBenchmarks.ParseAndAccessData: DefaultJob

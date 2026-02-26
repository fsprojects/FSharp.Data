(**
---
category: Type Providers
categoryindex: 1
index: 6
---
*)
(*** condition: prepare ***)
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Csv.Core.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Json.Core.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Http.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.dll"
(*** condition: fsx ***)
#if FSX
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"
#endif
(*** condition: ipynb ***)
#if IPYNB
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"

Formatter.SetPreferredMimeTypesFor(typeof<obj>, "text/plain")
Formatter.Register(fun (x: obj) (writer: TextWriter) -> fprintfn writer "%120A" x)
#endif
(**

# Type Inference and Missing Values

This page describes the **type inference rules** used by the FSharp.Data type providers
([CSV](CsvProvider.html), [JSON](JsonProvider.html), [XML](XmlProvider.html) and [HTML](HtmlProvider.html)).
Understanding these rules helps you know what F# types to expect for each property,
and how to handle missing, null, or optional values at runtime.

## Overview

All FSharp.Data type providers infer types from a **sample document** (or a list of samples)
at compile time (design time). The generated F# types reflect the structure of the sample.
At runtime, any document with a compatible structure can be read — but the generated types
are fixed by the sample.

A key principle: **the sample should be representative.** If a property is present in the
sample but absent from runtime data, it can raise a `KeyNotFoundException`. Conversely,
if runtime data contains new properties not in the sample, they are not accessible via the
generated type (though they may still be reachable through the underlying `JsonValue`,
`XElement`, etc.).

## Numeric Type Inference

When inferring numeric types, the providers prefer the most precise type that can represent
all values. The preference order (most preferred first) is:

1. `int` – 32-bit signed integer
2. `int64` – 64-bit signed integer
3. `decimal` – exact decimal arithmetic (preferred for financial/monetary values)
4. `float` – 64-bit floating point (used when `decimal` cannot represent the value,
   or when missing values appear in a CSV column that would otherwise be `decimal`)

If values in a column or array mix two types, the provider automatically promotes to the
wider type. For example, a JSON array `[1, 2, 3.14]` will produce `decimal` values.
*)

open FSharp.Data

// int is inferred when all values are integers
type IntsOnly = JsonProvider<""" [1, 2, 3] """>

// decimal is inferred when any value has a fractional part
type WithDecimal = JsonProvider<""" [1, 2, 3.14] """>

(*** include-fsi-merged-output ***)

(**
## Boolean Inference (CSV)

In CSV files, columns whose values are exclusively drawn from the set
`0`, `1`, `Yes`, `No`, `True`, `False` (case-insensitive) are inferred as `bool`.
Any other values in the column cause it to be treated as a string.

## Date and Time Inference

The providers recognise date and time strings in standard ISO 8601 formats:

| Inferred Type | When Used | Example Value |
|---|---|---|
| `DateTime` | Date + time strings (default) | `"2023-06-15T12:00:00"` |
| `DateTimeOffset` | Date + time + timezone offset (always) | `"2023-06-15T12:00:00+02:00"` |
| `DateTimeOffset` | Any date + time string when `PreferDateTimeOffset=true` | `"2023-06-15T12:00:00"` |
| `DateOnly` (.NET 6+) | Date-only strings when `PreferDateOnly=true` | `"2023-06-15"` |
| `TimeOnly` (.NET 6+) | Time-only strings when `PreferDateOnly=true` | `"12:00:00"` |

By default (`PreferDateOnly = false`), date-only strings such as `"2023-06-15"` are
inferred as `DateTime` for backward compatibility. Set `PreferDateOnly = true` on
.NET 6 and later to infer them as `DateOnly` instead.

Set `PreferDateTimeOffset = true` to infer all date-time values (that would otherwise be
`DateTime`) as `DateTimeOffset` instead. Values that already carry an explicit timezone
offset (e.g. `"2023-06-15T12:00:00+02:00"`) are always inferred as `DateTimeOffset`
regardless of this flag. `PreferDateTimeOffset` and `PreferDateOnly` are independent:
`DateOnly` values stay as `DateOnly` even when `PreferDateTimeOffset=true`.

If a column mixes `DateOnly` and `DateTime` values, they are unified to `DateTime`.

## Missing Values and Optionals

This is the most important topic for understanding how the providers behave at runtime.
The rules differ slightly across providers.

### JSON Provider

In JSON, a property can be **absent** from an object, or its value can be **null** (`null` literal).
Both cases are handled the same way by the JSON type provider:

- If a property is **missing in some samples**, it is inferred as `option<T>`.
- If a property has a **null value** in some samples, it is inferred as `option<T>`.

This means `None` represents either a missing key or a `null` value at runtime.
*)

// 'age' is missing from the second record → inferred as option<int>
type People =
    JsonProvider<"""
  [ { "name":"Alice", "age":30 },
    { "name":"Bob" } ] """>

for person in People.GetSamples() do
    printf "%s" person.Name

    match person.Age with
    | Some age -> printfn " (age %d)" age
    | None -> printfn " (age unknown)"

(*** include-fsi-merged-output ***)

(**
> **Important runtime note:** If a property is present and non-null in *all* samples, it will be
> inferred as a non-optional type. If such a property is then absent or null in runtime data,
> accessing it will throw a runtime exception. Use multiple samples (or `SampleIsList=true`)
> to ensure optional properties are correctly modelled.

#### Null values in JSON

A JSON `null` value that appears as the value of a typed property is treated as `None`.
A `null` value in a heterogeneous context (e.g. an array of numbers and nulls) is
represented via the `option` mechanism on the generated accessor.

### CSV Provider

CSV files do not have a native null/missing concept. Instead, certain string values are
treated as missing. By default, the following strings (case-insensitive) are recognised
as missing: `NaN`, `NA`, `N/A`, `#N/A`, `:`, `-`, `TBA`, `TBD` (and empty string `""`).

You can override this list with the `MissingValues` static parameter.

When a column has at least one missing value, the inferred type changes as follows:

| Base type | With missing values (default) | With `PreferOptionals=true` |
|---|---|---|
| `int` | `Nullable<int>` (`int?`) | `int option` |
| `int64` | `Nullable<int64>` (`int64?`) | `int64 option` |
| `decimal` | `float` (using `Double.NaN`) | `float option` |
| `float` | `float` (using `Double.NaN`) | `float option` |
| `bool` | `bool option` | `bool option` |
| `DateTime` | `DateTime option` | `DateTime option` |
| `DateTimeOffset` | `DateTimeOffset option` | `DateTimeOffset option` |
| `DateOnly` | `Nullable<DateOnly>` | `DateOnly option` |
| `Guid` | `Guid option` | `Guid option` |
| `string` | `string` (empty string `""` for missing) | `string option` |

The key differences between the default and `PreferOptionals=true`:
- In the default mode, integers use `Nullable<T>` and decimals are widened to `float` with `Double.NaN`.
- With `PreferOptionals=true`, **all** types use `T option` and you never get `Double.NaN` or `Nullable<T>`.
- Strings are never made into `string option` by default (empty string represents missing); use
  `PreferOptionals=true` to get `string option`.

**Design-time safety:** If your sample file contains no missing values in a column, but you know
that production data may have missing values, set `AssumeMissingValues=true` to force the provider
to treat all columns as nullable/optional.
*)

// With AssumeMissingValues=true, all columns become nullable/optional
// even if the sample has no missing values
type SafeCsv = CsvProvider<"A,B\n1,2\n3,4", AssumeMissingValues=true>

// With PreferOptionals=true, all columns use 'option' instead of Nullable or NaN
type OptionalsCsv = CsvProvider<"A,B\n1,2\n3,4", PreferOptionals=true>

(*** include-fsi-merged-output ***)

(**

### XML Provider

In XML, values can be missing at the attribute or element level:

- If an **attribute** is present in some sample elements but absent in others, it is
  inferred as `option<T>`.
- If a **child element** is present in some samples but not all, it is inferred as optional.
- If an attribute or element is **never present** in the sample, it cannot be accessed via the
  generated type at all (use `XElement.Attribute(...)` dynamically in that case).

*)

// 'born' attribute missing from one author → option<int>
type Authors =
    XmlProvider<"""
  <authors>
    <author name="Karl Popper" born="1902" />
    <author name="Thomas Kuhn" />
  </authors>
  """>

let sample = Authors.GetSample()

for author in sample.Authors do
    printf "%s" author.Name

    match author.Born with
    | Some year -> printfn " (born %d)" year
    | None -> printfn ""

(*** include-fsi-merged-output ***)

(**
> **Note:** If an attribute or element is absent from *all* sample data but present at
> runtime, it cannot be accessed through the generated type. You must include at least
> one occurrence (possibly with a dummy value) in the sample to have the provider
> generate an optional property.

## Heterogeneous Types

Sometimes a property can hold values of different types. The JSON type provider handles
this by generating a type with multiple optional accessors — one per observed type.
*)

// Value can be int or string → generates .Number and .String accessors
type HetValues = JsonProvider<""" [{"value":94}, {"value":"hello"}] """>

for item in HetValues.GetSamples() do
    match item.Value.Number, item.Value.String with
    | Some n, _ -> printfn "Number: %d" n
    | _, Some s -> printfn "String: %s" s
    | _ -> ()

(*** include-fsi-merged-output ***)

(**
## Design-Time vs Runtime Behaviour

The type providers perform inference **at compile time** using the sample document.
At runtime, the actual data is parsed against the inferred schema. This has a few
important implications:

1. **Properties that are required at design-time may be missing at runtime.** If a
   property is always present and non-null in your sample, the provider generates a
   non-optional accessor. If runtime data omits that property, a `KeyNotFoundException`
   is thrown when you access it.

2. **New properties in runtime data are ignored.** If runtime JSON has extra keys that
   are not in the sample, those keys are simply not accessible via the generated type.

3. **The sample should cover the full range of variability.** Include examples of all
   optional properties and heterogeneous value types in your sample. Use `SampleIsList=true`
   for JSON/XML when the root is an array of samples.

4. **Runtime errors are lazy.** The providers do not validate the entire document on load.
   A missing or mistyped field only causes an error when that specific property is accessed.

## Summary of Inference-Control Parameters

The following static parameters let you override the default inference behaviour:

| Parameter | Providers | Effect |
|---|---|---|
| `PreferOptionals` | CSV, JSON, XML | Use `T option` for all missing/null values instead of `Nullable<T>` or `Double.NaN` |
| `AssumeMissingValues` | CSV | Treat every column as nullable/optional even if the sample has no missing values |
| `MissingValues` | CSV | Comma-separated list of strings to recognise as missing (replaces defaults) |
| `InferRows` | CSV | Number of rows to use for type inference (default 1000; 0 = all rows) |
| `SampleIsList` | JSON, XML | Treat the top-level array as a list of sample objects, not a single sample |
| `PreferDateOnly` | CSV, JSON, XML | Infer date-only strings as `DateOnly` on .NET 6+ (default `false`) |
| `PreferDateTimeOffset` | CSV, JSON, XML | Infer all date-time values as `DateTimeOffset` instead of `DateTime` (default `false`) |
| `InferenceMode` | JSON, XML | Enable inline schema annotations (`ValuesAndInlineSchemasHints` or `ValuesAndInlineSchemasOverrides`) |
| `Schema` | CSV | Override column names and/or types directly |

For full details on each parameter, see the individual provider documentation:
[CSV](CsvProvider.html) · [JSON](JsonProvider.html) · [XML](XmlProvider.html) · [HTML](HtmlProvider.html)
*)

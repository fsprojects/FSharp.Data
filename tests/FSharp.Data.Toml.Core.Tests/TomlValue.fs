module FSharp.Data.Tests.TomlValue

open System
open NUnit.Framework
open FsUnit
open FSharp.Data

// --------------------------------------------------------------------------------------
// TomlValue union cases
// --------------------------------------------------------------------------------------

[<Test>]
let ``TomlValue String has correct value`` () =
    let v = TomlValue.String "hello"
    match v with
    | TomlValue.String s -> s |> should equal "hello"
    | _ -> failwith "Expected String"

[<Test>]
let ``TomlValue Integer has correct value`` () =
    let v = TomlValue.Integer 42L
    match v with
    | TomlValue.Integer i -> i |> should equal 42L
    | _ -> failwith "Expected Integer"

[<Test>]
let ``TomlValue Float has correct value`` () =
    let v = TomlValue.Float 3.14
    match v with
    | TomlValue.Float f -> f |> should (equalWithin 1e-10) 3.14
    | _ -> failwith "Expected Float"

[<Test>]
let ``TomlValue Boolean true`` () =
    let v = TomlValue.Boolean true
    match v with
    | TomlValue.Boolean b -> b |> should equal true
    | _ -> failwith "Expected Boolean"

[<Test>]
let ``TomlValue Boolean false`` () =
    let v = TomlValue.Boolean false
    match v with
    | TomlValue.Boolean b -> b |> should equal false
    | _ -> failwith "Expected Boolean"

[<Test>]
let ``TomlValue Array has correct elements`` () =
    let v = TomlValue.Array [| TomlValue.Integer 1L; TomlValue.Integer 2L |]
    match v with
    | TomlValue.Array arr -> arr.Length |> should equal 2
    | _ -> failwith "Expected Array"

[<Test>]
let ``TomlValue Table has correct properties`` () =
    let v = TomlValue.Table [| "a", TomlValue.String "x" |]
    match v with
    | TomlValue.Table props -> props.Length |> should equal 1
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// TomlValue.ToJsonValue conversion
// --------------------------------------------------------------------------------------

[<Test>]
let ``ToJsonValue converts String`` () =
    let json = (TomlValue.String "hi").ToJsonValue()
    json |> should equal (JsonValue.String "hi")

[<Test>]
let ``ToJsonValue converts Integer`` () =
    let json = (TomlValue.Integer 99L).ToJsonValue()
    json |> should equal (JsonValue.Number 99m)

[<Test>]
let ``ToJsonValue converts Float`` () =
    let json = (TomlValue.Float 1.5).ToJsonValue()
    json |> should equal (JsonValue.Float 1.5)

[<Test>]
let ``ToJsonValue converts Boolean true`` () =
    let json = (TomlValue.Boolean true).ToJsonValue()
    json |> should equal (JsonValue.Boolean true)

[<Test>]
let ``ToJsonValue converts Array`` () =
    let v = TomlValue.Array [| TomlValue.Integer 1L; TomlValue.Integer 2L |]
    let json = v.ToJsonValue()
    match json with
    | JsonValue.Array arr -> arr.Length |> should equal 2
    | _ -> failwith "Expected JSON array"

[<Test>]
let ``ToJsonValue converts Table`` () =
    let v = TomlValue.Table [| "key", TomlValue.String "val" |]
    let json = v.ToJsonValue()
    match json with
    | JsonValue.Record props -> props.Length |> should equal 1
    | _ -> failwith "Expected JSON record"

[<Test>]
let ``ToJsonValue converts OffsetDateTime to ISO 8601 string`` () =
    let dt = DateTimeOffset(2023, 11, 1, 10, 30, 0, TimeSpan.Zero)
    let json = (TomlValue.OffsetDateTime dt).ToJsonValue()
    match json with
    | JsonValue.String s -> s |> should startWith "2023-11-01T10:30:00"
    | _ -> failwith "Expected JSON string"

[<Test>]
let ``ToJsonValue converts LocalDateTime to ISO 8601 string`` () =
    let dt = DateTime(2023, 6, 15, 8, 0, 0)
    let json = (TomlValue.LocalDateTime dt).ToJsonValue()
    match json with
    | JsonValue.String s -> s |> should equal "2023-06-15T08:00:00"
    | _ -> failwith "Expected JSON string"

[<Test>]
let ``ToJsonValue converts LocalDate to date string`` () =
    let d = DateTime(2024, 1, 31)
    let json = (TomlValue.LocalDate d).ToJsonValue()
    match json with
    | JsonValue.String s -> s |> should equal "2024-01-31"
    | _ -> failwith "Expected JSON string"

[<Test>]
let ``ToJsonValue converts LocalTime to time string`` () =
    let t = TimeSpan(14, 30, 59)
    let json = (TomlValue.LocalTime t).ToJsonValue()
    match json with
    | JsonValue.String s -> s |> should equal "14:30:59"
    | _ -> failwith "Expected JSON string"

// --------------------------------------------------------------------------------------
// Parsing: basic scalars
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse empty document returns empty table`` () =
    let v = TomlValue.Parse ""
    match v with
    | TomlValue.Table props -> props.Length |> should equal 0
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse document with comment only`` () =
    let v = TomlValue.Parse "# this is a comment"
    match v with
    | TomlValue.Table props -> props.Length |> should equal 0
    | _ -> failwith "Expected empty table"

[<Test>]
let ``Parse basic string value`` () =
    let v = TomlValue.Parse """title = "hello world" """
    match v with
    | TomlValue.Table props ->
        props |> should haveLength 1
        snd props.[0] |> should equal (TomlValue.String "hello world")
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse literal string value`` () =
    let v = TomlValue.Parse "path = 'C:\\Users\\test'"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.String "C:\\Users\\test")
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse integer value`` () =
    let v = TomlValue.Parse "count = 42"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Integer 42L)
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse negative integer`` () =
    let v = TomlValue.Parse "count = -7"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Integer -7L)
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse positive integer with + prefix`` () =
    let v = TomlValue.Parse "n = +99"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Integer 99L)
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse integer with underscores`` () =
    let v = TomlValue.Parse "n = 1_000_000"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Integer 1000000L)
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse hex integer`` () =
    let v = TomlValue.Parse "n = 0xFF"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Integer 255L)
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse octal integer`` () =
    let v = TomlValue.Parse "n = 0o17"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Integer 15L)
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse binary integer`` () =
    let v = TomlValue.Parse "n = 0b1010"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Integer 10L)
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse float value`` () =
    let v = TomlValue.Parse "pi = 3.14"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Float f -> f |> should (equalWithin 1e-10) 3.14
        | _ -> failwith "Expected Float"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse float with exponent`` () =
    let v = TomlValue.Parse "x = 6.626e-34"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Float f -> f |> should (equalWithin 1e-40) 6.626e-34
        | _ -> failwith "Expected Float"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse float with underscores`` () =
    let v = TomlValue.Parse "x = 1_000.5"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Float f -> f |> should (equalWithin 1e-10) 1000.5
        | _ -> failwith "Expected Float"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse inf float`` () =
    let v = TomlValue.Parse "x = inf"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Float f -> f |> should equal Double.PositiveInfinity
        | _ -> failwith "Expected Float"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse positive inf float`` () =
    let v = TomlValue.Parse "x = +inf"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Float f -> f |> should equal Double.PositiveInfinity
        | _ -> failwith "Expected Float"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse negative inf float`` () =
    let v = TomlValue.Parse "x = -inf"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Float f -> f |> should equal Double.NegativeInfinity
        | _ -> failwith "Expected Float"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse nan float`` () =
    let v = TomlValue.Parse "x = nan"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Float f -> Double.IsNaN(f) |> should equal true
        | _ -> failwith "Expected Float"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse boolean true`` () =
    let v = TomlValue.Parse "flag = true"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Boolean true)
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse boolean false`` () =
    let v = TomlValue.Parse "flag = false"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Boolean false)
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// Parsing: string escape sequences
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse basic string with escape sequences`` () =
    let v = TomlValue.Parse """s = "tab:\there\nnewline" """
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.String "tab:\there\nnewline")
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse basic string with unicode escape`` () =
    let v = TomlValue.Parse """s = "\u0041" """  // 'A'
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.String "A")
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse basic string with backslash escape`` () =
    let v = TomlValue.Parse """s = "path\\to\\file" """
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.String "path\\to\\file")
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse basic string with quote escape`` () =
    let v = TomlValue.Parse """s = "say \"hi\"" """
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.String "say \"hi\"")
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse multiline basic string`` () =
    let toml = "s = \"\"\"\nline1\nline2\"\"\""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.String s ->
            s |> should contain "line1"
            s |> should contain "line2"
        | _ -> failwith "Expected String"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse multiline basic string with line continuation`` () =
    let toml = "s = \"\"\"\nfoo \\\n    bar\"\"\""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.String "foo bar")
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse multiline literal string`` () =
    let toml = "s = '''\nno \\escapes\nhere'''"
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.String "no \\escapes\nhere")
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse literal string preserves backslash`` () =
    let v = TomlValue.Parse "p = 'C:\\Users\\test'"
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.String "C:\\Users\\test")
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// Parsing: date/time values
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse offset date-time`` () =
    let v = TomlValue.Parse "dt = 1979-05-27T07:32:00+00:00"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.OffsetDateTime dt ->
            dt.Year |> should equal 1979
            dt.Month |> should equal 5
            dt.Day |> should equal 27
        | _ -> failwith "Expected OffsetDateTime"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse offset date-time with Z suffix`` () =
    let v = TomlValue.Parse "dt = 2023-01-01T12:00:00Z"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.OffsetDateTime dt ->
            dt.Year |> should equal 2023
            dt.Offset |> should equal TimeSpan.Zero
        | _ -> failwith "Expected OffsetDateTime"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse offset date-time with fractional seconds`` () =
    let v = TomlValue.Parse "dt = 1979-05-27T00:32:00.999999+00:00"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.OffsetDateTime _ -> ()  // just check it parses
        | _ -> failwith "Expected OffsetDateTime"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse local date-time`` () =
    let v = TomlValue.Parse "dt = 1979-05-27T07:32:00"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.LocalDateTime dt ->
            dt.Year |> should equal 1979
            dt.Hour |> should equal 7
        | _ -> failwith "Expected LocalDateTime"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse local date`` () =
    let v = TomlValue.Parse "d = 1979-05-27"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.LocalDate d ->
            d.Year |> should equal 1979
            d.Month |> should equal 5
            d.Day |> should equal 27
        | _ -> failwith "Expected LocalDate"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse local time`` () =
    let v = TomlValue.Parse "t = 07:32:00"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.LocalTime ts ->
            ts.Hours |> should equal 7
            ts.Minutes |> should equal 32
            ts.Seconds |> should equal 0
        | _ -> failwith "Expected LocalTime"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse local time with fractional seconds`` () =
    let v = TomlValue.Parse "t = 07:32:00.999999"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.LocalTime ts -> ts.Hours |> should equal 7
        | _ -> failwith "Expected LocalTime"
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// Parsing: arrays
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse empty array`` () =
    let v = TomlValue.Parse "a = []"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Array arr -> arr.Length |> should equal 0
        | _ -> failwith "Expected Array"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse array of integers`` () =
    let v = TomlValue.Parse "a = [1, 2, 3]"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Array arr ->
            arr.Length |> should equal 3
            arr.[0] |> should equal (TomlValue.Integer 1L)
            arr.[2] |> should equal (TomlValue.Integer 3L)
        | _ -> failwith "Expected Array"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse array of strings`` () =
    let v = TomlValue.Parse """a = ["cat", "dog"]"""
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Array arr ->
            arr.[0] |> should equal (TomlValue.String "cat")
            arr.[1] |> should equal (TomlValue.String "dog")
        | _ -> failwith "Expected Array"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse array with trailing comma`` () =
    let v = TomlValue.Parse "a = [1, 2, 3,]"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Array arr -> arr.Length |> should equal 3
        | _ -> failwith "Expected Array"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse multiline array`` () =
    let toml = "a = [\n  1,\n  2,\n  3\n]"
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Array arr -> arr.Length |> should equal 3
        | _ -> failwith "Expected Array"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse nested array`` () =
    let v = TomlValue.Parse "a = [[1, 2], [3, 4]]"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Array arr ->
            arr.Length |> should equal 2
            match arr.[0] with
            | TomlValue.Array inner -> inner.Length |> should equal 2
            | _ -> failwith "Expected inner Array"
        | _ -> failwith "Expected Array"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse array with comment`` () =
    let toml = "a = [1, # comment\n2]"
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Array arr -> arr.Length |> should equal 2
        | _ -> failwith "Expected Array"
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// Parsing: inline tables
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse inline table`` () =
    let v = TomlValue.Parse """person = {name = "Alice", age = 30}"""
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Table inner ->
            inner.Length |> should equal 2
            snd inner.[0] |> should equal (TomlValue.String "Alice")
            snd inner.[1] |> should equal (TomlValue.Integer 30L)
        | _ -> failwith "Expected Table"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse empty inline table`` () =
    let v = TomlValue.Parse "x = {}"
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Table inner -> inner.Length |> should equal 0
        | _ -> failwith "Expected Table"
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// Parsing: table headers
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse table header`` () =
    let toml = "[owner]\nname = \"Alice\""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        props.Length |> should equal 1
        fst props.[0] |> should equal "owner"
        match snd props.[0] with
        | TomlValue.Table inner ->
            inner.Length |> should equal 1
            fst inner.[0] |> should equal "name"
        | _ -> failwith "Expected nested Table"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse multiple table headers`` () =
    let toml = "[a]\nx = 1\n\n[b]\ny = 2"
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        props.Length |> should equal 2
        fst props.[0] |> should equal "a"
        fst props.[1] |> should equal "b"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse dotted table header`` () =
    let toml = "[a.b.c]\nkey = 1"
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        props.Length |> should equal 1
        fst props.[0] |> should equal "a"
        match snd props.[0] with
        | TomlValue.Table a ->
            match snd a.[0] with
            | TomlValue.Table b ->
                match snd b.[0] with
                | TomlValue.Table c ->
                    fst c.[0] |> should equal "key"
                | _ -> failwith "Expected Table c"
            | _ -> failwith "Expected Table b"
        | _ -> failwith "Expected Table a"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse array-of-tables`` () =
    let toml = "[[products]]\nname = \"Hammer\"\n\n[[products]]\nname = \"Nail\""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        props.Length |> should equal 1
        fst props.[0] |> should equal "products"
        match snd props.[0] with
        | TomlValue.Array arr ->
            arr.Length |> should equal 2
            match arr.[0] with
            | TomlValue.Table inner ->
                snd inner.[0] |> should equal (TomlValue.String "Hammer")
            | _ -> failwith "Expected Table in array"
        | _ -> failwith "Expected Array"
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// Parsing: dotted keys
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse dotted key`` () =
    let toml = "a.b = 1"
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        fst props.[0] |> should equal "a"
        match snd props.[0] with
        | TomlValue.Table inner ->
            fst inner.[0] |> should equal "b"
            snd inner.[0] |> should equal (TomlValue.Integer 1L)
        | _ -> failwith "Expected nested Table"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse multi-level dotted key`` () =
    let toml = "a.b.c = \"deep\""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Table a ->
            match snd a.[0] with
            | TomlValue.Table b ->
                snd b.[0] |> should equal (TomlValue.String "deep")
            | _ -> failwith "Expected Table b"
        | _ -> failwith "Expected Table a"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse quoted key`` () =
    let toml = """" key with spaces" = 1"""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        fst props.[0] |> should equal " key with spaces"
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// Parsing: complex real-world documents
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse TOML spec example`` () =
    let toml = """
# This is a TOML document

title = "TOML Example"

[owner]
name = "Tom Preston-Werner"
dob = 1979-05-27T07:32:00+00:00

[database]
enabled = true
ports = [ 8000, 8001, 8002 ]
data = [ ["delta", "phi"], [3.14] ]
temp_targets = { cpu = 79.5, case = 72.0 }

[servers]

[servers.alpha]
ip = "10.0.0.1"
role = "frontend"

[servers.beta]
ip = "10.0.0.2"
role = "backend"
"""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        props |> Array.map fst |> should contain "title"
        props |> Array.map fst |> should contain "owner"
        props |> Array.map fst |> should contain "database"
        props |> Array.map fst |> should contain "servers"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse owner dob as OffsetDateTime`` () =
    let toml = """
[owner]
name = "Tom"
dob = 1979-05-27T07:32:00+00:00
"""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Table owner ->
            match snd owner.[1] with
            | TomlValue.OffsetDateTime dt ->
                dt.Year |> should equal 1979
            | _ -> failwith "Expected OffsetDateTime"
        | _ -> failwith "Expected owner Table"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse array of tables with sub-properties`` () =
    let toml = """
[[fruits]]
name = "apple"

[fruits.physical]
color = "red"
shape = "round"

[[fruits]]
name = "banana"
"""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        match snd props.[0] with
        | TomlValue.Array arr ->
            arr.Length |> should equal 2
            match arr.[0] with
            | TomlValue.Table t ->
                t |> Array.map fst |> should contain "name"
                t |> Array.map fst |> should contain "physical"
            | _ -> failwith "Expected Table"
        | _ -> failwith "Expected Array"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse document with mixed types`` () =
    let toml = """
str = "hello"
num = 42
flt = 3.14
flag = true
arr = [1, 2, 3]
"""
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        props.Length |> should equal 5
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// Parsing: error cases
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse duplicate key raises error`` () =
    let toml = "a = 1\na = 2"
    (fun () -> TomlValue.Parse toml |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``Parse duplicate table header raises error`` () =
    let toml = "[a]\n[a]"
    (fun () -> TomlValue.Parse toml |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``Parse unterminated basic string raises error`` () =
    let toml = """s = "unterminated"""
    (fun () -> TomlValue.Parse toml |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``Parse unterminated array raises error`` () =
    let toml = "a = [1, 2"
    (fun () -> TomlValue.Parse toml |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``Parse invalid escape in basic string raises error`` () =
    let toml = "s = \"\\z\""
    (fun () -> TomlValue.Parse toml |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``TryParse returns Some on valid input`` () =
    let result = TomlValue.TryParse "x = 1"
    result |> should not' (equal None)

[<Test>]
let ``TryParse returns None on invalid input`` () =
    let result = TomlValue.TryParse "= invalid"
    result |> should equal None

// --------------------------------------------------------------------------------------
// Parsing: key formats
// --------------------------------------------------------------------------------------

[<Test>]
let ``Parse key with dash`` () =
    let v = TomlValue.Parse "my-key = 1"
    match v with
    | TomlValue.Table props -> fst props.[0] |> should equal "my-key"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse key with underscore`` () =
    let v = TomlValue.Parse "my_key = 1"
    match v with
    | TomlValue.Table props -> fst props.[0] |> should equal "my_key"
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse multiple key-value pairs`` () =
    let toml = "a = 1\nb = 2\nc = 3"
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        props.Length |> should equal 3
    | _ -> failwith "Expected Table"

[<Test>]
let ``Parse key-value pairs with inline comments`` () =
    let toml = "a = 1 # comment\nb = 2 # another comment"
    let v = TomlValue.Parse toml
    match v with
    | TomlValue.Table props ->
        props.Length |> should equal 2
        snd props.[0] |> should equal (TomlValue.Integer 1L)
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// Parsing: Load from stream / reader
// --------------------------------------------------------------------------------------

[<Test>]
let ``Load from TextReader`` () =
    use reader = new System.IO.StringReader("x = 42")
    let v = TomlValue.Load(reader)
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Integer 42L)
    | _ -> failwith "Expected Table"

[<Test>]
let ``Load from Stream`` () =
    let bytes = System.Text.Encoding.UTF8.GetBytes("x = 42")
    use stream = new System.IO.MemoryStream(bytes)
    let v = TomlValue.Load(stream)
    match v with
    | TomlValue.Table props ->
        snd props.[0] |> should equal (TomlValue.Integer 42L)
    | _ -> failwith "Expected Table"

// --------------------------------------------------------------------------------------
// TomlDocument
// --------------------------------------------------------------------------------------

[<Test>]
let ``TomlDocument Create from TomlValue`` () =
    let toml = TomlValue.Parse "x = 1"
    let doc = FSharp.Data.Runtime.BaseTypes.TomlDocument.Create(toml, "")
    doc |> should not' (equal null)

[<Test>]
let ``TomlDocument Create from TextReader`` () =
    use reader = new System.IO.StringReader("x = 42")
    let doc = FSharp.Data.Runtime.BaseTypes.TomlDocument.Create(reader)
    doc |> should not' (equal null)

[<Test>]
let ``TomlDocument JsonValue is valid JSON record`` () =
    let toml = TomlValue.Parse "x = 1\ny = 2"
    let doc = FSharp.Data.Runtime.BaseTypes.TomlDocument.Create(toml, "")
    match doc.JsonValue with
    | JsonValue.Record props -> props.Length |> should equal 2
    | _ -> failwith "Expected JSON record"

// --------------------------------------------------------------------------------------
// _Print formatting
// --------------------------------------------------------------------------------------

[<Test>]
let ``_Print formats String`` () =
    let v = TomlValue.String "hello"
    v._Print |> should contain "hello"

[<Test>]
let ``_Print formats Integer`` () =
    let v = TomlValue.Integer 42L
    v._Print |> should equal "42"

[<Test>]
let ``_Print formats Float`` () =
    let v = TomlValue.Float 3.14
    v._Print |> should contain "3.14"

[<Test>]
let ``_Print formats Boolean`` () =
    (TomlValue.Boolean true)._Print |> should equal "true"
    (TomlValue.Boolean false)._Print |> should equal "false"

[<Test>]
let ``_Print formats Array`` () =
    let v = TomlValue.Array [| TomlValue.Integer 1L; TomlValue.Integer 2L |]
    v._Print |> should contain "2 items"

[<Test>]
let ``_Print formats Table`` () =
    let v = TomlValue.Table [| "k", TomlValue.Integer 1L |]
    v._Print |> should contain "1 properties"

[<Test>]
let ``_Print formats LocalTime`` () =
    let v = TomlValue.LocalTime(TimeSpan(9, 5, 7))
    v._Print |> should equal "09:05:07"

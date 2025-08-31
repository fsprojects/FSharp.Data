/// Tools for generating nice member names that follow F# & .NET naming conventions
module FSharp.Data.Runtime.NameUtils

open System
open System.Collections.Generic
open FSharp.Data.Runtime

// --------------------------------------------------------------------------------------
// Active patterns & operators for parsing strings

let private tryAt (s: string) i =
    if i >= s.Length then ValueNone else ValueSome s.[i]

let private sat f (c: voption<char>) =
    match c with
    | ValueSome c when f c -> ValueSome c
    | _ -> ValueNone

[<return: Struct>]
let private (|EOF|_|) c =
    match c with
    | ValueSome _ -> ValueNone
    | _ -> ValueSome()

[<return: Struct>]
let private (|LetterDigit|_|) = sat Char.IsLetterOrDigit

[<return: Struct>]
let private (|Upper|_|) = sat (fun c -> Char.IsUpper c || Char.IsDigit c)

[<return: Struct>]
let private (|Lower|_|) = sat (fun c -> Char.IsLower c || Char.IsDigit c)


// --------------------------------------------------------------------------------------

/// Turns a given non-empty string into a nice 'PascalCase' identifier
let nicePascalName (s: string) =
    if s.Length = 1 then
        s.ToUpperInvariant()
    else
        // Starting to parse a new segment
        let rec restart i =
            match tryAt s i with
            | EOF -> Seq.empty
            | LetterDigit _ & Upper _ -> upperStart i (i + 1)
            | LetterDigit _ -> consume i false (i + 1)
            | _ -> restart (i + 1)
        // Parsed first upper case letter, continue either all lower or all upper
        and upperStart from i =
            match tryAt s i with
            | Upper _ -> consume from true (i + 1)
            | Lower _ -> consume from false (i + 1)
            | _ ->
                seq {
                    yield struct (from, i)
                    yield! restart (i + 1)
                }
        // Consume are letters of the same kind (either all lower or all upper)
        and consume from takeUpper i =
            match takeUpper, tryAt s i with
            | false, Lower _ -> consume from takeUpper (i + 1)
            | true, Upper _ -> consume from takeUpper (i + 1)
            | true, Lower _ ->
                seq {
                    yield struct (from, (i - 1))
                    yield! restart (i - 1)
                }
            | _ ->
                seq {
                    yield struct (from, i)
                    yield! restart i
                }

        // Split string into segments and turn them to PascalCase
        seq {
            for i1, i2 in restart 0 do
                let sub = s.Substring(i1, i2 - i1)

                if Array.forall Char.IsLetterOrDigit (sub.ToCharArray()) then
                    // Optimized: Use StringBuilder to avoid intermediate string allocations
                    let sb = Text.StringBuilder(sub.Length)
                    sb.Append(Char.ToUpperInvariant(sub.[0])) |> ignore

                    if sub.Length > 1 then
                        sb.Append(sub.ToLowerInvariant().Substring(1)) |> ignore

                    yield sb.ToString()
        }
        |> String.Concat

/// Turns a given non-empty string into a nice 'camelCase' identifier
let niceCamelName (s: string) =
    let name = nicePascalName s

    if name.Length > 0 then
        // Optimized: Use StringBuilder to avoid intermediate string allocations
        let sb = Text.StringBuilder(name.Length)
        sb.Append(Char.ToLowerInvariant(name.[0])) |> ignore

        if name.Length > 1 then
            sb.Append(name.Substring(1)) |> ignore

        sb.ToString()
    else
        name

/// Given a function to format names (such as 'niceCamelName' or 'nicePascalName')
/// returns a name generator that never returns duplicate name (by appending an
/// index to already used names)
///
/// This function is curried and should be used with partial function application:
///
///     let makeUnique = uniqueGenerator nicePascalName
///     let n1 = makeUnique "sample-name"
///     let n2 = makeUnique "sample-name"
///
let uniqueGenerator (niceName: string -> string) =
    let set = new HashSet<_>()

    fun name ->
        let mutable name = niceName name

        if name.Length = 0 then
            name <- "Unnamed"

        while set.Contains name do
            let mutable lastLetterPos = String.length name - 1

            while Char.IsDigit name.[lastLetterPos] && lastLetterPos > 0 do
                lastLetterPos <- lastLetterPos - 1

            if lastLetterPos = name.Length - 1 then
                // Optimized: Use StringBuilder to avoid string concatenation overhead
                let sb = Text.StringBuilder(name.Length + 2)
                sb.Append(name) |> ignore

                if name.Contains " " then
                    sb.Append(" 2") |> ignore
                else
                    sb.Append("2") |> ignore

                name <- sb.ToString()
            elif lastLetterPos = 0 && name.Length = 1 then
                name <- (UInt64.Parse name + 1UL).ToString()
            else
                let number = name.Substring(lastLetterPos + 1)
                // Optimized: Use StringBuilder to avoid string concatenation overhead
                let sb = Text.StringBuilder(name.Length + 4)
                sb.Append(name.Substring(0, lastLetterPos + 1)) |> ignore
                sb.Append((UInt64.Parse number + 1UL).ToString()) |> ignore
                name <- sb.ToString()

        set.Add name |> ignore
        name

let capitalizeFirstLetter (s: string) =
    match s.Length with
    | 0 -> ""
    | 1 -> (Char.ToUpperInvariant s.[0]).ToString()
    | _ ->
        // Optimized: Use StringBuilder to avoid string concatenation overhead
        let sb = Text.StringBuilder(s.Length)
        sb.Append(Char.ToUpperInvariant(s.[0])) |> ignore
        sb.Append(s.Substring(1)) |> ignore
        sb.ToString()

/// Trim HTML tags from a given string and replace all of them with spaces
/// Multiple tags are replaced with just a single space. (This is a recursive
/// implementation that is somewhat faster than regular expression.)
let trimHtml (s: string) =
    // Optimized: Avoid ToCharArray() allocation - work directly with string indexing
    let res = new Text.StringBuilder(s.Length)

    // Loop and keep track of whether we're inside a tag or not
    let rec loop i emitSpace inside =
        if i >= s.Length then
            ()
        else
            let c = s.[i]

            match inside, c with
            | true, '>' -> loop (i + 1) false false
            | false, '<' ->
                if emitSpace then
                    res.Append(' ') |> ignore

                loop (i + 1) false true
            | _ ->
                if not inside then
                    res.Append(c) |> ignore

                loop (i + 1) true inside

    loop 0 false false
    res.ToString().TrimEnd()

/// Return the plural of an English word
let pluralize s = Pluralizer.toPlural s

/// Return the singular of an English word
let singularize s = Pluralizer.toSingular s

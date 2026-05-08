/// Tools for generating nice member names that follow F# & .NET naming conventions
module FSharp.Data.Runtime.NameUtils

open System
open System.Collections.Generic
open FSharp.Data.Runtime

// --------------------------------------------------------------------------------------

/// Turns a given non-empty string into a nice 'PascalCase' identifier
let nicePascalName (s: string) =
    if s.Length = 1 then
        s.ToUpperInvariant()
    else
        let sb = Text.StringBuilder(s.Length)

        // Append s.[from..i-1] as a PascalCase segment: first char upper, rest lower.
        let appendSegment (from: int) (i: int) =
            if i > from then
                sb.Append(Char.ToUpperInvariant(s.[from])) |> ignore

                for k in from + 1 .. i - 1 do
                    sb.Append(Char.ToLowerInvariant(s.[k])) |> ignore

        // Starting to parse a new segment
        let rec restart i =
            if i < s.Length then
                if Char.IsLetterOrDigit(s.[i]) then
                    if Char.IsUpper(s.[i]) || Char.IsDigit(s.[i]) then
                        upperStart i (i + 1)
                    else
                        consume i false (i + 1)
                else
                    restart (i + 1)

        // Parsed first upper-case/digit letter; continue either all-lower or all-upper
        and upperStart from i =
            if i >= s.Length then
                appendSegment from i
            elif Char.IsUpper(s.[i]) || Char.IsDigit(s.[i]) then
                consume from true (i + 1)
            elif Char.IsLower(s.[i]) then
                consume from false (i + 1)
            else
                appendSegment from i
                restart (i + 1)

        // Consume letters of the same kind (all-lower or all-upper/digit)
        and consume from takeUpper i =
            if i >= s.Length then
                appendSegment from i
            elif not takeUpper && (Char.IsLower(s.[i]) || Char.IsDigit(s.[i])) then
                consume from false (i + 1)
            elif takeUpper && (Char.IsUpper(s.[i]) || Char.IsDigit(s.[i])) then
                consume from true (i + 1)
            elif takeUpper && Char.IsLower(s.[i]) then
                appendSegment from (i - 1)
                restart (i - 1)
            else
                appendSegment from i
                restart i

        restart 0
        sb.ToString()

/// Turns a given non-empty string into a nice 'camelCase' identifier
let niceCamelName (s: string) =
    let name = nicePascalName s

    if name.Length = 0 || Char.IsLower(name.[0]) then
        // Fast path: already starts with a lower-case letter — no allocation needed.
        name
    else
        // Lower-case the first character and append the remainder.
        let sb = Text.StringBuilder(name.Length)
        sb.Append(Char.ToLowerInvariant(name.[0])) |> ignore
        sb.Append(name, 1, name.Length - 1) |> ignore
        sb.ToString()

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
                if name.IndexOf(' ') >= 0 then
                    name <- name + " 2"
                else
                    name <- name + "2"
            elif lastLetterPos = 0 && name.Length = 1 then
                name <- (UInt64.Parse name + 1UL).ToString()
            else
                let number = name.Substring(lastLetterPos + 1)

                name <- name.Substring(0, lastLetterPos + 1) + (UInt64.Parse number + 1UL).ToString()

        set.Add name |> ignore
        name

let capitalizeFirstLetter (s: string) =
    match s.Length with
    | 0 -> ""
    | _ when Char.IsUpper(s.[0]) ->
        // Fast path: already starts with an upper-case letter — no allocation needed.
        s
    | 1 -> (Char.ToUpperInvariant s.[0]).ToString()
    | _ ->
        // Upper-case the first character and append the remainder via StringBuilder
        // to avoid creating two intermediate strings.
        let sb = Text.StringBuilder(s.Length)
        sb.Append(Char.ToUpperInvariant(s.[0])) |> ignore
        sb.Append(s, 1, s.Length - 1) |> ignore
        sb.ToString()

/// Trim HTML tags from a given string and replace all of them with spaces
/// Multiple tags are replaced with just a single space. (This is a recursive
/// implementation that is somewhat faster than regular expression.)
let trimHtml (s: string) =
    let res = new Text.StringBuilder()

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

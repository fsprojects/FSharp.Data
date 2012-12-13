#r "System.Web.dll"
#r "../packages/FSharp.Formatting.1.0.1/lib/net40/FSharp.CodeFormat.dll"
#r "../packages/FSharp.Formatting.1.0.1/lib/net40/FSharp.Markdown.dll"

open System
open System.IO
open System.Web
open System.Reflection
open System.Collections.Generic

open FSharp.Patterns
open FSharp.CodeFormat
open FSharp.Markdown

// --------------------------------------------------------------------------------------
module internal LiterateUtils =
  type Block = 
    | BlockComment of string
    | BlockSnippet of Line list 

  let trimBlanksAndReverse lines = 
    lines 
    |> Seq.skipWhile (function Line[] -> true | _ -> false)
    |> List.ofSeq |> List.rev
    |> Seq.skipWhile (function Line[] -> true | _ -> false)
    |> List.ofSeq

  let (|ConcatenatedComments|_|) tokens = 
    let comments =
      tokens |> List.choose (function
        | Token(TokenKind.Comment, text, _) -> Some text
        | _ -> None)
    if comments.Length <> tokens.Length then None
    else Some (String.concat "" comments)
  
  let rec collectComment comment lines = seq {
    match lines with
    | (Line(ConcatenatedComments text))::lines ->
        yield! collectComment (comment + "\n" + text) lines
    | lines ->
        let cend = comment.LastIndexOf("*)")
        yield BlockComment (comment.Substring(0, cend))
        if lines <> [] then yield! collectSnippet [] lines }
  and collectSnippet acc lines = seq {
    match lines with 
    | (Line[Token(TokenKind.Comment, text, _)])::lines when text.StartsWith("(**") ->
        if acc <> [] then yield BlockSnippet (trimBlanksAndReverse acc)
        yield! collectComment (text.Substring(3)) lines
    | x::xs -> 
        yield! collectSnippet (x::acc) xs
    | [] -> 
        yield BlockSnippet (trimBlanksAndReverse acc) }

// --------------------------------------------------------------------------------------

open LiterateUtils

let transform template sources output =
  let formatAgent = CodeFormat.CreateAgent(Assembly.Load("FSharp.Compiler"))
  for file in Directory.GetFiles(sources, "*.fsx") do
    // let file = @"C:\Tomas\Projects\FSharp.Data\tools\../samples\JsonProvider.fsx"
    let name = Path.GetFileNameWithoutExtension(file)
    let output = Path.Combine(output, name + ".html")
    let snippets, _ = formatAgent.ParseSource(file, File.ReadAllText(file))
    let (Snippet(_, lines)) = match snippets with [| it |] -> it | _ -> failwith "multiple snippets"
  
    let blocks = collectSnippet [] lines |> List.ofSeq
    let snippets = 
      blocks |> List.choose (function
        | BlockSnippet(lines) -> Some(Snippet("Untitled", lines))
        | _ -> None) |> Array.ofList
    let formatted = CodeFormat.FormatHtml(snippets, "fstip")

    let heading = ref None
    let mutable snippet = 0
    let sb = Text.StringBuilder()

    for block in blocks do
      match block with
      | BlockComment s -> 
          let mdoc = Markdown.Parse(s)
          mdoc.Paragraphs |> Seq.iter (function 
            | (Heading(1, text)) -> 
                heading := Some(Markdown.WriteHtml(MarkdownDocument([Span(text)], dict [])))
            | _ -> ())
          sb.Append(Markdown.WriteHtml(mdoc)) |> ignore
      | BlockSnippet _ ->
          sb.Append(formatted.SnippetsHtml.[snippet].Html) |> ignore
          snippet <- snippet + 1

    let heading = match !heading with None -> name | Some h -> h.Trim()
    let html = String.Format(template, heading, sb.ToString(), formatted.ToolTipHtml)
    File.WriteAllText(output, html)
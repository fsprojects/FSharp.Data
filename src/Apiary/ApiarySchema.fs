﻿// --------------------------------------------------------------------------------------
// Apiary type provider - schema extraction (from the JSON blueprint API)
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.IO
open System.Net
open System.Reflection
open System.Collections.Generic
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Data.Runtime.Caching

/// Discriminated union that distinguish between function nodes 
/// (with info & name, but no subtrees) modules (with name & sub-elements)
/// and entities (that are accessible via parameter)
type RestApi<'T> = 
  | Module of string * list<RestApi<'T>>
  | Function of string * 'T
  | Entity of string * list<'T> * list<RestApi<'T>>

module ApiarySchema =
  let download = 
    // Cache for storing downloaded specifications
    let cache, _ = createInternetFileCache "ApiarySchema" (TimeSpan.FromMinutes 30.0)
    fun uri ->
      match cache.TryRetrieve(uri) with
      | Some html -> html
      | None -> 
          let html = Http.RequestString(uri)
          cache.Set(uri, html)
          html
  
  let downloadSpecification apiName meth path = 
    let path = Uri.EscapeDataString(path)
    let uri = sprintf "http://api.apiary.io/blueprint/snippet/%s/%s/%s" apiName meth path
    JsonValue.Parse (download uri)
  
  /// Download a list of operations with their method & relative URL
  let private downloadOperations name =
    let uri = sprintf "http://api.apiary.io/blueprint/resources/%s" name
    let doc = JsonValue.Parse(download uri)
    [ for r in doc?resources -> r?``method``.AsString(), r?url.AsString() ]

  /// Split path into components separated by slash
  let getPathComponents (path:string) = 
    path.Split([|'/'|], StringSplitOptions.RemoveEmptyEntries) |> List.ofSeq

  /// Represents a tree with module/function names
  /// Every node has a name, list of values under the node
  /// and a list of subtrees (other modules/functions)
  type NameTree<'T> = 
    | NameNode of string * list<'T> * list<NameTree<'T>>

  /// Builds a name tree from items that have lists of names
  /// (items with the same prefixes end up in the same subtree)
  let rec private buildTree name items = 
    let empty, nonempty = items |> List.partition (fst >> List.isEmpty)
    let members = empty |> List.map snd

    let groups = nonempty |> Seq.groupBy (fst >> List.head)
    let nested = 
      [ for name, group in groups -> 
          let suffix = 
            [ for names, value in group ->
                List.tail names, value ]
          buildTree name suffix ]
    NameNode(name, members, nested)

  /// Removes all root elements that do not have any elements
  /// (i.e. if everything is nested under some common prefix)
  let rec private trimRoot modul =
    match modul with 
    | NameNode(_, [], [modul]) -> trimRoot modul
    | NameNode(_, [], moduls) -> moduls
    | _ -> [modul]

  /// Get a tree structure with operation/module names from Apiary
  let getOperationTree name = 
    let operations = downloadOperations name
    let operationsWithPath = 
      [ for meth, path in operations -> 
          getPathComponents path, (meth, path) ]
    operationsWithPath |> buildTree "root" |> trimRoot

  let isSpecialName (specialNames:Map<string, string>) (s:string) = 
    s.StartsWith(":") || (s.StartsWith("{") && s.EndsWith("}")) || specialNames.ContainsKey s

  /// Is there any specially named child node?
  let (|SpeciallyNamed|_|) specialNames items = 
    let special, other = items |> List.partition (fun (NameNode(name, _, _)) -> isSpecialName specialNames name)
    match special with
    | spec::rest -> Some(spec, rest @ other)
    | _ -> None

  /// Convert name tree obtained from Apiary to our view of REST API
  let asRestApi specialNames nodes =
    let rec loop (parentName:Lazy<string>) = function
      | NameNode(name, [info], []) -> 
          Some <| Function(name, info)
      | NameNode(name, [], nested) ->
          Some <| Module(name, List.choose (loop (lazy name)) nested)

      // Detect pattern when we have:
      //
      //   /foo     (GET)
      //   /foo     (POST)
      //
      | NameNode(name, ops, []) ->
          Some <| Entity(name, ops, [])

      // Detect pattern when we have:
      //
      //  /foo      (GET  - list foos)
      //  /foo      (POST - add foo)
      //  /foo/{id} (GET  - get foo {id})
      //  /foo/{id} (PUT  - update foo {id})
      //
      | NameNode(name, ops, SpeciallyNamed specialNames (NameNode(_, specialOps, specialNested), nested)) ->
          // Find the operation for listing
          let get, others = ops |> List.partition (fun (meth, _) -> 
            String.Compare(meth, "get", StringComparison.InvariantCultureIgnoreCase) = 0)
          let list = get |> List.map (fun info -> Function("List", info))

          // Create module with nested entity and 'list' operation
          let entity = Entity(name, others @ specialOps, nested @ specialNested |> List.choose (loop (lazy name)))
          Some <| Module(name, entity :: list)

      // Detect pattern when we have:
      // 
      //   /foo/{id}
      //   /foo/{id}/bar
      //
      | NameNode(name, ops, nested) when isSpecialName specialNames name ->
          Some <| Entity(parentName.Value, ops, List.choose (loop parentName) nested)

      | NameNode _ -> None

    nodes |> List.choose (loop (lazy failwith "Missing root element"))

[<AutoOpen>]
module ApiarySchemaAuto = 
  open ApiarySchema

  /// Find an operation that uses HTTP method specified by 'findMeth'
  /// and return the arguments together with (the method and) a path
  let (|FindMethod|_|) specialNames findMeth (ops:list<string * string>) =
    ops |> Seq.tryFind (fun (meth, _) -> String.Compare(meth, findMeth, StringComparison.OrdinalIgnoreCase) = 0)
        |> Option.map (fun (meth, path) ->
             let pathParts = path |> getPathComponents 
             let args = pathParts |> List.filter (isSpecialName specialNames)
             args, (meth, path))

  let (|NormalizedFunction|_|) (specialNames:Map<string,string>) = function
    | Function(name:string, (meth, path:string)) ->
        let name, args, servicePath =
          match path.IndexOf "{?", path.IndexOf "}" with
          | x, y when x > 0 && x < y-2 -> 
              let name = name.Replace(path.Substring(x), "")
              let args = path.Substring(x+2, y-x-2).Split(',') |> List.ofArray
              let argsWithBraces = [ for arg in args -> "{" + arg + "}" ]
              let servicePath = Http.AppendQueryToUrl(path.Substring(0, x), List.zip args argsWithBraces, true)
              name, argsWithBraces, servicePath
          | _, _ -> 
              let segments = 
                path.Split('/')
                |> Array.map (fun segment -> match specialNames.TryFind segment with
                                             | Some arg -> "{" + arg + "}", ["{" + arg + "}"]
                                             | None -> segment, [])
              let servicePath = segments |> Array.map fst |> String.concat "/"
              let args = segments |> Array.map snd |> Array.reduce (@)
              let name = 
                if specialNames.TryFind name |> Option.isSome
                then segments |> Array.filter (snd >> (=) []) |> Seq.last |> fst
                else name
              name, args, servicePath
        Some (name, meth, args, servicePath, path)
    | _ -> None

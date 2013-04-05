﻿// --------------------------------------------------------------------------------------
// CSV type provider (inference engine and code generator)
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.ProviderFileSystem

// --------------------------------------------------------------------------------------

[<TypeProvider>]
type public CsvProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.CsvProvider'
  let asm, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let csvProvTy = ProvidedTypeDefinition(asm, ns, "CsvProvider", Some(typeof<obj>))

  let buildTypes (typeName:string) (args:obj[]) =

    // Generate the required type
    let resTy = ProvidedTypeDefinition(asm, ns, typeName, Some(replacer.ToRuntime typeof<CsvFile>), HideObjectMethods = true)

    let sample = args.[0] :?> string
    let separator = args.[1] :?> string
    let culture = args.[2] :?> string
    let cultureInfo = Operations.GetCulture culture
    let inferRows = args.[3] :?> int
    let schema = args.[4] :?> string
    let hasHeaders = args.[5] :?> bool
    let ignoreErrors = args.[6] :?> bool
    let quote = args.[7] :?> char
    let resolutionFolder = args.[8] :?> string
    let isHostedExecution = cfg.IsHostedExecution
    let defaultResolutionFolder = cfg.ResolutionFolder

    // Infer the schema from a specified uri or inline text
    let sampleCsv, sampleIsUri = 
      try
        match ProviderHelpers.tryGetUri sample with
        | Some uri ->
            let reader = ProviderHelpers.readTextAtDesignTime defaultResolutionFolder this.Invalidate resolutionFolder uri
            new CsvFile(reader, separator, quote, hasHeaders, ignoreErrors), true
        | None ->
            new CsvFile(new StringReader(sample), separator, quote, hasHeaders, ignoreErrors), false
      with e ->
        failwithf "Specified argument is neither a file, nor well-formed CSV: %s" e.Message

    let rowType = ProvidedTypeDefinition("Row", Some(replacer.ToRuntime typeof<CsvRow>), HideObjectMethods = true)
    rowType.AddMembersDelayed(fun () ->
      use sampleCsv = sampleCsv
      let inferedFields = CsvInference.inferType sampleCsv inferRows cultureInfo schema ||> CsvInference.getFields
      CsvTypeBuilder.generateCsvRowProperties culture replacer inferedFields)
    resTy.AddMember rowType

    let (|Singleton|) = function Singleton s -> replacer.ToDesignTime s

    // 'Data' property has the generated type
    let p = ProvidedProperty("Data", typedefof<seq<_>>.MakeGenericType[| rowType :> Type |])
    p.GetterCode <- fun (Singleton self) -> replacer.ToRuntime <@@ (%%self : CsvFile).Data @@>
    resTy.AddMember p
    
    // Generate default constructor
    let c = ProvidedConstructor []
    c.InvokeCode <- 
      if sampleIsUri then
        fun _ -> replacer.ToRuntime <@@ let reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder sample
                                        new CsvFile(reader, separator, quote, hasHeaders, ignoreErrors) @@>
      else
        fun _ -> replacer.ToRuntime <@@ new CsvFile(new StringReader(sample), separator, quote, hasHeaders, ignoreErrors) @@>            
    resTy.AddMember c

    // Generate static Parse method
    let args = [ ProvidedParameter("text", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, resTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton text) -> replacer.ToRuntime <@@ new CsvFile(new StringReader(%%text:string), separator, quote, hasHeaders, ignoreErrors) @@>
    resTy.AddMember m

    // Generate static Load stream method
    let args = [ ProvidedParameter("stream", typeof<Stream>) ]
    let m = ProvidedMethod("Load", args, resTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton stream) -> replacer.ToRuntime <@@ new CsvFile(new StreamReader(%%stream:Stream), separator, quote, hasHeaders, ignoreErrors) @@>
    resTy.AddMember m

    // Generate static Load uri method
    let args = [ ProvidedParameter("uri", typeof<string>) ]
    let m = ProvidedMethod("Load", args, resTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton uri) -> replacer.ToRuntime <@@ let reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder %%uri
                                                                  new CsvFile(reader, separator, quote, hasHeaders, ignoreErrors) @@>
    resTy.AddMember m

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>) 
      ProvidedStaticParameter("Separator", typeof<string>, parameterDefaultValue = ",") 
      ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("InferRows", typeof<int>, parameterDefaultValue = 1000)
      ProvidedStaticParameter("Schema", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("HasHeaders", typeof<bool>, parameterDefaultValue = true)
      ProvidedStaticParameter("IgnoreErrors", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("Quote", typeof<char>, parameterDefaultValue = '"')
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a CSV file</summary>
       <param name='Sample'>Location of a CSV sample file or a string containing a sample CSV document</param>
       <param name='Separator'>Column delimiter. Defaults to ","</param>
       <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture</param>
       <param name='InferRows'>Number of rows to use for inference. Defaults to 1000. If this is zero, all rows are used</param>
       <param name='Schema'>Optional column types, in a comma separated list. Valid types are "int", "int64", "bool", "float", "decimal", "date", "string", "int?", "int64?", "bool?", "float?", "decimal?", "date?", "int option", "int64 option", "bool option", "float option", "decimal option", and "date option". You can also specify a unit and the name of the column like this: Name (type<unit>)</param>
       <param name='HasHeaders'>Whether the sample contains the names of the columns as its first line</param>
       <param name='IgnoreErrors'>Whether to ignore rows that have the wrong number of columns. Otherwise an exception is thrown when these rows are encountered</param>
       <param name='Quote'>The quotation mark (for surrounding values containing the delimiter). Defaults to '"'</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do csvProvTy.AddXmlDoc helpText
  do csvProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ csvProvTy ])
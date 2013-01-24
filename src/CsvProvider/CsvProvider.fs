// --------------------------------------------------------------------------------------
// CSV type provider (inference engine and code generator)
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.DataLoading
open FSharp.Data.RuntimeImplementation.TypeInference

// --------------------------------------------------------------------------------------
// Inference
// --------------------------------------------------------------------------------------

module CsvInference = 
  /// Infers the type of a CSV file using the specified number of rows
  /// (This handles units in the same way as the original MiniCSV provider)
  let inferType (csv:CsvFile) count =

    // Infer the units and names from the headers
    let headers = csv.Headers |> Seq.map (fun header ->
      let m = Regex.Match(header, @"(?<field>.+) \((?<unit>.+)\)")
      if m.Success then
        let headerName = m.Groups.["field"].Value
        let unitName = m.Groups.["unit"].Value
        Some(ProvidedMeasureBuilder.Default.SI unitName), headerName
      else None, header)

    // Infer the type of collection using structural inference
    Seq.reduce subtypeInfered
     (seq { for row in Seq.takeMax count csv.Data ->
              let fields = 
                [ for (unit, header), value in Seq.zip headers row.Columns ->
                    let typ = inferPrimitiveType value unit
                    { Name = header; Optional = false; Type = typ } ]
              Record(None, fields) })

// --------------------------------------------------------------------------------------

module CsvTypeBuilder =

    let generateCsvRowType culture (replacer:AssemblyReplacer) (parentType:ProvidedTypeDefinition) = function
        | Record(_, fields) ->
            let objectTy = ProvidedTypeDefinition("Row", Some(replacer.ToRuntime typeof<CsvRow>), HideObjectMethods = true)
            parentType.AddMember objectTy

            for index, field in fields |> Seq.mapi (fun i v -> i, v) do
              let baseTyp, propTyp =
                match field.Type with
                | Primitive(typ, Some unit) -> 
                    typ, ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit])
                | Primitive(typ, None) -> typ, typ
                | _ -> typeof<string>, typeof<string>

              let p = ProvidedProperty(NameUtils.nicePascalName field.Name, propTyp)
              let _, conv = Conversions.convertValue culture field.Name false baseTyp replacer
              p.GetterCode <- fun (Singleton row) -> let row = replacer.ToDesignTime row in conv <@@ Some((%%row:CsvRow).Columns.[index]) @@>
              objectTy.AddMember p

            objectTy
        | _ -> failwith "generateCsvRowType: Type inference returned wrong type"

// --------------------------------------------------------------------------------------

[<TypeProvider>]
type public CsvProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.CsvProvider'
  let asm, isPortable, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let csvProvTy = ProvidedTypeDefinition(asm, ns, "CsvProvider", Some(typeof<obj>))

  let buildTypes (typeName:string) (args:obj[]) =

    // Generate the required type
    let resTy = ProvidedTypeDefinition(asm, ns, typeName, Some(replacer.ToRuntime typeof<CsvFile>), HideObjectMethods = true)

    let sample = args.[0] :?> string
    let separator = args.[1] :?> string
    let culture = args.[2] :?> string
    let inferRows = args.[3] :?> int
    let resolutionFolder = args.[4] :?> string
    let isHostedExecution = cfg.IsHostedExecution
    let defaultResolutionFolder = cfg.ResolutionFolder

    // Infer the schema from a specified file or URI sample
    let sampleCsv, sampleIsUri = 
      try
        let reader = ProviderHelpers.readTextAtDesignTime defaultResolutionFolder this.Invalidate resolutionFolder sample
        new CsvFile(reader, separator), true
      with _ ->
        new CsvFile(new StringReader(sample), separator), false

    use sampleCsv = sampleCsv

    let inferedType = CsvInference.inferType sampleCsv inferRows
    let rowType = CsvTypeBuilder.generateCsvRowType culture replacer resTy inferedType

    let (|Singleton|) = function Singleton s -> replacer.ToDesignTime s

    // 'Data' property has the generated type
    let p = ProvidedProperty("Data", typedefof<seq<_>>.MakeGenericType[| rowType :> Type |])
    p.GetterCode <- fun (Singleton self) -> replacer.ToRuntime <@@ (%%self : CsvFile).Data @@>
    resTy.AddMember p
    
    // Generate default constructor
    let c = ProvidedConstructor []
    c.InvokeCode <- 
        if sampleIsUri then
            //TODO: if portable library and not a web location, don't generate this constructor, as it will throw at runtime
            fun _ -> replacer.ToRuntime <@@ let reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder sample
                                            new CsvFile(reader, separator) @@>
        else
            fun _ -> replacer.ToRuntime <@@ new CsvFile(new StringReader(sample), separator) @@>            
    resTy.AddMember c

    // Generate static Parse method
    let args = [ ProvidedParameter("text", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, resTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton text) -> replacer.ToRuntime <@@ new CsvFile(new StringReader(%%text:string), separator) @@>
    resTy.AddMember m

    // Generate static Load stream method
    let args = [ ProvidedParameter("stream", typeof<Stream>) ]
    let m = ProvidedMethod("Load", args, resTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton stream) -> replacer.ToRuntime <@@ new CsvFile(new StreamReader(%%stream:Stream), separator) @@>
    resTy.AddMember m

    // Generate static Load uri method
    let args = [ ProvidedParameter("uri", typeof<string>) ]
    let m = ProvidedMethod("Load", args, resTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton uri) -> replacer.ToRuntime <@@ let reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder %%uri
                                                                  new CsvFile(reader, separator) @@>
    resTy.AddMember m

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>) 
      ProvidedStaticParameter("Separator", typeof<string>, parameterDefaultValue = ",") 
      ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("InferRows", typeof<int>, parameterDefaultValue = Int32.MaxValue)
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a CSV file</summary>
       <param name='Sample'>Location of a CSV sample file or a string containing a sample CSV document</param>
       <param name='Separator'>Column delimiter</param>                     
       <param name='Culture'>The culture used for parsing numbers and dates.</param>                     
       <param name='InferRows'>Number of rows to use for inference. If this is zero (the default), all rows are used.</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do csvProvTy.AddXmlDoc helpText
  do csvProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ csvProvTy ])
// --------------------------------------------------------------------------------------
// HTML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Text
open System.IO
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open FSharp.Data.Runtime
open FSharp.Net
      

[<TypeProvider>]
type public HtmlProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.Experimental.HtmlProvider'
  let asm, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data.Experimental"
  let htmlProvTy = ProvidedTypeDefinition(asm, ns, "HtmlProvider", Some typeof<obj>)
  
  let buildTypes (typeName:string) (args:obj[]) =
      
      let sample = args.[0] :?> string
      //let resolutionFolder = args.[1] :?> string
      //TODO: Sample currently assumed to be a url 
      let generatedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
      let tableContainer = ProvidedTypeDefinition("Tables", Some typeof<obj>)
      let body =
        match Uri.TryCreate(sample,UriKind.Absolute) with
        | true, uri ->
            let response = FSharp.Net.Http.Request(uri.AbsoluteUri)
            match response.Body with
            | ResponseBody.Text(text) -> Encoding.UTF8.GetBytes(text)
            | ResponseBody.Binary(bytes) -> bytes
        | false, _ -> 
            Encoding.UTF8.GetBytes(sample)

      use ms = new MemoryStream(body)
      use sr = new StreamReader(ms)
      let dom = HtmlRuntime.parse sr
      HtmlRuntime.getTables dom
      |> Seq.iteri (fun i table ->
                match table.TryGetAttribute("id") with
                | Some(HtmlAttribute(id, idval)) ->
                    let htmlTableRT = replacer.ToRuntime typeof<HtmlTable>
                    let tableType = ProvidedTypeDefinition(idval, Some (htmlTableRT))
                    let ctor = ProvidedConstructor([])
                    let values =
                        String.Join(Environment.NewLine, HtmlRuntime.descendantsByName "tr" table |> Seq.map (fun x ->String.Join(",", x.Value)))

                    ctor.InvokeCode <- (fun _ -> <@ new HtmlTable(idval, values)  @> |> replacer.ToRuntime)
                    tableType.AddMember(ctor)
                    tableContainer.AddMember(tableType)
                | None -> ()
         )
      generatedType.AddMember(tableContainer)
      generatedType

  let parameters = 
    [ 
        ProvidedStaticParameter("Sample", typeof<string>)
    ] 

  do htmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ htmlProvTy ])
namespace ProviderImplementation

open System
open System.IO
open System.Linq.Expressions
open System.Reflection
open System.Globalization
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open FSharp.Data.Runtime
open ProviderImplementation.ProvidedTypes

[<TypeProvider>]
type public ApiaryProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Apiary.ApiaryProvider'
  let asm, _, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let apiaryProvTy = ProvidedTypeDefinition(asm, ns, "ApiaryProvider", Some(typeof<obj>))

  let buildTypes (typeName:string) (args:obj[]) =

    let apiName = args.[0] :?> string
    let specialNames = args.[1] :?> string
    let specialNames = 
      if String.IsNullOrEmpty specialNames
      then Map.empty
      else
        specialNames.Split(',')
        |> Array.map (fun x -> x.Split('='))
        |> Array.map (fun x -> x.[0].Trim(), x.[1].Trim())
        |> Map.ofArray

    // Generate the required type with empty constructor
    let tpType = ProvidedTypeDefinition(asm, ns, typeName, Some(replacer.ToRuntime typeof<ApiaryContext>))
    let args = [ ProvidedParameter("rootUrl", typeof<string>) ]
    ProvidedConstructor(args, InvokeCode = fun (Singleton root) -> 
        let root = replacer.ToDesignTime root in replacer.ToRuntime <@@ new ApiaryContext(%%root) @@>)
    |> tpType.AddMember

    let ctx = ApiaryGenerationContext.Create(apiName, tpType, replacer, specialNames)

    // Get the schema of API operations from Apiary & generate schema
    let names = 
        ApiarySchema.getOperationTree apiName 
        |> ApiarySchema.asRestApi ctx.SpecialNames
    names |> Seq.iter (ApiaryTypeBuilder.generateSchema ctx "" tpType)

    // Return the generated type
    tpType

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = [ ProvidedStaticParameter("ApiName", typeof<string>)
                     ProvidedStaticParameter("SpecialNames", typeof<string>, "") ]

  let helpText = 
    """<summary>Apiary Provider</summary>
       <param name='ApiName'>The name of the API</param>
       <param name='SpecialNames'>The names on the documentation that are being used as examples and not as parameter names. Format = "Example1=ParamName1, Example2=ParamName2</param>"""

  do apiaryProvTy.AddXmlDoc helpText
  do apiaryProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ apiaryProvTy ])


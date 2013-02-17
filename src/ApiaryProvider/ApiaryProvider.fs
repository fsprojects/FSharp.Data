namespace ProviderImplementation

open System
open System.IO
open System.Linq.Expressions
open System.Reflection
open System.Globalization
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open FSharp.Data.RuntimeImplementation.Apiary
open ProviderImplementation.ProvidedTypes

[<TypeProvider>]
type public ApiaryProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Apiary.ApiaryProvider'
  let asm, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let apiaryProvTy = ProvidedTypeDefinition(asm, ns, "ApiaryProvider", Some(typeof<obj>))

  let buildTypes (typeName:string) (args:obj[]) =

    // API name parameter of the provider
    let apiName = args.[0] :?> string

    // Generate the required type with empty constructor
    let resTy = ProvidedTypeDefinition(asm, ns, typeName, Some(replacer.ToRuntime typeof<ApiaryContext>))
    let args = [ ProvidedParameter("rootUrl", typeof<string>) ]
    ProvidedConstructor(args, InvokeCode = fun (Singleton root) -> 
        let root = replacer.ToDesignTime root in replacer.ToRuntime <@@ new ApiaryContext(%%root) @@>)
    |> resTy.AddMember

    // A type that is used to hide all generated domain types
    let domainTy = ProvidedTypeDefinition("DomainTypes", Some(typeof<obj>))
    resTy.AddMember(domainTy)

    let ctx = ApiaryGenerationContext.Create(apiName, domainTy, replacer)

    // Get the schema of API operations from Apiary & generate schema
    let names = ApiarySchema.getOperationTree apiName |> ApiarySchema.asRestApi
    names |> Seq.iter (ApiaryTypeBuilder.generateSchema "" ctx resTy)

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = [ ProvidedStaticParameter("ApiName", typeof<string>) ]
  do apiaryProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ apiaryProvTy ])


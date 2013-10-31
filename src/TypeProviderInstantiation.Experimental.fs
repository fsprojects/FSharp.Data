namespace ProviderImplementation

open ProviderImplementation.ProvidedTypes

type ApiaryProviderArgs = 
    { ApiName : string }

type TypeProviderInstantiation = 
    | Apiary of ApiaryProviderArgs

    member x.GenerateType resolutionFolder runtimeAssembly platform =
        let f, args =
            match x with
            | Apiary x ->
                (fun cfg -> new ApiaryProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.ApiName |] 
        Debug.generate resolutionFolder runtimeAssembly platform f args

    override x.ToString() =
        match x with
        | Apiary x -> ["Apiary"
                       x.ApiName]
        |> String.concat ","

    static member Parse (line:string) =
        let args = line.Split [|','|]
        match args.[0] with
        | "Apiary" ->
            Apiary { ApiName = args.[1] }
        | _ -> failwithf "Unknown: %s" args.[0]

open System.Runtime.CompilerServices

[<assembly:InternalsVisibleToAttribute("FSharp.Data.Tests.Experimental.DesignTime")>]
do()

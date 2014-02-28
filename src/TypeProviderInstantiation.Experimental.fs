namespace ProviderImplementation

open System
open System.IO
open ProviderImplementation
open ProviderImplementation.ProvidedTypes

type ApiaryProviderArgs = 
    { ApiName : string }

type TypeProviderInstantiation = 
    | Apiary of ApiaryProviderArgs

    member x.GenerateType resolutionFolder runtimeAssembly =
        let f, args =
            match x with
            | Apiary x ->
                (fun cfg -> new ApiaryProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.ApiName |] 
        Debug.generate resolutionFolder runtimeAssembly f args

    override x.ToString() =
        match x with
        | Apiary x -> ["Apiary"
                       x.ApiName]
        |> String.concat ","

    member x.ExpectedPath outputFolder = 
        Path.Combine(outputFolder, (x.ToString().Replace(">", "&gt;").Replace("<", "&lt;").Replace("://", "_").Replace("/", "_") + ".expected"))

    member x.Dump resolutionFolder outputFolder runtimeAssembly signatureOnly ignoreOutput =
        let replace (oldValue:string) (newValue:string) (str:string) = str.Replace(oldValue, newValue)        
        let output = 
            x.GenerateType resolutionFolder runtimeAssembly
            |> match x with
               | _ -> Debug.prettyPrint signatureOnly ignoreOutput 10 100
            |> replace "FSharp.Data.Runtime." "FDR."
            |> replace resolutionFolder "<RESOLUTION_FOLDER>"
        if outputFolder <> "" then
            File.WriteAllText(x.ExpectedPath outputFolder, output)
        output

    static member Parse (line:string) =
        let args = line.Split [|','|]
        match args.[0] with
        | "Apiary" ->
            Apiary { ApiName = args.[1] }
        | _ -> failwithf "Unknown: %s" args.[0]

open System.Runtime.CompilerServices

[<assembly:InternalsVisibleToAttribute("FSharp.Data.Tests.Experimental.DesignTime")>]
do()
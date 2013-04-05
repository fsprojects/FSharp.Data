namespace ProviderImplementation

open ProviderImplementation.ProvidedTypes

type ApiaryProviderArgs = 
    { ApiName : string }

type TypeProviderInstantiation = 
    | Apiary of ApiaryProviderArgs

    member x.generateType resolutionFolder runtimeAssembly =
        let f, args =
            match x with
            | Apiary x ->
                (fun cfg -> new ApiaryProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.ApiName |] 
        Debug.generate resolutionFolder runtimeAssembly f args

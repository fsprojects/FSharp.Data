// Copyright 2011-2015, Tomas Petricek (http://tomasp.net), Gustavo Guerra (http://functionalflow.co.uk), and other contributors
// Licensed under the Apache License, Version 2.0, see LICENSE.md in this project
//

module internal ProviderImplementation.AssemblyResolver

open System
open System.IO
open System.Net
open System.Reflection
open System.Xml.Linq
open FSharp.Core.CompilerServices
open ProviderImplementation
open ProviderImplementation.ProvidedTypes

let private designTimeAssemblies = 
  lazy
    [| yield Assembly.GetExecutingAssembly() 
       for asm in Assembly.GetExecutingAssembly().GetReferencedAssemblies() do
         let asm = try Assembly.Load(asm) with _ -> null
         if asm <> null then 
            yield asm |]

let mutable private initialized = false    

let init (cfg : TypeProviderConfig) (tp: TypeProviderForNamespaces) = 

    if not initialized then
        initialized <- true
        if WebRequest.DefaultWebProxy <> null then
            WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials
        ProvidedTypes.ProvidedTypeDefinition.Logger := Some FSharp.Data.Runtime.IO.log

    let runtimeFSharpDataAssembly = 
        let asmSimpleName = Path.GetFileNameWithoutExtension cfg.RuntimeAssembly
        match tp.TargetContext.TryBindSimpleAssemblyNameToTarget(asmSimpleName) with
        | Choice1Of2 loader -> loader
        | Choice2Of2 err -> raise err
    
    runtimeFSharpDataAssembly


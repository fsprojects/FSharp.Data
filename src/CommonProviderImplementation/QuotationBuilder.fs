// Copyright 2011-2015, Tomas Petricek (http://tomasp.net), Gustavo Guerra (http://functionalflow.co.uk), and other contributors
// Licensed under the Apache License, Version 2.0, see LICENSE.md in this project
//
// Utilities for building F# quotations without quotation literals

module ProviderImplementation.QuotationBuilder

open System
open System.Reflection
open FSharp.Quotations
open FSharp.Quotations.Patterns
open FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open UncheckedQuotations

/// Dynamic operator (?) that can be used for constructing quoted F# code without 
/// quotations (to simplify constructing F# quotations in portable libraries - where
/// we need to pass the System.Type of various types as arguments)
///
/// There are two possible uses:
///    typ?Name tyArgs args
///
/// tyArgs is a sequence of type arguments for method `Name`.
/// Actual arguments can be either expression (Expr<'T>) or primitive values, whic
/// are automatically wrapped using Expr.Value.
///
let (?) (typ:Type) (operation:string) (args1:'T) (args2: 'U) : Expr = 
  // Arguments are either Expr or other type - in the second case,
  // we treat them as Expr.Value (which will only work for primitives)
  let convertValue (arg:obj) = 
    match arg with
    | :? Expr as e -> e
    | :? Var as v -> Expr.Var v
    | value -> Expr.Value(value, value.GetType())

  let invokeOperation (tyargs:obj, tyargsT) (args:obj, argsT) =
    // To support (e1, e2, ..) syntax, we use tuples - extract tuple arguments
    // First, extract type arguments - a list of System.Type values
    let tyargs = 
      if tyargsT = typeof<unit> then []
      elif FSharpType.IsTuple(tyargsT) then
        [ for f in FSharpValue.GetTupleFields(tyargs) -> f :?> Type ]
      else [ tyargs :?> Type ]
    // Second, extract arguments (which are either Expr values or primitive constants)
    let args = 
      if argsT = typeof<unit> then []
      elif FSharpType.IsTuple(argsT) then
        [ for f in FSharpValue.GetTupleFields(args) -> convertValue f ]
      else [ convertValue args ]

    // Find a method that we want to call
    let flags = BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.Instance
    match typ.GetMember(operation, MemberTypes.All, flags) with 
    | [| :? MethodInfo as mi |] -> 
        let mi = 
          if tyargs = [] then mi
          else mi.MakeGenericMethod(tyargs |> Array.ofList)
        if mi.IsStatic then Expr.CallUnchecked(mi, args)
        else Expr.CallUnchecked(List.head args, mi, List.tail args)
    | [| :? ConstructorInfo as ci |] ->
        if tyargs <> [] then failwith "Constructor cannot be generic!"
        Expr.NewObjectUnchecked(ci, args)
    | [| :? PropertyInfo as pi |] ->
        let isStatic = 
          pi.CanRead && pi.GetGetMethod().IsStatic || 
          pi.CanWrite && pi.GetSetMethod().IsStatic
        if isStatic then Expr.PropertyGetUnchecked(pi, args)
        else Expr.PropertyGetUnchecked(List.head args, pi, List.tail args)
    | options -> failwithf "Constructing call of the '%s' operation failed. Got %A" operation options

  invokeOperation (args1, typeof<'T>) (args2, typeof<'U>) 

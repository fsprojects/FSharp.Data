// Copyright 2011-2015, Tomas Petricek (http://tomasp.net), Gustavo Guerra (http://functionalflow.co.uk), and other contributors
// Licensed under the Apache License, Version 2.0, see LICENSE.md in this project
//
// Utilities for transforming F# quotations to reference types from different assemblies

namespace ProviderImplementation

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.ExprShape
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes

/// A cross-targeting type provider must ultimately provide quotations and reflection objects w.r.t.
/// the type binding context for the target assembly reference set.  
///
/// To make building a cross-targeting type provider palatable, the type provider is written w.r.t. to 
/// homogeneous quotations and reflection objects referring to a copy of the target runtime constructs held
/// in the design-time assembly itself.   These are then systematically remapped (replaced/translated) to the
/// corresponding reflection objects in the target assembly reference set.
///
/// The AssemblyReplacer acts as a way of creating provided objects where the replacement is automatically and
/// systematically applied.
///
/// When making a cross-targeting type provider, calls to 
///    ProvidedTypeDefinition
///    ProvidedParameter,
///    ProvidedProperty
///    ProvidedConstructor, 
///    ProvidedMethod 
/// should be replaced by 
///    replacer.ProvidedTypeDefinition
///    replacer.ProvidedParameter,
///    replacer.ProvidedProperty
///    replacer.ProvidedConstructor, 
///    replacer.ProvidedMethod 
/// 
type AssemblyReplacer(designTimeAssemblies, referencedAssemblies) =

  /// When translating quotations, Expr.Var's are translated to new variable respecting reference equality.
  let varTable = Dictionary<Var, Var>()
  let typeCacheFwd = Dictionary<Type, Type>()
  let typeCacheBwd = Dictionary<Type, Type>()

  let fixName (fullName:string) =
      if fullName.StartsWith("FSI_") then 
          // when F# Interactive is the host of the design time assembly,
          // all namespaces are prefixed with FSI_, in the runtime assembly
          // the name won't have that prefix
          fullName.Substring(fullName.IndexOf('.') + 1)
      else 
          fullName

  let tryGetTypeFromAssembly fullName (asm:Assembly) =

        if asm.FullName.StartsWith "FSI-ASSEMBLY" then
            // when F# Interactive is the host of the design time assembly,
            // for each type in the runtime assembly there might be multiple
            // versions (FSI_0001.FullTypeName, FSI_0002.FullTypeName, etc).
            // Get the last one.
            asm.GetTypes() 
            |> Seq.filter (fun t -> fixName t.FullName = fullName)
            |> Seq.sortBy (fun t -> t.FullName)
            |> Seq.toList
            |> function [] -> None | xs -> Some (Seq.last xs, false)
        else
            asm.GetType fullName |> function null -> None | x -> Some (x, true)


  let replaceTypeDefinition fwd (t:Type) =
      let cache = (if fwd then typeCacheFwd else typeCacheBwd)
      match cache.TryGetValue(t) with
      | true, newT -> newT
      | false, _ -> 
            let asms = (if fwd then referencedAssemblies else designTimeAssemblies)
            let fullName = fixName t.FullName

            // For normal type provider hosts like fsc.exe System.Void is a special case and these
            // hosts expect it to always be provided as [FSharp.Core 4.4.0.0]typeof<System.Void>.  
            // This is really a mistake in ExtensionTyping.fs in the F# compiler which calls 
            // typeof<System.Void>.Equals(ty).
            if fullName = "System.Void" then typeof<System.Void> else

            match Array.choose (tryGetTypeFromAssembly fullName) asms |> Seq.distinct |> Seq.toArray with
            //| [| (newT, canSave) |] -> 
            //     if canSave then cache.[t] <- newT
            //     newT
            //| r when r.Length > 1 -> 
            //    let msg = 
            //        if fwd then sprintf "The type '%O' utilized by a type provider was found in multiple assemblies in the reference assembly set '%A'. You may need to adjust your assembly references to avoid ambiguities." t referencedAssemblies
            //        else sprintf "The type '%O' utilized by a type provider was not found in the assembly set '%A' used by the type provider itself. Please report this problem to the project site for the type provider." t designTimeAssemblies
            //    failwith msg
            | r when r.Length > 0 -> 
                 let (newT, canSave) = r.[0]
                 if canSave then cache.[t] <- newT
                 newT
            | _ -> 
                let msg = 
                    if fwd then sprintf "The type '%O' utilized by a type provider was not found in reference assembly set '%A'. You may be referencing a portable profile which contains fewer types than those needed by the type provider you are using." t referencedAssemblies
                    else sprintf "The runtime-time type '%O' utilized by a type provider was not found in the compilation-time assembly set '%A'. You may be referencing a portable profile which contains fewer types than those needed by the type provider you are using. Please report this problem to the project site for the type provider." t designTimeAssemblies
                failwith msg


  let rec replaceType fwd (t:Type) =
      if t :? ProvidedTypeDefinition then t
      // Don't try to translate F# abbreviations
      elif t :? ProvidedSymbolType && (t :?> ProvidedSymbolType).IsFSharpTypeAbbreviation then t
      // Types annotated with units-of-measure
      elif t :? ProvidedSymbolType && (t :?> ProvidedSymbolType).IsFSharpUnitAnnotated then 
          let genericType = t.GetGenericTypeDefinition()
          let newT = replaceTypeDefinition fwd genericType
          let typeArguments = t.GetGenericArguments() |> Array.map (replaceType fwd) |> Array.toList
          ProvidedMeasureBuilder.Default.AnnotateType(newT, typeArguments)
      elif t.IsGenericType && not t.IsGenericTypeDefinition then 
          let genericType = t.GetGenericTypeDefinition()
          let newT = replaceTypeDefinition fwd genericType
          let typeArguments = t.GetGenericArguments() |> Array.map (replaceType fwd)
          newT.MakeGenericType(typeArguments)
      elif t.IsGenericParameter then t
      elif t.IsArray || t.IsByRef || t.IsPointer then 
          let elemType = t.GetElementType()
          let elemTypeT = replaceType fwd elemType
          if t.IsArray then 
              let rank = t.GetArrayRank()
              if rank = 1 then elemTypeT.MakeArrayType() else elemTypeT.MakeArrayType(t.GetArrayRank())
          elif t.IsByRef then elemTypeT.MakeByRefType()
          else elemTypeT.MakePointerType()

      else 
          replaceTypeDefinition fwd t

  let replaceProperty fwd (p : PropertyInfo) =
    if p :? ProvidedProperty then p
    else 
      let t = replaceType fwd p.DeclaringType
      let isStatic = 
        p.CanRead && p.GetGetMethod().IsStatic || 
        p.CanWrite && p.GetSetMethod().IsStatic
      let bindingFlags = 
        BindingFlags.Public ||| BindingFlags.NonPublic ||| 
          (if isStatic then BindingFlags.Static else BindingFlags.Instance)
      let newP = t.GetProperty(p.Name, bindingFlags)
      if newP = null then
        failwithf "Property '%O' of type '%O' not found" p t 
      newP

  let replaceField fwd (f : FieldInfo) =
    if f :? ProvidedField then f
    else 
      let t = replaceType fwd f.DeclaringType
      let bindingFlags = 
        (if f.IsPublic then BindingFlags.Public else BindingFlags.NonPublic) ||| 
        (if f.IsStatic then BindingFlags.Static else BindingFlags.Instance)
      let newF = t.GetField(f.Name, bindingFlags)
      if newF = null then failwithf "Field '%O' of type '%O' not found" f t 
      newF
  
  let replaceMethod fwd (m : MethodInfo) =
    if m :? ProvidedMethod then m
    else 
      let declTyT = replaceType fwd m.DeclaringType
      let mT =
          if m.IsGenericMethod then 
            let genericMethod = m.GetGenericMethodDefinition()
            let parameterTypesT = genericMethod.GetParameters() |> Array.map (fun p -> replaceType fwd p.ParameterType) 
            let genericMethodT = declTyT.GetMethod(genericMethod.Name,parameterTypesT)
            if genericMethodT = null then null else
            let typeArgumentsT =  m.GetGenericArguments() |> Array.map (replaceType fwd) 
            genericMethodT.MakeGenericMethod(typeArgumentsT)
          else 
            let parameterTypesT = m.GetParameters() |> Array.map (fun p -> replaceType fwd p.ParameterType) 
            declTyT.GetMethod(m.Name, parameterTypesT)
      match mT with 
      | null -> failwithf "Method '%O' not found in type '%O'" m mT 
      | _ -> mT

  let replaceConstructor fwd (cons : ConstructorInfo) =
    if cons :? ProvidedConstructor then cons
    else 
        let declTyT = replaceType fwd cons.DeclaringType
        let parameterTypesT = cons.GetParameters() |> Array.map (fun p -> replaceType fwd p.ParameterType) 
        let consT = declTyT.GetConstructor(parameterTypesT)
        match consT with 
        | null -> failwithf "Constructor '%O' not found in type '%O'" cons declTyT 
        | _ -> consT

  let replaceVar fwd (v: Var) =
    if v.Type :? ProvidedTypeDefinition then v
    else 
      let createNewVar() = 
        Var (v.Name, replaceType fwd v.Type, v.IsMutable)
      if fwd then
        match varTable.TryGetValue v with
        | true, v -> v
        | false, _ -> 
            // It's a variable local to the quotation
            let newVar = createNewVar()
            // store it so we reuse it from now on
            varTable.Add(v, newVar)
            newVar
      else
        let newVar = createNewVar()
        // store the original var as we'll have to revert to it later
        varTable.Add(newVar, v)
        newVar
  
  let rec replaceExpr fwd quotation =
    let rt = replaceType fwd 
    let rp = replaceProperty fwd 
    let rf = replaceField fwd 
    let rm = replaceMethod fwd 
    let rc = replaceConstructor fwd 
    let rv = replaceVar fwd 
    let re = replaceExpr fwd 
    
    match quotation with
    | Call (obj, m, args) -> 
        let mR = rm m
        let argsR = List.map re args
        match obj with
        | Some obj -> Expr.CallUnchecked (re obj, mR, argsR)
        | None -> Expr.CallUnchecked (mR, argsR)
    | PropertyGet (obj, p, indexArgs) -> 
        let pR = rp p
        let indexArgsR = List.map re indexArgs
        match obj with
        | Some obj -> Expr.PropertyGetUnchecked (re obj, pR, indexArgsR)
        | None -> Expr.PropertyGetUnchecked (pR, indexArgsR)
    | PropertySet (obj, p, indexArgs, value) -> 
        let pR = rp p
        let indexArgsR = List.map re indexArgs
        match obj with
        | Some obj -> Expr.PropertySetUnchecked (re obj, pR, re value, indexArgsR)
        | None -> Expr.PropertySetUnchecked (pR, re value, indexArgsR)
    | NewObject (c, exprs) ->
        let exprsR = List.map re exprs
        Expr.NewObjectUnchecked (rc c, exprsR)
    | Coerce (expr, t) ->
        Expr.Coerce (re expr, rt t)
    | NewArray (t, exprs) ->
        Expr.NewArrayUnchecked (rt t, List.map re exprs)
    | NewTuple (exprs) ->
        Expr.NewTuple (List.map re exprs)
    | TupleGet (expr, i) ->
        Expr.TupleGetUnchecked (re expr, i)
    | NewDelegate (t, vars, expr) ->
        Expr.NewDelegateUnchecked (rt t, List.map rv vars, re expr)
    | FieldGet (obj, f) -> 
        match obj with
        | Some obj -> Expr.FieldGetUnchecked (re obj, rf f)
        | None -> Expr.FieldGetUnchecked (rf f)
    | FieldSet (obj, f, value) -> 
        match obj with
        | Some obj -> Expr.FieldSetUnchecked (re obj, rf f, re value)
        | None -> Expr.FieldSetUnchecked (rf f, re value)
    | Let (var, value, body) -> 
        Expr.LetUnchecked(rv var, re value, re body)

    // Eliminate some F# constructs which do not cross-target well
    | Application(f,e) -> 
        re (Expr.CallUnchecked(f, f.Type.GetMethod "Invoke", [ e ]) )
    | NewUnionCase(ci, es) ->
        re (Expr.CallUnchecked(Reflection.FSharpValue.PreComputeUnionConstructorInfo ci, es) )
    | NewRecord(ci, es) ->
        re (Expr.NewObjectUnchecked(FSharpValue.PreComputeRecordConstructorInfo ci, es) )
    | UnionCaseTest(e,uc) ->
        let tagInfo = FSharpValue.PreComputeUnionTagMemberInfo uc.DeclaringType
        let tagExpr = 
            match tagInfo with 
            | :? PropertyInfo as tagProp -> Expr.PropertyGetUnchecked(e,tagProp)
            | :? MethodInfo as tagMeth -> 
                    if tagMeth.IsStatic then Expr.CallUnchecked(tagMeth, [e])
                    else Expr.CallUnchecked(e,tagMeth,[])
            | _ -> failwith "unreachable: unexpected result from PreComputeUnionTagMemberInfo"
        let tagNumber = uc.Tag
        re <@@ (%%(tagExpr) : int) = tagNumber @@>

    // Traverse remaining constructs
    | ShapeVar v -> 
        Expr.Var (rv v)
    | ShapeLambda _ -> 
        failwith ("It's not possible to create a Lambda when cross targetting to a different FSharp.Core.\n" +
                  "Make sure you're not calling a function with signature A->(B->C) instead of A->B->C (using |> causes this).")
    | ShapeCombination (o, exprs) -> 
        RebuildShapeCombination (o, List.map re exprs)

  // Gets the equivalent runtime type
  let typeToTargetAssemblies t = t |> replaceType true 
  // Gets an equivalent expression with all the types replaced with runtime equivalents
  let exprToTargetAssemblies e = e |> replaceExpr true 
  // Gets an equivalent expression with all the types replaced with designTime equivalents
  let exprToOriginalAssemblies e = e |> replaceExpr false

  // For the Provided* type InvokeCode and GetterCode, we to first transform the argument expressions
  // to the design time types, so we can splice it in the quotation, and then after that we have to convert
  // it back to the runtime type. 

  /// When making a cross-targeting type provider, use this method instead of the ProvidedParameter constructor from ProvidedTypes
  member replacer.ProvidedParameter(paramName, typ) = 
      ProvidedParameter(paramName, typ |> typeToTargetAssemblies)

  /// When making a cross-targeting type provider, use this method instead of the ProvidedProperty constructor from ProvidedTypes
  member replacer.ProvidedProperty(propertyName, typ, getterCode) = 
      ProvidedProperty(propertyName, typ |> typeToTargetAssemblies, GetterCode = (fun args -> args |> List.map exprToOriginalAssemblies |> getterCode |> exprToTargetAssemblies))

  /// When making a cross-targeting type provider, use this method instead of the ProvidedConstructor constructor from ProvidedTypes
  member replacer.ProvidedConstructor(parameters, invokeCode: Expr list -> Expr) = 
      ProvidedConstructor(parameters, InvokeCode = (fun args -> args |> List.map exprToOriginalAssemblies |> invokeCode |> exprToTargetAssemblies))

  /// When making a cross-targeting type provider, use this method instead of the ProvidedMethod constructor from ProvidedTypes
  member replacer.ProvidedMethod(nm, parameters, resultType: Type, isStatic, invokeCode: Expr list -> Expr) = 
      ProvidedMethod(nm, parameters, 
                     resultType |> typeToTargetAssemblies, 
                     IsStaticMethod = isStatic, 
                     InvokeCode = (fun args -> args |> List.map exprToOriginalAssemblies |> invokeCode |> exprToTargetAssemblies))

  /// When making a cross-targeting type provider, use this method instead of the corresponding ProvidedTypeDefinition constructor from ProvidedTypes
  member replacer.ProvidedTypeDefinition(nm, baseType: Type, hideObjectMethods, nonNullable) = 
      ProvidedTypeDefinition(nm, Some (baseType |> typeToTargetAssemblies), HideObjectMethods = hideObjectMethods, NonNullable = nonNullable)

  /// When making a cross-targeting type provider, use this method instead of the corresponding ProvidedTypeDefinition constructor from ProvidedTypes
  member replacer.ProvidedTypeDefinition(asm, ns, typeName, baseType: Type, hideObjectMethods, nonNullable) = 
      ProvidedTypeDefinition(asm, ns, typeName, Some (baseType |> typeToTargetAssemblies), HideObjectMethods = hideObjectMethods, NonNullable = nonNullable)



// Copyright 2011-2015, Tomas Petricek (http://tomasp.net), Gustavo Guerra (http://functionalflow.co.uk), and other contributors
// Licensed under the Apache License, Version 2.0, see LICENSE.md in this project
//

#nowarn "40"
#nowarn "52"

// Notes by Don Syme:
//
// The FSharp.Core 2.0 - 4.0 (4.0.0.0 - 4.4.0.0) quotations implementation is overly strict in that it doesn't allow 
// generation of quotations for cross-targeted FSharp.Core.  This file define a series of Unchecked methods
// implemented via reflection hacks to allow creation of various nodes when using a cross-targets FSharp.Core and
// mscorlib.dll.  
//
//   - Most importantly, these cross-targeted quotations can be provided to the F# compiler by a type provider.  
//     They are generally produced via the AssemblyReplacer.fs component through a process of rewriting design-time quotations that
//     are not cross-targeted.
//
//   - However, these quotation values are a bit fragile. Using existing FSharp.Core.Quotations.Patterns 
//     active patterns on these quotation nodes will generally work correctly. But using ExprShape.RebuildShapeCombination 
//     on these new nodes will not succed, nor will operations that build new quotations such as Expr.Call. 
//     Instead, use the replacement provided in this module.
//
//   - Likewise, some operations in these quotation values like "expr.Type" may be a bit fragile, possibly returning non cross-targeted types in 
//     the result. However those operations are not used by the F# compiler.

namespace ProviderImplementation

open System
open System.Text
open System.IO
open System.Reflection
open System.Reflection.Emit
open Microsoft.FSharp.Quotations

[<AutoOpen>]
module internal UncheckedQuotations =

    let qTy = typeof<Microsoft.FSharp.Quotations.Var>.Assembly.GetType("Microsoft.FSharp.Quotations.ExprConstInfo") 
    assert (qTy <> null)
    let pTy = typeof<Microsoft.FSharp.Quotations.Var>.Assembly.GetType("Microsoft.FSharp.Quotations.PatternsModule")
    assert (pTy<> null)

    // These are handles to the internal functions that create quotation nodes of different sizes. Although internal, 
    // these function names have been stable since F# 2.0.
    let mkFE0 = pTy.GetMethod("mkFE0", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (mkFE0 <> null)
    let mkFE1 = pTy.GetMethod("mkFE1", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (mkFE1 <> null)
    let mkFE2 = pTy.GetMethod("mkFE2", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (mkFE2 <> null)
    let mkFEN = pTy.GetMethod("mkFEN", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (mkFEN <> null)

    // These are handles to the internal tags attached to quotation nodes of different sizes. Although internal, 
    // these function names have been stable since F# 2.0.
    let newDelegateOp = qTy.GetMethod("NewNewDelegateOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (newDelegateOp <> null)
    let instanceCallOp = qTy.GetMethod("NewInstanceMethodCallOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (instanceCallOp <> null)
    let staticCallOp = qTy.GetMethod("NewStaticMethodCallOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (staticCallOp <> null)
    let newObjectOp = qTy.GetMethod("NewNewObjectOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (newObjectOp <> null)
    let newArrayOp = qTy.GetMethod("NewNewArrayOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (newArrayOp <> null)
    let appOp = qTy.GetMethod("get_AppOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (appOp <> null)
    let instancePropGetOp = qTy.GetMethod("NewInstancePropGetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (instancePropGetOp <> null)
    let staticPropGetOp = qTy.GetMethod("NewStaticPropGetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (staticPropGetOp <> null)
    let instancePropSetOp = qTy.GetMethod("NewInstancePropSetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (instancePropSetOp <> null)
    let staticPropSetOp = qTy.GetMethod("NewStaticPropSetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (staticPropSetOp <> null)
    let instanceFieldGetOp = qTy.GetMethod("NewInstanceFieldGetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (instanceFieldGetOp <> null)
    let staticFieldGetOp = qTy.GetMethod("NewStaticFieldGetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (staticFieldGetOp <> null)
    let instanceFieldSetOp = qTy.GetMethod("NewInstanceFieldSetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (instanceFieldSetOp <> null)
    let staticFieldSetOp = qTy.GetMethod("NewStaticFieldSetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (staticFieldSetOp <> null)
    let tupleGetOp = qTy.GetMethod("NewTupleGetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (tupleGetOp <> null)
    let letOp = qTy.GetMethod("get_LetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (letOp <> null)
      
    type Microsoft.FSharp.Quotations.Expr with 

        static member NewDelegateUnchecked (ty: Type, vs: Var list, body: Expr) =
            let e =  List.foldBack (fun v acc -> Expr.Lambda(v,acc)) vs body 
            let op = newDelegateOp.Invoke(null, [| box ty |])
            mkFE1.Invoke(null, [| box op; box e |]) :?> Expr

        static member NewObjectUnchecked (cinfo: ConstructorInfo, args : Expr list) =
            let op = newObjectOp.Invoke(null, [| box cinfo |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member NewArrayUnchecked (elementType: Type, elements : Expr list) =
            let op = newArrayOp.Invoke(null, [| box elementType |])
            mkFEN.Invoke(null, [| box op; box elements |]) :?> Expr

        static member CallUnchecked (minfo: MethodInfo, args : Expr list) =
            let op = staticCallOp.Invoke(null, [| box minfo |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member CallUnchecked (obj: Expr, minfo: MethodInfo, args : Expr list) =
            let op = instanceCallOp.Invoke(null, [| box minfo |])
            mkFEN.Invoke(null, [| box op; box (obj::args) |]) :?> Expr

        static member ApplicationUnchecked (f: Expr, x: Expr) =
            let op = appOp.Invoke(null, [| |])
            mkFE2.Invoke(null, [| box op; box f; box x |]) :?> Expr

        static member PropertyGetUnchecked (pinfo: PropertyInfo, args : Expr list) =
            let op = staticPropGetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member PropertyGetUnchecked (obj: Expr, pinfo: PropertyInfo, ?args : Expr list) =
            let args = defaultArg args []
            let op = instancePropGetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box (obj::args) |]) :?> Expr

        static member PropertySetUnchecked (pinfo: PropertyInfo, value: Expr, ?args : Expr list) =
            let args = defaultArg args []
            let op = staticPropSetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box (args@[value]) |]) :?> Expr

        static member PropertySetUnchecked (obj: Expr, pinfo: PropertyInfo, value: Expr, args : Expr list) =
            let op = instancePropSetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box (obj::(args@[value])) |]) :?> Expr

        static member FieldGetUnchecked (pinfo: FieldInfo) =
            let op = staticFieldGetOp.Invoke(null, [| box pinfo |])
            mkFE0.Invoke(null, [| box op; |]) :?> Expr

        static member FieldGetUnchecked (obj: Expr, pinfo: FieldInfo) =
            let op = instanceFieldGetOp.Invoke(null, [| box pinfo |])
            mkFE1.Invoke(null, [| box op; box obj |]) :?> Expr

        static member FieldSetUnchecked (pinfo: FieldInfo, value: Expr) =
            let op = staticFieldSetOp.Invoke(null, [| box pinfo |])
            mkFE1.Invoke(null, [| box op; box value |]) :?> Expr

        static member FieldSetUnchecked (obj: Expr, pinfo: FieldInfo, value: Expr) =
            let op = instanceFieldSetOp.Invoke(null, [| box pinfo |])
            mkFE2.Invoke(null, [| box op; box obj; box value |]) :?> Expr

        static member TupleGetUnchecked (e: Expr, n:int) =
            let op = tupleGetOp.Invoke(null, [| box e.Type; box n |])
            mkFE1.Invoke(null, [| box op; box e |]) :?> Expr

        static member LetUnchecked (v:Var, e: Expr, body:Expr) =
            let lam = Expr.Lambda(v,body)
            let op = letOp.Invoke(null, [| |])
            mkFE2.Invoke(null, [| box op; box e; box lam |]) :?> Expr

    type Shape = Shape of (Expr list -> Expr)
    
    let (|ShapeCombinationUnchecked|ShapeVarUnchecked|ShapeLambdaUnchecked|) e =
        match e with 
        | Patterns.NewObject (cinfo, args) ->
            ShapeCombinationUnchecked (Shape (function args -> Expr.NewObjectUnchecked (cinfo, args)), args)
        | Patterns.NewArray (ty, args) ->
            ShapeCombinationUnchecked (Shape (function args -> Expr.NewArrayUnchecked (ty, args)), args)
        | Patterns.NewDelegate (t, vars, expr) ->
            ShapeCombinationUnchecked (Shape (function [expr] -> Expr.NewDelegateUnchecked (t, vars, expr) | _ -> invalidArg "expr" "invalid shape"), [expr])
        | Patterns.TupleGet (expr, n) ->
            ShapeCombinationUnchecked (Shape (function [expr] -> Expr.TupleGetUnchecked (expr, n) | _ -> invalidArg "expr" "invalid shape"), [expr])
        | Patterns.Application (f, x) ->
            ShapeCombinationUnchecked (Shape (function [f; x] -> Expr.ApplicationUnchecked (f, x) | _ -> invalidArg "expr" "invalid shape"), [f; x])
        | Patterns.Call (objOpt, minfo, args) ->
            match objOpt with 
            | None -> ShapeCombinationUnchecked (Shape (function args -> Expr.CallUnchecked (minfo, args)), args)
            | Some obj -> ShapeCombinationUnchecked (Shape (function (obj::args) -> Expr.CallUnchecked (obj, minfo, args) | _ -> invalidArg "expr" "invalid shape"), obj::args)
        | Patterns.PropertyGet (objOpt, pinfo, args) ->
            match objOpt with 
            | None -> ShapeCombinationUnchecked (Shape (function args -> Expr.PropertyGetUnchecked (pinfo, args)), args)
            | Some obj -> ShapeCombinationUnchecked (Shape (function (obj::args) -> Expr.PropertyGetUnchecked (obj, pinfo, args) | _ -> invalidArg "expr" "invalid shape"), obj::args)
        | Patterns.PropertySet (objOpt, pinfo, args, value) ->
            match objOpt with 
            | None -> ShapeCombinationUnchecked (Shape (function (value::args) -> Expr.PropertySetUnchecked (pinfo, value, args) | _ -> invalidArg "expr" "invalid shape"), value::args)
            | Some obj -> ShapeCombinationUnchecked (Shape (function (obj::value::args) -> Expr.PropertySetUnchecked (obj, pinfo, value, args) | _ -> invalidArg "expr" "invalid shape"), obj::value::args)
        | Patterns.FieldGet (objOpt, pinfo) ->
            match objOpt with 
            | None -> ShapeCombinationUnchecked (Shape (function _ -> Expr.FieldGetUnchecked (pinfo)), [])
            | Some obj -> ShapeCombinationUnchecked (Shape (function [obj] -> Expr.FieldGetUnchecked (obj, pinfo) | _ -> invalidArg "expr" "invalid shape"), [obj])
        | Patterns.FieldSet (objOpt, pinfo, value) ->
            match objOpt with 
            | None -> ShapeCombinationUnchecked (Shape (function [value] -> Expr.FieldSetUnchecked (pinfo, value) | _ -> invalidArg "expr" "invalid shape"), [value])
            | Some obj -> ShapeCombinationUnchecked (Shape (function [obj;value] -> Expr.FieldSetUnchecked (obj, pinfo, value) | _ -> invalidArg "expr" "invalid shape"), [obj; value])
        | Patterns.Let (var, value, body) -> 
            ShapeCombinationUnchecked (Shape (function [value;Patterns.Lambda(var, body)] -> Expr.LetUnchecked(var, value, body) | _ -> invalidArg "expr" "invalid shape"), [value; Expr.Lambda(var, body)])
        | Patterns.TupleGet (expr, i) ->
            ShapeCombinationUnchecked (Shape (function [expr] -> Expr.TupleGetUnchecked (expr, i) | _ -> invalidArg "expr" "invalid shape"), [expr])
        | ExprShape.ShapeCombination (comb,args) -> 
            ShapeCombinationUnchecked (Shape (fun args -> ExprShape.RebuildShapeCombination(comb, args)), args)
        | ExprShape.ShapeVar v -> ShapeVarUnchecked v
        | ExprShape.ShapeLambda (v, e) -> ShapeLambdaUnchecked (v,e)

    let RebuildShapeCombinationUnchecked (Shape comb,args) = comb args

// Note, the FSharp.Core implementation of Fsharp.Reflection UnionCaseInfo is also overly strict and assumes you are reflecting
// over infos drawn from a homogeneous (i.e. non-cross-targeted) FSharp.Core.  For example, it does special-cased
// things for option and list types, and these won't work if used with infos drawn from a cross-targeted FSharp.Core.
//
// This makes it hard to construct meaningful cross-targeted quotations that include NewUnionCase and UnionCaseGet nodes.
// So we don't try to cover those cases in this module.
(*
      let UnionCaseInfoUnchecked (declTy: Type, tag: int) = 
          let uci = typeof<Microsoft.FSharp.Reflection.UnionCaseInfo>
          let mkUci = uci.GetConstructor(BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic, null, [| typeof<Type>; typeof<int> |], null)
          mkUci.Invoke [| box declTy; box tag |] :?> UnionCaseInfo
*)

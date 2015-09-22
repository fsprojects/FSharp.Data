#nowarn "40"
#nowarn "52"
// Based on code for the F# 3.0 Developer Preview release of September 2011,
// Copyright (c) Microsoft Corporation 2005-2012.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 

// This file contains a set of helper types and methods for providing types in an implementation 
// of ITypeProvider.

// This code has been modified and is appropriate for use in conjunction with the F# 3.0, F# 3.1, and F# 3.1.1 releases

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
    let mkFE1 = pTy.GetMethod("mkFE1", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (mkFE1 <> null)
    let mkFE2 = pTy.GetMethod("mkFE2", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (mkFE2 <> null)
    let mkFEN = pTy.GetMethod("mkFEN", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (mkFEN <> null)
    let newDelegateOp = qTy.GetMethod("NewNewDelegateOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (newDelegateOp <> null)
    let instanceCallOp = qTy.GetMethod("NewInstanceMethodCallOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (instanceCallOp <> null)
    let staticCallOp = qTy.GetMethod("NewStaticMethodCallOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (staticCallOp <> null)
    let newObjectOp = qTy.GetMethod("NewNewObjectOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (newObjectOp <> null)
    let instancePropGetOp = qTy.GetMethod("NewInstancePropGetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (instancePropGetOp <> null)
    let staticPropGetOp = qTy.GetMethod("NewStaticPropGetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (staticPropGetOp <> null)
    let instancePropSetOp = qTy.GetMethod("NewInstancePropSetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (instancePropSetOp <> null)
    let staticPropSetOp = qTy.GetMethod("NewStaticPropSetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (staticPropSetOp <> null)
    let tupleGetOp = qTy.GetMethod("NewTupleGetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (tupleGetOp <> null)
    let letOp = qTy.GetMethod("get_LetOp", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    assert (letOp <> null)
      
    // The FSharp.Core 4.3.0.0-4.4.0.0 quotations implementation is overly strict in that it doesn't allow 
    // generation of quotations for cross-targeted FSharp.Core.  This is a series of hacks to allow 
    // creation of various nodes without the checks that require the use of function/tuple/... types from a specific
    // FSharp.Core DLL.
    type Microsoft.FSharp.Quotations.Expr with 

        static member NewDelegateUnchecked (ty: Type, vs: Var list, body: Expr) =
            let e =  List.foldBack (fun v acc -> Expr.Lambda(v,acc)) vs body 
            let op = newDelegateOp.Invoke(null, [| box ty |])
            mkFE1.Invoke(null, [| box op; box e |]) :?> Expr

        static member NewObjectUnchecked (cinfo: ConstructorInfo, args : Expr list) =
            let op = newObjectOp.Invoke(null, [| box cinfo |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member CallUnchecked (minfo: MethodInfo, args : Expr list) =
            let op = staticCallOp.Invoke(null, [| box minfo |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member CallUnchecked (obj: Expr, minfo: MethodInfo, args : Expr list) =
            let op = instanceCallOp.Invoke(null, [| box minfo |])
            mkFEN.Invoke(null, [| box op; box (obj::args) |]) :?> Expr

        static member PropertyGetUnchecked (pinfo: PropertyInfo, args : Expr list) =
            let op = staticPropGetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box args |]) :?> Expr

        static member PropertyGetUnchecked (obj: Expr, pinfo: PropertyInfo, args : Expr list) =
            let op = instancePropGetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box (obj::args) |]) :?> Expr


        static member PropertySetUnchecked (pinfo: PropertyInfo, value: Expr, args : Expr list) =
            let op = staticPropSetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box (args@[value]) |]) :?> Expr

        static member PropertySetUnchecked (obj: Expr, pinfo: PropertyInfo, value: Expr, args : Expr list) =
            let op = instancePropSetOp.Invoke(null, [| box pinfo |])
            mkFEN.Invoke(null, [| box op; box (obj::(args@[value])) |]) :?> Expr

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
        | Patterns.NewDelegate (t, vars, expr) ->
            ShapeCombinationUnchecked (Shape (function [expr] -> Expr.NewDelegateUnchecked (t, vars, expr) | _ -> invalidArg "expr" "invalid shape"), [expr])
        | Patterns.TupleGet (expr, n) ->
            ShapeCombinationUnchecked (Shape (function [expr] -> Expr.TupleGetUnchecked (expr, n) | _ -> invalidArg "expr" "invalid shape"), [expr])
        | Patterns.Call (objOpt, minfo, args) ->
            match objOpt with 
            | None -> ShapeCombinationUnchecked (Shape (function args -> Expr.CallUnchecked (minfo, args)), args)
            | Some obj -> ShapeCombinationUnchecked (Shape (function (obj::args) -> Expr.CallUnchecked (obj, minfo, args) | _ -> invalidArg "expr" "invalid shape"), obj::args)
        | Patterns.Let (var, value, body) -> 
            ShapeCombinationUnchecked (Shape (function [value;Patterns.Lambda(var, body)] -> Expr.LetUnchecked(var, value, body) | _ -> invalidArg "expr" "invalid shape"), [value; Expr.Lambda(var, body)])
        | Patterns.TupleGet (expr, i) ->
            ShapeCombinationUnchecked (Shape (function [expr] -> Expr.TupleGetUnchecked (expr, i) | _ -> invalidArg "expr" "invalid shape"), [expr])
        | ExprShape.ShapeCombination (comb,args) -> 
            ShapeCombinationUnchecked (Shape (fun args -> ExprShape.RebuildShapeCombination(comb, args)), args)
        | ExprShape.ShapeVar v -> ShapeVarUnchecked v
        | ExprShape.ShapeLambda (v, e) -> ShapeLambdaUnchecked (v,e)

    let RebuildShapeCombinationUnchecked (Shape comb,args) = comb args

(*
      let UnionCaseInfoUnchecked (declTy: Type, tag: int) = 
          let uci = typeof<Microsoft.FSharp.Reflection.UnionCaseInfo>
          let mkUci = uci.GetConstructor(BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic, null, [| typeof<Type>; typeof<int> |], null)
          mkUci.Invoke [| box declTy; box tag |] :?> UnionCaseInfo
*)

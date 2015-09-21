
module internal ProviderImplementation.AssemblyReader

open System
open System.IO
open System.Collections.Generic
open System.Reflection
 
// -------------------------------------------------------------------- 
// Utilities
// -------------------------------------------------------------------- 

let tryFindMulti k map = match Map.tryFind k map with Some res -> res | None -> [| |]

let splitNameAt (nm:string) idx = 
    if idx < 0 then failwith "splitNameAt: idx < 0";
    let last = nm.Length - 1 
    if idx > last then failwith "splitNameAt: idx > last";
    (nm.Substring(0,idx)),
    (if idx < last then nm.Substring (idx+1,last - idx) else "")

let splitILTypeName (nm:string) = 
    match nm.LastIndexOf '.' with
    | -1 -> None, nm
    | idx -> let a,b = splitNameAt nm idx in Some a, b

let joinILTypeName (nspace: string option) (nm:string) = 
    match nspace with 
    | None -> nm
    | Some ns -> ns + "." + nm

// -------------------------------------------------------------------- 
// Ordered lists with a lookup table
// --------------------------------------------------------------------

let lazyMap f (x:Lazy<_>) =  if x.IsValueCreated then Lazy.CreateFromValue (f (x.Force())) else lazy (f (x.Force()))


/// This is used to store event, property and field maps.
type LazyOrderedMultiMap<'Key,'Data when 'Key : equality>(keyf : 'Data -> 'Key, lazyItems : Lazy<'Data[]>) = 

    let quickMap= 
        lazyItems |> lazyMap (fun entries -> 
            let t = new Dictionary<_,_>(entries.Length, HashIdentity.Structural)
            do entries |> Array.iter (fun y -> let key = keyf y in t.[key] <- y :: (if t.ContainsKey(key) then t.[key] else [])) 
            t)

    member self.Entries() = lazyItems.Force()

    member self.Item with get(x) = let t = quickMap.Force() in if t.ContainsKey x then t.[x] else []


//---------------------------------------------------------------------
// SHA1 hash-signing algorithm.  Used to get the public key token from
// the public key.
//---------------------------------------------------------------------

let b0 n =  (n &&& 0xFF)
let b1 n =  ((n >>> 8) &&& 0xFF)
let b2 n =  ((n >>> 16) &&& 0xFF)
let b3 n =  ((n >>> 24) &&& 0xFF)

module SHA1 = 
    let inline (>>>&)  (x:int) (y:int)  = int32 (uint32 x >>> y)
    let f(t,b,c,d) = 
        if t < 20 then (b &&& c) ||| ((~~~b) &&& d)
        elif t < 40 then b ^^^ c ^^^ d
        elif t < 60 then (b &&& c) ||| (b &&& d) ||| (c &&& d)
        else b ^^^ c ^^^ d

    let [<Literal>] k0to19 = 0x5A827999
    let [<Literal>] k20to39 = 0x6ED9EBA1
    let [<Literal>] k40to59 = 0x8F1BBCDC
    let [<Literal>] k60to79 = 0xCA62C1D6

    let k t = 
        if t < 20 then k0to19 
        elif t < 40 then k20to39 
        elif t < 60 then k40to59 
        else k60to79 

    type SHAStream = 
        { stream: byte[];
          mutable pos: int;
          mutable eof:  bool; }

    let rotLeft32 x n =  (x <<< n) ||| (x >>>& (32-n))

    // padding and length (in bits!) recorded at end 
    let shaAfterEof sha  = 
        let n = sha.pos
        let len = sha.stream.Length
        if n = len then 0x80
        else 
          let padded_len = (((len + 9 + 63) / 64) * 64) - 8
          if n < padded_len - 8  then 0x0  
          elif (n &&& 63) = 56 then int32 ((int64 len * int64 8) >>> 56) &&& 0xff
          elif (n &&& 63) = 57 then int32 ((int64 len * int64 8) >>> 48) &&& 0xff
          elif (n &&& 63) = 58 then int32 ((int64 len * int64 8) >>> 40) &&& 0xff
          elif (n &&& 63) = 59 then int32 ((int64 len * int64 8) >>> 32) &&& 0xff
          elif (n &&& 63) = 60 then int32 ((int64 len * int64 8) >>> 24) &&& 0xff
          elif (n &&& 63) = 61 then int32 ((int64 len * int64 8) >>> 16) &&& 0xff
          elif (n &&& 63) = 62 then int32 ((int64 len * int64 8) >>> 8) &&& 0xff
          elif (n &&& 63) = 63 then (sha.eof <- true; int32 (int64 len * int64 8) &&& 0xff)
          else 0x0

    let shaRead8 sha = 
        let s = sha.stream 
        let b = if sha.pos >= s.Length then shaAfterEof sha else int32 s.[sha.pos]
        sha.pos <- sha.pos + 1
        b
        
    let shaRead32 sha  = 
        let b0 = shaRead8 sha
        let b1 = shaRead8 sha
        let b2 = shaRead8 sha
        let b3 = shaRead8 sha
        let res = (b0 <<< 24) ||| (b1 <<< 16) ||| (b2 <<< 8) ||| b3
        res

    let sha1Hash sha = 
        let mutable h0 = 0x67452301
        let mutable h1 = 0xEFCDAB89
        let mutable h2 = 0x98BADCFE
        let mutable h3 = 0x10325476
        let mutable h4 = 0xC3D2E1F0
        let mutable a = 0
        let mutable b = 0
        let mutable c = 0
        let mutable d = 0
        let mutable e = 0
        let w = Array.create 80 0x00
        while (not sha.eof) do
            for i = 0 to 15 do
                w.[i] <- shaRead32 sha
            for t = 16 to 79 do
                w.[t] <- rotLeft32 (w.[t-3] ^^^ w.[t-8] ^^^ w.[t-14] ^^^ w.[t-16]) 1
            a <- h0 
            b <- h1
            c <- h2
            d <- h3
            e <- h4
            for t = 0 to 79 do
                let temp = (rotLeft32 a 5) + f(t,b,c,d) + e + w.[t] + k(t)
                e <- d
                d <- c
                c <- rotLeft32 b 30
                b <- a
                a <- temp
            h0 <- h0 + a
            h1 <- h1 + b
            h2 <- h2 + c
            h3 <- h3 + d
            h4 <- h4 + e
        h0,h1,h2,h3,h4

    let sha1HashBytes s = 
        let (_h0,_h1,_h2,h3,h4) = sha1Hash { stream = s; pos = 0; eof = false }   // the result of the SHA algorithm is stored in registers 3 and 4
        Array.map byte [|  b0 h4; b1 h4; b2 h4; b3 h4; b0 h3; b1 h3; b2 h3; b3 h3; |]


let sha1HashBytes s = SHA1.sha1HashBytes s


// --------------------------------------------------------------------
// 
// -------------------------------------------------------------------- 

type ILVersionInfo = uint16 * uint16 * uint16 * uint16

[<StructuralEquality; StructuralComparison>]
type PublicKey =
    | PublicKey of byte[]
    | PublicKeyToken of byte[]
    member x.IsKey=match x with PublicKey _ -> true | _ -> false
    member x.IsKeyToken=match x with PublicKeyToken _ -> true | _ -> false
    member x.Key=match x with PublicKey b -> b | _ -> invalidOp "not a key"
    member x.KeyToken=match x with PublicKeyToken b -> b | _ -> invalidOp"not a key token"

    member x.ToToken() = 
        match x with 
        | PublicKey bytes -> SHA1.sha1HashBytes bytes
        | PublicKeyToken token -> token
    static member KeyAsToken(k) = PublicKeyToken(PublicKey(k).ToToken())

[<Sealed>]
type ILAssemblyRef(name: string, hash: byte[] option, publicKey: PublicKey option, retargetable: bool, version: ILVersionInfo option, locale: string option)  =  
    member x.Name=name
    member x.Hash=hash
    member x.PublicKey=publicKey
    member x.Retargetable=retargetable
    member x.Version=version
    member x.Locale=locale
    static member FromAssemblyName (aname:System.Reflection.AssemblyName) =
        let locale = None
        let publicKey = 
           match aname.GetPublicKey()  with 
           | null | [| |] -> 
               match aname.GetPublicKeyToken()  with 
               | null | [| |] -> None
               | bytes -> Some (PublicKeyToken bytes)
           | bytes -> 
               Some (PublicKey bytes)
        
        let version = 
           match aname.Version with 
           | null -> None
           | v -> Some (uint16 v.Major,uint16 v.Minor,uint16 v.Build,uint16 v.Revision)
           
        let retargetable = aname.Flags = System.Reflection.AssemblyNameFlags.Retargetable

        ILAssemblyRef(aname.Name,None,publicKey,retargetable,version,locale)
 
    member aref.QualifiedName = 
        let b = new System.Text.StringBuilder(100)
        let add (s:string) = (b.Append(s) |> ignore)
        let addC (s:char) = (b.Append(s) |> ignore)
        add(aref.Name);
        match aref.Version with 
        | None -> ()
        | Some (a,b,c,d) -> 
            add ", Version=";
            add (string (int a))
            add ".";
            add (string (int b))
            add ".";
            add (string (int c))
            add ".";
            add (string (int d))
            add ", Culture="
            match aref.Locale with 
            | None -> add "neutral"
            | Some b -> add b
            add ", PublicKeyToken="
            match aref.PublicKey with 
            | None -> add "null"
            | Some pki -> 
                  let pkt = pki.ToToken()
                  let convDigit(digit) = 
                      let digitc = 
                          if digit < 10 
                          then  System.Convert.ToInt32 '0' + digit 
                          else System.Convert.ToInt32 'a' + (digit - 10) 
                      System.Convert.ToChar(digitc)
                  for i = 0 to pkt.Length-1 do
                      let v = pkt.[i]
                      addC (convDigit(System.Convert.ToInt32(v)/16))
                      addC (convDigit(System.Convert.ToInt32(v)%16))
            // retargetable can be true only for system assemblies that definitely have Version
            if aref.Retargetable then
                add ", Retargetable=Yes" 
        b.ToString()
    override x.ToString() = x.QualifiedName


type ILModuleRef(name:string, hasMetadata: bool, hash: byte[] option) = 
    member x.Name=name
    member x.HasMetadata=hasMetadata
    member x.Hash=hash 
    override x.ToString() = "module " + name


[<RequireQualifiedAccess>]
type ILScopeRef = 
    | Local
    | Module of ILModuleRef 
    | Assembly of ILAssemblyRef
    member x.IsLocalRef   = match x with ILScopeRef.Local      -> true | _ -> false
    member x.IsModuleRef  = match x with ILScopeRef.Module _   -> true | _ -> false
    member x.IsAssemblyRef= match x with ILScopeRef.Assembly _ -> true | _ -> false
    member x.ModuleRef    = match x with ILScopeRef.Module x   -> x | _ -> failwith "not a module reference"
    member x.AssemblyRef  = match x with ILScopeRef.Assembly x -> x | _ -> failwith "not an assembly reference"

    member x.QualifiedName = 
        match x with 
        | ILScopeRef.Local -> ""
        | ILScopeRef.Module mref -> "module "+mref.Name
        | ILScopeRef.Assembly aref -> aref.QualifiedName

    override x.ToString() = x.QualifiedName

type ILArrayBound = int32 option 
type ILArrayBounds = ILArrayBound * ILArrayBound

[<StructuralEquality; StructuralComparison>]
type ILArrayShape = 
    | ILArrayShape of ILArrayBounds[] (* lobound/size pairs *)
    member x.Rank = (let (ILArrayShape l) = x in l.Length)
    static member SingleDimensional = ILArrayShapeStatics.SingleDimensional
    static member FromRank n = if n = 1 then ILArrayShape.SingleDimensional else ILArrayShape(List.replicate n (Some 0,None) |> List.toArray)


and ILArrayShapeStatics() = 
    static let singleDimensional = ILArrayShape [| (Some 0, None) |]    
    static member SingleDimensional = singleDimensional

/// Calling conventions.  These are used in method pointer types.
[<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
type ILArgConvention = 
    | Default
    | CDecl 
    | StdCall 
    | ThisCall 
    | FastCall 
    | VarArg
      
[<StructuralEquality; StructuralComparison; RequireQualifiedAccess>]
type ILThisConvention =
    | Instance
    | InstanceExplicit
    | Static

[<StructuralEquality; StructuralComparison>]
type ILCallingConv =
    | Callconv of ILThisConvention * ILArgConvention
    member x.ThisConv           = let (Callconv(a,_b)) = x in a
    member x.BasicConv          = let (Callconv(_a,b)) = x in b
    member x.IsInstance         = match x.ThisConv with ILThisConvention.Instance -> true | _ -> false
    member x.IsInstanceExplicit = match x.ThisConv with ILThisConvention.InstanceExplicit -> true | _ -> false
    member x.IsStatic           = match x.ThisConv with ILThisConvention.Static -> true | _ -> false

    static member Instance = ILCallingConvStatics.Instance
    static member Static = ILCallingConvStatics.Static

/// Static storage to amortize the allocation of ILCallingConv.Instance and ILCallingConv.Static
and ILCallingConvStatics() = 
    static let instanceCallConv = Callconv(ILThisConvention.Instance,ILArgConvention.Default)
    static let staticCallConv =  Callconv(ILThisConvention.Static,ILArgConvention.Default)
    static member Instance = instanceCallConv
    static member Static = staticCallConv

type ILBoxity = 
    | AsObject 
    | AsValue

[<RequireQualifiedAccess>]
type ILTypeRefScope = 
    | Top of ILScopeRef
    | Nested of ILTypeRef
    member x.AddQualifiedNameExtension(basic) = 
        match x with 
        | Top scoref -> 
            let sco = scoref.QualifiedName
            if sco = "" then basic else String.concat ", " [basic;sco]
        | Nested tref ->
            tref.AddQualifiedNameExtension(basic)


// IL type references have a pre-computed hash code to enable quick lookup tables during binary generation.
and ILTypeRef(enc: ILTypeRefScope, nsp: string option, name: string) = 
          
    member x.Scope = enc
    member x.Name = name
    member x.Namespace = nsp

    member tref.FullName = 
        match enc with 
        | ILTypeRefScope.Top _ -> tref.Name
        | ILTypeRefScope.Nested enc -> enc.FullName + "." + tref.Name
        
    member tref.BasicQualifiedName = 
        match enc with 
        | ILTypeRefScope.Top _ -> tref.Name
        | ILTypeRefScope.Nested enc -> enc.BasicQualifiedName + "+" + tref.Name

    member tref.AddQualifiedNameExtension(basic) = enc.AddQualifiedNameExtension(basic)

    member tref.QualifiedName = enc.AddQualifiedNameExtension(tref.BasicQualifiedName)

    override x.ToString() = x.FullName

        
and ILTypeSpec(typeRef: ILTypeRef, inst: ILGenericArgs) = 
    member x.TypeRef = typeRef
    member x.Scope = x.TypeRef.Scope
    member x.Name = x.TypeRef.Name
    member x.Namespace = x.TypeRef.Namespace
    member x.GenericArgs = inst
    member x.BasicQualifiedName = 
        let tc = x.TypeRef.BasicQualifiedName
        if x.GenericArgs.Length = 0 then
            tc
        else 
            tc + "[" + String.concat "," (x.GenericArgs |> Array.map (fun arg -> "[" + arg.QualifiedName + "]")) + "]"

    member x.AddQualifiedNameExtension(basic) = 
        x.TypeRef.AddQualifiedNameExtension(basic)

    member x.FullName = x.TypeRef.FullName

    override x.ToString() = x.TypeRef.ToString() + (if x.GenericArgs.Length = 0 then "" else "<...>")

and [<RequireQualifiedAccess>]
    ILType =
    | Void                   
    | Array    of ILArrayShape * ILType 
    | Value    of ILTypeSpec      
    | Boxed    of ILTypeSpec      
    | Ptr      of ILType             
    | Byref    of ILType           
    | FunctionPointer     of ILCallingSignature 
    | Var    of int
    | Modified of bool * ILTypeRef * ILType

    member x.BasicQualifiedName = 
        match x with 
        | ILType.Var n -> "!" + string n
        | ILType.Modified(_,_ty1,ty2) -> ty2.BasicQualifiedName
        | ILType.Array (ILArrayShape(s),ty) -> ty.BasicQualifiedName + "[" + System.String(',',s.Length-1) + "]"
        | ILType.Value tr | ILType.Boxed tr -> tr.BasicQualifiedName
        | ILType.Void -> "void"
        | ILType.Ptr _ty -> failwith "unexpected pointer type"
        | ILType.Byref _ty -> failwith "unexpected byref type"
        | ILType.FunctionPointer _mref -> failwith "unexpected function pointer type"

    member x.AddQualifiedNameExtension(basic) = 
        match x with 
        | ILType.Var _n -> basic
        | ILType.Modified(_,_ty1,ty2) -> ty2.AddQualifiedNameExtension(basic)
        | ILType.Array (ILArrayShape(_s),ty) -> ty.AddQualifiedNameExtension(basic)
        | ILType.Value tr | ILType.Boxed tr -> tr.AddQualifiedNameExtension(basic)
        | ILType.Void -> failwith "void"
        | ILType.Ptr _ty -> failwith "unexpected pointer type"
        | ILType.Byref _ty -> failwith "unexpected byref type"
        | ILType.FunctionPointer _mref -> failwith "unexpected function pointer type"
        
    member x.QualifiedName = 
        x.AddQualifiedNameExtension(x.BasicQualifiedName)

    member x.TypeSpec =
      match x with 
      | ILType.Boxed tr | ILType.Value tr -> tr
      | _ -> invalidOp "not a nominal type"

    member x.Boxity =
      match x with 
      | ILType.Boxed _ -> AsObject
      | ILType.Value _ -> AsValue
      | _ -> invalidOp "not a nominal type"

    member x.TypeRef = 
      match x with 
      | ILType.Boxed tspec | ILType.Value tspec -> tspec.TypeRef
      | _ -> invalidOp "not a nominal type"

    member x.IsNominal = 
      match x with 
      | ILType.Boxed _ | ILType.Value _ -> true
      | _ -> false

    member x.GenericArgs =
      match x with 
      | ILType.Boxed tspec | ILType.Value tspec -> tspec.GenericArgs
      | _ -> [| |]

    member x.IsTyvar =
      match x with 
      | ILType.Var _ -> true | _ -> false

    override x.ToString() = x.QualifiedName

and ILCallingSignature(callingConv: ILCallingConv, argTypes: ILTypes, returnType: ILType) = 
    member __.CallingConv = callingConv
    member __.ArgTypes = argTypes
    member __.ReturnType = returnType

and ILGenericArgs = ILType[]
and ILTypes = ILType[]


type ILMethodRef(parent: ILTypeRef, callconv: ILCallingConv, genericArity: int, name: string, args: ILTypes, ret: ILType) =
    member x.EnclosingTypeRef = parent
    member x.CallingConv = callconv
    member x.Name = name
    member x.GenericArity = genericArity
    member x.ArgCount = args.Length
    member x.ArgTypes = args
    member x.ReturnType = ret

    member x.CallingSignature = ILCallingSignature (x.CallingConv,x.ArgTypes,x.ReturnType)
    override x.ToString() = x.EnclosingTypeRef.ToString() + "::" + x.Name + "(...)"


type ILFieldRef(enclosingTypeRef: ILTypeRef, name: string, typ: ILType) =
    member __.EnclosingTypeRef = enclosingTypeRef
    member __.Name = name
    member __.Type = typ
    override x.ToString() = x.EnclosingTypeRef.ToString() + "::" + x.Name

type ILMethodSpec(methodRef: ILMethodRef, enclosingType: ILType, methodInst: ILGenericArgs) = 
    member x.MethodRef = methodRef
    member x.EnclosingType=enclosingType
    member x.GenericArgs=methodInst
    member x.Name=x.MethodRef.Name
    member x.CallingConv=x.MethodRef.CallingConv
    member x.GenericArity = x.MethodRef.GenericArity
    member x.FormalArgTypes = x.MethodRef.ArgTypes
    member x.FormalReturnType = x.MethodRef.ReturnType
    override x.ToString() = x.MethodRef.ToString() + "(...)"

type ILFieldSpec(fieldRef: ILFieldRef, enclosingType: ILType) = 
    member x.FieldRef = fieldRef
    member x.EnclosingType = enclosingType
    member x.FormalType       = fieldRef.Type
    member x.Name             = fieldRef.Name
    member x.EnclosingTypeRef = fieldRef.EnclosingTypeRef
    override x.ToString() = x.FieldRef.ToString()


// --------------------------------------------------------------------
// Debug info.                                                     
// -------------------------------------------------------------------- 

type Guid =  byte[]

type ILPlatform = 
    | X86
    | AMD64
    | IA64


// --------------------------------------------------------------------
// Custom attributes                                                     
// -------------------------------------------------------------------- 

type ILCustomAttrArg =  (ILType * obj)
type ILCustomAttrNamedArg =  (string * ILType * bool * obj)
type ILCustomAttr = 
    { Method: ILMethodSpec;
      Data: byte[] }

[<NoEquality; NoComparison>]
type ILCustomAttrs(contents: Lazy<ILCustomAttr[]>) =
   static let empty = ILCustomAttrs(Lazy.CreateFromValue [| |])
   member x.Elements = contents.Force()
   static member Empty = empty

[<RequireQualifiedAccess>]
type ILMemberAccess = 
    | Assembly
    | CompilerControlled
    | FamilyAndAssembly
    | FamilyOrAssembly
    | Family
    | Private 
    | Public 

[<RequireQualifiedAccess>]
type ILFieldInit = 
    | String of string
    | Bool of bool
    | Char of uint16
    | Int8 of int8
    | Int16 of int16
    | Int32 of int32
    | Int64 of int64
    | UInt8 of uint8
    | UInt16 of uint16
    | UInt32 of uint32
    | UInt64 of uint64
    | Single of single
    | Double of double
    | Null
  
type ILParameter =
    { Name: string option;
      Type: ILType;
      Default: ILFieldInit option;  
      //Marshal: ILNativeType option; 
      IsIn: bool;
      IsOut: bool;
      IsOptional: bool;
      CustomAttrs: ILCustomAttrs }

type ILParameters = ILParameter[]

type ILReturn = 
    { //Marshal: ILNativeType option;
      Type: ILType; 
      CustomAttrs: ILCustomAttrs }

type ILOverridesSpec = 
    | OverridesSpec of ILMethodRef * ILType
    member x.MethodRef = let (OverridesSpec(mr,_ty)) = x in mr
    member x.EnclosingType = let (OverridesSpec(_mr,ty)) = x in ty

type ILMethodVirtualInfo = 
    { IsFinal: bool
      IsNewSlot: bool 
      IsCheckAccessOnOverride: bool
      IsAbstract: bool }

type MethodKind =
    | Static 
    | Cctor 
    | Ctor 
    | NonVirtual 
    | Virtual of ILMethodVirtualInfo

[<RequireQualifiedAccess>]
type ILMethodBody = 
    | IL (* of ILMethodBody *) 
    | PInvoke (* of PInvokeMethod *) 
    | Abstract
    | Native

[<RequireQualifiedAccess>]
type MethodCodeKind =
    | IL
    | Native
    | Runtime

let typesOfILParamsRaw (ps:ILParameters) : ILTypes = ps |> Array.map (fun p -> p.Type) 
let typesOfILParamsList (ps:ILParameter[]) = ps |> Array.map (fun p -> p.Type) 

[<StructuralEquality; StructuralComparison>]
type ILGenericVariance = 
    | NonVariant            
    | CoVariant             
    | ContraVariant         

type ILGenericParameterDef =
    { Name: string
      Constraints: ILTypes
      Variance: ILGenericVariance
      HasReferenceTypeConstraint: bool
      CustomAttrs : ILCustomAttrs
      HasNotNullableValueTypeConstraint: bool
      HasDefaultConstructorConstraint: bool }

    override x.ToString() = x.Name 

type ILGenericParameterDefs = ILGenericParameterDef[]

[<NoComparison; NoEquality>]
type ILMethodDef = 
    { MetadataToken: int32
      Name: string
      Kind: MethodKind
      CallingConv: ILCallingConv
      Parameters: ILParameters
      Return: ILReturn
      Access: ILMemberAccess
      mdBody: ILMethodBody   
      mdCodeKind: MethodCodeKind   
      IsInternalCall: bool
      IsManaged: bool
      IsForwardRef: bool
      //SecurityDecls: ILPermissions
      HasSecurity: bool
      IsEntryPoint:bool
      IsReqSecObj: bool
      IsHideBySig: bool
      IsSpecialName: bool
      IsUnmanagedExport: bool
      IsSynchronized: bool
      IsPreserveSig: bool
      IsMustRun: bool
      IsNoInline: bool
      GenericParams: ILGenericParameterDefs
      CustomAttrs: ILCustomAttrs }
    member x.ParameterTypes = typesOfILParamsRaw x.Parameters

    member x.IsClassInitializer   = match x.Kind with | MethodKind.Cctor      -> true | _ -> false
    member x.IsConstructor        = match x.Kind with | MethodKind.Ctor       -> true | _ -> false
    member x.IsStatic             = match x.Kind with | MethodKind.Static     -> true | _ -> false
    member x.IsNonVirtualInstance = match x.Kind with | MethodKind.NonVirtual -> true | _ -> false
    member x.IsVirtual            = match x.Kind with | MethodKind.Virtual _  -> true | _ -> false

    member x.IsFinal                = match x.Kind with | MethodKind.Virtual v -> v.IsFinal    | _ -> invalidOp "not virtual"
    member x.IsNewSlot              = match x.Kind with | MethodKind.Virtual v -> v.IsNewSlot  | _ -> invalidOp "not virtual"
    member x.IsCheckAccessOnOverride= match x.Kind with | MethodKind.Virtual v -> v.IsCheckAccessOnOverride   | _ -> invalidOp "not virtual"
    member x.IsAbstract             = match x.Kind with | MethodKind.Virtual v -> v.IsAbstract | _ -> invalidOp "not virtual"

    member md.CallingSignature =  ILCallingSignature (md.CallingConv,md.ParameterTypes,md.Return.Type)
    override x.ToString() = "method " + x.Name

/// Index table by name and arity. 
type MethodDefMap = Map<string, ILMethodDef[]>

[<NoEquality; NoComparison>]
type ILMethodDefs(larr: Lazy<ILMethodDef[]>, lmap:Lazy<MethodDefMap>) = 

    member x.Elements = larr.Force() 
    member x.FindByName nm  =  tryFindMulti nm (lmap.Force() )
    member x.FindByNameAndArity (nm,arity) =  x.FindByName nm |> Array.filter (fun x -> x.Parameters.Length = arity) 


[<NoComparison; NoEquality>]
type ILEventDef =
    { //EventHandlerType: ILType option
      Name: string
      IsRTSpecialName: bool
      IsSpecialName: bool
      AddMethod: ILMethodRef
      RemoveMethod: ILMethodRef
      //FireMethod: ILMethodRef option
      //OtherMethods: ILMethodRef[]
      CustomAttrs: ILCustomAttrs }
    member x.EventHandlerType = x.AddMethod.ArgTypes.[0]
    member x.IsStatic = x.AddMethod.CallingConv.IsStatic
    override x.ToString() = "event " + x.Name

(* Index table by name. *)
[<NoEquality; NoComparison>]
type ILEventDefs = 
    | Events of LazyOrderedMultiMap<string, ILEventDef>
    member x.Elements = let (Events t) = x in t.Entries()
    member x.LookupByName s = let (Events t) = x in t.[s]

[<NoComparison; NoEquality>]
type ILPropertyDef = 
    { Name: string
      IsRTSpecialName: bool
      IsSpecialName: bool
      SetMethod: ILMethodRef option
      GetMethod: ILMethodRef option
      CallingConv: ILThisConvention
      PropertyType: ILType
      Init: ILFieldInit option
      IndexParameterTypes: ILTypes
      CustomAttrs: ILCustomAttrs }
    member x.IsStatic = (match x.CallingConv with ILThisConvention.Static -> true | _ -> false)
    member x.IndexParameters = x.IndexParameterTypes |> Array.mapi (fun i ty -> 
        {  Name = Some("arg"+string i)
           Type = ty
           Default = None
           IsIn = false
           IsOptional = false
           IsOut = false 
           CustomAttrs = ILCustomAttrs.Empty })
    override x.ToString() = "property " + x.Name
    
// Index table by name.
[<NoEquality; NoComparison>]
type ILPropertyDefs = 
    | Properties of LazyOrderedMultiMap<string, ILPropertyDef>
    member x.Elements = let (Properties t) = x in t.Entries()
    member x.LookupByName s = let (Properties t) = x in t.[s]

[<NoComparison; NoEquality>]
type ILFieldDef = 
    { Name: string
      FieldType: ILType
      IsStatic: bool
      Access: ILMemberAccess
      //Data:  byte[] option
      LiteralValue:  ILFieldInit option
      Offset:  int32 option
      IsSpecialName: bool
      //Marshal: ILNativeType option
      NotSerialized: bool
      IsLiteral: bool 
      IsInitOnly: bool
      CustomAttrs: ILCustomAttrs }
    override x.ToString() = "field " + x.Name


// Index table by name.  Keep a canonical list to make sure field order is not disturbed for binary manipulation.
type ILFieldDefs = 
    | Fields of LazyOrderedMultiMap<string, ILFieldDef>
    member x.Elements = let (Fields t) = x in t.Entries()
    member x.LookupByName s = let (Fields t) = x in t.[s]

type ILMethodImplDef =
    { Overrides: ILOverridesSpec;
      OverrideBy: ILMethodSpec }

// Index table by name and arity. 
type ILMethodImplDefs(larr : Lazy<ILMethodImplDef[]>) =
    member x.Elements = larr.Force()

and MethodImplsMap = Map<string * int, ILMethodImplDef array>

[<RequireQualifiedAccess>]
type ILTypeDefLayout =
    | Auto
    | Sequential of ILTypeDefLayoutInfo
    | Explicit of ILTypeDefLayoutInfo (* REVIEW: add field info here *)

and ILTypeDefLayoutInfo =
    { Size: int32 option;
      Pack: uint16 option } 

[<RequireQualifiedAccess>]
type ILTypeInit =
    | BeforeField
    | OnAny

[<RequireQualifiedAccess>]
type ILDefaultPInvokeEncoding =
    | Ansi
    | Auto
    | Unicode

type ILTypeDefAccess =
    | Public 
    | Private
    | Nested of ILMemberAccess 

[<RequireQualifiedAccess>]
type ILTypeDefKind =
    | Class
    | ValueType
    | Interface
    | Enum 
    | Delegate
    | Other of IlxExtensionTypeKind

and IlxExtensionTypeKind = Ext_type_def_kind of obj

type internal_type_def_kind_extension = 
    { internalTypeDefKindExtIs: IlxExtensionTypeKind -> bool; }


[<NoComparison; NoEquality>]
type ILTypeDef =  
    { Kind: ILTypeDefKind
      Namespace: string option
      Name: string  
      GenericParams: ILGenericParameterDefs   (* class is generic *)
      Access: ILTypeDefAccess  
      IsAbstract: bool
      IsSealed: bool 
      IsSerializable: bool 
      IsComInterop: bool (* Class or interface generated for COM interop *) 
      Layout: ILTypeDefLayout
      IsSpecialName: bool
      Encoding: ILDefaultPInvokeEncoding
      NestedTypes: ILTypeDefs
      Implements: ILTypes  
      Extends: ILType option 
      Methods: ILMethodDefs
      //SecurityDecls: ILPermissions
      HasSecurity: bool
      Fields: ILFieldDefs
      MethodImpls: ILMethodImplDefs
      InitSemantics: ILTypeInit
      Events: ILEventDefs
      Properties: ILPropertyDefs
      CustomAttrs: ILCustomAttrs }
    member x.IsClass =     (match x.Kind with ILTypeDefKind.Class -> true | _ -> false)
    member x.IsInterface = (match x.Kind with ILTypeDefKind.Interface -> true | _ -> false)
    member x.IsEnum =      (match x.Kind with ILTypeDefKind.Enum -> true | _ -> false)
    member x.IsDelegate =  (match x.Kind with ILTypeDefKind.Delegate -> true | _ -> false)

    member tdef.IsStructOrEnum = 
        match tdef.Kind with
        | ILTypeDefKind.ValueType | ILTypeDefKind.Enum -> true
        | _ -> false

    override x.ToString() = "type " + x.Name

and ILTypeDefs(larr : Lazy<Lazy<ILTypeDef>[]>) =

    let lmap = 
        larr |> lazyMap (fun arr -> 
            (Map.empty, arr) ||> Array.fold (fun tab ltd -> 
                let td = ltd.Force() 
                let key = (td.Namespace, td.Name)
                tab.Add (key, ltd)))

    member x.Elements = 
        [| for td in larr.Force() -> td.Force() |]
    
    member x.TryFindByName (nsp,nm)  = 
        let tdefs = lmap.Force()
        let key = (nsp,nm)
        if tdefs.ContainsKey key then 
            Some (tdefs.[key].Force())
        else
            None
        
/// keyed first on namespace then on type name.  The namespace is often a unique key for a given type map.
and ILTypeDefsMap = 
     Map<string,Lazy<ILTypeDef>>

type ILNestedExportedType =
    { Name: string
      Access: ILMemberAccess
      Nested: ILNestedExportedTypesAndForwarders
      CustomAttrs: ILCustomAttrs } 
    override x.ToString() = "nested fwd " + x.Name

and ILNestedExportedTypesAndForwarders(larr:Lazy<ILNestedExportedType[]>) =
    let lmap = lazy ((Map.empty, larr.Force()) ||> Array.fold (fun m x -> m.Add(x.Name,x)))
    member x.Elements = larr.Force()
    member x.TryFindByName nm = lmap.Force().TryFind nm

and [<NoComparison; NoEquality>]
    ILExportedTypeOrForwarder =
    { ScopeRef: ILScopeRef
      Namespace : string option
      Name: string
      IsForwarder: bool
      Access: ILTypeDefAccess
      Nested: ILNestedExportedTypesAndForwarders
      CustomAttrs: ILCustomAttrs } 
    override x.ToString() = "fwd " + x.Name

and ILExportedTypesAndForwarders(larr:Lazy<ILExportedTypeOrForwarder[]>) =
    let lmap = lazy ((Map.empty, larr.Force()) ||> Array.fold (fun m x -> m.Add((x.Namespace, x.Name),x)))
    member x.Elements = larr.Force()
    member x.TryFindByName (nsp,nm) = lmap.Force().TryFind (nsp,nm)

[<RequireQualifiedAccess>]
type ILResourceAccess = 
    | Public 
    | Private 

[<RequireQualifiedAccess>]
type ILResourceLocation =
    | Local of (unit -> byte[])
    | File of ILModuleRef * int32
    | Assembly of ILAssemblyRef

type ILResource =
    { Name: string
      Location: ILResourceLocation
      Access: ILResourceAccess
      CustomAttrs: ILCustomAttrs }
    override x.ToString() = "resource " + x.Name

type ILResources(larr: Lazy<ILResource[]>) = 
    member x.Elements = larr.Force()

type ILAssemblyManifest = 
    { Name: string
      //SecurityDecls: ILPermissions;
      PublicKey: byte[] option
      Version: ILVersionInfo option
      Locale: string option
      CustomAttrs: ILCustomAttrs
      //DisableJitOptimizations: bool
      //JitTracking: bool
      Retargetable: bool
      ExportedTypes: ILExportedTypesAndForwarders
      EntrypointElsewhere: ILModuleRef option } 
    member x.GetName() = 
        let asmName = AssemblyName(Name=x.Name)
        x.PublicKey |> Option.iter (fun bytes -> asmName.SetPublicKey(bytes))
        x.Version |> Option.iter (fun (major,minor,build,rev) -> asmName.Version <- Version (int32 major,int32 minor,int32 build, int32 rev))
        asmName.CultureInfo <- System.Globalization.CultureInfo.InvariantCulture;
        asmName
    override x.ToString() = "manifest " + x.Name

type ILModuleDef = 
    { Manifest: ILAssemblyManifest option
      CustomAttrs: ILCustomAttrs
      Name: string
      TypeDefs: ILTypeDefs
      //SubsystemVersion : int * int
      //UseHighEntropyVA : bool
      //SubSystemFlags: int32
      //IsDLL: bool
      //IsILOnly: bool
      //Platform: ILPlatform option
      //StackReserveSize: int32 option
      //Is32Bit: bool
      //Is32BitPreferred: bool
      //Is64Bit: bool
      //VirtualAlignment: int32
      //PhysicalAlignment: int32
      //ImageBase: int32
      //MetadataVersion: string
      Resources: ILResources 
      }

    member x.ManifestOfAssembly = 
        match x.Manifest with 
        | Some m -> m
        | None -> failwith "no manifest"

    member m.HasManifest = m.Manifest.IsSome

    override x.ToString() = "module " + x.Name


[<NoEquality; NoComparison>]
type ILGlobals = 
    { typ_Object: ILType
      typ_String: ILType
      typ_Type: ILType
      typ_TypedReference: ILType option
      typ_SByte: ILType
      typ_Int16: ILType
      typ_Int32: ILType
      typ_Int64: ILType
      typ_Byte: ILType
      typ_UInt16: ILType
      typ_UInt32: ILType
      typ_UInt64: ILType
      typ_Single : ILType
      typ_Double: ILType
      typ_Boolean: ILType
      typ_Char: ILType
      typ_IntPtr: ILType
      typ_UIntPtr: ILType 
      systemRuntimeScopeRef : ILScopeRef }
    override x.ToString() = "<ILGlobals>"

let singleOfBits (x:int32) = System.BitConverter.ToSingle(System.BitConverter.GetBytes(x),0)
let doubleOfBits (x:int64) = System.BitConverter.Int64BitsToDouble(x)

//---------------------------------------------------------------------
// Utilities.  
//---------------------------------------------------------------------

[<Struct>]
type TableName(idx: int) = 
    member x.Index = idx
    static member FromIndex n = TableName n 

module TableNames = 
    let Module               = TableName 0  
    let TypeRef              = TableName 1  
    let TypeDef              = TableName 2  
    let FieldPtr             = TableName 3  
    let Field                = TableName 4  
    let MethodPtr            = TableName 5  
    let Method               = TableName 6  
    let ParamPtr             = TableName 7  
    let Param                = TableName 8  
    let InterfaceImpl        = TableName 9  
    let MemberRef            = TableName 10 
    let Constant             = TableName 11 
    let CustomAttribute      = TableName 12 
    let FieldMarshal         = TableName 13 
    let Permission           = TableName 14 
    let ClassLayout          = TableName 15 
    let FieldLayout          = TableName 16 
    let StandAloneSig        = TableName 17 
    let EventMap             = TableName 18 
    let EventPtr             = TableName 19 
    let Event                = TableName 20 
    let PropertyMap          = TableName 21 
    let PropertyPtr          = TableName 22 
    let Property             = TableName 23 
    let MethodSemantics      = TableName 24 
    let MethodImpl           = TableName 25 
    let ModuleRef            = TableName 26 
    let TypeSpec             = TableName 27 
    let ImplMap              = TableName 28 
    let FieldRVA             = TableName 29 
    let ENCLog               = TableName 30 
    let ENCMap               = TableName 31 
    let Assembly             = TableName 32 
    let AssemblyProcessor    = TableName 33 
    let AssemblyOS           = TableName 34 
    let AssemblyRef          = TableName 35 
    let AssemblyRefProcessor = TableName 36 
    let AssemblyRefOS        = TableName 37 
    let File                 = TableName 38 
    let ExportedType         = TableName 39 
    let ManifestResource     = TableName 40 
    let Nested               = TableName 41 
    let GenericParam           = TableName 42 
    let MethodSpec           = TableName 43 
    let GenericParamConstraint = TableName 44
    let UserStrings           = TableName 0x70 (* Special encoding of embedded UserString tokens - See 1.9 Partition III *) 

[<Struct>]
type TypeDefOrRefOrSpecTag(tag: int32) = 
    member x.Tag = tag
    static member TypeDef = TypeDefOrRefOrSpecTag 0x00
    static member TypeRef = TypeDefOrRefOrSpecTag 0x01
    static member TypeSpec = TypeDefOrRefOrSpecTag 0x2

[<Struct>]
type HasConstantTag(tag: int32) = 
    member x.Tag = tag
    static member FieldDef = HasConstantTag 0x0
    static member ParamDef  = HasConstantTag 0x1
    static member Property = HasConstantTag 0x2

[<Struct>]
type HasCustomAttributeTag(tag: int32) = 
    member x.Tag = tag
    static member MethodDef       = HasCustomAttributeTag 0x0
    static member FieldDef        = HasCustomAttributeTag 0x1
    static member TypeRef         = HasCustomAttributeTag 0x2
    static member TypeDef         = HasCustomAttributeTag 0x3
    static member ParamDef        = HasCustomAttributeTag 0x4
    static member InterfaceImpl   = HasCustomAttributeTag 0x5
    static member MemberRef       = HasCustomAttributeTag 0x6
    static member Module          = HasCustomAttributeTag 0x7
    static member Permission      = HasCustomAttributeTag 0x8
    static member Property        = HasCustomAttributeTag 0x9
    static member Event           = HasCustomAttributeTag 0xa
    static member StandAloneSig   = HasCustomAttributeTag 0xb
    static member ModuleRef       = HasCustomAttributeTag 0xc
    static member TypeSpec        = HasCustomAttributeTag 0xd
    static member Assembly        = HasCustomAttributeTag 0xe
    static member AssemblyRef     = HasCustomAttributeTag 0xf
    static member File            = HasCustomAttributeTag 0x10
    static member ExportedType    = HasCustomAttributeTag 0x11
    static member ManifestResource        = HasCustomAttributeTag 0x12
    static member GenericParam            = HasCustomAttributeTag 0x13
    static member GenericParamConstraint  = HasCustomAttributeTag 0x14
    static member MethodSpec              = HasCustomAttributeTag 0x15

[<Struct>]
type HasFieldMarshalTag(tag: int32) = 
    member x.Tag = tag
    static member FieldDef =  HasFieldMarshalTag 0x00
    static member ParamDef =  HasFieldMarshalTag 0x01

[<Struct>]
type HasDeclSecurityTag(tag: int32) = 
    member x.Tag = tag
    static member TypeDef =  HasDeclSecurityTag 0x00
    static member MethodDef =  HasDeclSecurityTag 0x01
    static member Assembly =  HasDeclSecurityTag 0x02

[<Struct>]
type MemberRefParentTag(tag: int32) = 
    member x.Tag = tag
    static member TypeRef = MemberRefParentTag 0x01
    static member ModuleRef = MemberRefParentTag 0x02
    static member MethodDef = MemberRefParentTag 0x03
    static member TypeSpec  = MemberRefParentTag 0x04

[<Struct>]
type HasSemanticsTag(tag: int32) = 
    member x.Tag = tag
    static member Event =  HasSemanticsTag 0x00
    static member Property =  HasSemanticsTag 0x01

[<Struct>]
type MethodDefOrRefTag(tag: int32) = 
    member x.Tag = tag
    static member MethodDef =  MethodDefOrRefTag 0x00
    static member MemberRef =  MethodDefOrRefTag 0x01
    static member MethodSpec =  MethodDefOrRefTag 0x02

[<Struct>]
type MemberForwardedTag(tag: int32) = 
    member x.Tag = tag
    static member FieldDef =  MemberForwardedTag 0x00
    static member MethodDef =  MemberForwardedTag 0x01

[<Struct>]
type ImplementationTag(tag: int32) = 
    member x.Tag = tag
    static member File =  ImplementationTag 0x00
    static member AssemblyRef =  ImplementationTag 0x01
    static member ExportedType =  ImplementationTag 0x02

[<Struct>]
type CustomAttributeTypeTag(tag: int32) = 
    member x.Tag = tag
    static member MethodDef =  CustomAttributeTypeTag 0x02
    static member MemberRef =  CustomAttributeTypeTag 0x03

[<Struct>]
type ResolutionScopeTag(tag: int32) = 
    member x.Tag = tag
    static member Module =  ResolutionScopeTag 0x00
    static member ModuleRef =  ResolutionScopeTag 0x01
    static member AssemblyRef  =  ResolutionScopeTag 0x02
    static member TypeRef =  ResolutionScopeTag 0x03

[<Struct>]
type TypeOrMethodDefTag(tag: int32) = 
    member x.Tag = tag
    static member TypeDef = TypeOrMethodDefTag 0x00
    static member MethodDef = TypeOrMethodDefTag 0x01

let et_END = 0x00uy
let et_VOID = 0x01uy
let et_BOOLEAN = 0x02uy
let et_CHAR = 0x03uy
let et_I1 = 0x04uy
let et_U1 = 0x05uy
let et_I2 = 0x06uy
let et_U2 = 0x07uy
let et_I4 = 0x08uy
let et_U4 = 0x09uy
let et_I8 = 0x0Auy
let et_U8 = 0x0Buy
let et_R4 = 0x0Cuy
let et_R8 = 0x0Duy
let et_STRING = 0x0Euy
let et_PTR = 0x0Fuy
let et_BYREF = 0x10uy
let et_VALUETYPE      = 0x11uy
let et_CLASS          = 0x12uy
let et_VAR            = 0x13uy
let et_ARRAY          = 0x14uy
let et_WITH           = 0x15uy
let et_TYPEDBYREF     = 0x16uy
let et_I              = 0x18uy
let et_U              = 0x19uy
let et_FNPTR          = 0x1Buy
let et_OBJECT         = 0x1Cuy
let et_SZARRAY        = 0x1Duy
let et_MVAR           = 0x1euy
let et_CMOD_REQD      = 0x1Fuy
let et_CMOD_OPT       = 0x20uy

let et_SENTINEL       = 0x41uy // sentinel for varargs 
let et_PINNED         = 0x45uy

let e_IMAGE_CEE_CS_CALLCONV_FASTCALL = 0x04uy
let e_IMAGE_CEE_CS_CALLCONV_STDCALL = 0x02uy
let e_IMAGE_CEE_CS_CALLCONV_THISCALL = 0x03uy
let e_IMAGE_CEE_CS_CALLCONV_CDECL = 0x01uy
let e_IMAGE_CEE_CS_CALLCONV_VARARG = 0x05uy
let e_IMAGE_CEE_CS_CALLCONV_FIELD = 0x06uy
let e_IMAGE_CEE_CS_CALLCONV_LOCAL_SIG = 0x07uy
let e_IMAGE_CEE_CS_CALLCONV_PROPERTY = 0x08uy

let e_IMAGE_CEE_CS_CALLCONV_GENERICINST = 0x0auy
let e_IMAGE_CEE_CS_CALLCONV_GENERIC = 0x10uy
let e_IMAGE_CEE_CS_CALLCONV_INSTANCE = 0x20uy
let e_IMAGE_CEE_CS_CALLCONV_INSTANCE_EXPLICIT = 0x40uy


// Logical shift right treating int32 as unsigned integer.
// Code that uses this should probably be adjusted to use unsigned integer types.
let (>>>&) (x:int32) (n:int32) = int32 (uint32 x >>> n)

let align alignment n = ((n + alignment - 0x1) / alignment) * alignment

let uncodedToken (tab:TableName) idx = ((tab.Index <<< 24) ||| idx)

let i32ToUncodedToken tok  = 
    let idx = tok &&& 0xffffff
    let tab = tok >>>& 24
    (TableName.FromIndex tab,  idx)


[<Struct>]
type TaggedIndex<'T> = 
    val tag: 'T
    val index : int32
    new(tag,index) = { tag=tag; index=index }

let uncodedTokenToTypeDefOrRefOrSpec (tab,tok) = 
    let tag =
        if tab = TableNames.TypeDef then TypeDefOrRefOrSpecTag.TypeDef 
        elif tab = TableNames.TypeRef then TypeDefOrRefOrSpecTag.TypeRef
        elif tab = TableNames.TypeSpec then TypeDefOrRefOrSpecTag.TypeSpec
        else failwith "bad table in uncodedTokenToTypeDefOrRefOrSpec" 
    TaggedIndex(tag,tok)

let uncodedTokenToMethodDefOrRef (tab,tok) = 
    let tag =
        if tab = TableNames.Method then MethodDefOrRefTag.MethodDef 
        elif tab = TableNames.MemberRef then MethodDefOrRefTag.MemberRef
        else failwith "bad table in uncodedTokenToMethodDefOrRef" 
    TaggedIndex(tag,tok)

let (|TaggedIndex|) (x:TaggedIndex<'T>) = x.tag, x.index    
let tokToTaggedIdx f nbits tok = 
    let tagmask = 
        if nbits = 1 then 1 
        elif nbits = 2 then 3 
        elif nbits = 3 then 7 
        elif nbits = 4 then 15 
           elif nbits = 5 then 31 
           else failwith "too many nbits"
    let tag = tok &&& tagmask
    let idx = tok >>>& nbits
    TaggedIndex(f tag, idx) 
       
//---------------------------------------------------------------------
// Read file from memory blocks 
//---------------------------------------------------------------------


type ByteFile(bytes:byte[]) = 

    static member OpenIn f = ByteFile(File.ReadAllBytes f)
    static member OpenBytes bytes = ByteFile(bytes)

    member mc.ReadByte addr = bytes.[addr]
    member mc.ReadBytes addr len = Array.sub bytes addr len
    member m.CountUtf8String addr = 
        let mutable p = addr
        while bytes.[p] <> 0uy do
            p <- p + 1
        p - addr

    member m.ReadUTF8String addr = 
        let n = m.CountUtf8String addr 
        System.Text.Encoding.UTF8.GetString (bytes, addr, n)

    member is.ReadInt32 addr = 
        let b0 = is.ReadByte addr
        let b1 = is.ReadByte (addr+1)
        let b2 = is.ReadByte (addr+2)
        let b3 = is.ReadByte (addr+3)
        int b0 ||| (int b1 <<< 8) ||| (int b2 <<< 16) ||| (int b3 <<< 24)

    member is.ReadUInt16 addr = 
        let b0 = is.ReadByte addr
        let b1 = is.ReadByte (addr+1)
        uint16 b0 ||| (uint16 b1 <<< 8) 

let seekReadByte (is:ByteFile) addr = is.ReadByte addr
let seekReadBytes (is:ByteFile) addr len = is.ReadBytes addr len
let seekReadInt32 (is:ByteFile) addr = is.ReadInt32 addr
let seekReadUInt16 (is:ByteFile) addr = is.ReadUInt16 addr

let seekReadByteAsInt32 is addr = int32 (seekReadByte is addr)

let seekReadInt64 is addr = 
    let b0 = seekReadByte is addr
    let b1 = seekReadByte is (addr+1)
    let b2 = seekReadByte is (addr+2)
    let b3 = seekReadByte is (addr+3)
    let b4 = seekReadByte is (addr+4)
    let b5 = seekReadByte is (addr+5)
    let b6 = seekReadByte is (addr+6)
    let b7 = seekReadByte is (addr+7)
    int64 b0 ||| (int64 b1 <<< 8) ||| (int64 b2 <<< 16) ||| (int64 b3 <<< 24) |||
    (int64 b4 <<< 32) ||| (int64 b5 <<< 40) ||| (int64 b6 <<< 48) ||| (int64 b7 <<< 56)

let seekReadUInt16AsInt32 is addr = int32 (seekReadUInt16 is addr)

let seekReadCompressedUInt32 is addr = 
    let b0 = seekReadByte is addr
    if b0 <= 0x7Fuy then int b0, addr+1
    elif b0 <= 0xBFuy then 
        let b0 = b0 &&& 0x7Fuy
        let b1 = seekReadByteAsInt32 is (addr+1) 
        (int b0 <<< 8) ||| int b1, addr+2
    else 
        let b0 = b0 &&& 0x3Fuy
        let b1 = seekReadByteAsInt32 is (addr+1) 
        let b2 = seekReadByteAsInt32 is (addr+2) 
        let b3 = seekReadByteAsInt32 is (addr+3) 
        (int b0 <<< 24) ||| (int b1 <<< 16) ||| (int b2 <<< 8) ||| int b3, addr+4

let seekReadSByte         is addr = sbyte (seekReadByte is addr)
    
let rec seekCountUtf8String is addr n = 
    let c = seekReadByteAsInt32 is addr
    if c = 0 then n 
    else seekCountUtf8String is (addr+1) (n+1)

let seekReadUTF8String is addr = 
    let n = seekCountUtf8String is addr 0
    let bytes = seekReadBytes is addr n
    System.Text.Encoding.UTF8.GetString (bytes, 0, bytes.Length)

let seekReadBlob is addr = 
    let len, addr = seekReadCompressedUInt32 is addr
    seekReadBytes is addr len
    
let seekReadUserString is addr = 
    let len, addr = seekReadCompressedUInt32 is addr
    let bytes = seekReadBytes is addr (len - 1)
    System.Text.Encoding.Unicode.GetString(bytes, 0, bytes.Length)
    
let seekReadGuid is addr =  seekReadBytes is addr 0x10

let seekReadUncodedToken is addr  = 
    i32ToUncodedToken (seekReadInt32 is addr)

    
//---------------------------------------------------------------------
// Primitives to help read signatures.  These do not use the file cursor
//---------------------------------------------------------------------

let sigptrGetByte (bytes:byte[]) sigptr = 
    bytes.[sigptr], sigptr + 1

let sigptrGetBool bytes sigptr = 
    let b0,sigptr = sigptrGetByte bytes sigptr
    (b0 = 0x01uy) ,sigptr

let sigptrGetSByte bytes sigptr = 
    let i,sigptr = sigptrGetByte bytes sigptr
    sbyte i,sigptr

let sigptrGetUInt16 bytes sigptr = 
    let b0,sigptr = sigptrGetByte bytes sigptr
    let b1,sigptr = sigptrGetByte bytes sigptr
    uint16 (int b0 ||| (int b1 <<< 8)),sigptr

let sigptrGetInt16 bytes sigptr = 
    let u,sigptr = sigptrGetUInt16 bytes sigptr
    int16 u,sigptr

let sigptrGetInt32 (bytes: byte[]) sigptr = 
    let b0 = bytes.[sigptr]
    let b1 = bytes.[sigptr+1]
    let b2 = bytes.[sigptr+2]
    let b3 = bytes.[sigptr+3]
    let res = int b0 ||| (int b1 <<< 8) ||| (int b2 <<< 16) ||| (int b3 <<< 24)
    res, sigptr + 4

let sigptrGetUInt32 bytes sigptr = 
    let u,sigptr = sigptrGetInt32 bytes sigptr
    uint32 u,sigptr

let sigptrGetUInt64 bytes sigptr = 
    let u0,sigptr = sigptrGetUInt32 bytes sigptr
    let u1,sigptr = sigptrGetUInt32 bytes sigptr
    (uint64 u0 ||| (uint64 u1 <<< 32)),sigptr

let sigptrGetInt64 bytes sigptr = 
    let u,sigptr = sigptrGetUInt64 bytes sigptr
    int64 u,sigptr

let sigptrGetSingle bytes sigptr = 
    let u,sigptr = sigptrGetInt32 bytes sigptr
    singleOfBits u,sigptr

let sigptrGetDouble bytes sigptr = 
    let u,sigptr = sigptrGetInt64 bytes sigptr
    doubleOfBits u,sigptr

let sigptrGetZInt32 bytes sigptr = 
    let b0,sigptr = sigptrGetByte bytes sigptr
    if b0 <= 0x7Fuy then int b0, sigptr
    elif b0 <= 0xBFuy then 
        let b0 = b0 &&& 0x7Fuy
        let b1,sigptr = sigptrGetByte bytes sigptr
        (int b0 <<< 8) ||| int b1, sigptr
    else 
        let b0 = b0 &&& 0x3Fuy
        let b1,sigptr = sigptrGetByte bytes sigptr
        let b2,sigptr = sigptrGetByte bytes sigptr
        let b3,sigptr = sigptrGetByte bytes sigptr
        (int b0 <<< 24) ||| (int  b1 <<< 16) ||| (int b2 <<< 8) ||| int b3, sigptr
         
let rec sigptrFoldAcc f n (bytes:byte[]) (sigptr:int) i acc = 
    if i < n then 
        let x,sp = f bytes sigptr
        sigptrFoldAcc f n bytes sp (i+1) (x::acc)
    else 
        Array.ofList (List.rev acc), sigptr

let sigptrFold f n (bytes:byte[]) (sigptr:int) = 
    sigptrFoldAcc f n bytes sigptr 0 []

let sigptrGetBytes n (bytes:byte[]) sigptr = 
        let res = Array.zeroCreate n
        for i = 0 to (n - 1) do 
            res.[i] <- bytes.[sigptr + i]
        res, sigptr + n

let sigptrGetString n bytes sigptr = 
    let bytearray,sigptr = sigptrGetBytes n bytes sigptr
    (System.Text.Encoding.UTF8.GetString(bytearray, 0, bytearray.Length)),sigptr
  
//---------------------------------------------------------------------
// 
//---------------------------------------------------------------------

type ImageChunk = { size: int32; addr: int32 }

let chunk sz next = ({addr=next; size=sz},next + sz) 
let nochunk next = ({addr= 0x0;size= 0x0; } ,next)

type RowElementKind = 
    | UShort 
    | ULong 
    | Byte 
    | Data 
    | GGuid 
    | Blob 
    | SString 
    | SimpleIndex of TableName
    | TypeDefOrRefOrSpec
    | TypeOrMethodDef
    | HasConstant 
    | HasCustomAttribute
    | HasFieldMarshal 
    | HasDeclSecurity 
    | MemberRefParent 
    | HasSemantics 
    | MethodDefOrRef
    | MemberForwarded
    | Implementation 
    | CustomAttributeType
    | ResolutionScope

type RowKind = RowKind of RowElementKind list

let kindAssemblyRef            = RowKind [ UShort; UShort; UShort; UShort; ULong; Blob; SString; SString; Blob; ]
let kindModuleRef              = RowKind [ SString ]
let kindFileRef                = RowKind [ ULong; SString; Blob ]
let kindTypeRef                = RowKind [ ResolutionScope; SString; SString ]
let kindTypeSpec               = RowKind [ Blob ]
let kindTypeDef                = RowKind [ ULong; SString; SString; TypeDefOrRefOrSpec; SimpleIndex TableNames.Field; SimpleIndex TableNames.Method ]
let kindPropertyMap            = RowKind [ SimpleIndex TableNames.TypeDef; SimpleIndex TableNames.Property ]
let kindEventMap               = RowKind [ SimpleIndex TableNames.TypeDef; SimpleIndex TableNames.Event ]
let kindInterfaceImpl          = RowKind [ SimpleIndex TableNames.TypeDef; TypeDefOrRefOrSpec ]
let kindNested                 = RowKind [ SimpleIndex TableNames.TypeDef; SimpleIndex TableNames.TypeDef ]
let kindCustomAttribute        = RowKind [ HasCustomAttribute; CustomAttributeType; Blob ]
let kindDeclSecurity           = RowKind [ UShort; HasDeclSecurity; Blob ]
let kindMemberRef              = RowKind [ MemberRefParent; SString; Blob ]
let kindStandAloneSig          = RowKind [ Blob ]
let kindFieldDef               = RowKind [ UShort; SString; Blob ]
let kindFieldRVA               = RowKind [ Data; SimpleIndex TableNames.Field ]
let kindFieldMarshal           = RowKind [ HasFieldMarshal; Blob ]
let kindConstant               = RowKind [ UShort;HasConstant; Blob ]
let kindFieldLayout            = RowKind [ ULong; SimpleIndex TableNames.Field ]
let kindParam                  = RowKind [ UShort; UShort; SString ]
let kindMethodDef              = RowKind [ ULong;  UShort; UShort; SString; Blob; SimpleIndex TableNames.Param ]
let kindMethodImpl             = RowKind [ SimpleIndex TableNames.TypeDef; MethodDefOrRef; MethodDefOrRef ]
let kindImplMap                = RowKind [ UShort; MemberForwarded; SString; SimpleIndex TableNames.ModuleRef ]
let kindMethodSemantics        = RowKind [ UShort; SimpleIndex TableNames.Method; HasSemantics ]
let kindProperty               = RowKind [ UShort; SString; Blob ]
let kindEvent                  = RowKind [ UShort; SString; TypeDefOrRefOrSpec ]
let kindManifestResource       = RowKind [ ULong; ULong; SString; Implementation ]
let kindClassLayout            = RowKind [ UShort; ULong; SimpleIndex TableNames.TypeDef ]
let kindExportedType           = RowKind [ ULong; ULong; SString; SString; Implementation ]
let kindAssembly               = RowKind [ ULong; UShort; UShort; UShort; UShort; ULong; Blob; SString; SString ]
let kindGenericParam_v1_1      = RowKind [ UShort; UShort; TypeOrMethodDef; SString; TypeDefOrRefOrSpec ]
let kindGenericParam_v2_0      = RowKind [ UShort; UShort; TypeOrMethodDef; SString ]
let kindMethodSpec             = RowKind [ MethodDefOrRef; Blob ]
let kindGenericParamConstraint = RowKind [ SimpleIndex TableNames.GenericParam; TypeDefOrRefOrSpec ]
let kindModule                 = RowKind [ UShort; SString; GGuid; GGuid; GGuid ]
let kindIllegal                = RowKind [ ]

//---------------------------------------------------------------------
// Used for binary searches of sorted tables.  Each function that reads
// a table row returns a tuple that contains the elements of the row.
// One of these elements may be a key for a sorted table.  These
// keys can be compared using the functions below depending on the
// kind of element in that column.
//---------------------------------------------------------------------

let hcCompare (TaggedIndex((t1: HasConstantTag), (idx1:int))) (TaggedIndex((t2: HasConstantTag), idx2)) = 
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let hsCompare (TaggedIndex((t1:HasSemanticsTag), (idx1:int))) (TaggedIndex((t2:HasSemanticsTag), idx2)) = 
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let hcaCompare (TaggedIndex((t1:HasCustomAttributeTag), (idx1:int))) (TaggedIndex((t2:HasCustomAttributeTag), idx2)) = 
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let mfCompare (TaggedIndex((t1:MemberForwardedTag), (idx1:int))) (TaggedIndex((t2:MemberForwardedTag), idx2)) = 
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let hdsCompare (TaggedIndex((t1:HasDeclSecurityTag), (idx1:int))) (TaggedIndex((t2:HasDeclSecurityTag), idx2)) = 
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let hfmCompare (TaggedIndex((t1:HasFieldMarshalTag), idx1)) (TaggedIndex((t2:HasFieldMarshalTag), idx2)) = 
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let tomdCompare (TaggedIndex((t1:TypeOrMethodDefTag), idx1)) (TaggedIndex((t2:TypeOrMethodDefTag), idx2)) = 
    if idx1 < idx2 then -1 elif idx1 > idx2 then 1 else compare t1.Tag t2.Tag

let simpleIndexCompare (idx1:int) (idx2:int) = 
    compare idx1 idx2

//---------------------------------------------------------------------
// The various keys for the various caches.  
//---------------------------------------------------------------------

type TypeDefAsTypIdx = TypeDefAsTypIdx of ILBoxity * ILGenericArgs * int
type TypeRefAsTypIdx = TypeRefAsTypIdx of ILBoxity * ILGenericArgs * int
type BlobAsMethodSigIdx = BlobAsMethodSigIdx of int * int32
type BlobAsFieldSigIdx = BlobAsFieldSigIdx of int * int32
type BlobAsPropSigIdx = BlobAsPropSigIdx of int * int32
type BlobAsLocalSigIdx = BlobAsLocalSigIdx of int * int32
type MemberRefAsMspecIdx =  MemberRefAsMspecIdx of int * int
type MethodSpecAsMspecIdx =  MethodSpecAsMspecIdx of int * int
type MemberRefAsFspecIdx = MemberRefAsFspecIdx of int * int
type CustomAttrIdx = CustomAttrIdx of CustomAttributeTypeTag * int * int32
type SecurityDeclIdx   = SecurityDeclIdx of uint16 * int32
type GenericParamsIdx = GenericParamsIdx of int * TypeOrMethodDefTag * int

//---------------------------------------------------------------------
// Polymorphic caches for row and heap readers
//---------------------------------------------------------------------

let mkCacheInt32 lowMem _inbase _nm _sz  =
    if lowMem then (fun f x -> f x) else
    let cache = ref null 
    fun f (idx:int32) ->
        let cache = 
            match !cache with
            | null -> cache :=  new Dictionary<int32,_>(11)
            | _ -> ()
            !cache
        let mutable res = Unchecked.defaultof<_>
        let ok = cache.TryGetValue(idx, &res)
        if ok then 
            res
        else 
            let res = f idx 
            cache.[idx] <- res; 
            res 

let mkCacheGeneric lowMem _inbase _nm _sz  =
    if lowMem then (fun f x -> f x) else
    let cache = ref null 
    fun f (idx :'T) ->
        let cache = 
            match !cache with
            | null -> cache := new Dictionary<_,_>(11 (* sz:int *) ) 
            | _ -> ()
            !cache
        if cache.ContainsKey idx then cache.[idx]
        else let res = f idx in cache.[idx] <- res; res 

//-----------------------------------------------------------------------
// Polymorphic general helpers for searching for particular rows.
// ----------------------------------------------------------------------

let seekFindRow numRows rowChooser =
    let mutable i = 1
    while (i <= numRows &&  not (rowChooser i)) do 
        i <- i + 1;
    i  

// search for rows satisfying predicate 
let seekReadIndexedRows (numRows, rowReader, keyFunc, keyComparer, binaryChop, rowConverter) =
    if binaryChop then
        let mutable low = 0
        let mutable high = numRows + 1
        begin 
          let mutable fin = false
          while not fin do 
              if high - low <= 1  then 
                  fin <- true 
              else 
                  let mid = (low + high) / 2
                  let midrow = rowReader mid
                  let c = keyComparer (keyFunc midrow)
                  if c > 0 then 
                      low <- mid
                  elif c < 0 then 
                      high <- mid 
                  else 
                      fin <- true
        end;
        let mutable res = []
        if high - low > 1 then 
            // now read off rows, forward and backwards 
            let mid = (low + high) / 2
            // read forward 
            begin 
                let mutable fin = false
                let mutable curr = mid
                while not fin do 
                  if curr > numRows then 
                      fin <- true;
                  else 
                      let currrow = rowReader curr
                      if keyComparer (keyFunc currrow) = 0 then 
                          res <- rowConverter currrow :: res;
                      else 
                          fin <- true;
                      curr <- curr + 1;
                done;
            end;
            res <- List.rev res;
            // read backwards 
            begin 
                let mutable fin = false
                let mutable curr = mid - 1
                while not fin do 
                  if curr = 0 then 
                    fin <- true
                  else  
                    let currrow = rowReader curr
                    if keyComparer (keyFunc currrow) = 0 then 
                        res <- rowConverter currrow :: res;
                    else 
                        fin <- true;
                    curr <- curr - 1;
            end;
        res |> List.toArray
    else 
        let res = ref []
        for i = 1 to numRows do
            let rowinfo = rowReader i
            if keyComparer (keyFunc rowinfo) = 0 then 
              res := rowConverter rowinfo :: !res;
        List.rev !res  |> List.toArray


let seekReadOptionalIndexedRow (info) =
    match seekReadIndexedRows info with 
    | [| |] -> None
    | xs -> Some xs.[0]
        
let seekReadIndexedRow (info) =
    match seekReadOptionalIndexedRow info with 
    | Some row -> row
    | None -> failwith ("no row found for key when indexing table")


type ILVarArgs = ILTypes option
type MethodData = MethodData of ILType * ILCallingConv * string * ILTypes * ILType * ILTypes
type VarArgMethodData = VarArgMethodData of ILType * ILCallingConv * string * ILTypes * ILVarArgs * ILType * ILTypes

  
let getName (ltd: Lazy<ILTypeDef>) = 
    let td = ltd.Force()
    (td.Name,ltd)


let mkILTy boxed tspec = 
    match boxed with 
    | AsObject -> ILType.Boxed tspec 
    | _ -> ILType.Value tspec

let mkILArr1DTy ty = ILType.Array (ILArrayShape.SingleDimensional, ty)

let typeNameForGlobalFunctions = "<Module>"

let mkILNonGenericTySpec tref =  ILTypeSpec (tref,[| |])
let mkILTypeForGlobalFunctions scoref = ILType.Boxed (mkILNonGenericTySpec (ILTypeRef(ILTypeRefScope.Top scoref, None, typeNameForGlobalFunctions)))

let mkILMethSpecInTyRaw (typ:ILType, cc, nm, args, rty, minst:ILGenericArgs) =
    ILMethodSpec (ILMethodRef (typ.TypeRef,cc,minst.Length,nm,args,rty),typ,minst)

let mkILFieldsLazy l =  Fields (LazyOrderedMultiMap((fun (f:ILFieldDef) -> f.Name),l))
let mkILEventsLazy l =  Events (LazyOrderedMultiMap((fun (e: ILEventDef) -> e.Name),l))
let mkILPropertiesLazy l =  Properties (LazyOrderedMultiMap((fun (p: ILPropertyDef) -> p.Name),l) )
let addILMethodToMap (y: ILMethodDef) tab =
    let key = y.Name
    let prev = tryFindMulti key tab
    Map.add key (Array.append [| y |] prev) tab

let mkILMethodsLazy (ltab : Lazy<_>) =  ILMethodDefs (ltab, lazy (Array.foldBack addILMethodToMap (ltab.Force()) Map.empty))

let mkILFieldSpecInTy (typ:ILType,nm,fty) = 
    ILFieldSpec (ILFieldRef (typ.TypeRef,nm,fty), typ)

let mkILFormalGenericArgsRaw (gparams:ILGenericParameterDefs)  =
    gparams |> Array.mapi (fun n _gf -> ILType.Var n) 

//---------------------------------------------------------------------
// The big fat reader.
//---------------------------------------------------------------------

let mkILGlobals systemRuntimeScopeRef = 
      let mkILTyspec nsp nm =  mkILNonGenericTySpec(ILTypeRef(ILTypeRefScope.Top(systemRuntimeScopeRef),Some nsp,nm))
      { typ_Object = ILType.Boxed (mkILTyspec "System" "Object")
        typ_String = ILType.Boxed (mkILTyspec "System" "String")
        typ_Type = ILType.Boxed (mkILTyspec "System" "Type")
        typ_Int64 = ILType.Value (mkILTyspec "System" "Int64")
        typ_UInt64 = ILType.Value (mkILTyspec "System" "UInt64")
        typ_Int32 = ILType.Value (mkILTyspec "System" "Int32")
        typ_UInt32 = ILType.Value (mkILTyspec "System" "UInt32")
        typ_Int16 = ILType.Value (mkILTyspec "System" "Int16")
        typ_UInt16 = ILType.Value (mkILTyspec "System" "UInt16")
        typ_SByte = ILType.Value (mkILTyspec "System" "SByte")
        typ_Byte = ILType.Value (mkILTyspec "System" "Byte")
        typ_Single = ILType.Value (mkILTyspec "System" "Single")
        typ_Double = ILType.Value (mkILTyspec "System" "Double")
        typ_Boolean = ILType.Value (mkILTyspec "System" "Boolean")
        typ_Char = ILType.Value (mkILTyspec "System" "Char")
        typ_IntPtr = ILType.Value (mkILTyspec "System" "IntPtr")
        typ_TypedReference = Some (ILType.Value (mkILTyspec "System" "TypedReference"))
        typ_UIntPtr = ILType.Value (mkILTyspec "System" "UIntPtr")
        systemRuntimeScopeRef = systemRuntimeScopeRef }

type ILModuleReader(infile: string, is: ByteFile, ilg: ILGlobals) =
      
    //-----------------------------------------------------------------------
    // Crack the binary headers, build a reader context and return the lazy
    // read of the AbsIL module.
    // ----------------------------------------------------------------------

    (* MSDOS HEADER *)
    let peSignaturePhysLoc = seekReadInt32 is 0x3c

    (* PE HEADER *)
    let peFileHeaderPhysLoc = peSignaturePhysLoc + 0x04
    let peOptionalHeaderPhysLoc = peFileHeaderPhysLoc + 0x14
    let peSignature = seekReadInt32 is (peSignaturePhysLoc + 0)
    do if peSignature <>  0x4550 then failwithf "not a PE file - bad magic PE number 0x%08x, is = %A" peSignature is;


    (* PE SIGNATURE *)
    //let machine = seekReadUInt16AsInt32 is (peFileHeaderPhysLoc + 0)
    let numSections = seekReadUInt16AsInt32 is (peFileHeaderPhysLoc + 2)
    let optHeaderSize = seekReadUInt16AsInt32 is (peFileHeaderPhysLoc + 16)
    do if optHeaderSize <>  0xe0 &&
         optHeaderSize <> 0xf0 then failwith "not a PE file - bad optional header size";
    let x64adjust = optHeaderSize - 0xe0
    //let only64 = (optHeaderSize = 0xf0)    (* May want to read in the optional header Magic number and check that as well... *)
    //let platform = match machine with | 0x8664 -> Some(AMD64) | 0x200 -> Some(IA64) | _ -> Some(X86) 
    let sectionHeadersStartPhysLoc = peOptionalHeaderPhysLoc + optHeaderSize

    //let flags = seekReadUInt16AsInt32 is (peFileHeaderPhysLoc + 18)
    //let isDll = (flags &&& 0x2000) <> 0x0

    (* OPTIONAL PE HEADER *)
    (* x86: 000000a0 *) 
    (* x86: 000000b0 *) 
    //let dataSegmentAddr       = seekReadInt32 is (peOptionalHeaderPhysLoc + 24) (* e.g. 0x0000c000 *)
    //let imageBaseReal = if only64 then dataSegmentAddr else seekReadInt32 is (peOptionalHeaderPhysLoc + 28)  (* Image Base Always 0x400000 (see Section 23.1). - QUERY : no it's not always 0x400000, e.g. 0x034f0000 *)
    //let alignVirt      = seekReadInt32 is (peOptionalHeaderPhysLoc + 32)   (*  Section Alignment Always 0x2000 (see Section 23.1). *)
    //let alignPhys      = seekReadInt32 is (peOptionalHeaderPhysLoc + 36)  (* File Alignment Either 0x200 or 0x1000. *)
    (* x86: 000000c0 *) 
    //let subsysMajor = seekReadUInt16AsInt32 is (peOptionalHeaderPhysLoc + 48)   (* SubSys Major Always 4 (see Section 23.1). *)
    //let subsysMinor = seekReadUInt16AsInt32 is (peOptionalHeaderPhysLoc + 50)   (* SubSys Minor Always 0 (see Section 23.1). *)
    (* x86: 000000d0 *) 
    //let subsys           = seekReadUInt16 is (peOptionalHeaderPhysLoc + 68)   (* SubSystem Subsystem required to run this image. Shall be either IMAGE_SUBSYSTEM_WINDOWS_CE_GUI (!0x3) or IMAGE_SUBSYSTEM_WINDOWS_GUI (!0x2). QUERY: Why is this 3 on the images ILASM produces??? *)
    //let useHighEntropyVA = 
    //    let n = seekReadUInt16 is (peOptionalHeaderPhysLoc + 70)
    //    let highEnthropyVA = 0x20us
    //    (n &&& highEnthropyVA) = highEnthropyVA

     (* x86: 000000e0 *) 
     (* x86: 000000f0, x64: 00000100 *) 
     (* x86: 00000100 - these addresses are for x86 - for the x64 location, add x64adjust (0x10) *) 
     (* x86: 00000110 *) 
     (* x86: 00000120 *) 
     (* x86: 00000130 *) 
     (* x86: 00000140 *) 
     (* x86: 00000150 *) 
     (* x86: 00000160 *) 
    let cliHeaderAddr = seekReadInt32 is (peOptionalHeaderPhysLoc + 208 + x64adjust)
    
    let anyV2P (n,v) = 
      let rec look i pos = 
        if i >= numSections then (failwith (infile + ": bad "+n+", rva "+string v); 0x0)
        else
          let virtSize = seekReadInt32 is (pos + 8)
          let virtAddr = seekReadInt32 is (pos + 12)
          let physLoc = seekReadInt32 is (pos + 20)
          if (v >= virtAddr && (v < virtAddr + virtSize)) then (v - virtAddr) + physLoc 
          else look (i+1) (pos + 0x28)
      look 0 sectionHeadersStartPhysLoc

    let cliHeaderPhysLoc = anyV2P ("cli header",cliHeaderAddr)

    let metadataAddr         = seekReadInt32 is (cliHeaderPhysLoc + 8)
    //let cliFlags             = seekReadInt32 is (cliHeaderPhysLoc + 16)
    //let ilOnly             = (cliFlags &&& 0x01) <> 0x00
    //let only32             = (cliFlags &&& 0x02) <> 0x00
    //let is32bitpreferred   = (cliFlags &&& 0x00020003) <> 0x00
    
    let entryPointToken = seekReadUncodedToken is (cliHeaderPhysLoc + 20)
    let resourcesAddr     = seekReadInt32 is (cliHeaderPhysLoc + 24)

    let metadataPhysLoc = anyV2P ("metadata",metadataAddr)
    let magic = seekReadUInt16AsInt32 is metadataPhysLoc
    do if magic <> 0x5342 then failwith (infile + ": bad metadata magic number: " + string magic);
    let magic2 = seekReadUInt16AsInt32 is (metadataPhysLoc + 2)
    do if magic2 <> 0x424a then failwith "bad metadata magic number";

    let versionLength = seekReadInt32 is (metadataPhysLoc + 12)
    //let ilMetadataVersion = seekReadBytes is (metadataPhysLoc + 16) versionLength |> Array.filter (fun b -> b <> 0uy)
    let x = align 0x04 (16 + versionLength)
    let numStreams = seekReadUInt16AsInt32 is (metadataPhysLoc + x + 2)
    let streamHeadersStart = (metadataPhysLoc + x + 4)

    (* Crack stream headers *)

    let tryFindStream name = 
      let rec look i pos = 
        if i >= numStreams then None
        else
          let offset = seekReadInt32 is (pos + 0)
          let length = seekReadInt32 is (pos + 4)
          let res = ref true
          let fin = ref false
          let n = ref 0
          // read and compare the stream name byte by byte 
          while (not !fin) do 
              let c= seekReadByteAsInt32 is (pos + 8 + (!n))
              if c = 0 then 
                  fin := true
              elif !n >= Array.length name || c <> name.[!n] then 
                  res := false;
              incr n
          if !res then Some(offset + metadataPhysLoc,length) 
          else look (i+1) (align 0x04 (pos + 8 + (!n)))
      look 0 streamHeadersStart

    let findStream name = 
        match tryFindStream name with
        | None -> (0x0, 0x0)
        | Some positions ->  positions

    let (tablesStreamPhysLoc, _tablesStreamSize) = 
      match tryFindStream [| 0x23; 0x7e |] (* #~ *) with
      | Some res -> res
      | None -> 
        match tryFindStream [| 0x23; 0x2d |] (* #-: at least one DLL I've seen uses this! *)   with
        | Some res -> res
        | None -> 
         let firstStreamOffset = seekReadInt32 is (streamHeadersStart + 0)
         let firstStreamLength = seekReadInt32 is (streamHeadersStart + 4)
         firstStreamOffset,firstStreamLength

    let (stringsStreamPhysicalLoc, stringsStreamSize) = findStream [| 0x23; 0x53; 0x74; 0x72; 0x69; 0x6e; 0x67; 0x73; |] (* #Strings *)
    let (blobsStreamPhysicalLoc, blobsStreamSize) = findStream [| 0x23; 0x42; 0x6c; 0x6f; 0x62; |] (* #Blob *)

    let tables_streamMajor_version = seekReadByteAsInt32 is (tablesStreamPhysLoc + 4)
    let tables_streamMinor_version = seekReadByteAsInt32 is (tablesStreamPhysLoc + 5)

    let usingWhidbeyBeta1TableSchemeForGenericParam = (tables_streamMajor_version = 1) && (tables_streamMinor_version = 1)

    let tableKinds = 
        [|kindModule               (* Table 0  *); 
          kindTypeRef              (* Table 1  *);
          kindTypeDef              (* Table 2  *);
          kindIllegal (* kindFieldPtr *)             (* Table 3  *);
          kindFieldDef                (* Table 4  *);
          kindIllegal (* kindMethodPtr *)            (* Table 5  *);
          kindMethodDef               (* Table 6  *);
          kindIllegal (* kindParamPtr *)             (* Table 7  *);
          kindParam                (* Table 8  *);
          kindInterfaceImpl        (* Table 9  *);
          kindMemberRef            (* Table 10 *);
          kindConstant             (* Table 11 *);
          kindCustomAttribute      (* Table 12 *);
          kindFieldMarshal         (* Table 13 *);
          kindDeclSecurity         (* Table 14 *);
          kindClassLayout          (* Table 15 *);
          kindFieldLayout          (* Table 16 *);
          kindStandAloneSig        (* Table 17 *);
          kindEventMap             (* Table 18 *);
          kindIllegal (* kindEventPtr *)             (* Table 19 *);
          kindEvent                (* Table 20 *);
          kindPropertyMap          (* Table 21 *);
          kindIllegal (* kindPropertyPtr *)          (* Table 22 *);
          kindProperty             (* Table 23 *);
          kindMethodSemantics      (* Table 24 *);
          kindMethodImpl           (* Table 25 *);
          kindModuleRef            (* Table 26 *);
          kindTypeSpec             (* Table 27 *);
          kindImplMap              (* Table 28 *);
          kindFieldRVA             (* Table 29 *);
          kindIllegal (* kindENCLog *)               (* Table 30 *);
          kindIllegal (* kindENCMap *)               (* Table 31 *);
          kindAssembly             (* Table 32 *);
          kindIllegal (* kindAssemblyProcessor *)    (* Table 33 *);
          kindIllegal (* kindAssemblyOS *)           (* Table 34 *);
          kindAssemblyRef          (* Table 35 *);
          kindIllegal (* kindAssemblyRefProcessor *) (* Table 36 *);
          kindIllegal (* kindAssemblyRefOS *)        (* Table 37 *);
          kindFileRef                 (* Table 38 *);
          kindExportedType         (* Table 39 *);
          kindManifestResource     (* Table 40 *);
          kindNested               (* Table 41 *);
         (if usingWhidbeyBeta1TableSchemeForGenericParam then kindGenericParam_v1_1 else  kindGenericParam_v2_0);        (* Table 42 *)
          kindMethodSpec         (* Table 43 *);
          kindGenericParamConstraint         (* Table 44 *);
          kindIllegal         (* Table 45 *);
          kindIllegal         (* Table 46 *);
          kindIllegal         (* Table 47 *);
          kindIllegal         (* Table 48 *);
          kindIllegal         (* Table 49 *);
          kindIllegal         (* Table 50 *);
          kindIllegal         (* Table 51 *);
          kindIllegal         (* Table 52 *);
          kindIllegal         (* Table 53 *);
          kindIllegal         (* Table 54 *);
          kindIllegal         (* Table 55 *);
          kindIllegal         (* Table 56 *);
          kindIllegal         (* Table 57 *);
          kindIllegal         (* Table 58 *);
          kindIllegal         (* Table 59 *);
          kindIllegal         (* Table 60 *);
          kindIllegal         (* Table 61 *);
          kindIllegal         (* Table 62 *);
          kindIllegal         (* Table 63 *);
        |]

    let heapSizes = seekReadByteAsInt32 is (tablesStreamPhysLoc + 6)
    let valid = seekReadInt64 is (tablesStreamPhysLoc + 8)
    let sorted = seekReadInt64 is (tablesStreamPhysLoc + 16)
    let tableRowCount, startOfTables = 
        let numRows = Array.create 64 0
        let prevNumRowIdx = ref (tablesStreamPhysLoc + 24)
        for i = 0 to 63 do 
            if (valid &&& (int64 1 <<< i)) <> int64  0 then 
                numRows.[i] <-  (seekReadInt32 is !prevNumRowIdx);
                prevNumRowIdx := !prevNumRowIdx + 4
        numRows, !prevNumRowIdx

    let getNumRows (tab:TableName) = tableRowCount.[tab.Index]
    let stringsBigness = (heapSizes &&& 1) <> 0
    let guidsBigness = (heapSizes &&& 2) <> 0
    let blobsBigness = (heapSizes &&& 4) <> 0

    let tableBigness = Array.map (fun n -> n >= 0x10000) tableRowCount
      
    let codedBigness nbits tab =
      let rows = getNumRows tab
      rows >= (0x10000 >>>& nbits)
    
    let tdorBigness = 
      codedBigness 2 TableNames.TypeDef || 
      codedBigness 2 TableNames.TypeRef || 
      codedBigness 2 TableNames.TypeSpec
    
    let tomdBigness = 
      codedBigness 1 TableNames.TypeDef || 
      codedBigness 1 TableNames.Method
    
    let hcBigness = 
      codedBigness 2 TableNames.Field ||
      codedBigness 2 TableNames.Param ||
      codedBigness 2 TableNames.Property
    
    let hcaBigness = 
      codedBigness 5 TableNames.Method ||
      codedBigness 5 TableNames.Field ||
      codedBigness 5 TableNames.TypeRef  ||
      codedBigness 5 TableNames.TypeDef ||
      codedBigness 5 TableNames.Param ||
      codedBigness 5 TableNames.InterfaceImpl ||
      codedBigness 5 TableNames.MemberRef ||
      codedBigness 5 TableNames.Module ||
      codedBigness 5 TableNames.Permission ||
      codedBigness 5 TableNames.Property ||
      codedBigness 5 TableNames.Event ||
      codedBigness 5 TableNames.StandAloneSig ||
      codedBigness 5 TableNames.ModuleRef ||
      codedBigness 5 TableNames.TypeSpec ||
      codedBigness 5 TableNames.Assembly ||
      codedBigness 5 TableNames.AssemblyRef ||
      codedBigness 5 TableNames.File ||
      codedBigness 5 TableNames.ExportedType ||
      codedBigness 5 TableNames.ManifestResource ||
      codedBigness 5 TableNames.GenericParam ||
      codedBigness 5 TableNames.GenericParamConstraint ||
      codedBigness 5 TableNames.MethodSpec

    
    let hfmBigness = 
      codedBigness 1 TableNames.Field || 
      codedBigness 1 TableNames.Param
    
    let hdsBigness = 
      codedBigness 2 TableNames.TypeDef || 
      codedBigness 2 TableNames.Method ||
      codedBigness 2 TableNames.Assembly
    
    let mrpBigness = 
      codedBigness 3 TableNames.TypeRef ||
      codedBigness 3 TableNames.ModuleRef ||
      codedBigness 3 TableNames.Method ||
      codedBigness 3 TableNames.TypeSpec
    
    let hsBigness = 
      codedBigness 1 TableNames.Event || 
      codedBigness 1 TableNames.Property 
    
    let mdorBigness =
      codedBigness 1 TableNames.Method ||    
      codedBigness 1 TableNames.MemberRef 
    
    let mfBigness =
      codedBigness 1 TableNames.Field ||
      codedBigness 1 TableNames.Method 
    
    let iBigness =
      codedBigness 2 TableNames.File || 
      codedBigness 2 TableNames.AssemblyRef ||    
      codedBigness 2 TableNames.ExportedType 
    
    let catBigness =  
      codedBigness 3 TableNames.Method ||    
      codedBigness 3 TableNames.MemberRef 
    
    let rsBigness = 
      codedBigness 2 TableNames.Module ||    
      codedBigness 2 TableNames.ModuleRef || 
      codedBigness 2 TableNames.AssemblyRef  ||
      codedBigness 2 TableNames.TypeRef
      
    let rowKindSize (RowKind kinds) = 
      kinds |> List.sumBy (fun x -> 
            match x with 
            | UShort -> 2
            | ULong -> 4
            | Byte -> 1
            | Data -> 4
            | GGuid -> (if guidsBigness then 4 else 2)
            | Blob  -> (if blobsBigness then 4 else 2)
            | SString  -> (if stringsBigness then 4 else 2)
            | SimpleIndex tab -> (if tableBigness.[tab.Index] then 4 else 2)
            | TypeDefOrRefOrSpec -> (if tdorBigness then 4 else 2)
            | TypeOrMethodDef -> (if tomdBigness then 4 else 2)
            | HasConstant  -> (if hcBigness then 4 else 2)
            | HasCustomAttribute -> (if hcaBigness then 4 else 2)
            | HasFieldMarshal  -> (if hfmBigness then 4 else 2)
            | HasDeclSecurity  -> (if hdsBigness then 4 else 2)
            | MemberRefParent  -> (if mrpBigness then 4 else 2)
            | HasSemantics  -> (if hsBigness then 4 else 2)
            | MethodDefOrRef -> (if mdorBigness then 4 else 2)
            | MemberForwarded -> (if mfBigness then 4 else 2)
            | Implementation  -> (if iBigness then 4 else 2)
            | CustomAttributeType -> (if catBigness then 4 else 2)
            | ResolutionScope -> (if rsBigness then 4 else 2)) 

    let tableRowSizes = tableKinds |> Array.map rowKindSize 

    let tablePhysLocations = 
         let res = Array.create 64 0x0
         let prevTablePhysLoc = ref startOfTables
         for i = 0 to 63 do 
             res.[i] <- !prevTablePhysLoc;
             prevTablePhysLoc := !prevTablePhysLoc + (tableRowCount.[i] * tableRowSizes.[i]);
         res
    
    let inbase = infile + ": "

    let lowMem = false
    // All the caches.  The sizes are guesstimates for the rough sharing-density of the assembly 
    let cacheAssemblyRef               = mkCacheInt32 lowMem inbase "ILAssemblyRef"  (getNumRows TableNames.AssemblyRef)
    let cacheMemberRefAsMemberData     = mkCacheGeneric lowMem inbase "MemberRefAsMemberData" (getNumRows TableNames.MemberRef / 20 + 1)
    let cacheCustomAttr                = mkCacheGeneric lowMem inbase "CustomAttr" (getNumRows TableNames.CustomAttribute / 50 + 1)
    let cacheTypeRef                   = mkCacheInt32 lowMem inbase "ILTypeRef" (getNumRows TableNames.TypeRef / 20 + 1)
    let cacheTypeRefAsType             = mkCacheGeneric lowMem inbase "TypeRefAsType" (getNumRows TableNames.TypeRef / 20 + 1)
    let cacheBlobHeapAsPropertySig     = mkCacheGeneric lowMem inbase "BlobHeapAsPropertySig" (getNumRows TableNames.Property / 20 + 1)
    let cacheBlobHeapAsFieldSig        = mkCacheGeneric lowMem inbase "BlobHeapAsFieldSig" (getNumRows TableNames.Field / 20 + 1)
    let cacheBlobHeapAsMethodSig       = mkCacheGeneric lowMem inbase "BlobHeapAsMethodSig" (getNumRows TableNames.Method / 20 + 1)
    let cacheTypeDefAsType             = mkCacheGeneric lowMem inbase "TypeDefAsType" (getNumRows TableNames.TypeDef / 20 + 1)
    let cacheMethodDefAsMethodData     = mkCacheInt32 lowMem inbase "MethodDefAsMethodData" (getNumRows TableNames.Method / 20 + 1)
    let cacheGenericParams             = mkCacheGeneric lowMem inbase "GenericParams" (getNumRows TableNames.GenericParam / 20 + 1)
    // nb. Lots and lots of cache hits on this cache, hence never optimize cache away 
    let cacheStringHeap                = mkCacheInt32 false inbase "string heap" ( stringsStreamSize / 50 + 1)
    let cacheBlobHeap                  = mkCacheInt32 lowMem inbase "blob heap" ( blobsStreamSize / 50 + 1) 

    let cacheNestedRow          = mkCacheInt32 lowMem inbase "Nested Table Rows" (getNumRows TableNames.Nested / 20 + 1)
    let cacheConstantRow        = mkCacheInt32 lowMem inbase "Constant Rows" (getNumRows TableNames.Constant / 20 + 1)
    let cacheMethodSemanticsRow = mkCacheInt32 lowMem inbase "MethodSemantics Rows" (getNumRows TableNames.MethodSemantics / 20 + 1)
    let cacheTypeDefRow         = mkCacheInt32 lowMem inbase "ILTypeDef Rows" (getNumRows TableNames.TypeDef / 20 + 1)
    let cacheInterfaceImplRow   = mkCacheInt32 lowMem inbase "InterfaceImpl Rows" (getNumRows TableNames.InterfaceImpl / 20 + 1)
    //let cacheFieldMarshalRow    = mkCacheInt32 lowMem inbase "FieldMarshal Rows" (getNumRows TableNames.FieldMarshal / 20 + 1)
    let cachePropertyMapRow     = mkCacheInt32 lowMem inbase "PropertyMap Rows" (getNumRows TableNames.PropertyMap / 20 + 1)

   //-----------------------------------------------------------------------

    let rowAddr (tab:TableName) idx = tablePhysLocations.[tab.Index] + (idx - 1) * tableRowSizes.[tab.Index]

    let seekReadUInt16Adv (addr: byref<int>) =  
        let res = seekReadUInt16 is addr
        addr <- addr + 2
        res

    let seekReadInt32Adv (addr: byref<int>) = 
        let res = seekReadInt32 is addr
        addr <- addr+4
        res

    let seekReadUInt16AsInt32Adv (addr: byref<int>) = 
        let res = seekReadUInt16AsInt32 is addr
        addr <- addr+2
        res

    let seekReadTaggedIdx f nbits big (addr: byref<int>) =  
        let tok = if big then seekReadInt32Adv &addr else seekReadUInt16AsInt32Adv &addr 
        tokToTaggedIdx f nbits tok


    let seekReadIdx big (addr: byref<int>) =  
        if big then seekReadInt32Adv &addr else seekReadUInt16AsInt32Adv &addr

    let seekReadUntaggedIdx (tab:TableName) (addr: byref<int>) =  
        seekReadIdx tableBigness.[tab.Index] &addr


    let seekReadResolutionScopeIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> ResolutionScopeTag idx)    2 rsBigness   &addr
    let seekReadTypeDefOrRefOrSpecIdx  (addr: byref<int>) = seekReadTaggedIdx (fun idx -> TypeDefOrRefOrSpecTag idx)  2 tdorBigness &addr   
    let seekReadTypeOrMethodDefIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> TypeOrMethodDefTag idx)    1 tomdBigness &addr
    let seekReadHasConstantIdx         (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasConstantTag idx)        2 hcBigness   &addr   
    let seekReadHasCustomAttributeIdx  (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasCustomAttributeTag idx)  5 hcaBigness  &addr
    //let seekReadHasFieldMarshalIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasFieldMarshalTag idx)    1 hfmBigness &addr
    //let seekReadHasDeclSecurityIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasDeclSecurityTag idx)    2 hdsBigness &addr
    let seekReadMemberRefParentIdx     (addr: byref<int>) = seekReadTaggedIdx (fun idx -> MemberRefParentTag idx)    3 mrpBigness &addr
    let seekReadHasSemanticsIdx        (addr: byref<int>) = seekReadTaggedIdx (fun idx -> HasSemanticsTag idx)       1 hsBigness &addr
    let seekReadMethodDefOrRefIdx      (addr: byref<int>) = seekReadTaggedIdx (fun idx -> MethodDefOrRefTag idx)     1 mdorBigness &addr
    let seekReadImplementationIdx      (addr: byref<int>) = seekReadTaggedIdx (fun idx -> ImplementationTag idx)     2 iBigness &addr
    let seekReadCustomAttributeTypeIdx (addr: byref<int>) = seekReadTaggedIdx (fun idx -> CustomAttributeTypeTag idx) 3 catBigness &addr  
    let seekReadStringIdx (addr: byref<int>) = seekReadIdx stringsBigness &addr
    let seekReadGuidIdx (addr: byref<int>) = seekReadIdx guidsBigness &addr
    let seekReadBlobIdx (addr: byref<int>) = seekReadIdx blobsBigness &addr 

    let seekReadModuleRow idx =
        if idx = 0 then failwith "cannot read Module table row 0";
        let mutable addr = rowAddr TableNames.Module idx
        let generation = seekReadUInt16Adv &addr
        let nameIdx = seekReadStringIdx &addr
        let mvidIdx = seekReadGuidIdx &addr
        let encidIdx = seekReadGuidIdx &addr
        let encbaseidIdx = seekReadGuidIdx &addr
        (generation, nameIdx, mvidIdx, encidIdx, encbaseidIdx) 

    /// Read Table ILTypeRef 
    let seekReadTypeRefRow idx =
        let mutable addr = rowAddr TableNames.TypeRef idx
        let scopeIdx = seekReadResolutionScopeIdx &addr
        let nameIdx = seekReadStringIdx &addr
        let namespaceIdx = seekReadStringIdx &addr
        (scopeIdx,nameIdx,namespaceIdx) 

    /// Read Table ILTypeDef 
    let seekReadTypeDefRowUncached idx =

        let mutable addr = rowAddr TableNames.TypeDef idx
        let flags = seekReadInt32Adv &addr
        let nameIdx = seekReadStringIdx &addr
        let namespaceIdx = seekReadStringIdx &addr
        let extendsIdx = seekReadTypeDefOrRefOrSpecIdx &addr
        let fieldsIdx = seekReadUntaggedIdx TableNames.Field &addr
        let methodsIdx = seekReadUntaggedIdx TableNames.Method &addr
        (flags, nameIdx, namespaceIdx, extendsIdx, fieldsIdx, methodsIdx) 

    let seekReadTypeDefRow = cacheTypeDefRow  seekReadTypeDefRowUncached

    /// Read Table Field 
    let seekReadFieldRow idx =
        let mutable addr = rowAddr TableNames.Field idx
        let flags = seekReadUInt16AsInt32Adv &addr
        let nameIdx = seekReadStringIdx &addr
        let typeIdx = seekReadBlobIdx &addr
        (flags,nameIdx,typeIdx)  

    /// Read Table Method 
    let seekReadMethodRow idx =
        let mutable addr = rowAddr TableNames.Method idx
        let codeRVA = seekReadInt32Adv &addr
        let implflags = seekReadUInt16AsInt32Adv &addr
        let flags = seekReadUInt16AsInt32Adv &addr
        let nameIdx = seekReadStringIdx &addr
        let typeIdx = seekReadBlobIdx &addr
        let paramIdx = seekReadUntaggedIdx TableNames.Param &addr
        (codeRVA, implflags, flags, nameIdx, typeIdx, paramIdx) 

    /// Read Table Param 
    let seekReadParamRow idx =
        let mutable addr = rowAddr TableNames.Param idx
        let flags = seekReadUInt16AsInt32Adv &addr
        let seq =  seekReadUInt16AsInt32Adv &addr
        let nameIdx = seekReadStringIdx &addr
        (flags,seq,nameIdx) 

    let seekReadInterfaceImplRowUncached idx =
        let mutable addr = rowAddr TableNames.InterfaceImpl idx
        let tidx = seekReadUntaggedIdx TableNames.TypeDef &addr
        let intfIdx = seekReadTypeDefOrRefOrSpecIdx &addr
        (tidx,intfIdx)

    /// Read Table InterfaceImpl 
    let seekReadInterfaceImplRow = cacheInterfaceImplRow  seekReadInterfaceImplRowUncached

    /// Read Table MemberRef 
    let seekReadMemberRefRow idx =
        let mutable addr = rowAddr TableNames.MemberRef idx
        let mrpIdx = seekReadMemberRefParentIdx &addr
        let nameIdx = seekReadStringIdx &addr
        let typeIdx = seekReadBlobIdx &addr
        (mrpIdx,nameIdx,typeIdx) 

    /// Read Table Constant 
    let seekReadConstantRowUncached idx =
        let mutable addr = rowAddr TableNames.Constant idx
        let kind = seekReadUInt16Adv &addr
        let parentIdx = seekReadHasConstantIdx &addr
        let valIdx = seekReadBlobIdx &addr
        (kind, parentIdx, valIdx)

    let seekReadConstantRow = cacheConstantRow  seekReadConstantRowUncached

    /// Read Table CustomAttribute 
    let seekReadCustomAttributeRow idx =
        let mutable addr = rowAddr TableNames.CustomAttribute idx
        let parentIdx = seekReadHasCustomAttributeIdx &addr
        let typeIdx = seekReadCustomAttributeTypeIdx &addr
        let valIdx = seekReadBlobIdx &addr
        (parentIdx, typeIdx, valIdx)  

    /// Read Table ClassLayout 
    let seekReadClassLayoutRow idx =
        let mutable addr = rowAddr TableNames.ClassLayout idx
        let pack = seekReadUInt16Adv &addr
        let size = seekReadInt32Adv &addr
        let tidx = seekReadUntaggedIdx TableNames.TypeDef &addr
        (pack,size,tidx)  

    /// Read Table FieldLayout 
    let seekReadFieldLayoutRow idx =
        let mutable addr = rowAddr TableNames.FieldLayout idx
        let offset = seekReadInt32Adv &addr
        let fidx = seekReadUntaggedIdx TableNames.Field &addr
        (offset,fidx)  

    /// Read Table EventMap 
    let seekReadEventMapRow idx =
        let mutable addr = rowAddr TableNames.EventMap idx
        let tidx = seekReadUntaggedIdx TableNames.TypeDef &addr
        let eventsIdx = seekReadUntaggedIdx TableNames.Event &addr
        (tidx,eventsIdx) 

    /// Read Table Event 
    let seekReadEventRow idx =
        let mutable addr = rowAddr TableNames.Event idx
        let flags = seekReadUInt16AsInt32Adv &addr
        let nameIdx = seekReadStringIdx &addr
        let typIdx = seekReadTypeDefOrRefOrSpecIdx &addr
        (flags,nameIdx,typIdx) 
   
    /// Read Table PropertyMap 
    let seekReadPropertyMapRowUncached idx =
        let mutable addr = rowAddr TableNames.PropertyMap idx
        let tidx = seekReadUntaggedIdx TableNames.TypeDef &addr
        let propsIdx = seekReadUntaggedIdx TableNames.Property &addr
        (tidx,propsIdx)

    let seekReadPropertyMapRow = cachePropertyMapRow  seekReadPropertyMapRowUncached

    /// Read Table Property 
    let seekReadPropertyRow idx =
        let mutable addr = rowAddr TableNames.Property idx
        let flags = seekReadUInt16AsInt32Adv &addr
        let nameIdx = seekReadStringIdx &addr
        let typIdx = seekReadBlobIdx &addr
        (flags,nameIdx,typIdx) 

    /// Read Table MethodSemantics 
    let seekReadMethodSemanticsRowUncached idx =
        let mutable addr = rowAddr TableNames.MethodSemantics idx
        let flags = seekReadUInt16AsInt32Adv &addr
        let midx = seekReadUntaggedIdx TableNames.Method &addr
        let assocIdx = seekReadHasSemanticsIdx &addr
        (flags,midx,assocIdx)

    let seekReadMethodSemanticsRow = cacheMethodSemanticsRow seekReadMethodSemanticsRowUncached 

    /// Read Table MethodImpl 
    let seekReadMethodImplRow idx =
        let mutable addr = rowAddr TableNames.MethodImpl idx
        let tidx = seekReadUntaggedIdx TableNames.TypeDef &addr
        let mbodyIdx = seekReadMethodDefOrRefIdx &addr
        let mdeclIdx = seekReadMethodDefOrRefIdx &addr
        (tidx,mbodyIdx,mdeclIdx) 

    /// Read Table ILModuleRef 
    let seekReadModuleRefRow idx =
        let mutable addr = rowAddr TableNames.ModuleRef idx
        let nameIdx = seekReadStringIdx &addr
        nameIdx  

    /// Read Table ILTypeSpec 
    let seekReadTypeSpecRow idx =
        let mutable addr = rowAddr TableNames.TypeSpec idx
        let blobIdx = seekReadBlobIdx &addr
        blobIdx  

    /// Read Table Assembly 
    let seekReadAssemblyRow idx =
        let mutable addr = rowAddr TableNames.Assembly idx
        let hash = seekReadInt32Adv &addr
        let v1 = seekReadUInt16Adv &addr
        let v2 = seekReadUInt16Adv &addr
        let v3 = seekReadUInt16Adv &addr
        let v4 = seekReadUInt16Adv &addr
        let flags = seekReadInt32Adv &addr
        let publicKeyIdx = seekReadBlobIdx &addr
        let nameIdx = seekReadStringIdx &addr
        let localeIdx = seekReadStringIdx &addr
        (hash,v1,v2,v3,v4,flags,publicKeyIdx, nameIdx, localeIdx)

    /// Read Table ILAssemblyRef 
    let seekReadAssemblyRefRow idx =
        let mutable addr = rowAddr TableNames.AssemblyRef idx
        let v1 = seekReadUInt16Adv &addr
        let v2 = seekReadUInt16Adv &addr
        let v3 = seekReadUInt16Adv &addr
        let v4 = seekReadUInt16Adv &addr
        let flags = seekReadInt32Adv &addr
        let publicKeyOrTokenIdx = seekReadBlobIdx &addr
        let nameIdx = seekReadStringIdx &addr
        let localeIdx = seekReadStringIdx &addr
        let hashValueIdx = seekReadBlobIdx &addr
        (v1,v2,v3,v4,flags,publicKeyOrTokenIdx, nameIdx, localeIdx,hashValueIdx) 

    /// Read Table File 
    let seekReadFileRow idx =
        let mutable addr = rowAddr TableNames.File idx
        let flags = seekReadInt32Adv &addr
        let nameIdx = seekReadStringIdx &addr
        let hashValueIdx = seekReadBlobIdx &addr
        (flags, nameIdx, hashValueIdx) 

    /// Read Table ILExportedTypeOrForwarder 
    let seekReadExportedTypeRow idx =
        let mutable addr = rowAddr TableNames.ExportedType idx
        let flags = seekReadInt32Adv &addr
        let tok = seekReadInt32Adv &addr
        let nameIdx = seekReadStringIdx &addr
        let namespaceIdx = seekReadStringIdx &addr
        let implIdx = seekReadImplementationIdx &addr
        (flags,tok,nameIdx,namespaceIdx,implIdx) 

    /// Read Table ManifestResource 
    let seekReadManifestResourceRow idx =
        let mutable addr = rowAddr TableNames.ManifestResource idx
        let offset = seekReadInt32Adv &addr
        let flags = seekReadInt32Adv &addr
        let nameIdx = seekReadStringIdx &addr
        let implIdx = seekReadImplementationIdx &addr
        (offset,flags,nameIdx,implIdx) 

    /// Read Table Nested 
    let seekReadNestedRowUncached idx =
        let mutable addr = rowAddr TableNames.Nested idx
        let nestedIdx = seekReadUntaggedIdx TableNames.TypeDef &addr
        let enclIdx = seekReadUntaggedIdx TableNames.TypeDef &addr
        (nestedIdx,enclIdx)

    let seekReadNestedRow = cacheNestedRow  seekReadNestedRowUncached

    /// Read Table GenericParam 
    let seekReadGenericParamRow idx =
        let mutable addr = rowAddr TableNames.GenericParam idx
        let seq = seekReadUInt16Adv &addr
        let flags = seekReadUInt16Adv &addr
        let ownerIdx = seekReadTypeOrMethodDefIdx &addr
        let nameIdx = seekReadStringIdx &addr
        (idx,seq,flags,ownerIdx,nameIdx) 

    // Read Table GenericParamConstraint 
    let seekReadGenericParamConstraintRow idx =
        let mutable addr = rowAddr TableNames.GenericParamConstraint idx
        let pidx = seekReadUntaggedIdx TableNames.GenericParam &addr
        let constraintIdx = seekReadTypeDefOrRefOrSpecIdx &addr
        (pidx,constraintIdx) 

    //let readUserStringHeapUncached idx = seekReadUserString is (userStringsStreamPhysicalLoc + idx)
    //let readUserStringHeap = cacheUserStringHeap readUserStringHeapUncached

    let readStringHeapUncached idx =  seekReadUTF8String is (stringsStreamPhysicalLoc + idx) 
    let readStringHeap = cacheStringHeap readStringHeapUncached
    let readStringHeapOption idx = if idx = 0 then None else Some (readStringHeap idx) 

    let emptyByteArray: byte[] = [||]
    let readBlobHeapUncached idx = 
        // valid index lies in range [1..streamSize)
        // NOTE: idx cannot be 0 - Blob\String heap has first empty element that is one byte 0
        if idx <= 0 || idx >= blobsStreamSize then emptyByteArray
        else seekReadBlob is (blobsStreamPhysicalLoc + idx) 
    let readBlobHeap        = cacheBlobHeap readBlobHeapUncached
    let readBlobHeapOption idx = if idx = 0 then None else Some (readBlobHeap idx) 

    //let readGuidHeap idx = seekReadGuid is (guidsStreamPhysicalLoc + idx) 

    // read a single value out of a blob heap using the given function 
    let readBlobHeapAsBool   vidx = fst (sigptrGetBool   (readBlobHeap vidx) 0) 
    let readBlobHeapAsSByte  vidx = fst (sigptrGetSByte  (readBlobHeap vidx) 0) 
    let readBlobHeapAsInt16  vidx = fst (sigptrGetInt16  (readBlobHeap vidx) 0) 
    let readBlobHeapAsInt32  vidx = fst (sigptrGetInt32  (readBlobHeap vidx) 0) 
    let readBlobHeapAsInt64  vidx = fst (sigptrGetInt64  (readBlobHeap vidx) 0) 
    let readBlobHeapAsByte   vidx = fst (sigptrGetByte   (readBlobHeap vidx) 0) 
    let readBlobHeapAsUInt16 vidx = fst (sigptrGetUInt16 (readBlobHeap vidx) 0) 
    let readBlobHeapAsUInt32 vidx = fst (sigptrGetUInt32 (readBlobHeap vidx) 0) 
    let readBlobHeapAsUInt64 vidx = fst (sigptrGetUInt64 (readBlobHeap vidx) 0) 
    let readBlobHeapAsSingle vidx = fst (sigptrGetSingle (readBlobHeap vidx) 0) 
    let readBlobHeapAsDouble vidx = fst (sigptrGetDouble (readBlobHeap vidx) 0) 
   
    //-----------------------------------------------------------------------
    // Read the AbsIL structure (lazily) by reading off the relevant rows.
    // ----------------------------------------------------------------------

    let isSorted (tab:TableName) = ((sorted &&& (int64 1 <<< tab.Index)) <> int64 0x0) 

    //let subsysversion = (subsysMajor, subsysMinor)
    //let ilMetadataVersion = System.Text.Encoding.UTF8.GetString (ilMetadataVersion, 0, ilMetadataVersion.Length)

    let rec seekReadModule idx =
        let (_generation, nameIdx, _mvidIdx, _encidIdx, _encbaseidIdx) = seekReadModuleRow idx
        let ilModuleName = readStringHeap nameIdx
        //let nativeResources = readNativeResources ctxt

        { Manifest =      
             if getNumRows (TableNames.Assembly) > 0 then Some (seekReadAssemblyManifest 1) 
             else None;
          CustomAttrs = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.Module,idx));
          Name = ilModuleName;
          //NativeResources=nativeResources;
          TypeDefs = ILTypeDefs (lazy (seekReadTopTypeDefs ()));
          //SubSystemFlags = int32 subsys;
          //IsILOnly = ilOnly;
          //SubsystemVersion = subsysversion
          //UseHighEntropyVA = useHighEntropyVA
          //Platform = platform;
          //StackReserveSize = None;  
          //Is32Bit = only32;
          //Is32BitPreferred = is32bitpreferred;
          //Is64Bit = only64;
          //IsDLL=isDll;
          //VirtualAlignment = alignVirt;
          //PhysicalAlignment = alignPhys;
          //ImageBase = imageBaseReal;
          //MetadataVersion = ilMetadataVersion;
          Resources = seekReadManifestResources (); 
          }  

    and seekReadAssemblyManifest idx =
        let (_hash,v1,v2,v3,v4,flags,publicKeyIdx, nameIdx, localeIdx) = seekReadAssemblyRow idx
        let name = readStringHeap nameIdx
        let pubkey = readBlobHeapOption publicKeyIdx
        { Name= name; 
          //SecurityDecls= seekReadSecurityDecls (TaggedIndex(hds_Assembly,idx));
          PublicKey= pubkey;  
          Version= Some (v1,v2,v3,v4);
          Locale= readStringHeapOption localeIdx;
          CustomAttrs = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.Assembly,idx));
          ExportedTypes= seekReadTopExportedTypes ();
          EntrypointElsewhere=(if fst entryPointToken = TableNames.File then Some (seekReadFile (snd entryPointToken)) else None);
          Retargetable = 0 <> (flags &&& 0x100);
          //DisableJitOptimizations = 0 <> (flags &&& 0x4000);
          //JitTracking = 0 <> (flags &&& 0x8000) 
          } 
     
    and seekReadAssemblyRef idx = cacheAssemblyRef  seekReadAssemblyRefUncached idx
    and seekReadAssemblyRefUncached idx = 
        let (v1,v2,v3,v4,flags,publicKeyOrTokenIdx, nameIdx, localeIdx,hashValueIdx) = seekReadAssemblyRefRow idx
        let nm = readStringHeap nameIdx
        let publicKey = 
            match readBlobHeapOption publicKeyOrTokenIdx with 
              | None -> None
              | Some blob -> Some (if (flags &&& 0x0001) <> 0x0 then PublicKey blob else PublicKeyToken blob)
          
        ILAssemblyRef
            (name=nm, 
             hash=readBlobHeapOption hashValueIdx, 
             publicKey=publicKey,
             retargetable=((flags &&& 0x0100) <> 0x0), 
             version=Some(v1,v2,v3,v4), 
             locale=readStringHeapOption localeIdx;)

    and seekReadModuleRef idx =
        let nameIdx = seekReadModuleRefRow idx
        ILModuleRef(name=readStringHeap nameIdx, hasMetadata=true, hash=None)

    and seekReadFile idx =
        let (flags, nameIdx, hashValueIdx) = seekReadFileRow idx
        ILModuleRef(name =  readStringHeap nameIdx,
                    hasMetadata= ((flags &&& 0x0001) = 0x0),
                    hash= readBlobHeapOption hashValueIdx)

    and seekReadClassLayout idx =
        match seekReadOptionalIndexedRow (getNumRows TableNames.ClassLayout,seekReadClassLayoutRow,(fun (_,_,tidx) -> tidx),simpleIndexCompare idx,isSorted TableNames.ClassLayout,(fun (pack,size,_) -> pack,size)) with 
        | None -> { Size = None; Pack = None }
        | Some (pack,size) -> { Size = Some size; 
                               Pack = Some pack; }

    and memberAccessOfFlags flags =
        let f = (flags &&& 0x00000007)
        if f = 0x00000001 then  ILMemberAccess.Private 
        elif f = 0x00000006 then  ILMemberAccess.Public 
        elif f = 0x00000004 then  ILMemberAccess.Family 
        elif f = 0x00000002 then  ILMemberAccess.FamilyAndAssembly 
        elif f = 0x00000005 then  ILMemberAccess.FamilyOrAssembly 
        elif f = 0x00000003 then  ILMemberAccess.Assembly 
        else ILMemberAccess.CompilerControlled

    and typeAccessOfFlags flags =
        let f = (flags &&& 0x00000007)
        if f = 0x00000001 then ILTypeDefAccess.Public 
        elif f = 0x00000002 then ILTypeDefAccess.Nested ILMemberAccess.Public 
        elif f = 0x00000003 then ILTypeDefAccess.Nested ILMemberAccess.Private 
        elif f = 0x00000004 then ILTypeDefAccess.Nested ILMemberAccess.Family 
        elif f = 0x00000006 then ILTypeDefAccess.Nested ILMemberAccess.FamilyAndAssembly 
        elif f = 0x00000007 then ILTypeDefAccess.Nested ILMemberAccess.FamilyOrAssembly 
        elif f = 0x00000005 then ILTypeDefAccess.Nested ILMemberAccess.Assembly 
        else ILTypeDefAccess.Private

    and typeLayoutOfFlags flags tidx = 
        let f = (flags &&& 0x00000018)
        if f = 0x00000008 then ILTypeDefLayout.Sequential (seekReadClassLayout tidx)
        elif f = 0x00000010 then  ILTypeDefLayout.Explicit (seekReadClassLayout tidx)
        else ILTypeDefLayout.Auto

    and typeKindOfFlags nspace nm _mdefs _fdefs (super:ILType option) flags =
        if (flags &&& 0x00000020) <> 0x0 then ILTypeDefKind.Interface 
        else 
             let isEnum = (match super with None -> false | Some ty -> ty.TypeSpec.Namespace = Some "System" && ty.TypeSpec.Name = "Enum")
             let isDelegate = (match super with None -> false | Some ty -> ty.TypeSpec.Namespace = Some "System" && ty.TypeSpec.Name = "Delegate")
             let isMulticastDelegate = (match super with None -> false | Some ty -> ty.TypeSpec.Namespace = Some "System" && ty.TypeSpec.Name = "MulticastDelegate")
             let selfIsMulticastDelegate = (nspace = Some "System" && nm = "MulticastDelegate")
             let isValueType = (match super with None -> false | Some ty -> ty.TypeSpec.Namespace = Some "System" && ty.TypeSpec.Name = "ValueType" && not (nspace = Some "System" && nm = "Enum"))
             if isEnum then ILTypeDefKind.Enum 
             elif  (isDelegate && not selfIsMulticastDelegate) || isMulticastDelegate then ILTypeDefKind.Delegate
             elif isValueType then ILTypeDefKind.ValueType 
             else ILTypeDefKind.Class 

    and typeEncodingOfFlags flags = 
        let f = (flags &&& 0x00030000)
        if f = 0x00020000 then ILDefaultPInvokeEncoding.Auto 
        elif f = 0x00010000 then ILDefaultPInvokeEncoding.Unicode 
        else ILDefaultPInvokeEncoding.Ansi

    and isTopTypeDef flags =
        (typeAccessOfFlags flags =  ILTypeDefAccess.Private) ||
         typeAccessOfFlags flags =  ILTypeDefAccess.Public
       
    and seekIsTopTypeDefOfIdx idx =
        let (flags,_,_, _, _,_) = seekReadTypeDefRow idx
        isTopTypeDef flags
       
    and readStringHeapAsTypeName (nameIdx,namespaceIdx) = 
        let name = readStringHeap nameIdx
        let nspace = readStringHeapOption namespaceIdx
        nspace, name

    and seekReadTypeDefRowExtents _info (idx:int) =
        if idx >= getNumRows TableNames.TypeDef then 
            getNumRows TableNames.Field + 1,
            getNumRows TableNames.Method + 1
        else
            let (_, _, _, _, fieldsIdx, methodsIdx) = seekReadTypeDefRow (idx + 1)
            fieldsIdx, methodsIdx 

    and seekReadTypeDefRowWithExtents (idx:int) =
        let info= seekReadTypeDefRow idx
        info,seekReadTypeDefRowExtents info idx

    and seekReadTypeDef toponly (idx:int) =
        let (flags,_,_, _, _, _) = seekReadTypeDefRow idx
        if toponly && not (isTopTypeDef flags) then None
        else

         let rest = 
            lazy
               let ((flags,nameIdx,namespaceIdx, extendsIdx, fieldsIdx, methodsIdx) as info) = seekReadTypeDefRow idx
               let name = readStringHeap nameIdx
               let nspace = readStringHeapOption namespaceIdx
               let (endFieldsIdx, endMethodsIdx) = seekReadTypeDefRowExtents info idx
               let typars = seekReadGenericParams 0 (TypeOrMethodDefTag.TypeDef,idx)
               let numtypars = typars.Length
               let super = seekReadOptionalTypeDefOrRef numtypars AsObject extendsIdx
               let layout = typeLayoutOfFlags flags idx
               let hasLayout = (match layout with ILTypeDefLayout.Explicit _ -> true | _ -> false)
               let mdefs = seekReadMethods numtypars methodsIdx endMethodsIdx
               let fdefs = seekReadFields (numtypars,hasLayout) fieldsIdx endFieldsIdx
               let kind = typeKindOfFlags nspace name mdefs fdefs super flags
               let nested = seekReadNestedTypeDefs idx 
               let impls  = seekReadInterfaceImpls numtypars idx
               //let sdecls =  seekReadSecurityDecls (TaggedIndex(hds_TypeDef,idx))
               let mimpls = seekReadMethodImpls numtypars idx
               let props  = seekReadProperties numtypars idx
               let events = seekReadEvents numtypars idx
               let cas = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.TypeDef,idx))
               { Kind= kind
                 Namespace=nspace
                 Name=name
                 GenericParams=typars 
                 Access= typeAccessOfFlags flags
                 IsAbstract= (flags &&& 0x00000080) <> 0x0
                 IsSealed= (flags &&& 0x00000100) <> 0x0 
                 IsSerializable= (flags &&& 0x00002000) <> 0x0 
                 IsComInterop= (flags &&& 0x00001000) <> 0x0 
                 Layout = layout
                 IsSpecialName= (flags &&& 0x00000400) <> 0x0
                 Encoding=typeEncodingOfFlags flags
                 NestedTypes= nested
                 Implements =  impls  
                 Extends = super 
                 Methods = mdefs 
                 //SecurityDecls = sdecls
                 HasSecurity=(flags &&& 0x00040000) <> 0x0
                 Fields=fdefs
                 MethodImpls=mimpls
                 InitSemantics=
                     if kind = ILTypeDefKind.Interface then ILTypeInit.OnAny
                     elif (flags &&& 0x00100000) <> 0x0 then ILTypeInit.BeforeField
                     else ILTypeInit.OnAny 
                 Events= events
                 Properties=props
                 CustomAttrs=cas }
         Some rest 

    and seekReadTopTypeDefs () =
        [| for i = 1 to getNumRows TableNames.TypeDef do
              match seekReadTypeDef true i  with 
              | None -> ()
              | Some td -> yield td |]

    and seekReadNestedTypeDefs tidx =
        ILTypeDefs
          (lazy 
               let nestedIdxs = seekReadIndexedRows (getNumRows TableNames.Nested,seekReadNestedRow,snd,simpleIndexCompare tidx,false,fst)
               [| for i in nestedIdxs do 
                     match seekReadTypeDef false i with 
                     | None -> ()
                     | Some td -> yield td |])

    and seekReadInterfaceImpls numtypars tidx =
        seekReadIndexedRows (getNumRows TableNames.InterfaceImpl,seekReadInterfaceImplRow ,fst,simpleIndexCompare tidx,isSorted TableNames.InterfaceImpl,(snd >> seekReadTypeDefOrRef numtypars AsObject [| |])) 

    and seekReadGenericParams numtypars (a,b) : ILGenericParameterDefs =  cacheGenericParams seekReadGenericParamsUncached (GenericParamsIdx(numtypars,a,b))

    and seekReadGenericParamsUncached (GenericParamsIdx(numtypars,a,b)) =
        let pars =
            seekReadIndexedRows
                (getNumRows TableNames.GenericParam,seekReadGenericParamRow,
                 (fun (_,_,_,tomd,_) -> tomd),
                 tomdCompare (TaggedIndex(a,b)),
                 isSorted TableNames.GenericParam,
                 (fun (gpidx,seq,flags,_,nameIdx) -> 
                     let flags = int32 flags
                     let varianceFlags = flags &&& 0x0003
                     let variance = 
                         if varianceFlags = 0x0000 then NonVariant
                         elif varianceFlags = 0x0001 then CoVariant
                         elif varianceFlags = 0x0002 then ContraVariant 
                         else NonVariant
                     let constraints = seekReadGenericParamConstraintsUncached numtypars gpidx
                     let cas = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.GenericParam,gpidx))
                     seq, {Name=readStringHeap nameIdx
                           Constraints= constraints
                           Variance=variance  
                           CustomAttrs=cas
                           HasReferenceTypeConstraint= (flags &&& 0x0004) <> 0
                           HasNotNullableValueTypeConstraint= (flags &&& 0x0008) <> 0
                           HasDefaultConstructorConstraint=(flags &&& 0x0010) <> 0 }))
        pars |> Array.sortBy fst |> Array.map snd 

    and seekReadGenericParamConstraintsUncached numtypars gpidx =
        seekReadIndexedRows 
            (getNumRows TableNames.GenericParamConstraint,
             seekReadGenericParamConstraintRow,
             fst,
             simpleIndexCompare gpidx,
             isSorted TableNames.GenericParamConstraint,
             (snd >>  seekReadTypeDefOrRef numtypars AsObject (*ok*) [| |]))

    and seekReadTypeDefAsType boxity (ginst:ILTypes) idx = cacheTypeDefAsType seekReadTypeDefAsTypeUncached (TypeDefAsTypIdx (boxity,ginst,idx))

    and seekReadTypeDefAsTypeUncached (TypeDefAsTypIdx (boxity,ginst,idx)) =
        mkILTy boxity (ILTypeSpec(seekReadTypeDefAsTypeRef idx, ginst))

    and seekReadTypeDefAsTypeRef idx =
         let enc = 
           if seekIsTopTypeDefOfIdx idx then ILTypeRefScope.Top ILScopeRef.Local
           else 
             let enclIdx = seekReadIndexedRow (getNumRows TableNames.Nested,seekReadNestedRow,fst,simpleIndexCompare idx,isSorted TableNames.Nested,snd)
             let tref = seekReadTypeDefAsTypeRef enclIdx
             ILTypeRefScope.Nested tref
         let (_, nameIdx, namespaceIdx, _, _, _) = seekReadTypeDefRow idx
         let nsp, nm = readStringHeapAsTypeName (nameIdx,namespaceIdx)
         ILTypeRef(enc=enc, nsp = nsp, name = nm )

    and seekReadTypeRef idx = cacheTypeRef seekReadTypeRefUncached idx
    and seekReadTypeRefUncached idx =
         let scopeIdx,nameIdx,namespaceIdx = seekReadTypeRefRow idx
         let enc = seekReadTypeRefScope scopeIdx
         let nsp, nm = readStringHeapAsTypeName (nameIdx,namespaceIdx)
         ILTypeRef(enc, nsp, nm) 

    and seekReadTypeRefAsType boxity ginst idx = cacheTypeRefAsType seekReadTypeRefAsTypeUncached (TypeRefAsTypIdx (boxity,ginst,idx))
    and seekReadTypeRefAsTypeUncached (TypeRefAsTypIdx (boxity,ginst,idx)) =
         mkILTy boxity (ILTypeSpec(seekReadTypeRef idx, ginst))

    and seekReadTypeDefOrRef numtypars boxity (ginst:ILTypes) (TaggedIndex(tag,idx) ) =
        match tag with 
        | tag when tag = TypeDefOrRefOrSpecTag.TypeDef -> seekReadTypeDefAsType boxity ginst idx
        | tag when tag = TypeDefOrRefOrSpecTag.TypeRef -> seekReadTypeRefAsType boxity ginst idx
        | tag when tag = TypeDefOrRefOrSpecTag.TypeSpec -> readBlobHeapAsType numtypars (seekReadTypeSpecRow idx)
        | _ -> failwith "seekReadTypeDefOrRef"

    and seekReadTypeDefOrRefAsTypeRef (TaggedIndex(tag,idx) ) =
        match tag with 
        | tag when tag = TypeDefOrRefOrSpecTag.TypeDef -> seekReadTypeDefAsTypeRef idx
        | tag when tag = TypeDefOrRefOrSpecTag.TypeRef -> seekReadTypeRef idx
        | tag when tag = TypeDefOrRefOrSpecTag.TypeSpec -> ilg.typ_Object.TypeRef
        | _ -> failwith "seekReadTypeDefOrRefAsTypeRef_readTypeDefOrRefOrSpec"

    and seekReadMethodRefParent numtypars (TaggedIndex(tag,idx)) =
        match tag with 
        | tag when tag = MemberRefParentTag.TypeRef -> seekReadTypeRefAsType AsObject (* not ok - no way to tell if a member ref parent is a value type or not *) [| |] idx
        | tag when tag = MemberRefParentTag.ModuleRef -> mkILTypeForGlobalFunctions (ILScopeRef.Module (seekReadModuleRef idx))
        | tag when tag = MemberRefParentTag.MethodDef -> 
            let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData idx
            let mspec = mkILMethSpecInTyRaw(enclTyp, cc, nm, argtys, retty, minst)
            mspec.EnclosingType
        | tag when tag = MemberRefParentTag.TypeSpec -> readBlobHeapAsType numtypars (seekReadTypeSpecRow idx)
        | _ -> failwith "seekReadMethodRefParent"

    and seekReadMethodDefOrRef numtypars (TaggedIndex(tag,idx)) =
        match tag with 
        | tag when tag = MethodDefOrRefTag.MethodDef -> 
            let (MethodData(enclTyp, cc, nm, argtys, retty,minst)) = seekReadMethodDefAsMethodData idx
            VarArgMethodData(enclTyp, cc, nm, argtys, None,retty,minst)
        | tag when tag = MethodDefOrRefTag.MemberRef -> 
            seekReadMemberRefAsMethodData numtypars idx
        | _ -> failwith "seekReadMethodDefOrRef"

    and seekReadMethodDefOrRefNoVarargs numtypars x =
         let (VarArgMethodData(enclTyp, cc, nm, argtys, _varargs, retty, minst)) =     seekReadMethodDefOrRef numtypars x 
         MethodData(enclTyp, cc, nm, argtys, retty, minst)

    and seekReadCustomAttrType (TaggedIndex(tag,idx) ) =
        match tag with 
        | tag when tag = CustomAttributeTypeTag.MethodDef -> 
            let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData idx
            mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty, minst)
        | tag when tag = CustomAttributeTypeTag.MemberRef -> 
            let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMemberRefAsMethDataNoVarArgs 0 idx
            mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty, minst)
        | _ -> failwith "seekReadCustomAttrType"
    
    and seekReadImplAsScopeRef (TaggedIndex(tag,idx) ) =
         if idx = 0 then ILScopeRef.Local
         else 
           match tag with 
           | tag when tag = ImplementationTag.File -> ILScopeRef.Module (seekReadFile idx)
           | tag when tag = ImplementationTag.AssemblyRef -> ILScopeRef.Assembly (seekReadAssemblyRef idx)
           | tag when tag = ImplementationTag.ExportedType -> failwith "seekReadImplAsScopeRef"
           | _ -> failwith "seekReadImplAsScopeRef"

    and seekReadTypeRefScope (TaggedIndex(tag,idx) ) : ILTypeRefScope =
        match tag with 
        | tag when tag = ResolutionScopeTag.Module -> ILTypeRefScope.Top(ILScopeRef.Local)
        | tag when tag = ResolutionScopeTag.ModuleRef -> ILTypeRefScope.Top(ILScopeRef.Module (seekReadModuleRef idx))
        | tag when tag = ResolutionScopeTag.AssemblyRef -> ILTypeRefScope.Top(ILScopeRef.Assembly (seekReadAssemblyRef idx))
        | tag when tag = ResolutionScopeTag.TypeRef -> ILTypeRefScope.Nested (seekReadTypeRef idx)
        | _ -> failwith "seekReadTypeRefScope"

    and seekReadOptionalTypeDefOrRef numtypars boxity idx = 
        if idx = TaggedIndex(TypeDefOrRefOrSpecTag.TypeDef, 0) then None
        else Some (seekReadTypeDefOrRef numtypars boxity [| |] idx)

    and seekReadField (numtypars, hasLayout) (idx:int) =
         let (flags,nameIdx,typeIdx) = seekReadFieldRow idx
         let nm = readStringHeap nameIdx
         let isStatic = (flags &&& 0x0010) <> 0
         let fd = 
           { Name = nm
             FieldType = readBlobHeapAsFieldSig numtypars typeIdx
             Access = memberAccessOfFlags flags
             IsStatic = isStatic
             IsInitOnly = (flags &&& 0x0020) <> 0
             IsLiteral = (flags &&& 0x0040) <> 0
             NotSerialized = (flags &&& 0x0080) <> 0
             IsSpecialName = (flags &&& 0x0200) <> 0 || (flags &&& 0x0400) <> 0 (* REVIEW: RTSpecialName *)
             LiteralValue = if (flags &&& 0x8000) = 0 then None else Some (seekReadConstant (TaggedIndex(HasConstantTag.FieldDef,idx)))
    (*
             Marshal = 
                 if (flags &&& 0x1000) = 0 then None else 
                 Some (seekReadIndexedRow (getNumRows TableNames.FieldMarshal,seekReadFieldMarshalRow,
                                           fst,hfmCompare (TaggedIndex(hfm_FieldDef,idx)),
                                           isSorted TableNames.FieldMarshal,
                                           (snd >> readBlobHeapAsNativeType ctxt)))
             Data = 
                 if (flags &&& 0x0100) = 0 then None 
                 else 
                   let rva = seekReadIndexedRow (getNumRows TableNames.FieldRVA,seekReadFieldRVARow,
                                                 snd,simpleIndexCompare idx,isSorted TableNames.FieldRVA,fst) 
                   Some (rvaToData "field" rva)
    *)
             Offset = 
                 if hasLayout && not isStatic then 
                     Some (seekReadIndexedRow (getNumRows TableNames.FieldLayout,seekReadFieldLayoutRow,
                                               snd,simpleIndexCompare idx,isSorted TableNames.FieldLayout,fst)) else None 
             CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.FieldDef,idx)) }
         fd
     
    and seekReadFields (numtypars, hasLayout) fidx1 fidx2 =
        mkILFieldsLazy 
           (lazy
               [| for i = fidx1 to fidx2 - 1 do
                   yield seekReadField (numtypars, hasLayout) i |])

    and seekReadMethods numtypars midx1 midx2 =
        mkILMethodsLazy 
           (lazy 
               [| for i = midx1 to midx2 - 1 do
                     yield seekReadMethod numtypars i |])

    and sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr = 
        let n, sigptr = sigptrGetZInt32 bytes sigptr
        if (n &&& 0x01) = 0x0 then (* Type Def *)
            TaggedIndex(TypeDefOrRefOrSpecTag.TypeDef,  (n >>>& 2)), sigptr
        else (* Type Ref *)
            TaggedIndex(TypeDefOrRefOrSpecTag.TypeRef,  (n >>>& 2)), sigptr         

    and sigptrGetTy numtypars bytes sigptr = 
        let b0,sigptr = sigptrGetByte bytes sigptr
        if b0 = et_OBJECT then ilg.typ_Object , sigptr
        elif b0 = et_STRING then ilg.typ_String, sigptr
        elif b0 = et_I1 then ilg.typ_SByte, sigptr
        elif b0 = et_I2 then ilg.typ_Int16, sigptr
        elif b0 = et_I4 then ilg.typ_Int32, sigptr
        elif b0 = et_I8 then ilg.typ_Int64, sigptr
        elif b0 = et_I then ilg.typ_IntPtr, sigptr
        elif b0 = et_U1 then ilg.typ_Byte, sigptr
        elif b0 = et_U2 then ilg.typ_UInt16, sigptr
        elif b0 = et_U4 then ilg.typ_UInt32, sigptr
        elif b0 = et_U8 then ilg.typ_UInt64, sigptr
        elif b0 = et_U then ilg.typ_UIntPtr, sigptr
        elif b0 = et_R4 then ilg.typ_Single, sigptr
        elif b0 = et_R8 then ilg.typ_Double, sigptr
        elif b0 = et_CHAR then ilg.typ_Char, sigptr
        elif b0 = et_BOOLEAN then ilg.typ_Boolean, sigptr
        elif b0 = et_WITH then 
            let b0,sigptr = sigptrGetByte bytes sigptr
            let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
            let n, sigptr = sigptrGetZInt32 bytes sigptr
            let argtys,sigptr = sigptrFold (sigptrGetTy numtypars) n bytes sigptr
            seekReadTypeDefOrRef numtypars (if b0 = et_CLASS then AsObject else AsValue) argtys tdorIdx,
            sigptr
        
        elif b0 = et_CLASS then 
            let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
            seekReadTypeDefOrRef numtypars AsObject [| |] tdorIdx, sigptr
        elif b0 = et_VALUETYPE then 
            let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
            seekReadTypeDefOrRef numtypars AsValue [| |] tdorIdx, sigptr
        elif b0 = et_VAR then 
            let n, sigptr = sigptrGetZInt32 bytes sigptr
            ILType.Var n,sigptr
        elif b0 = et_MVAR then 
            let n, sigptr = sigptrGetZInt32 bytes sigptr
            ILType.Var (n + numtypars), sigptr
        elif b0 = et_BYREF then 
            let typ, sigptr = sigptrGetTy numtypars bytes sigptr
            ILType.Byref typ, sigptr
        elif b0 = et_PTR then 
            let typ, sigptr = sigptrGetTy numtypars bytes sigptr
            ILType.Ptr typ, sigptr
        elif b0 = et_SZARRAY then 
            let typ, sigptr = sigptrGetTy numtypars bytes sigptr
            mkILArr1DTy typ, sigptr
        elif b0 = et_ARRAY then
            let typ, sigptr = sigptrGetTy numtypars bytes sigptr
            let rank, sigptr = sigptrGetZInt32 bytes sigptr
            let numSized, sigptr = sigptrGetZInt32 bytes sigptr
            let sizes, sigptr = sigptrFold sigptrGetZInt32 numSized bytes sigptr
            let numLoBounded, sigptr = sigptrGetZInt32 bytes sigptr
            let lobounds, sigptr = sigptrFold sigptrGetZInt32 numLoBounded bytes sigptr
            let shape = 
                let dim i =
                  (if i <  numLoBounded then Some lobounds.[i] else None),
                  (if i <  numSized then Some sizes.[i] else None)
                ILArrayShape (Array.init rank dim)
            ILType.Array (shape, typ), sigptr
        
        elif b0 = et_VOID then ILType.Void, sigptr
        elif b0 = et_TYPEDBYREF then 
            match ilg.typ_TypedReference with
            | Some t -> t, sigptr
            | _ -> failwith "system runtime doesn't contain System.TypedReference"
        elif b0 = et_CMOD_REQD || b0 = et_CMOD_OPT  then 
            let tdorIdx, sigptr = sigptrGetTypeDefOrRefOrSpecIdx bytes sigptr
            let typ, sigptr = sigptrGetTy numtypars bytes sigptr
            ILType.Modified((b0 = et_CMOD_REQD), seekReadTypeDefOrRefAsTypeRef tdorIdx, typ), sigptr
        elif b0 = et_FNPTR then
            let ccByte,sigptr = sigptrGetByte bytes sigptr
            let generic,cc = byteAsCallConv ccByte
            if generic then failwith "fptr sig may not be generic"
            let numparams,sigptr = sigptrGetZInt32 bytes sigptr
            let retty,sigptr = sigptrGetTy numtypars bytes sigptr
            let argtys,sigptr = sigptrFold (sigptrGetTy numtypars) ( numparams) bytes sigptr
            ILType.FunctionPointer (ILCallingSignature(cc, argtys, retty)),sigptr
        elif b0 = et_SENTINEL then failwith "varargs NYI"
        else ILType.Void , sigptr
        
    and sigptrGetVarArgTys n numtypars bytes sigptr = 
        sigptrFold (sigptrGetTy numtypars) n bytes sigptr 

    and sigptrGetArgTys n numtypars bytes sigptr acc = 
        if n <= 0 then (Array.ofList (List.rev acc),None),sigptr 
        else
          let b0,sigptr2 = sigptrGetByte bytes sigptr
          if b0 = et_SENTINEL then 
            let varargs,sigptr = sigptrGetVarArgTys n numtypars bytes sigptr2
            (Array.ofList (List.rev acc),Some( varargs)),sigptr
          else
            let x,sigptr = sigptrGetTy numtypars bytes sigptr
            sigptrGetArgTys (n-1) numtypars bytes sigptr (x::acc)
         
    and readBlobHeapAsMethodSig numtypars blobIdx  = cacheBlobHeapAsMethodSig readBlobHeapAsMethodSigUncached (BlobAsMethodSigIdx (numtypars,blobIdx))

    and readBlobHeapAsMethodSigUncached (BlobAsMethodSigIdx (numtypars,blobIdx)) =
        let bytes = readBlobHeap blobIdx
        let sigptr = 0
        let ccByte,sigptr = sigptrGetByte bytes sigptr
        let generic,cc = byteAsCallConv ccByte
        let genarity,sigptr = if generic then sigptrGetZInt32 bytes sigptr else 0x0,sigptr
        let numparams,sigptr = sigptrGetZInt32 bytes sigptr
        let retty,sigptr = sigptrGetTy numtypars bytes sigptr
        let (argtys,varargs),_sigptr = sigptrGetArgTys  ( numparams) numtypars bytes sigptr []
        generic,genarity,cc,retty,argtys,varargs
      
    and readBlobHeapAsType numtypars blobIdx = 
        let bytes = readBlobHeap blobIdx
        let ty,_sigptr = sigptrGetTy numtypars bytes 0
        ty

    and readBlobHeapAsFieldSig numtypars blobIdx  = cacheBlobHeapAsFieldSig readBlobHeapAsFieldSigUncached (BlobAsFieldSigIdx (numtypars,blobIdx))

    and readBlobHeapAsFieldSigUncached (BlobAsFieldSigIdx (numtypars,blobIdx)) =
        let bytes = readBlobHeap blobIdx
        let sigptr = 0
        let _ccByte,sigptr = sigptrGetByte bytes sigptr
        let retty,_sigptr = sigptrGetTy numtypars bytes sigptr
        retty

      
    and readBlobHeapAsPropertySig numtypars blobIdx  = cacheBlobHeapAsPropertySig readBlobHeapAsPropertySigUncached (BlobAsPropSigIdx (numtypars,blobIdx))
    and readBlobHeapAsPropertySigUncached (BlobAsPropSigIdx (numtypars,blobIdx))  =
        let bytes = readBlobHeap blobIdx
        let sigptr = 0
        let ccByte,sigptr = sigptrGetByte bytes sigptr
        let hasthis = byteAsHasThis ccByte
        let numparams,sigptr = sigptrGetZInt32 bytes sigptr
        let retty,sigptr = sigptrGetTy numtypars bytes sigptr
        let argtys,_sigptr = sigptrFold (sigptrGetTy numtypars) ( numparams) bytes sigptr
        hasthis,retty, argtys
      
    and byteAsHasThis b = 
        let hasthis_masked = b &&& 0x60uy
        if hasthis_masked = e_IMAGE_CEE_CS_CALLCONV_INSTANCE then ILThisConvention.Instance
        elif hasthis_masked = e_IMAGE_CEE_CS_CALLCONV_INSTANCE_EXPLICIT then ILThisConvention.InstanceExplicit 
        else ILThisConvention.Static 

    and byteAsCallConv b = 
        let cc = 
            let ccMaxked = b &&& 0x0Fuy
            if ccMaxked =  e_IMAGE_CEE_CS_CALLCONV_FASTCALL then ILArgConvention.FastCall 
            elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_STDCALL then ILArgConvention.StdCall 
            elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_THISCALL then ILArgConvention.ThisCall 
            elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_CDECL then ILArgConvention.CDecl 
            elif ccMaxked = e_IMAGE_CEE_CS_CALLCONV_VARARG then ILArgConvention.VarArg 
            else  ILArgConvention.Default
        let generic = (b &&& e_IMAGE_CEE_CS_CALLCONV_GENERIC) <> 0x0uy
        generic, Callconv (byteAsHasThis b,cc) 
      
    and seekReadMemberRefAsMethodData numtypars idx : VarArgMethodData =  cacheMemberRefAsMemberData  seekReadMemberRefAsMethodDataUncached (MemberRefAsMspecIdx (numtypars,idx))

    and seekReadMemberRefAsMethodDataUncached (MemberRefAsMspecIdx (numtypars,idx)) = 
        let (mrpIdx,nameIdx,typeIdx) = seekReadMemberRefRow idx
        let nm = readStringHeap nameIdx
        let enclTyp = seekReadMethodRefParent numtypars mrpIdx
        let _generic,genarity,cc,retty,argtys,varargs = readBlobHeapAsMethodSig enclTyp.GenericArgs.Length typeIdx
        let minst =  Array.init genarity (fun n -> ILType.Var (numtypars+n)) 
        (VarArgMethodData(enclTyp, cc, nm, argtys, varargs,retty,minst))

    and seekReadMemberRefAsMethDataNoVarArgs numtypars idx : MethodData =
       let (VarArgMethodData(enclTyp, cc, nm, argtys, _varargs, retty,minst)) =  seekReadMemberRefAsMethodData numtypars idx
       (MethodData(enclTyp, cc, nm, argtys, retty,minst))

    // One extremely annoying aspect of the MD format is that given a 
    // ILMethodDef token it is non-trivial to find which ILTypeDef it belongs 
    // to.  So we do a binary chop through the ILTypeDef table 
    // looking for which ILTypeDef has the ILMethodDef within its range.  
    // Although the ILTypeDef table is not "sorted", it is effectively sorted by 
    // method-range and field-range start/finish indexes  
    and seekReadMethodDefAsMethodData idx = cacheMethodDefAsMethodData seekReadMethodDefAsMethodDataUncached idx
    and seekReadMethodDefAsMethodDataUncached idx =
       let (_code_rva, _implflags, _flags, nameIdx, typeIdx, _paramIdx) = seekReadMethodRow idx
       let nm = readStringHeap nameIdx
       // Look for the method def parent. 
       let tidx = 
         seekReadIndexedRow (getNumRows TableNames.TypeDef,
                                (fun i -> i, seekReadTypeDefRowWithExtents i),
                                (fun r -> r),
                                (fun (_,((_, _, _, _, _, methodsIdx),
                                          (_, endMethodsIdx)))  -> 
                                            if endMethodsIdx <= idx then 1 
                                            elif methodsIdx <= idx && idx < endMethodsIdx then 0 
                                            else -1),
                                true,fst)
       let _generic,_genarity,cc,retty,argtys,_varargs = readBlobHeapAsMethodSig 0 typeIdx
       let finst = mkILFormalGenericArgsRaw (seekReadGenericParams 0 (TypeOrMethodDefTag.TypeDef,tidx))
       let minst = mkILFormalGenericArgsRaw (seekReadGenericParams finst.Length (TypeOrMethodDefTag.MethodDef,idx))
       let enclTyp = seekReadTypeDefAsType AsObject (* not ok: see note *) finst tidx
       MethodData(enclTyp, cc, nm, argtys, retty, minst)

    and seekReadMethod numtypars (idx:int) =
         let (_codeRVA, implflags, flags, nameIdx, typeIdx, paramIdx) = seekReadMethodRow idx
         let nm = readStringHeap nameIdx
         let isStatic = (flags &&& 0x0010) <> 0x0
         let final = (flags &&& 0x0020) <> 0x0
         let virt = (flags &&& 0x0040) <> 0x0
         let strict = (flags &&& 0x0200) <> 0x0
         let hidebysig = (flags &&& 0x0080) <> 0x0
         let newslot = (flags &&& 0x0100) <> 0x0
         let abstr = (flags &&& 0x0400) <> 0x0
         let specialname = (flags &&& 0x0800) <> 0x0
         let pinvoke = (flags &&& 0x2000) <> 0x0
         let export = (flags &&& 0x0008) <> 0x0
         let reqsecobj = (flags &&& 0x8000) <> 0x0
         let hassec = (flags &&& 0x4000) <> 0x0
         let codetype = implflags &&& 0x0003
         let unmanaged = (implflags &&& 0x0004) <> 0x0
         let forwardref = (implflags &&& 0x0010) <> 0x0
         let preservesig = (implflags &&& 0x0080) <> 0x0
         let internalcall = (implflags &&& 0x1000) <> 0x0
         let synchronized = (implflags &&& 0x0020) <> 0x0
         let noinline = (implflags &&& 0x0008) <> 0x0
         let mustrun = (implflags &&& 0x0040) <> 0x0
         let cctor = (nm = ".cctor")
         let ctor = (nm = ".ctor")
         let _generic,_genarity,cc,retty,argtys,_varargs = readBlobHeapAsMethodSig numtypars typeIdx
     
         let endParamIdx =
           if idx >= getNumRows TableNames.Method then 
             getNumRows TableNames.Param + 1
           else
             let (_,_,_,_,_, paramIdx) = seekReadMethodRow (idx + 1)
             paramIdx
     
         let ret,ilParams = seekReadParams (retty,argtys) paramIdx endParamIdx

         { MetadataToken=idx // This value is not a strict metadata token but it's good enough (if needed we could get the real one pretty easily)
           Name=nm
           Kind = 
               (if cctor then MethodKind.Cctor 
                elif ctor then MethodKind.Ctor 
                elif isStatic then MethodKind.Static 
                elif virt then 
                 MethodKind.Virtual 
                   { IsFinal=final 
                     IsNewSlot=newslot 
                     IsCheckAccessOnOverride=strict
                     IsAbstract=abstr }
                else MethodKind.NonVirtual)
           Access = memberAccessOfFlags flags
           //SecurityDecls=seekReadSecurityDecls (TaggedIndex(hds_MethodDef,idx))
           HasSecurity=hassec
           IsEntryPoint= (fst entryPointToken = TableNames.Method && snd entryPointToken = idx)
           IsReqSecObj=reqsecobj
           IsHideBySig=hidebysig
           IsSpecialName=specialname
           IsUnmanagedExport=export
           IsSynchronized=synchronized
           IsNoInline=noinline
           IsMustRun=mustrun
           IsPreserveSig=preservesig
           IsManaged = not unmanaged
           IsInternalCall = internalcall
           IsForwardRef = forwardref
           mdCodeKind = (if (codetype = 0x00) then MethodCodeKind.IL elif (codetype = 0x01) then MethodCodeKind.Native elif (codetype = 0x03) then MethodCodeKind.Runtime else MethodCodeKind.Native)
           GenericParams=seekReadGenericParams numtypars (TypeOrMethodDefTag.MethodDef,idx)
           CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.MethodDef,idx)) 
           Parameters= ilParams
           CallingConv=cc
           Return=ret
           mdBody=
             if (codetype = 0x01) && pinvoke then 
               ILMethodBody.Native
             elif pinvoke then 
               ILMethodBody.PInvoke
             elif internalcall || abstr || unmanaged || (codetype <> 0x00) then 
               ILMethodBody.Abstract
             else 
               ILMethodBody.IL   //seekReadMethodRVA (idx,nm,internalcall,noinline,numtypars) codeRVA   
         }
     
     
    and seekReadParams (retty,argtys) pidx1 pidx2 =
        let retRes : ILReturn ref =  ref { (* Marshal=None *) Type=retty; CustomAttrs=ILCustomAttrs.Empty }
        let paramsRes = 
            argtys 
            |> Array.map (fun ty ->  
                { Name=None;
                  Default=None;
                  //Marshal=None;
                  IsIn=false;
                  IsOut=false;
                  IsOptional=false;
                  Type=ty;
                  CustomAttrs=ILCustomAttrs.Empty })
        for i = pidx1 to pidx2 - 1 do
            seekReadParamExtras (retRes,paramsRes) i
        !retRes, paramsRes

    and seekReadParamExtras (retRes,paramsRes) (idx:int) =
       let (flags,seq,nameIdx) = seekReadParamRow idx
       let inOutMasked = (flags &&& 0x00FF)
       //let _hasMarshal = (flags &&& 0x2000) <> 0x0
       let hasDefault = (flags &&& 0x1000) <> 0x0
       //let fmReader idx = seekReadIndexedRow (getNumRows TableNames.FieldMarshal,seekReadFieldMarshalRow,fst,hfmCompare idx,isSorted TableNames.FieldMarshal,(snd >> readBlobHeapAsNativeType ctxt))
       let cas = seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.ParamDef,idx))
       if seq = 0 then
           retRes := { !retRes with 
                            //Marshal=(if hasMarshal then Some (fmReader (TaggedIndex(hfm_ParamDef,idx))) else None);
                            CustomAttrs = cas }
       else 
           paramsRes.[seq - 1] <- 
              { paramsRes.[seq - 1] with 
                   //Marshal=(if hasMarshal then Some (fmReader (TaggedIndex(hfm_ParamDef,idx))) else None);
                   Default = (if hasDefault then Some (seekReadConstant (TaggedIndex(HasConstantTag.ParamDef,idx))) else None);
                   Name = readStringHeapOption nameIdx;
                   IsIn = ((inOutMasked &&& 0x0001) <> 0x0);
                   IsOut = ((inOutMasked &&& 0x0002) <> 0x0);
                   IsOptional = ((inOutMasked &&& 0x0010) <> 0x0);
                   CustomAttrs =cas }
          
    and seekReadMethodImpls numtypars tidx =
       ILMethodImplDefs
          (lazy 
              let mimpls = seekReadIndexedRows (getNumRows TableNames.MethodImpl,seekReadMethodImplRow,(fun (a,_,_) -> a),simpleIndexCompare tidx,isSorted TableNames.MethodImpl,(fun (_,b,c) -> b,c))
              mimpls |> Array.map (fun (b,c) -> 
                  { OverrideBy=
                      let (MethodData(enclTyp, cc, nm, argtys, retty,minst)) = seekReadMethodDefOrRefNoVarargs numtypars b
                      mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty,minst);
                    Overrides=
                      let (MethodData(enclTyp, cc, nm, argtys, retty,minst)) = seekReadMethodDefOrRefNoVarargs numtypars c
                      let mspec = mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty,minst)
                      OverridesSpec(mspec.MethodRef, mspec.EnclosingType) }))

    and seekReadMultipleMethodSemantics (flags,id) =
        seekReadIndexedRows 
          (getNumRows TableNames.MethodSemantics ,
           seekReadMethodSemanticsRow,
           (fun (_flags,_,c) -> c),
           hsCompare id,
           isSorted TableNames.MethodSemantics,
           (fun (a,b,_c) -> 
               let (MethodData(enclTyp, cc, nm, argtys, retty, minst)) = seekReadMethodDefAsMethodData b
               a, (mkILMethSpecInTyRaw (enclTyp, cc, nm, argtys, retty, minst)).MethodRef))
        |> Array.filter (fun (flags2,_) -> flags = flags2) 
        |> Array.map snd 


    and seekReadOptionalMethodSemantics id =
        match seekReadMultipleMethodSemantics id with 
        | [| |] -> None
        | xs -> Some xs.[0]

    and seekReadMethodSemantics id =
        match seekReadOptionalMethodSemantics id with 
        | None -> failwith "seekReadMethodSemantics ctxt: no method found"
        | Some x -> x

    and seekReadEvent _numtypars idx =
       let (flags,nameIdx,_typIdx) = seekReadEventRow idx
       { Name = readStringHeap nameIdx;
         //EventHandlerType = seekReadOptionalTypeDefOrRef numtypars AsObject typIdx;
         IsSpecialName  = (flags &&& 0x0200) <> 0x0; 
         IsRTSpecialName = (flags &&& 0x0400) <> 0x0;
         AddMethod= seekReadMethodSemantics (0x0008,TaggedIndex(HasSemanticsTag.Event, idx));
         RemoveMethod=seekReadMethodSemantics (0x0010,TaggedIndex(HasSemanticsTag.Event,idx));
         //FireMethod=seekReadOptionalMethodSemantics (0x0020,TaggedIndex(HasSemanticsTag.Event,idx));
         //OtherMethods = seekReadMultipleMethodSemantics (0x0004, TaggedIndex(HasSemanticsTag.Event, idx));
         CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.Event,idx)) }
   
    and seekReadEvents numtypars tidx =
       mkILEventsLazy 
          (lazy 
               match seekReadOptionalIndexedRow (getNumRows TableNames.EventMap,(fun i -> i, seekReadEventMapRow i),(fun (_,row) -> fst row),compare tidx,false,(fun (i,row) -> (i,snd row))) with 
               | None -> [| |]
               | Some (rowNum,beginEventIdx) ->
                   let endEventIdx =
                       if rowNum >= getNumRows TableNames.EventMap then 
                           getNumRows TableNames.Event + 1
                       else
                           let (_, endEventIdx) = seekReadEventMapRow (rowNum + 1)
                           endEventIdx

                   [| for i in beginEventIdx .. endEventIdx - 1 do
                       yield seekReadEvent numtypars i |])

    and seekReadProperty numtypars idx =
       let (flags,nameIdx,typIdx) = seekReadPropertyRow idx
       let cc,retty,argtys = readBlobHeapAsPropertySig numtypars typIdx
       let setter= seekReadOptionalMethodSemantics (0x0001,TaggedIndex(HasSemanticsTag.Property,idx))
       let getter = seekReadOptionalMethodSemantics (0x0002,TaggedIndex(HasSemanticsTag.Property,idx))
       let cc2 =
           match getter with 
           | Some mref -> mref.CallingConv.ThisConv
           | None -> 
               match setter with 
               | Some mref ->  mref.CallingConv .ThisConv
               | None -> cc
       { Name=readStringHeap nameIdx;
         CallingConv = cc2;
         IsRTSpecialName=(flags &&& 0x0400) <> 0x0; 
         IsSpecialName= (flags &&& 0x0200) <> 0x0; 
         SetMethod=setter;
         GetMethod=getter;
         PropertyType=retty;
         Init= if (flags &&& 0x1000) = 0 then None else Some (seekReadConstant (TaggedIndex(HasConstantTag.Property,idx)));
         IndexParameterTypes=argtys;
         CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.Property,idx)) }
   
    and seekReadProperties numtypars tidx =
       mkILPropertiesLazy
          (lazy 
               match seekReadOptionalIndexedRow (getNumRows TableNames.PropertyMap,(fun i -> i, seekReadPropertyMapRow i),(fun (_,row) -> fst row),compare tidx,false,(fun (i,row) -> (i,snd row))) with 
               | None -> [| |]
               | Some (rowNum,beginPropIdx) ->
                   let endPropIdx =
                       if rowNum >= getNumRows TableNames.PropertyMap then 
                           getNumRows TableNames.Property + 1
                       else
                           let (_, endPropIdx) = seekReadPropertyMapRow (rowNum + 1)
                           endPropIdx
                   [| for i in beginPropIdx .. endPropIdx - 1 do
                         yield seekReadProperty numtypars i |])


    and seekReadCustomAttrs idx = 
        ILCustomAttrs
           (lazy seekReadIndexedRows (getNumRows TableNames.CustomAttribute,
                                      seekReadCustomAttributeRow,(fun (a,_,_) -> a),
                                      hcaCompare idx,
                                      isSorted TableNames.CustomAttribute,
                                      (fun (_,b,c) -> seekReadCustomAttr (b,c))))

    and seekReadCustomAttr (TaggedIndex(cat,idx),b) = cacheCustomAttr seekReadCustomAttrUncached (CustomAttrIdx (cat,idx,b))

    and seekReadCustomAttrUncached (CustomAttrIdx (cat,idx,valIdx)) = 
        { Method=seekReadCustomAttrType (TaggedIndex(cat,idx));
          Data=
            match readBlobHeapOption valIdx with
            | Some bytes -> bytes
            | None -> [| |] }

    (*
    and seekReadSecurityDecls idx = 
       mkILLazySecurityDecls
        (lazy
             seekReadIndexedRows (getNumRows TableNames.Permission,
                                     seekReadPermissionRow,
                                     (fun (_,par,_) -> par),
                                     hdsCompare idx,
                                     isSorted TableNames.Permission,
                                     (fun (act,_,ty) -> seekReadSecurityDecl (act,ty))))

    and seekReadSecurityDecl (a,b) = 
        ctxt.seekReadSecurityDecl (SecurityDeclIdx (a,b))

    and seekReadSecurityDeclUncached ctxtH (SecurityDeclIdx (act,ty)) = 
        PermissionSet ((if List.memAssoc (int act) (Lazy.force ILSecurityActionRevMap) then List.assoc (int act) (Lazy.force ILSecurityActionRevMap) else failwith "unknown security action"),
                       readBlobHeap ty)

    *)

    and seekReadConstant idx =
      let kind,vidx = seekReadIndexedRow (getNumRows TableNames.Constant,
                                          seekReadConstantRow,
                                          (fun (_,key,_) -> key), 
                                          hcCompare idx,isSorted TableNames.Constant,(fun (kind,_,v) -> kind,v))
      match kind with 
      | x when x = uint16 et_STRING -> 
        let blobHeap = readBlobHeap vidx
        let s = System.Text.Encoding.Unicode.GetString(blobHeap, 0, blobHeap.Length)
        ILFieldInit.String (s)  
      | x when x = uint16 et_BOOLEAN -> ILFieldInit.Bool (readBlobHeapAsBool vidx) 
      | x when x = uint16 et_CHAR -> ILFieldInit.Char (readBlobHeapAsUInt16 vidx) 
      | x when x = uint16 et_I1 -> ILFieldInit.Int8 (readBlobHeapAsSByte vidx) 
      | x when x = uint16 et_I2 -> ILFieldInit.Int16 (readBlobHeapAsInt16 vidx) 
      | x when x = uint16 et_I4 -> ILFieldInit.Int32 (readBlobHeapAsInt32 vidx) 
      | x when x = uint16 et_I8 -> ILFieldInit.Int64 (readBlobHeapAsInt64 vidx) 
      | x when x = uint16 et_U1 -> ILFieldInit.UInt8 (readBlobHeapAsByte vidx) 
      | x when x = uint16 et_U2 -> ILFieldInit.UInt16 (readBlobHeapAsUInt16 vidx) 
      | x when x = uint16 et_U4 -> ILFieldInit.UInt32 (readBlobHeapAsUInt32 vidx) 
      | x when x = uint16 et_U8 -> ILFieldInit.UInt64 (readBlobHeapAsUInt64 vidx) 
      | x when x = uint16 et_R4 -> ILFieldInit.Single (readBlobHeapAsSingle vidx) 
      | x when x = uint16 et_R8 -> ILFieldInit.Double (readBlobHeapAsDouble vidx) 
      | x when x = uint16 et_CLASS || x = uint16 et_OBJECT ->  ILFieldInit.Null
      | _ -> ILFieldInit.Null

    and seekReadManifestResources () = 
        ILResources 
          (lazy
             [| for i = 1 to getNumRows TableNames.ManifestResource do
                 let (offset,flags,nameIdx,implIdx) = seekReadManifestResourceRow i
                 let scoref = seekReadImplAsScopeRef implIdx
                 let datalab = 
                   match scoref with
                   | ILScopeRef.Local -> 
                      let start = anyV2P ("resource",offset + resourcesAddr)
                      let len = seekReadInt32 is start
                      ILResourceLocation.Local (fun () -> seekReadBytes is (start + 4) len)
                   | ILScopeRef.Module mref -> ILResourceLocation.File (mref,offset)
                   | ILScopeRef.Assembly aref -> ILResourceLocation.Assembly aref

                 let r = 
                   { Name= readStringHeap nameIdx;
                     Location = datalab;
                     Access = (if (flags &&& 0x01) <> 0x0 then ILResourceAccess.Public else ILResourceAccess.Private);
                     CustomAttrs =  seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.ManifestResource, i)) }
                 yield r |])

    and seekReadNestedExportedTypes parentIdx = 
        ILNestedExportedTypesAndForwarders
          (lazy
             [| for i = 1 to getNumRows TableNames.ExportedType do
                   let (flags,_tok,nameIdx,namespaceIdx,implIdx) = seekReadExportedTypeRow i
                   if not (isTopTypeDef flags) then
                       let (TaggedIndex(tag,idx) ) = implIdx
                       match tag with 
                       | tag when tag = ImplementationTag.ExportedType && idx = parentIdx  ->
                           let _nsp, nm = readStringHeapAsTypeName (nameIdx,namespaceIdx)
                           yield 
                             { Name=nm
                               Access=(match typeAccessOfFlags flags with ILTypeDefAccess.Nested n -> n | _ -> failwith "non-nested access for a nested type described as being in an auxiliary module")
                               Nested=seekReadNestedExportedTypes i
                               CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.ExportedType, i)) } 
                       | _ -> () |])
      
    and seekReadTopExportedTypes () = 
        ILExportedTypesAndForwarders
          (lazy
             [| for i = 1 to getNumRows TableNames.ExportedType do
                 let (flags,_tok,nameIdx,namespaceIdx,implIdx) = seekReadExportedTypeRow i
                 if isTopTypeDef flags then 
                   let (TaggedIndex(tag,_idx) ) = implIdx
               
                   // the nested types will be picked up by their enclosing types
                   if tag <> ImplementationTag.ExportedType then
                       let nsp, nm = readStringHeapAsTypeName (nameIdx,namespaceIdx)
                   
                       let scoref = seekReadImplAsScopeRef implIdx
                        
                       let entry = 
                         { ScopeRef=scoref
                           Namespace=nsp
                           Name=nm
                           IsForwarder =   ((flags &&& 0x00200000) <> 0)
                           Access=typeAccessOfFlags flags
                           Nested=seekReadNestedExportedTypes i
                           CustomAttrs=seekReadCustomAttrs (TaggedIndex(HasCustomAttributeTag.ExportedType, i)) } 
                       yield entry |])

     
    let ilModule = seekReadModule 1
    let ilAssemblyRefs = [ for i in 1 .. getNumRows TableNames.AssemblyRef do yield seekReadAssemblyRef i ]

    member x.ILModuleDef = ilModule
    member x.ILAssemblyRefs = ilAssemblyRefs
    
let sigptr_get_byte (bytes: byte[]) sigptr = 
    int bytes.[sigptr], sigptr + 1

let sigptr_get_u8 bytes sigptr = 
    let b0,sigptr = sigptr_get_byte bytes sigptr
    byte b0,sigptr

let sigptr_get_bool bytes sigptr = 
    let b0,sigptr = sigptr_get_byte bytes sigptr
    (b0 = 0x01) ,sigptr

let sigptr_get_i8 bytes sigptr = 
    let i,sigptr = sigptr_get_u8 bytes sigptr
    sbyte i,sigptr

let sigptr_get_u16 bytes sigptr = 
    let b0,sigptr = sigptr_get_byte bytes sigptr
    let b1,sigptr = sigptr_get_byte bytes sigptr
    uint16 (b0 ||| (b1 <<< 8)),sigptr

let sigptr_get_i16 bytes sigptr = 
    let u,sigptr = sigptr_get_u16 bytes sigptr
    int16 u,sigptr

let sigptr_get_i32 bytes sigptr = 
    let b0,sigptr = sigptr_get_byte bytes sigptr
    let b1,sigptr = sigptr_get_byte bytes sigptr
    let b2,sigptr = sigptr_get_byte bytes sigptr
    let b3,sigptr = sigptr_get_byte bytes sigptr
    b0 ||| (b1 <<< 8) ||| (b2 <<< 16) ||| (b3 <<< 24),sigptr

let sigptr_get_u32 bytes sigptr = 
    let u,sigptr = sigptr_get_i32 bytes sigptr
    uint32 u,sigptr

let sigptr_get_i64 bytes sigptr = 
    let b0,sigptr = sigptr_get_byte bytes sigptr
    let b1,sigptr = sigptr_get_byte bytes sigptr
    let b2,sigptr = sigptr_get_byte bytes sigptr
    let b3,sigptr = sigptr_get_byte bytes sigptr
    let b4,sigptr = sigptr_get_byte bytes sigptr
    let b5,sigptr = sigptr_get_byte bytes sigptr
    let b6,sigptr = sigptr_get_byte bytes sigptr
    let b7,sigptr = sigptr_get_byte bytes sigptr
    int64 b0 ||| (int64 b1 <<< 8) ||| (int64 b2 <<< 16) ||| (int64 b3 <<< 24) |||
    (int64 b4 <<< 32) ||| (int64 b5 <<< 40) ||| (int64 b6 <<< 48) ||| (int64 b7 <<< 56),
    sigptr

let sigptr_get_u64 bytes sigptr = 
    let u,sigptr = sigptr_get_i64 bytes sigptr
    uint64 u,sigptr


let ieee32_of_bits (x:int32) = System.BitConverter.ToSingle(System.BitConverter.GetBytes(x),0)
let ieee64_of_bits (x:int64) = System.BitConverter.Int64BitsToDouble(x)

let sigptr_get_ieee32 bytes sigptr = 
    let u,sigptr = sigptr_get_i32 bytes sigptr
    ieee32_of_bits u,sigptr

let sigptr_get_ieee64 bytes sigptr = 
    let u,sigptr = sigptr_get_i64 bytes sigptr
    ieee64_of_bits u,sigptr

let rec decodeCustomAttrElemType ilg bytes sigptr x = 
    match x with
    | x when x =  et_I1 -> ilg.typ_SByte, sigptr
    | x when x = et_U1 -> ilg.typ_Byte, sigptr
    | x when x =  et_I2 -> ilg.typ_Int16, sigptr
    | x when x =  et_U2 -> ilg.typ_UInt16, sigptr
    | x when x =  et_I4 -> ilg.typ_Int32, sigptr
    | x when x =  et_U4 -> ilg.typ_UInt32, sigptr
    | x when x =  et_I8 -> ilg.typ_Int64, sigptr
    | x when x =  et_U8 -> ilg.typ_UInt64, sigptr
    | x when x =  et_R8 -> ilg.typ_Double, sigptr
    | x when x =  et_R4 -> ilg.typ_Single, sigptr
    | x when x = et_CHAR -> ilg.typ_Char, sigptr
    | x when x =  et_BOOLEAN -> ilg.typ_Boolean, sigptr
    | x when x =  et_STRING -> ilg.typ_String, sigptr
    | x when x =  et_OBJECT -> ilg.typ_Object, sigptr
    | x when x =  et_SZARRAY -> 
         let et,sigptr = sigptr_get_u8 bytes sigptr 
         let elemTy,sigptr = decodeCustomAttrElemType ilg bytes sigptr et
         mkILArr1DTy elemTy, sigptr
    | x when x = 0x50uy -> ilg.typ_Type, sigptr
    | _ ->  failwithf "decodeCustomAttrElemType ilg: unrecognized custom element type: %A" x

// Parse an IL type signature argument within a custom attribute blob
type ILTypeSigParser(tstring : string) =

    let mutable startPos = 0
    let mutable currentPos = 0

    //let reset() = startPos <- 0 ; currentPos <- 0
    let nil = '\r' // cannot appear in a type sig

    // take a look at the next value, but don't advance
    let peek() = if currentPos < (tstring.Length-1) then tstring.[currentPos+1] else nil
    let peekN(skip) = if currentPos < (tstring.Length - skip) then tstring.[currentPos+skip] else nil
    // take a look at the current value, but don't advance
    let here() = if currentPos < tstring.Length then tstring.[currentPos] else nil
    // move on to the next character
    let step() = currentPos <- currentPos+1
    // ignore the current lexeme
    let skip() = startPos <- currentPos
    // ignore the current lexeme, advance
    let drop() = skip() ; step() ; skip()
    // return the current lexeme, advance
    let take() = 
        let s = if currentPos < tstring.Length then tstring.[startPos..currentPos] else ""
        drop()
        s

    // The format we accept is
    // "<type name>{`<arity>[<type>,+]}{<array rank>}{<scope>}"  E.g.,
    //
    // System.Collections.Generic.Dictionary
    //     `2[
    //         [System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],
    //         dev.virtualearth.net.webservices.v1.search.CategorySpecificPropertySet], 
    // mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
    //
    // Note that 
    //   • Since we're only reading valid IL, we assume that the signature is properly formed
    //   • For type parameters, if the type is non-local, it will be wrapped in brackets ([])
    member x.ParseType() =

        // Does the type name start with a leading '['?  If so, ignore it
        // (if the specialization type is in another module, it will be wrapped in bracket)
        if here() = '[' then drop()
    
        // 1. Iterate over beginning of type, grabbing the type name and determining if it's generic or an array
        let typeName = 
            while (peek() <> '`') && (peek() <> '[') && (peek() <> ']') && (peek() <> ',') && (peek() <> nil) do step()
            take()
    
        // 2. Classify the type

        // Is the type generic?
        let typeName, specializations = 
            if here() = '`' then
                drop() // step to the number
                // fetch the arity
                let arity = 
                    while (int(here()) >= (int('0'))) && (int(here()) <= ((int('9')))) && (int(peek()) >= (int('0'))) && (int(peek()) <= ((int('9')))) do step()
                    System.Int32.Parse(take())
                // skip the '['
                drop()
                // get the specializations
                typeName+"`"+(arity.ToString()), Some(([| for _i in 0..arity-1 do yield x.ParseType() |]))
            else
                typeName, None

        // Is the type an array?
        let rank = 
            if here() = '[' then
                let mutable rank = 0

                while here() <> ']' do
                    rank <- rank + 1
                    step()
                drop()

                Some(ILArrayShape(Array.create rank (Some 0, None)))
            else
                None

        // Is there a scope?
        let scope = 
            if (here() = ',' || here() = ' ') && (peek() <> '[' && peekN(2) <> '[') then
                let grabScopeComponent() =
                    if here() = ',' then drop() // ditch the ','
                    if here() = ' ' then drop() // ditch the ' '

                    while (peek() <> ',' && peek() <> ']' && peek() <> nil) do step()
                    take()

                let scope =
                    [ yield grabScopeComponent() // assembly
                      yield grabScopeComponent() // version
                      yield grabScopeComponent() // culture
                      yield grabScopeComponent() // public key token
                    ] |> String.concat ","
                ILScopeRef.Assembly(ILAssemblyRef.FromAssemblyName(System.Reflection.AssemblyName(scope)))        
            else
                ILScopeRef.Local

        // strip any extraneous trailing brackets or commas
        if (here() = ']')  then drop()
        if (here() = ',') then drop()

        // build the IL type
        let tref =
            let nsp, nm = splitILTypeName typeName
            ILTypeRef(ILTypeRefScope.Top scope, nsp, nm)

        let genericArgs = 
            match specializations with
            | None -> [| |]
            | Some(genericArgs) -> genericArgs
        let tspec = ILTypeSpec(tref,genericArgs)
        let ilty = 
            match tspec.Name with
            | "System.SByte"
            | "System.Byte"
            | "System.Int16"
            | "System.UInt16"
            | "System.Int32"
            | "System.UInt32"
            | "System.Int64"
            | "System.UInt64"
            | "System.Char"
            | "System.Double"
            | "System.Single"
            | "System.Boolean" -> ILType.Value(tspec)
            | _ -> ILType.Boxed(tspec)

        // if it's an array, wrap it - otherwise, just return the IL type
        match rank with
        | Some(r) -> ILType.Array(r,ilty)
        | _ -> ilty
    

let sigptr_get_z_i32 bytes sigptr = 
    let b0,sigptr = sigptr_get_byte bytes sigptr
    if b0 <= 0x7F then b0, sigptr
    elif b0 <= 0xbf then 
        let b0 = b0 &&& 0x7f
        let b1,sigptr = sigptr_get_byte bytes sigptr
        (b0 <<< 8) ||| b1, sigptr
    else 
        let b0 = b0 &&& 0x3f
        let b1,sigptr = sigptr_get_byte bytes sigptr
        let b2,sigptr = sigptr_get_byte bytes sigptr
        let b3,sigptr = sigptr_get_byte bytes sigptr
        (b0 <<< 24) ||| (b1 <<< 16) ||| (b2 <<< 8) ||| b3, sigptr

let sigptr_get_bytes n (bytes:byte[]) sigptr = 
    let res = Array.zeroCreate n
    for i = 0 to n - 1 do 
        res.[i] <- bytes.[sigptr + i]
    res, sigptr + n

let sigptr_get_string n bytes sigptr = 
    let intarray,sigptr = sigptr_get_bytes n bytes sigptr
    System.Text.Encoding.UTF8.GetString(intarray , 0, intarray.Length), sigptr

let sigptr_get_serstring  bytes sigptr = 
    let len,sigptr = sigptr_get_z_i32 bytes sigptr 
    sigptr_get_string len bytes sigptr 

let sigptr_get_serstring_possibly_null  bytes sigptr = 
    let b0,new_sigptr = sigptr_get_byte bytes sigptr  
    if b0 = 0xFF then // null case
        None,new_sigptr
    else  // throw away  new_sigptr, getting length & text advance
        let len,sigptr = sigptr_get_z_i32 bytes sigptr 
        let s, sigptr = sigptr_get_string len bytes sigptr
        Some(s),sigptr


let decodeILCustomAttribData ilg (ca: ILCustomAttr) : ILCustomAttrArg list  = 
    let bytes = ca.Data
    let sigptr = 0
    let bb0,sigptr = sigptr_get_byte bytes sigptr
    let bb1,sigptr = sigptr_get_byte bytes sigptr
    if not (bb0 = 0x01 && bb1 = 0x00) then failwith "decodeILCustomAttribData: invalid data";

    let rec parseVal argty sigptr = 
      match argty with 
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "SByte" ->  
          let n,sigptr = sigptr_get_i8 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "Byte" ->  
          let n,sigptr = sigptr_get_u8 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "Int16" ->  
          let n,sigptr = sigptr_get_i16 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "UInt16" ->  
          let n,sigptr = sigptr_get_u16 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "Int32" ->  
          let n,sigptr = sigptr_get_i32 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "UInt32" ->  
          let n,sigptr = sigptr_get_u32 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "Int64" ->  
          let n,sigptr = sigptr_get_i64 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "UInt64" ->  
          let n,sigptr = sigptr_get_u64 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "Double" ->  
          let n,sigptr = sigptr_get_ieee64 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "Single" ->  
          let n,sigptr = sigptr_get_ieee32 bytes sigptr
          (argty, box n), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "Char" ->  
          let n,sigptr = sigptr_get_u16 bytes sigptr
          (argty, box (char n)), sigptr
      | ILType.Value tspec when tspec.Namespace = Some "System" && tspec.Name = "Boolean" ->  
          let n,sigptr = sigptr_get_byte bytes sigptr
          (argty, box (not (n = 0))), sigptr
      | ILType.Boxed tspec when tspec.Namespace = Some "System" && tspec.Name = "String" ->  
          let n,sigptr = sigptr_get_serstring_possibly_null bytes sigptr
          (argty, box (match n with None -> null | Some s -> s)), sigptr
      | ILType.Boxed tspec when tspec.Namespace = Some "System" && tspec.Name = "Type" ->  
          let nOpt,sigptr = sigptr_get_serstring_possibly_null bytes sigptr
          match nOpt with
          | None -> (argty, box null) , sigptr // TODO: read System.Type attributes
          | Some n -> 
            try
                let parser = ILTypeSigParser(n)
                parser.ParseType() |> ignore
                (argty, box null) , sigptr // TODO: read System.Type attributes
            with e ->
                failwith (sprintf "decodeILCustomAttribData: error parsing type in custom attribute blob: %s" e.Message)
      | ILType.Boxed tspec when tspec.Namespace = Some "System" && tspec.Name = "Object" ->  
          let et,sigptr = sigptr_get_u8 bytes sigptr
          if et = 0xFFuy then 
              (argty, null), sigptr
          else
              let ty,sigptr = decodeCustomAttrElemType ilg bytes sigptr et 
              parseVal ty sigptr 
      | ILType.Array(shape,elemTy) when shape = ILArrayShape.SingleDimensional ->  
          let n,sigptr = sigptr_get_i32 bytes sigptr
          if n = 0xFFFFFFFF then (argty, null),sigptr else
          let rec parseElems acc n sigptr = 
            if n = 0 then List.rev acc else
            let v,sigptr = parseVal elemTy sigptr
            parseElems (v ::acc) (n-1) sigptr
          let elems = parseElems [] n sigptr |> List.map snd |> List.toArray
          (argty, box elems), sigptr
      | ILType.Value _ ->  (* assume it is an enumeration *)
          let n,sigptr = sigptr_get_i32 bytes sigptr
          (argty, box n), sigptr
      | _ ->  failwith "decodeILCustomAttribData: attribute data involves an enum or System.Type value"
    let rec parseFixed argtys sigptr = 
      match argtys with 
        [] -> [],sigptr
      | h::t -> 
          let nh,sigptr = parseVal h sigptr
          let nt,sigptr = parseFixed t sigptr
          nh ::nt, sigptr
    let fixedArgs,_sigptr = parseFixed (List.ofArray ca.Method.FormalArgTypes) sigptr
(*
    let nnamed,sigptr = sigptr_get_u16 bytes sigptr
    let rec parseNamed acc n sigptr = 
      if n = 0 then List.rev acc else
      let isPropByte,sigptr = sigptr_get_u8 bytes sigptr
      let isProp = (int isPropByte = 0x54)
      let et,sigptr = sigptr_get_u8 bytes sigptr
      // We have a named value 
      let ty,sigptr = 
        if (0x50 = (int et) || 0x55 = (int et)) then
            let qualified_tname,sigptr = sigptr_get_serstring bytes sigptr
            let unqualified_tname, rest = 
                let pieces = qualified_tname.Split(',')
                if pieces.Length > 1 then 
                    pieces.[0], Some (String.concat "," pieces.[1..])
                else 
                    pieces.[0], None
            let scoref = 
                match rest with 
                | Some aname -> ILTypeRefScope.Top(ILScopeRef.Assembly(ILAssemblyRef.FromAssemblyName(System.Reflection.AssemblyName(aname))))
                | None -> ilg.typ_Boolean.TypeSpec.Scope

            let nsp, nm = splitILTypeName unqualified_tname
            let tref = ILTypeRef (scoref, nsp, nm)
            let tspec = mkILNonGenericTySpec tref
            ILType.Value(tspec),sigptr            
        else
            decodeCustomAttrElemType ilg bytes sigptr et
      let nm,sigptr = sigptr_get_serstring bytes sigptr
      let (_,v),sigptr = parseVal ty sigptr
      parseNamed ((nm,ty,isProp,v) :: acc) (n-1) sigptr
    let named = parseNamed [] (int nnamed) sigptr
    fixedArgs, named     
*)
    fixedArgs

  
let OpenILModuleReaderAfterReadingAllBytes (infile, systemRuntimeScopeRef) = 
    let mc = ByteFile.OpenIn infile
    ILModuleReader(infile, mc, systemRuntimeScopeRef)


(* NOTE: ecma_ prefix refers to the standard "mscorlib" *)
let ecmaPublicKey = PublicKeyToken ([|0xdeuy; 0xaduy; 0xbeuy; 0xefuy; 0xcauy; 0xfeuy; 0xfauy; 0xceuy |]) 
let ecmaMscorlibScopeRef = ILScopeRef.Assembly (ILAssemblyRef("mscorlib", None, Some ecmaPublicKey, true, None, None))

// -------------------------------------------------------------------- 
// Reflection converters
// -------------------------------------------------------------------- 

let convFieldInit x = 
    match x with 
    | ILFieldInit.String s       -> box s
    | ILFieldInit.Bool bool      -> box bool   
    | ILFieldInit.Char u16       -> box (char (int u16))  
    | ILFieldInit.Int8 i8        -> box i8     
    | ILFieldInit.Int16 i16      -> box i16    
    | ILFieldInit.Int32 i32      -> box i32    
    | ILFieldInit.Int64 i64      -> box i64    
    | ILFieldInit.UInt8 u8       -> box u8     
    | ILFieldInit.UInt16 u16     -> box u16    
    | ILFieldInit.UInt32 u32     -> box u32    
    | ILFieldInit.UInt64 u64     -> box u64    
    | ILFieldInit.Single ieee32 -> box ieee32 
    | ILFieldInit.Double ieee64 -> box ieee64 
    | ILFieldInit.Null            -> (null :> Object)


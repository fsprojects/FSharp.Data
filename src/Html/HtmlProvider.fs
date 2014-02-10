// --------------------------------------------------------------------------------------
// HTML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Text
open System.IO
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open ProviderImplementation.QuotationBuilder
open FSharp.Data.Runtime
open FSharp.Net
open System.Collections.Generic
open FSharp.Data.Runtime.StructuralTypes

type private FieldInfo = 
  { TypeForTuple : Type
    Property : ProvidedProperty
    Convert: Expr -> Expr
    ConvertBack: Expr -> Expr } 

module Helpers = 

    type Cache private () =
       static let mutable instance = Dictionary<_, _>()
       static member Instance = instance   
          
    let memoize f =
        fun n ->
            match Cache.Instance.TryGetValue(n) with
            | (true, v) -> v
            | _ ->
                let temp = f(n)
                Cache.Instance.Add(n, temp)
                temp   
    
[<TypeProvider>]
type public HtmlProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.Experimental.HtmlProvider'
  let asm, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data.Experimental"
  let htmlProvTy = ProvidedTypeDefinition(asm, ns, "HtmlProvider", Some typeof<obj>)
  
  
  let buildTypes (typeName:string,sample,culture,resolutionFolder) =
      
      let cultureInfo = TextRuntime.GetCulture culture
      let missingValues = String.Join(",", TextConversions.DefaultMissingValues)
      let generatedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
      let tableContainer = ProvidedTypeDefinition("Tables", Some typeof<obj>)
      let (dom, _) = ProviderImplementation.ProviderHelpers.parseTextAtDesignTime sample (fun _ sample -> Html.Table.parse (sample)) "HTML" this cfg resolutionFolder

      let getInferedRowType culture (table:HtmlTable) = 
          let inferedTypeToProperty name optional (typ:InferedType) = 
                match typ with
                | InferedType.Primitive(typ, unitType) -> PrimitiveInferedProperty.Create(name, typ, optional)                              
                | _ -> PrimitiveInferedProperty.Create(name, typeof<string>, optional) 

          let inferRowType' culture (headers:string list) values = 
              let inferProperty index value =
                  {
                      Name = (if List.isEmpty headers && index >= headers.Length then "Column_" + (string index) else headers.[index])
                      Optional = false
                      Type = (StructuralInference.inferPrimitiveType culture value None)
                  }
              StructuralTypes.InferedType.Record(None, values |> List.mapi inferProperty)
               
          let inferedType =
              table.Rows
              |> Seq.map (inferRowType' culture table.Headers)
              |> Seq.reduce (StructuralInference.subtypeInfered true)

          match inferedType with
          | StructuralTypes.InferedType.Record(_, props) -> 
              inferedType, props |> List.map (fun p -> inferedTypeToProperty p.Name p.Optional p.Type)
          | _ -> failwith "expected record" 

      let providedTableTypes = 
          dom
          |> List.map (fun table ->
                let _, props = getInferedRowType cultureInfo table
                let fields = props |> List.mapi (fun index field ->
                    let typ, typWithoutMeasure, conv, convBack = ConversionsGenerator.convertStringValue replacer missingValues culture field
                    { TypeForTuple = typWithoutMeasure
                      Property = ProvidedProperty(field.Name, typ, GetterCode = fun (Singleton row) -> Expr.TupleGet(row, index))
                      Convert = fun rowVarExpr -> conv <@ TextConversions.AsString((%%rowVarExpr:string[]).[index]) @>
                      ConvertBack = fun rowVarExpr -> convBack (Expr.TupleGet(rowVarExpr, index)) } )
                // The erased row type will be a tuple of all the field types (without the units of measure)
                let rowErasedType = 
                  FSharpType.MakeTupleType([| for field in fields -> field.TypeForTuple |])
                  |> replacer.ToRuntime
                
                let rowType = ProvidedTypeDefinition("Row", Some rowErasedType, HideObjectMethods = true)
                
                // Each property of the generated row type will simply be a tuple get
                for field in fields do
                  rowType.AddMember field.Property

                let tableErasedWithRowErasedType = (replacer.ToRuntime typedefof<HtmlTable<_>>).MakeGenericType(rowErasedType)
                let tableErasedTypeWithGeneratedRow = (replacer.ToRuntime typedefof<HtmlTable<_>>).MakeGenericType(rowType)
                let tableType = ProvidedTypeDefinition(table.Name, Some tableErasedTypeWithGeneratedRow)
                tableType.AddMember(rowType)

                let rowConverter =             
                    let rowVar = Var("row", typeof<string[]>)
                    let rowVarExpr = Expr.Var rowVar
                    let body = 
                      Expr.NewTuple [ for field in fields -> field.Convert rowVarExpr ]
                      |> replacer.ToRuntime

                    let delegateType = 
                      typedefof<Func<_,_>>.MakeGenericType(typeof<string[]>, rowErasedType)

                    Expr.NewDelegate(delegateType, [rowVar], body)

                let args = [ ProvidedParameter("text", typeof<string>) ]
                let m = ProvidedMethod("Load", args, tableType, IsStaticMethod = true)
                m.InvokeCode <- (fun (Singleton text) -> 
                                    let stringArrayToRowVar = Var("rowConveter", rowConverter.Type)
                                    let body = 
                                        tableErasedWithRowErasedType?Create () (Expr.Var stringArrayToRowVar, table.Name, table.Headers, text)
                                    Expr.Let(stringArrayToRowVar, rowConverter, body)
                                )
                tableType.AddMember(m)
                tableType
             )
      tableContainer.AddMembers(providedTableTypes |> Seq.toList)
      generatedType.AddMember(tableContainer)
      generatedType

  let parameters = 
    [ 
        ProvidedStaticParameter("Sample", typeof<string>)
        ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
        ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") 
    ] 

  do htmlProvTy.DefineStaticParameters(
                parameters, 
                (fun typeName [|:? string as sample; :? string as culture; :? string as resolutionFolder |] -> 
                        Helpers.memoize buildTypes (typeName, sample, culture, resolutionFolder)
                ))

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ htmlProvTy ])
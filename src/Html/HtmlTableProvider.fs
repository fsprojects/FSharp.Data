// --------------------------------------------------------------------------------------
// HTML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

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
type public HtmlTableProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.HtmlTableProvider'
  let asm, _,replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let htmlProvTy = ProvidedTypeDefinition(asm, ns, "HtmlTableProvider", Some typeof<obj>)  
  
  let buildTypes (typeName:string,sample,preferOptionals,culture,resolutionFolder) =
      
      let cultureInfo = TextRuntime.GetCulture culture
      let missingValues = String.Join(",", TextConversions.DefaultMissingValues)
      let generatedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
      let tableContainer = ProvidedTypeDefinition("Tables", Some typeof<obj>)
      let (dom, _, _) = ProviderImplementation.ProviderHelpers.parseTextAtDesignTime sample (fun _ sample -> Html.Table.parse sample) "HTML" this cfg resolutionFolder

      let getInferedRowType culture (table:HtmlTable) = 
          let rec inferedTypeToProperty name (typ:InferedType) =
              match typ with
              | InferedType.Primitive(typ, unit, optional) -> 
                  let wrapper = 
                      if optional
                      then if preferOptionals then TypeWrapper.Option else TypeWrapper.Nullable
                      else TypeWrapper.None
                  PrimitiveInferedProperty.Create(name, typ, wrapper, unit)
              | InferedType.Null -> PrimitiveInferedProperty.Create(name, typeof<float>, false, None)
              | _ -> PrimitiveInferedProperty.Create(name, typeof<string>, preferOptionals, None)

          let inferRowType' culture (headers:string[]) values = 
              let getName headers index = 
                if Array.isEmpty headers && index >= headers.Length && (String.IsNullOrEmpty(headers.[index]))
                then "Column_" + (string index) 
                else headers.[index]
              let inferProperty index value =
                  let inferedtype = 
                      if String.IsNullOrWhiteSpace value || value = "&nbsp;" || value = "&nbsp" then InferedType.Null
                      elif Array.exists ((=) <| value.Trim()) TextConversions.DefaultMissingValues 
                      then InferedType.Null 
                      // if preferOptionals then InferedType.Null else InferedType.Primitive(typeof<float>, None)
                      else StructuralInference.getInferedTypeFromString culture value None
                  { Name = (getName headers index)
                    Type = inferedtype }
              StructuralTypes.InferedType.Record(None, values |> Array.mapi inferProperty |> Seq.toList, false)
               
          let inferedType =
              table.Rows
              |> Seq.map (inferRowType' culture table.Headers)
              |> Seq.reduce (StructuralInference.subtypeInfered (not preferOptionals))

          match inferedType with
          | StructuralTypes.InferedType.Record(_, props, false) -> 
              inferedType, props |> List.map (fun p -> inferedTypeToProperty p.Name p.Type)
          | _ -> failwith "expected record" 

      let providedTableTypes = 
          dom
          |> List.filter (fun table -> table.Headers.Length > 0)
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
                                        tableErasedWithRowErasedType?Create () (Expr.Var stringArrayToRowVar, table.Name, text)
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
        ProvidedStaticParameter("PreferOptionals", typeof<bool>, false)
        ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
        ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") 
    ] 

  do htmlProvTy.DefineStaticParameters(
                parameters, 
                (fun typeName [|:? string as sample; :? bool as preferOptionals; :? string as culture; :? string as resolutionFolder |] -> 
                        Helpers.memoize buildTypes (typeName, sample, preferOptionals, culture, resolutionFolder)
                ))

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ htmlProvTy ])
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

type private FieldInfo = 
  { TypeForTuple : Type
    Property : ProvidedProperty
    Convert: Expr -> Expr
    ConvertBack: Expr -> Expr }     

[<TypeProvider>]
type public HtmlProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.Experimental.HtmlProvider'
  let asm, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data.Experimental"
  let htmlProvTy = ProvidedTypeDefinition(asm, ns, "HtmlProvider", Some typeof<obj>)
  
  
  let buildTypes (typeName:string) (args:obj[]) =
      
      let sample = args.[0] :?> string
      let culture = args.[1] :?> string
      let cultureInfo = TextRuntime.GetCulture culture
      let missingValues = String.Join(",", TextConversions.DefaultMissingValues)
      //let resolutionFolder = args.[1] :?> string
      //TODO: Sample currently assumed to be a url 
      let generatedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
      let typeContainer = ProvidedTypeDefinition("TypeContainer", Some typeof<obj>)
      let tableContainer = ProvidedTypeDefinition("Tables", Some typeof<obj>)


      generatedType.AddMember(typeContainer)
      let body =
        match Uri.TryCreate(sample,UriKind.Absolute) with
        | true, uri ->
            let response = FSharp.Net.Http.Request(uri.AbsoluteUri)
            match response.Body with
            | ResponseBody.Text(text) -> Encoding.UTF8.GetBytes(text)
            | ResponseBody.Binary(bytes) -> bytes
        | false, _ -> 
            Encoding.UTF8.GetBytes(sample)

      use ms = new MemoryStream(body)
      use sr = new StreamReader(ms)
      let dom = HtmlElement.Parse(sr)
      let providedTableTypes = 
          dom.Tables()
          |> Seq.map (fun table ->
                let _, props = table.GetInferedRowType()
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
                let tableType = ProvidedTypeDefinition(table.Id, Some tableErasedTypeWithGeneratedRow)
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
                let m = ProvidedMethod("Parse", args, tableType, IsStaticMethod = true)
                m.InvokeCode <- (fun (Singleton text) -> 
                                    let stringArrayToRowVar = Var("rowConveter", rowConverter.Type)
                                    let body = 
                                        tableErasedWithRowErasedType?Create () (Expr.Var stringArrayToRowVar, table.Id, table.Headers, text)
                                    Expr.Let(stringArrayToRowVar, rowConverter, body)
                                )
                tableType.AddMember(m)
                //System.Diagnostics.Debug.WriteLine (Debug.prettyPrint false false 10 120 tableType)
                tableType
             )
      tableContainer.AddMembers(providedTableTypes |> Seq.toList)
      generatedType.AddMember(tableContainer)
      generatedType

  let parameters = 
    [ 
        ProvidedStaticParameter("Sample", typeof<string>)
        ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
    ] 

  do htmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ htmlProvTy ])
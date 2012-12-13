// --------------------------------------------------------------------------------------

#load "literate.fsx"
open System.IO

let templateFile = Path.Combine(__SOURCE_DIRECTORY__, "template.html")
let template = File.ReadAllText(templateFile)
let sources = Path.Combine(__SOURCE_DIRECTORY__, "../samples")
let output = Path.Combine(__SOURCE_DIRECTORY__, "../docs")

Literate.transform template sources output

(*
#r "../packages/FSPowerPack.Metadata.Community.2.0.0.1/Lib/Net40/FSharp.PowerPack.Metadata.dll"
open Microsoft.FSharp.Metadata

let bin = Path.Combine(__SOURCE_DIRECTORY__, "../bin/FSharp.Data.dll")
let asm = FSharpAssembly.FromFile(bin)


for entry in asm.Entities do
  printfn "/// %s" entry.XmlDocSig
  if entry.IsAbbreviation then
    printfn "type %s = ?" entry.DisplayName
  elif entry.IsExceptionDeclaration then
    printfn "exception %s = ?" entry.DisplayName
  elif entry.IsExternal then
    printfn "extern %s = ?" entry.DisplayName
  elif entry.IsMeasure then
    printfn "type [<Measure>] %s = ?" entry.DisplayName
  elif entry.IsModule then
    printfn "module %s = ?" entry.DisplayName
  elif entry.IsRecord then
    printfn "type %s = { .. }" entry.DisplayName
  elif entry.IsUnion then
    printfn "type %s = .. | .." entry.DisplayName
  elif entry.IsValueType then
    printfn "type %s = .. = 0 | .. = 1" entry.DisplayName
  else 
    printfn "type %s = UNKNOWN" entry.DisplayName
  printfn "%A\n" entry.ReflectionType
*)
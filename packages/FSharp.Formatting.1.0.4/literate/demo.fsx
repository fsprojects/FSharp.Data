(*
*)
#load "literate.fsx"
open FSharp.Literate

do
  let file = __SOURCE_DIRECTORY__ + "\\literate.fsx"
  let template = __SOURCE_DIRECTORY__ + "\\templates\\template-file.html"
  Literate.ProcessScriptFile(file, template)


do
  let dir = __SOURCE_DIRECTORY__
  let template = __SOURCE_DIRECTORY__ + "\\templates\\template-project.html"
  let projInfo =
    [ "page-description", "F# Literate programming"
      "page-author", "Tomas Petricek"
      "github-link", "https://github.com/tpetricek/FSharp.Formatting"
      "project-name", "F# Formatting" ]

  Literate.ProcessDirectory(dir, template, dir + "\\output", replacements = projInfo)

  ()
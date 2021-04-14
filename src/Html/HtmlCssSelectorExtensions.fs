namespace FSharp.Data

open FSharp.Data
open System.Runtime.CompilerServices

[<AutoOpen>]
module CssSelectorExtensions =

    [<Extension>]
    type CssSelectorExtensions =
        /// Gets descendants matched by Css selector
        [<Extension>]
        static member CssSelect (doc, selector) = 
            HtmlNode.Select (doc |> HtmlDocument.elements) selector

        /// Gets descendants matched by Css selector
        [<Extension>]
        static member CssSelect (nodes, selector) = 
            HtmlNode.Select (nodes |> List.ofSeq) selector

        /// Gets descendants matched by Css selector
        [<Extension>]
        static member CssSelect (node, selector) = 
            HtmlNode.cssSelect node selector

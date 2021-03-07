FSharp.Data: Data Access Made Simple
================================

The FSharp.Data package implements core functionality to 
access common data formats in your F# applications and scripts. It contains F# type 
providers for working with structured file formats (CSV, HTML, JSON and XML) and helpers for parsing 
CSV, HTML and JSON files and for sending HTTP requests.

This library focuses on providing simple access to the structured documents 
and other data sources. 

FSharp.Data stems from [Types from data Making structured data first-class citizens in F#](http://tomasp.net/academic/papers/fsharp-data/) by Petricek, Syme and Guerra. This paper
received a Distinguished Paper award at PLDI 2016 and was selected as one of three CACM Research
Highlight in 2018. 🏆🏆🏆

The package is available on <a href="https://nuget.org/packages/FSharp.Data">NuGet</a>. [![NuGet Status](//img.shields.io/nuget/v/FSharp.Data.svg?style=flat)](https://www.nuget.org/packages/FSharp.Data/)



## Type Providers

<div class="container-fluid" style="margin:15px 0px 15px 0px;">
    <div class="row-fluid">
        <div class="span1"></div>
        <div class="span10" id="anim-holder">
            <a id="lnk" href="images/json.gif"><img id="anim" src="images/json.gif" /></a>
        </div>
        <div class="span1"></div>
    </div>
</div>

The FSharp.Data type providers for CSV, HTML, JSON and XML infer types from the structure of a sample 
document (or a document containing multiple samples). The structure is then used
to provide easy to use type-safe access to documents that follow the same structure.

 * [CSV Type Provider](library/CsvProvider.html) - discusses the `CsvProvider<..>` type
 * [HTML Type Provider](library/HtmlProvider.html) - discusses the `HtmlProvider<...>` type
 * [JSON Type Provider](library/JsonProvider.html) - discusses the `JsonProvider<..>` type
 * [XML Type Provider](library/XmlProvider.html) - discusses the `XmlProvider<..>` type

The package also contains a type provider for accessing data from 
[the WorldBank](library/WorldBank.html).

## Data Access Tools
 
The package contains functionality to simplify data access. In particular, it includes tools for HTTP web requests and 
CSV, HTML, and JSON parsers with simple dynamic API. For more information, see the 
following topics:

 * [HTTP Utilities](library/Http.html) - discusses the `Http` type that can be used
   to send HTTP web requests.
 * [CSV Parser](library/CsvFile.html) - introduces the CSV parser 
   (without using the type provider)
 * [HTML Parser](library/HtmlParser.html) - introduces the HTML parser 
   (without using the type provider)
 * [JSON Parser](library/JsonValue.html) - introduces the JSON parser 
   (without using the type provider)

## Tutorials

The following tutorials contain additional examples that 
use multiple features together:

 * [Converting between JSON and XML](tutorials/JsonToXml.html) - implements two serialization 
   functions that convert between the standard .NET `XElement` and the `JsonValue` from FSharp.Data.
   The tutorial demonstrates pattern matching on `JsonValue`.
 * [Anonymizing JSON](tutorials/JsonAnonymizer.html) - implements a function to anonymize a `JsonValue` from FSharp.Data.
   The tutorial demonstrates pattern matching on `JsonValue`.

Below is a brief practical demonstration of using FSharp.Data:

<div style="padding-left:20px"><iframe src="https://channel9.msdn.com/posts/Understanding-the-World-with-F/player" width="640" height="360" allowFullScreen frameBorder="0"></iframe></div>

## Reference Documentation

There's also [reference documentation](reference) available. Please note that everything under 
the `FSharp.Data.Runtime` namespace is not considered as part of the public API and can change without notice.

## Contributing and license

The library is available under Apache 2.0. For more information see the 
[License file][license] in the GitHub repository. In summary, this means that you can 
use the library for commercial purposes, fork it, and modify it as you wish. FSharp.Data is made possible by the volunteer work [of more than a dozen 
contributors](https://github.com/fsharp/FSharp.Data/graphs/contributors) and we're open to 
contributions from anyone. If you want to help out but don't know where to start, you 
can take one of the [Up-For-Grabs](https://github.com/fsharp/FSharp.Data/issues?labels=up-for-grabs&state=open) 
issues, or help to improve the documentation.

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding new public API's, please also 
contribute [samples][samples] to the docs.

  [source]: https://github.com/fsharp/FSharp.Data/zipball/master
  [compiled]: https://github.com/fsharp/FSharp.Data/zipball/release
  [samples]: https://github.com/fsprojects/FSharp.Data/tree/master/docs/
  [gh]: https://github.com/fsharp/FSharp.Data
  [issues]: https://github.com/fsharp/FSharp.Data/issues
  [license]: https://github.com/fsharp/FSharp.Data/blob/master/LICENSE.md
  [contributing]: https://github.com/fsharp/FSharp.Data/blob/master/CONTRIBUTING.md
  [fsharp-oss]: http://groups.google.com/group/fsharp-opensource

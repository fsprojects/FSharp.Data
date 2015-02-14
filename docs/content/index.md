F# Data: Library for Data Access
================================

The F# Data library implements everything you need to 
access data in your F# applications and scripts. It contains F# type 
providers for working with structured file formats (CSV, HTML, JSON and XML) 
and for accessing the WorldBank data. It also includes helpers for parsing 
CSV, HTML and JSON files and for sending HTTP requests.

This library focuses on providing a simple, mostly read-only, access to the structured documents 
and other data sources. It does not aim to be a comprehensive collection of F# type providers 
(which can be used for numerous other purposes). It's also designed to play well with other libraries
like [Deedle](http://bluemountaincapital.github.io/Deedle), [R Type Provider](http://bluemountaincapital.github.io/FSharpRProvider), 
[F# Charting](http://fsharp.github.io/FSharp.Charting) and [FunScript](http://funscript.info).

### F# Data type providers in action

<div class="container-fluid" style="margin:15px 0px 15px 0px;">
    <div class="row-fluid">
        <div class="span1"></div>
        <div class="span10" id="anim-holder">
            <div id="wbtn" style="right:10px">WorldBank</div>
            <div id="jbtn" style="right:110px">JSON</div>
            <div id="cbtn" style="right:210px">CSV</div>
            <a id="lnk" href="images/start.png"><img id="anim" src="images/start.png" /></a>
        </div>
        <div class="span1"></div>
    </div>
</div>
<script type="text/javascript">
$(function(){
  var wi = new Image();
  var ji = new Image();
  var ci = new Image();
  wi.src ='images/wb.gif';
  ji.src ='images/json.gif';
  ci.src ='images/csv.gif';
  $('#wbtn').click(function(){ $('#anim').attr('src',wi.src); $('#lnk').attr('href',wi.src); });
  $('#jbtn').click(function(){ $('#anim').attr('src',ji.src); $('#lnk').attr('href',ji.src); });
  $('#cbtn').click(function(){ $('#anim').attr('src',ci.src); $('#lnk').attr('href',ci.src); });
});</script>


### How to get F# Data

* The F# Data Library is available as <a href="https://nuget.org/packages/FSharp.Data">FSharp.Data on NuGet</a>. [![NuGet Status](//img.shields.io/nuget/v/FSharp.Data.svg?style=flat)](https://www.nuget.org/packages/FSharp.Data/)

* In addition to the official releases, you can also get NuGet packages from the [Continuous Integration 
  package source](https://ci.appveyor.com/nuget/fsharp-data-q9vtdm6ej782).

* Alternatively, you can download the [source as a ZIP file][source] or download the [compiled binaries][compiled] as a ZIP. <br /> Please note that on windows when downloading a zip file with `dll` files the files will be blocked, and you have to manually unblock them in the file properties.


F# Data documentation and tutorials
-----------------------------------

### F# type providers

The type providers for structured file formats infer the structure of a sample 
document (or a document containing multiple samples). The structure is then used
to provide easy to use type-safe access to documents that follow the same structure.
The library also implements a type provider for accessing data from 
[the WorldBank](http://data.worldbank.org/).


 * [CSV Type Provider](library/CsvProvider.html) - discusses the `CsvProvider<..>` type
 * [HTML Type Provider](library/HtmlProvider.html) - discusses the `HtmlProvider<...>` type
 * [JSON Type Provider](library/JsonProvider.html) - discusses the `JsonProvider<..>` type
 * [XML Type Provider](library/XmlProvider.html) - discusses the `XmlProvider<..>` type
 * [WorldBank Provider](library/WorldBank.html) - discusses the `WorldBankData` type 
   and the `WorldBankDataProvider<..>` type

### Data access tools
 
In addition to the F# type providers, the library also defines several types that 
simplify data access. In particular, it includes tools for HTTP web requests and 
CSV, HTML, and JSON parsers with simple dynamic API. For more information about these types, see the 
following topics:

 * [HTTP Utilities](library/Http.html) - discusses the `Http` type that can be used
   to send HTTP web requests.
 * [CSV Parser](library/CsvFile.html) - introduces the CSV parser 
   (without using the type provider)
 * [HTML Parser](library/HtmlParser.html) - introduces the HTML parser 
   (without using the type provider)
 * [JSON Parser](library/JsonValue.html) - introduces the JSON parser 
   (without using the type provider)

### Tutorials

The above articles cover all key features of the F# Data library. However, if you're interested
in more samples or more details, then the following tutorials contain additional examples that 
use multiple different features together:

 * [Converting between JSON and XML](tutorials/JsonToXml.html) - implements two serialization 
   functions that convert between the standard .NET `XElement` and the `JsonValue` from F# Data.
   The tutorial demonstrates pattern matching on `JsonValue`.
 * [Anonymizing JSON](tutorials/JsonAnonymizer.html) - implements a function to anonymize a `JsonValue` from F# Data.
   The tutorial demonstrates pattern matching on `JsonValue`.

### Reference Documentation

There's also [reference documentation](reference) available. Please note that everything under 
the `FSharp.Data.Runtime` namespace is not considered as part of the public API and can change without notice.

Contributing and license
------------------------

The library is available under Apache 2.0. For more information see the 
[License file][license] in the GitHub repository. In summary, this means that you can 
use the library for commercial purposes, fork it, and modify it as you wish.

F# Data is made possible by the volunteer work [of more than a dozen 
contributors](https://github.com/fsharp/FSharp.Data/graphs/contributors) and we're open to 
contributions from anyone. If you want to help out but don't know where to start, you 
can take one of the [Up-For-Grabs](https://github.com/fsharp/FSharp.Data/issues?labels=up-for-grabs&state=open) 
issues, or help to improve the documentation.

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding new public API's, please also 
contribute [samples][samples] that can be turned into a documentation.

 * If you want to discuss an issue or feature that you want to add the to the library,
   then you can submit [an issue or feature request][issues] via Github or you can 
   send an email to the [F# open source][fsharp-oss] mailing list.

 * For more information about the library architecture, organization, how to debug, etc., see the [contributing to F# data][contributing] page.

  [source]: https://github.com/fsharp/FSharp.Data/zipball/master
  [compiled]: https://github.com/fsharp/FSharp.Data/zipball/release
  [samples]: https://github.com/fsharp/FSharp.Data/tree/master/docs/content
  [gh]: https://github.com/fsharp/FSharp.Data
  [issues]: https://github.com/fsharp/FSharp.Data/issues
  [license]: https://github.com/fsharp/FSharp.Data/blob/master/LICENSE.md
  [contributing]: https://github.com/fsharp/FSharp.Data/blob/master/CONTRIBUTING.md
  [fsharp-oss]: http://groups.google.com/group/fsharp-opensource

F# Data: Library for Data Access
================================

The F# Data library (`FSharp.Data.dll`) implements everything you need to 
access data in your F# applications and scripts. It implements F# type 
providers for working with structured file formats (CSV, JSON and XML) 
and for accessing the WorldBank and Freebase data. It also includes helpers for parsing 
JSON and CSV files and for sending HTTP requests.

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The F# Data Library is available as <a href="https://nuget.org/packages/FSharp.Data">FSharp.Data on NuGet</a>.
      To install the library, run the following command in the <a href="http://docs.nuget.org/docs/start-here/using-the-package-manager-console">Package Manager Console</a>:
      <pre>PM> Install-Package FSharp.Data</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Alternatively, you can download the [source as a ZIP file][source] or download 
the [compiled binaries][compiled] as a ZIP.

Documentation
-------------
One of the key benefits of this library is that it comes with a comprehensive 
documentation. The documentation is automatically generated from `*.fsx` files in 
[the samples folder][samples]. If you find a typo, please submit a pull request!

 * [F# Data](fsharpdata.html) is the documentation home with links
   to pages that document individual type providers (CSV, XML, JSON, WorldBank and Freebase) 
   as well as for other public types available in `FSharp.Data.dll`.

Contributing
------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding new public API, please also 
contribute [samples][samples] that can be turned into a documentation.

 * If you want to discuss an issue or feature that you want to add the to the library,
   then you can submit [an issue or feature request][issues] via Github or you can 
   send an email to the [F# open source][fsharp-oss] mailing list.

 * For more information about the library architecture, organization and more
   (such as the support for portable libraries for Windows Phone, Silverlight etc.)
   see the [contributing to F# data](contributing.html) page.

### Library philosophy

This library focuses on providing a simple read-only access to the structured documents 
and other data sources. It does not aim to be a comprehensive collection of F# type providers 
(which can be used for numerous other purposes). Moreover, this library (currently) does not 
provide API for creating or mutating documents.

### Library license

The library is available under Apache 2.0. For more information see the 
[License file][license] in the GitHub repository. In summary, this means that you can 
use the library for commercial purposes, fork it, modify it as you wish.



  [source]: https://github.com/fsharp/FSharp.Data/zipball/master
  [compiled]: https://github.com/fsharp/FSharp.Data/zipball/release
  [samples]: https://github.com/fsharp/FSharp.Data/tree/master/samples
  [gh]: https://github.com/fsharp/FSharp.Data
  [issues]: https://github.com/fsharp/FSharp.Data/issues
  [license]: https://github.com/fsharp/FSharp.Data/blob/master/LICENSE.md
  [fsharp-oss]: http://groups.google.com/group/fsharp-opensource

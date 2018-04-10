# F# Data: Library for Data Access

The F# Data library (`FSharp.Data.dll`) implements everything you need to access data in your F# applications 
and scripts. It implements F# type providers for working with structured file formats (CSV, HTML, JSON and XML) and 
for accessing the WorldBank data. It also includes helpers for parsing CSV, HTML and JSON files and for sending HTTP requests.

We're open to contributions from anyone. If you want to help out but don't know where to start, you can take one of the [Up-For-Grabs](https://github.com/fsharp/FSharp.Data/labels/up-for-grabs) issues, or help to improve the [documentation][3].

You can see the version history [here](RELEASE_NOTES.md).

## Building

- Install .NET SDK 2.1.100 or higher
- Build FSharp.Data.sln and FSharp.Data.Tests.sln in Visual Studio 2017, Visual Studio 2017 for Mac (previously Xamarin Studio), or MonoDevelop. You can also use the FAKE script:

  * Windows: Run *build.cmd* 
    * [![AppVeyor build status](https://ci.appveyor.com/api/projects/status/vlw9avsb91rjfy39)](https://ci.appveyor.com/project/ovatsus/fsharp-data)
  * Mono: Run *build.sh*
    * [![Travis build status](https://travis-ci.org/fsharp/FSharp.Data.svg)](https://travis-ci.org/fsharp/FSharp.Data)

## Supported F# Runtimes

When targeting .NET Framework 4.5+:

- FSharp.Core 4.3.1.0. nuget 3.1.x (default for F# 3.1/Visual Studio 2013)
- FSharp.Core 4.4.0.0, nuget 4.0.x (default for F# 4.0/Visual Studio 2015)
- FSharp.Core 4.4.1.0, nuget 4.2.x (default for F# Tools 4.1 SDK / Visual Studio 2017)
- FSharp.Core 4.4.3.0, nuget 4.3.x (default for F# Tools 10.1 SDK / Visual Studio 2017 15.6+)
- or higher versions of the same

When targeting .NET Standard 2.0 or .NET Core App 2.x:

- FSharp.Core 4.4.1.0, nuget 4.2.x (default for F# Tools 4.1 SDK / Visual Studio 2017) or higher
- FSharp.Core 4.4.3.0, nuget 4.3.x (default for F# Tools 10.1 SDK / Visual Studio 2017 15.6+)
- or higher versions of the same

## Supported Design-time Environments

- .NET SDK 2.1.100 or higher (runs tools using .NET Core)
- Visual F# Tools 3.1 or higher (runs tools using .NET Framework)
- Mono 5.0.0 or higher (runs tools using Mono)
- Other F# tooling based on FSharp.Compiler.Service must have FSharp.Compiler.Service 21.0+ and FSharp.Core nuget 4.2.x+.

## Documentation 

This library comes with comprehensive documentation. The documentation is 
automatically generated from `*.fsx` files in [the content folder][2] and from the comments in the code. If you find a typo, please submit a pull request! 
 - [F# Data Library home page][3] with more information about the library, contributions, etc.
 - The samples from the documentation are included as part of `FSharp.Data.Tests.sln`, make sure you build the
solution before trying out the samples to ensure that all needed packages are installed.

## Support and community

 - If you have a question about `FSharp.Data`, ask at StackOverflow and [mark your question with the `f#-data` tag](http://stackoverflow.com/questions/tagged/f%23-data). 
 - If you want to submit a bug, a feature request or help with fixing bugs then look at [issues](https://github.com/fsharp/FSharp.Data/issues) and read [contributing to F# Data](https://github.com/fsharp/FSharp.Data/blob/master/CONTRIBUTING.md).
 - To discuss more general issues about F# Data, its goals and other open-source F# projects, join the [fsharp-opensource mailing list](http://groups.google.com/group/fsharp-opensource)

## Library license

The library is available under Apache 2.0. For more information see the [License file][1] in the GitHub repository.

## Maintainers

Although this project is hosted in the [fsharp](https://github.com/fsharp) repository for historical reasons, it is _not_ maintained and managed by the F# Core Engineering Group. The F# Core Engineering Group acknowledges that the independent owners and maintainers of this project are [Gustavo Guerra](http://github.com/ovatsus), [Tomas Petricek](http://github.com/tpetricek) and [Colin Bull](http://github.com/colinbull).



 [1]: https://github.com/fsharp/FSharp.Data/blob/master/LICENSE.md
 [2]: https://github.com/fsharp/FSharp.Data/tree/master/docs/content
 [3]: http://fsharp.github.io/FSharp.Data/

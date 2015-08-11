# F# Data: Library for Data Access

The F# Data library (`FSharp.Data.dll`) implements everything you need to access data in your F# applications 
and scripts. It implements F# type providers for working with structured file formats (CSV, HTML, JSON and XML) and 
for accessing the WorldBank data. It also includes helpers for parsing CSV, HTML and JSON files and for sending HTTP requests.

We're open to contributions from anyone. If you want to help out but don't know where to start, you can take one of the [Up-For-Grabs](https://github.com/fsharp/FSharp.Data/issues?labels=up-for-grabs&state=open) issues, or help to improve the [documentation][3].

You can see the version history [here](RELEASE_NOTES.md).

## Building

- Simply build FSharp.Data.sln in Visual Studio 2013, Visual Studio 2015, Mono Develop, or Xamarin Studio. You can also use the FAKE script:

  * Windows: Run *build.cmd* 
    * [![AppVeyor build status](https://ci.appveyor.com/api/projects/status/vlw9avsb91rjfy39)](https://ci.appveyor.com/project/ovatsus/fsharp-data)
  * Mono: Run *build.sh*
    * [![Travis build status](https://travis-ci.org/fsharp/FSharp.Data.png)](https://travis-ci.org/fsharp/FSharp.Data)

## Supported platforms

- VS2012 compiling to FSharp.Core 4.3.0.0
- VS2012 compiling to FSharp.Core 2.3.5.0 (PCL profile 47)
- Mono F# 3.0 compiling to FSharp.Core 4.3.0.0
- Mono F# 3.0 compiling to FSharp.Core 2.3.5.0 (PCL profile 47)
- VS2013 compiling to FSharp.Core 4.3.0.0
- VS2013 compiling to FSharp.Core 4.3.1.0
- VS2013 compiling to FSharp.Core 2.3.5.0 (PCL profile 47)
- VS2013 compiling to FSharp.Core 2.3.6.0 (PCL profile 47)
- Mono F# 3.1 compiling to FSharp.Core 4.3.0.0
- Mono F# 3.1 compiling to FSharp.Core 4.3.1.0
- Mono F# 3.1 compiling to FSharp.Core 2.3.5.0 (PCL profile 47)
- Mono F# 3.1 compiling to FSharp.Core 2.3.6.0 (PCL profile 47)

## Documentation 

This library is that it comes with comprehensive documentation. The documentation is 
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

 [1]: https://github.com/fsharp/FSharp.Data/blob/master/LICENSE.md
 [2]: https://github.com/fsharp/FSharp.Data/tree/master/docs/content
 [3]: http://fsharp.github.io/FSharp.Data/

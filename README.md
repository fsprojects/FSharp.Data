# F# Data: Library for Data Access

The F# Data library (`FSharp.Data.dll`) implements everything you need to access data in your F# applications 
and scripts. It implements F# type providers for working with structured file formats (CSV, JSON and XML) and 
for accessing the WorldBank and Freebase data. It also includes helpers for parsing JSON files and for sending HTTP requests.

Status: [![Build Status](https://travis-ci.org/fsharp/FSharp.Data.png)](https://travis-ci.org/fsharp/FSharp.Data)

## Documentation 

One of the key benefits of this library is that it comes with a comprehensive documentation. The documentation is 
automatically generated from `*.fsx` files in [the samples folder][2]. If you find a typo, please submit a pull request! 

 - [F# Data Library home page][3] with more information about the library, contributions etc.
 - [F# Data Library documentation][4] with links to pages that document individual type providers 
   (CSV, XML, JSON and WorldBank) as well as for other public types available in FSharp.Data.dll. 

 - The samples from the documentation are included as part of `FSharp.Data.Tests.sln`, make sure you build the
solution before trying out the samples to ensure that all needed packages are installed.

## Support and community

 - If you have a question about `FSharp.Data`, ask at StackOverflow and [mark your question with the `f#-data` tag](http://stackoverflow.com/questions/tagged/f%23-data). 
 - If you want to submit a bug, a feature request or help witht fixing bugs then look at [issues](https://github.com/fsharp/FSharp.Data/issues) and read [contributing to F# Data](http://fsharp.github.io/FSharp.Data/contributing.html).
 - To discuss more general issues about F# Data, its goals and other open-source F# projects, join the [fsharp-opensource mailing list](http://groups.google.com/group/fsharp-opensource)

## Building

- Simply build FSharp.Data.sln in Visual Studio, Mono Develop, or Xamarin Studio. You can also use the FAKE script (`build.cmd` on Windows or `./build.sh` on Unix)

## Library license

The library is available under Apache 2.0. For more information see the [License file][1] in the GitHub repository.

 [1]: https://github.com/fsharp/FSharp.Data/blob/master/LICENSE.md
 [2]: https://github.com/fsharp/FSharp.Data/tree/master/samples
 [3]: http://fsharp.github.io/FSharp.Data/
 [4]: http://fsharp.github.io/FSharp.Data/fsharpdata.html

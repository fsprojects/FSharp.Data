# FSharp.Data: Making Data Access Simple

The FSharp.Data package (`FSharp.Data.dll`) implements everything you need to access data in your F# applications and scripts. It implements F# type providers for working with structured file formats (CSV, HTML, JSON and XML) and for accessing the WorldBank data. It also includes helpers for parsing CSV, HTML and JSON files and for sending HTTP requests.

We're open to contributions from anyone. If you want to help out but don't know where to start, you can take one of the [Up-For-Grabs](https://github.com/fsharp/FSharp.Data/labels/up-for-grabs) issues, or help to improve the [documentation][3].

You can see the version history [here](RELEASE_NOTES.md).

[![NuGet Badge](http://img.shields.io/nuget/v/FSharp.Data.svg?style=flat)](https://www.nuget.org/packages/FSharp.Data)

## Building

- Install the .NET SDK specified in the `global.json` file
- `build.sh -t Build` or `build.cmd -t Build`

## Formatting

    dotnet fake build -t Format
    dotnet fake build -t CheckFormat

## Documentation

This library comes with comprehensive documentation. The documentation is 
automatically generated from `*.fsx` files in [the content folder][2] and from the comments in the code. If you find a typo, please submit a pull request!

 - [FSharp.Data package home page][3] with more information about the library, contributions, etc.
 - The samples from the documentation are included as part of `FSharp.Data.Tests.sln`, make sure you build the
solution before trying out the samples to ensure that all needed packages are installed.

## Releasing

Releasing of the NuGet package is done by GitHub actions CI from master branch when a new version is pushed.

Releasing of docs is done by GitHub actions CI on each push to master branch.

## Support and community

 - If you have a question about `FSharp.Data`, ask at StackOverflow and [mark your question with the `f#-data` tag](http://stackoverflow.com/questions/tagged/f%23-data). 
 - If you want to submit a bug, a feature request or help with fixing bugs then look at [issues](https://github.com/fsharp/FSharp.Data/issues) and read [contributing to FSharp.Data](https://github.com/fsharp/FSharp.Data/blob/master/CONTRIBUTING.md).
 - To discuss more general issues about FSharp.Data, its goals and other open-source F# projects, join the [fsharp-opensource mailing list](http://groups.google.com/group/fsharp-opensource)

## Code of Conduct

This repository is governed by the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/).

We pledge to be overt in our openness, welcoming all people to contribute, and pledging in return to value them as whole human beings and to foster an atmosphere of kindness, cooperation, and understanding.

## Library license

The library is available under Apache 2.0. For more information see the [License file][1] in the GitHub repository.

## Maintainers

Current maintainers are [Don Syme](https://github.com/dsyme) and [Phillip Carter](https://github.com/cartermp)

Historical maintainers of this project are [Gustavo Guerra](https://github.com/ovatsus), [Tomas Petricek](https://github.com/tpetricek) and [Colin Bull](https://github.com/colinbull).

 [1]: https://github.com/fsharp/FSharp.Data/blob/master/LICENSE.md
 [2]: https://github.com/fsharp/FSharp.Data/tree/master/docs/content
 [3]: https://fsprojects.github.io/FSharp.Data/

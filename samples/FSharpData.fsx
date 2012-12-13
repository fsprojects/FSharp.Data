(** 
# F# Data: Type Providers and more

The F# Data library implements type providers for working with a number of structured
document formats:

 * `XmlProvider` can be used to access XML documents
 * `JsonProvider` can be used to parse JSON data

In the current version, this library focuses on providing read-only access to the documents.
The [fsharpx](https://github.com/fsharp/fsharpx/) library provides better support for 
mutating the documents, but we aim to integrate both of the implementations in the future.

## How F# Data type providers work?

All of the type providers in this library provide a statically typed access to documents.
They take takes a sample document as an input (or document containing multiple samples - 
either JSON array or XML node with multiple children). The generated type can then be used
to read files with the same structure. If the loaded file does not match the 
structure of the sample, an exception may occur (but only when accessing e.g. non-existing
element).

All type providers are located in `FSharp.Data.dll`. Assuming the assembly is located
in `../bin` directory, we can load it and open the `FSharp.Data` namespace as follows:
*)

#r "../bin/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
open System.IO
open FSharp.Data

(**
After opening the `FSharp.Data` namespace, you can start using the type providers for XML 
and JSON by writing `XmlProvider<"...">` or `JsonProvider<"...">`. Both of these take one
static parameter of type `string`. The parameter can be _either_ a sample string _or_ a sample 
file (relatively to the current folder or online accessible via `http` or `https`). 
It is not likely that this could lead to ambiguities. 

## F# Data components

For more information about the type providers for XML and JSON, see the following two articles:

 * [F# Data: XML Type Provider](XmlProvider.html) - discusses the `XmlProvider<..>` type
 * [F# Data: JSON Type Provider](JsonProvider.html) - discusses the `JsonProvider<..>` type

The library also defines several types for working with data that are not type providers.
For more information about these types, see the following articles:

 * [F# Data: JSON Rarser and Reader](JsonValue.html) - introduces the JSON parser 
   (wihtout using the type provider)
 * [F# Data: HTTP Utilities](Http.html) - discusses the `Http` type that can be used
   to send simple HTTP web requests.

## License

The library is available under Apache 2.0. For more information see the 
[License file](https://github.com/tpetricek/FSharp.Data/blob/master/README.md) in the 
GitHub repository.
*)
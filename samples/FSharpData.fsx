(** 
# F# Data: Library for Data Access

The F# Data library implements everything you need to access data in your F# 
applications and scripts. It includes helpers for parsing JSON files, for sending
HTTP requests and it implements F# type providers for working with a number of 
structured formats including:

 * XML documents
 * JSON files
 * CSV files 

This library focuses on providing a simple read-only access to the documents.
The [FSharpx library](https://github.com/fsharp/fsharpx/) provides better support for 
mutating the documents and also provides additional type providers for registry, file system 
and other sources.

## Type Providers

For more information about the type providers for XML and JSON, see the following two articles:

 * [F# Data: XML Type Provider](XmlProvider.html) - discusses the `XmlProvider<..>` type
 * [F# Data: JSON Type Provider](JsonProvider.html) - discusses the `JsonProvider<..>` type
 * [F# Data: CSV Type Provider](CsvProvider.html) - discusses the `CsvProvider<..>` type

## Data Access Tools
 
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
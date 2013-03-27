﻿# F# Data: Library for Data Access

The F# Data library (`FSharp.Data.dll`) implements everything you need to access data 
in your F# applications and scripts. It implements F# type providers for working with 
structured file formats and for accessing the WorldBank and Freebase data. It 
also includes helpers for parsing JSON files and for sending HTTP requests.

## F# type providers

The type providers for structured file formats infer the structure of a sample 
document (or a document containing multiple samples). The structure is then used
to provide easy to use type-safe access to documents that follow the same structure.
For more information see:

 * [XML Type Provider](library/XmlProvider.html) - discusses the `XmlProvider<..>` type
 * [JSON Type Provider](library/JsonProvider.html) - discusses the `JsonProvider<..>` type
 * [CSV Type Provider](library/CsvProvider.html) - discusses the `CsvProvider<..>` type

The library also implements a type provider for accessing data from 
[the WorldBank](http://data.worldbank.org/) and [Freebase graph database](http://www.freebase.com/).

 * [WorldBank Provider](library/WorldBank.html) - discusses the `WorldBankData` type 
   and the `WorldBankDataProvider<..>` type
 * [Freebase Provider](library/Freebase.html) - discusses the `FreebaseData` type 
   and the `FreebaseDataProvider<..>` type

## Data access tools
 
In addition to the F# type providers, the library also defines several types that 
simplify data access. In particular, it incldues tools for HTTP web requests and a 
JSON parser with simple dynamic API. For more information about these types, see the 
following topcs:

 * [JSON Rarser and Reader](library/JsonValue.html) - introduces the JSON parser 
   (wihtout using the type provider)
 * [HTTP Utilities](library/Http.html) - discusses the `Http` type that can be used
   to send simple HTTP web requests.

## Tutorials

The above articles cover all key features of the F# Data library. However, if you're interested
in more samples or more details, then the following tutorials contain additional advanced examples 
or demos that use multiple different features together:

 * [Converting between JSON and XML](tutorials/JsonToXml.html) - implements two serialization 
   functions that convert between the standard .NET `XElement` and the `JsonValue` from F# Data.
   The tutorial demonstrates pattern matching on `JsonValue`.

## Related projects

This library focuses on providing type providers for data access.
It does not aim to be a comprehensive collection of F# type 
providers (which can be used for numerous other purposes). Moreover, this library 
(currently) does not provide API for _creating_ documents.

If you're interested in other F# type providers or if you need to mutate or create 
XML and JSON documents, then the [FSharpx library](https://github.com/fsharp/fsharpx/) 
might be of interest. 
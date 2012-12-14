(** 
# F# Data: Library for Data Access

The F# Data library (`FSharp.Data.dll`) implements everything you need to access data 
in your F# applications and scripts. It implements F# type providers for working with 
structured file formats and for accessing the WorldBank data. It 
also includes helpers for parsing JSON files and for sending HTTP requests.

## F# type providers

The type providers for structured file formats infer the structure of a sample 
document (or a document containing multiple samples). The structure is then used
to provide easy to use type-safe access to documents that follow the same structure.
For more information see:

 * [F# Data: XML Type Provider](XmlProvider.html) - discusses the `XmlProvider<..>` type
 * [F# Data: JSON Type Provider](JsonProvider.html) - discusses the `JsonProvider<..>` type
 * [F# Data: CSV Type Provider](CsvProvider.html) - discusses the `CsvProvider<..>` type

In addition, the library also implements a type provider for accessing data from 
[the WorldBank](http://data.worldbank.org/). The type provider generates types that
provide easy access to regions, countries and indicators in the data set.

 * [F# Data: WorldBank Provider](WorldBank.html) - discusses the `WorldBank` type 
   and the `WorldBankProvider<..>` type

## Data access tools
 
In addition to the F# type providers, the library also defines several types that 
simplify data access. In particular, it incldues tools for HTTP web requests and a 
JSON parser with simple dynamic API. For more information about these types, see the 
following topcs:

 * [F# Data: JSON Rarser and Reader](JsonValue.html) - introduces the JSON parser 
   (wihtout using the type provider)
 * [F# Data: HTTP Utilities](Http.html) - discusses the `Http` type that can be used
   to send simple HTTP web requests.

## Related projects

This library focuses on providing a simple read-only access to the structured documents
and other data sources. It does not aim to be a comprehensive collection of F# type 
providers (which can be used for numerous other purposes). Moreover, this library 
(currently) does not provide API for _creating_ documents.

If you're interested in other F# type prviders or if you need to mutate or create 
XML and JSON documents, then the [FSharpx library](https://github.com/fsharp/fsharpx/) 
might be of interest. 
*)
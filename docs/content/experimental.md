# F# Data: Experimental Type Providers

The experimental additions of the F# Data library (`FSharp.Data.Experimental.dll`) include
type providers for data access that have not been fully tested and do not match the high
quality standards of F# Data yet. 

This is a good place to test new type providers - if you're working on an interesting 
(data-access related) type provider and want to share it with the world, then please
consider submitting it to the experimental package! The code is included in [the main
GitHub repository][gh], but is referenced only from the `FSharp.Data.Experimental.sln`
solution.

## F# type providers

The library currently contains a type provider for calling REST APIs based on the 
[apiary.io](http://apiary.io) service. If you host a documentation for your REST API
at apiary and your REST API follows standard patterns, you can easily call it using
the type provider.

 * [F# Data: Apiary Provider (Experimental)](experimental/ApiaryProvider.html) - discusses 
   the `ApiaryProvider` type. 

## Related projects

This library focuses on providing type providers for data access.
It does not aim to be a comprehensive collection of F# type 
providers (which can be used for numerous other purposes). Moreover, this library 
(currently) does not provide API for _creating_ documents.

  [gh]: https://github.com/fsharp/FSharp.Data
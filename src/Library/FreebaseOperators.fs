// --------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation 2005-2012.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 
// --------------------------------------------------------------------------------------

namespace FSharp.Data

/// Extension members for operations permitted in queries of the Freebase service
module FreebaseOperators = 

    open System

    type System.String with 
        /// A Freebase query operation that represents a perl-style match of a string, e.g. "book club", "book*", "*book", "*book*", "^book", "book$", "* book *", "book-club", "book\-club". See http://www.freebase.com/docs/mql/ch03.html#directives.
        [<CompiledName("ApproximatelyMatches")>]
        member s.ApproximatelyMatches(_pat:string) : bool = failwith "'ApproximatelyMatches' may only be used in a query executed on the Freebase server."

        /// A Freebase query operation that represents approximately matching one of the given strings. See http://www.freebase.com/docs/mql/ch03.html#directives.
        [<CompiledName("ApproximatelyOneOf")>]
        member s.ApproximatelyOneOf([<ParamArray>] args:string[]) : bool = 
            if args.Length = 0 then false
            else failwith "'ApproximatelyOneOf' may only be used in a query executed on the server. It must be given at least one value."

    type System.Linq.IQueryable<'T> with 
       /// A Freebase query operation returning an approximate count of the items satisfying a query.
       [<CompiledName("ApproximateCount")>]
       member s.ApproximateCount() : int = 
           // Uses the standard LINQ technique to fold the operator into the query
           let m = match <@ Unchecked.defaultof<System.Linq.IQueryable<'T>>.ApproximateCount() @> with Quotations.Patterns.Call(None, mb, _) -> mb | _ -> failwith "unexpected"
           let expr = System.Linq.Expressions.Expression.Call(null,m,[| s.Expression |])
           s.Provider.Execute<int32>(expr)

       /// Synonym for LINQ's Count
       /// Included so you don't have to open System.LINQ to use the queries
       [<CompiledName("Count")>]
       member s.Count() : int =  System.Linq.Queryable.Count(s)

       /// Synonym for LINQ's Where
       /// Included so you don't have to open System.LINQ to use the queries
       [<CompiledName("Where")>]
       member s.Where(p:System.Linq.Expressions.Expression<System.Func<_,_>>) =  System.Linq.Queryable.Where(s,p)

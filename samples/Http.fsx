(**
# F# Data: HTTP Utilities
*)

(** 
Refernece the DLL and open `FSharp.Net`:
*)

#r "../bin/FSharp.Data.dll"
open FSharp.Net

(**
Sending simple request:
*)

Http.Download("http://tomasp.net")

(** 
Specifying the GET method and get parameters:
*)
Http.Download
  ( "http://www.htmlcodetutorial.com/cgi-bin/mycgi.pl", 
    query = ["test", "foo"], meth="GET")


(** 
Specifying query parameters and headers (using the default GET method):
*)

Http.Download
  ( "http://api.themoviedb.org/3/search/movie",
    query   = [ "api_key", "6ce0ef5b176501f8c07c634dfa933cff"
                "query", "batman" ],
    headers = [ "accept", "application/json" ])

(**
Making POST request with some body:
*)
Http.Download
  ( "http://www.htmlcodetutorial.com/cgi-bin/mycgi.pl", 
    meth="POST", body="test=foo")


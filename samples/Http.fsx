#r "../bin/FSharp.Data.dll"

open FSharp.Net

Http.Download("http://tomasp.net")

Http.Download
  ( "http://api.themoviedb.org/3/search/movie",
    query   = [ "api_key", "6ce0ef5b176501f8c07c634dfa933cff"
                "query", "batman" ],
    headers = [ "accept", "application/json" ])

Http.Download
  ( "http://www.htmlcodetutorial.com/cgi-bin/mycgi.pl", 
    query = ["test", "foo"], meth="GET")

Http.Download
  ( "http://www.htmlcodetutorial.com/cgi-bin/mycgi.pl", 
    meth="POST", body="test=foo")


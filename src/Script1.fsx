#I "bin/Debug"

#r "FSharp.Data.dll"

open FSharp.Data
open FSharp.Data.Runtime

let simpleLists = 
    """<html>
          <body>
              <ul>
                  <li>
                      <ul>
                          <li>1</li>
                          <li>2</li>
                      </ul>
                  </li>
                  <li>2</li>
                  <li>3</li>
              </ul>
          </body>
      </html>""" |> HtmlDocument.Parse

HtmlRuntime.getLists simpleLists

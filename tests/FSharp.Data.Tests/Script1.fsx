#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
#else
module FSharp.Data.Tests.HtmlProvider
#endif

open FSharp.Data

let xml (url:string) = 
    HtmlDocument.Load url
    |> HtmlDocument.bodyToXDocument

//open System
//open FSharp.Data
//
//let table = HtmlProvider<"""<html>
//                <body>
//                    <table>
//                        <tr><td>Date</td><td>Distance (m)</td><td>Time (s)</td><td>Column 3</td><td>Column 4</td></tr>
//                        <tr><td>01/01/2013 12:00</td><td>2</td><td>30.5</td><td>2</td><td>2</td></tr>
//                        <tr><td>01/01/2013 12:00</td><td>1.5</td><td>30.5</td><td>2</td><td>2</td></tr>
//                    </table>
//                </body>
//            </html>""">.GetSample().Tables.Table1
//let velocity = table |> Seq.map (fun x -> x.Distance * x.Time) |> Seq.toList
//
//let [<Literal>] data= 
//    """<html>
//           <body>
//               <table>
//                   <tr><td>Date</td><td>Distance (m)</td><td>Time (s)</td><td>Column 3</td><td>Column 4</td></tr>
//                   <tr>
//                       <td>01/01/2013 12:00</td>
//                       <td>2</td><td>30.5</td>
//                       <td>
//                           <div itemscope itemtype ="http://schema.org/Movie">
//                             <h1 itemprop="name">Avatar</h1>
//                             <div itemprop="director" itemscope itemtype="http://schema.org/Person">
//                             Director: <span itemprop="name">James Cameron</span> (born <span itemprop="birthDate">August 16, 1954 </span>)
//                             </div>
//                             <span itemprop="genre">Science fiction / Blue People</span>
//                             <a href="../movies/avatar-theatrical-trailer.html" itemprop="trailer">Trailer</a>
//                           </div>
//                       </td>
//                       <td>2</td>
//                   </tr>
//                   <tr>
//                       <td>01/01/2013 12:00</td>
//                       <td>1.5</td>
//                       <td>30.5</td>
//                       <td>
//                           <div itemscope itemtype ="http://schema.org/Movie">
//                             <h1 itemprop="name">Alien</h1>
//                             <div itemprop="director" itemscope itemtype="http://schema.org/Person">
//                             Director: <span itemprop="name">James Cameron</span> (born <span itemprop="birthDate">August 16, 1954 </span>)
//                             </div>
//                             <span itemprop="genre">Science fiction</span>
//                             <a href="../movies/avatar-theatrical-trailer.html" itemprop="trailer">Trailer</a>
//                           </div>
//                       </td>
//                       <td>2</td>
//                   </tr>
//               </table>
//           </body>
//       </html>"""
//
//let table1 = HtmlProvider<data>.GetSample().Tables.Table1
//
//let movieName = table1 |> Seq.map (fun x -> x.) |> Seq.toList
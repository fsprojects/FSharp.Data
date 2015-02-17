#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
#else
module FSharp.Data.Tests.HtmlProvider
#endif

open System
open FSharp.Data

let [<Literal>] xmlTable = """
<example_table>
    <rows>
        <row>
            <Date>01/01/2013 12:00</Date>
            <Distance_m>2</Distance_m>
            <Time_s>1.5</Time_s>
            <Column_3>2</Column_3>
            <Column_4>2</Column_4>
        </row>
        <row>
            <Date>01/01/2013 12:00</Date>
            <Distance_m>2</Distance_m>
            <Time_s>1.5</Time_s>
            <Column_3>2</Column_3>
            <Column_4>2</Column_4>
        </row>
    </rows>
</example_table>
"""

let [<Literal>] xmlTableAdv = """
<example_table>
    <rows>
        <row>
            <Date>01/01/2013 12:00</Date>
            <Distance_m>2</Distance_m>
            <Time_s>1.5</Time_s>
            <Column_3>
                 <movie>
                   <name>Alien</name>
                   <director>
                        <name>James Cameron</name>
                        <birthDate>August 16, 1954 </birthDate>
                   </director>
                   <genre>Science fiction / Green People</genre>
                   <trailer>
                        <href>../movies/avatar-theatrical-trailer.html</href>
                   </trailer>
                 </movie>
            </Column_3>
            <Column_4>2</Column_4>
        </row>
        <row>
            <Date>01/01/2013 12:00</Date>
            <Distance_m>2</Distance_m>
            <Time_s>1.5</Time_s>
            <Column_3>
                 <movie>
                   <name>Avatar</name>
                   <director>
                        <name>James Cameron</name>
                        <birthDate>August 16, 1954 </birthDate>
                   </director>
                   <genre>Science fiction / Blue People</genre>
                   <trailer>
                        <href>../movies/avatar-theatrical-trailer.html</href>
                   </trailer>
                 </movie>
            </Column_3>
            <Column_4>2</Column_4>
        </row>
    </rows>
</example_table>
"""
    

type SimpleTable = XmlProvider<xmlTable>
let rs = SimpleTable.GetSample().Rows |> Seq.toArray

type AdvTable = XmlProvider<xmlTableAdv>
let rsA = AdvTable.GetSample().Rows |> Seq.map (fun x -> x.Column3.Movie.Name) |> Seq.toArray

let table = HtmlProvider<"""<html>
                <body>
                    <table>
                        <tr><td>Date</td><td>Distance (m)</td><td>Time (s)</td><td>Column 3</td><td>Column 4</td></tr>
                        <tr><td>01/01/2013 12:00</td><td>2</td><td>30.5</td><td>2</td><td>2</td></tr>
                        <tr><td>01/01/2013 12:00</td><td>1.5</td><td>30.5</td><td>2</td><td>2</td></tr>
                    </table>
                </body>
            </html>""">.GetSample().Tables.Table1
let velocity = table |> Seq.map (fun x -> x.Distance * x.Time) |> Seq.toList

let [<Literal>] data= 
    """<html>
           <body>
               <table>
                   <tr><td>Date</td><td>Distance (m)</td><td>Time (s)</td><td>Column 3</td><td>Column 4</td></tr>
                   <tr>
                       <td>01/01/2013 12:00</td>
                       <td>2</td><td>30.5</td>
                       <td>
                           <div itemscope itemtype ="http://schema.org/Movie">
                             <h1 itemprop="name">Avatar</h1>
                             <div itemprop="director" itemscope itemtype="http://schema.org/Person">
                             Director: <span itemprop="name">James Cameron</span> (born <span itemprop="birthDate">August 16, 1954 </span>)
                             </div>
                             <span itemprop="genre">Science fiction / Blue People</span>
                             <a href="../movies/avatar-theatrical-trailer.html" itemprop="trailer">Trailer</a>
                           </div>
                       </td>
                       <td>2</td>
                   </tr>
                   <tr>
                       <td>01/01/2013 12:00</td>
                       <td>1.5</td>
                       <td>30.5</td>
                       <td>
                           <div itemscope itemtype ="http://schema.org/Movie">
                             <h1 itemprop="name">Alien</h1>
                             <div itemprop="director" itemscope itemtype="http://schema.org/Person">
                             Director: <span itemprop="name">James Cameron</span> (born <span itemprop="birthDate">August 16, 1954 </span>)
                             </div>
                             <span itemprop="genre">Science fiction</span>
                             <a href="../movies/avatar-theatrical-trailer.html" itemprop="trailer">Trailer</a>
                           </div>
                       </td>
                       <td>2</td>
                   </tr>
               </table>
           </body>
       </html>"""

let table1 = HtmlProvider<data>.GetSample().Tables.Table1

let movieName = table1 |> Seq.map (fun x -> x.``Column 3``.Name) |> Seq.toList
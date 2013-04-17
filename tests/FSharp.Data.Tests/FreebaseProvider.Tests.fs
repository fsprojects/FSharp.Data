module FSharp.Data.Tests.FreebaseProvider.Tests

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open NUnit.Framework
open FsUnit
open System
open System.Linq
open FSharp.Data
open FSharp.Data.FreebaseOperators

[<Literal>]
let apiKey = "AIzaSyBTcOKmU7L7gFB4AdyAz75JRmdHixdLYjY"

let data = FreebaseDataProvider<apiKey>.GetDataContext()

[<Test>]
let ``Can access the first 10 amino acids``() =

    let aminoAcids = data.``Science and Technology``.Biology.``Amino Acids``

    let q = query {
        for acid in aminoAcids do
        take 10
        select (acid.Name, String.Join(" ", acid.Blurb.ToArray())) }

    let a = q.ToArray()
    a.Count() |> should equal 10

[<Test>]
let ``Can access the symbol for hydrogen``() =

    let elements = data.``Science and Technology``.Chemistry.``Chemical Elements``

    let hydrogen = elements.Individuals.Hydrogen
    hydrogen.Symbol |> should equal "H"

let findCountryByFifaCode code = 
    query { for x in data.``Time and Space``.Location.Countries do 
            where (x.``FIFA Code`` = code) 
            exactlyOne }

[<Test>]
let ``Can access Australia's national anthem by Fifa code``() =
    let country = findCountryByFifaCode "AUS"
    let anthem = country.``National anthem`` |> Seq.head
    anthem.Anthem.Name |> should equal "Advance Australia Fair"

[<Test>]
let ``Can access Austrias national anthem by Fifa code``() =
    let country = findCountryByFifaCode "AUT"
    let anthem = country.``National anthem`` |> Seq.head
    anthem.Anthem.Name |> should equal "Land der Berge, Land am Strome"

[<Test>]
let ``Can access the webpages for music composers``() =

    let webPage = 
        data.``Arts and Entertainment``.Music.Composers
        |> Seq.map (fun composer -> String.concat "\n" composer.``Topical webpage``)
        |> Seq.find (not << String.IsNullOrWhiteSpace)

    webPage |> should equal "http://www.discogs.com/artist/John+Barry"

[<Test>]
let ``Can access the webpages of stock exchanges``() =

    let webPage = 
        data.``Products and Services``.Business.``Stock exchanges``
        |> Seq.map (fun exchange -> String.concat "\n" exchange.``Official website``)
        |> Seq.find (not << String.IsNullOrWhiteSpace)

    webPage |> should equal "http://www.nasdaqomx.com/\nhttp://www.nasdaq.com/"

[<Test>]
let ``Can execute > 1500 chars MQL query``() =

    let astronomy = data.``Science and Technology``.Astronomy.Stars

    let someStarDistances = 
        query { for e in astronomy do 
                where e.Distance.HasValue
                select (e.Name, e.Distance) } 
            |> Seq.toList
    someStarDistances |> Seq.head |> fst |> should equal "Arcturus"

[<Test>]
let ``Can access olympics info``() =
    
    let firstOlympicCity = 
        query { for game in data.Commons.Olympics.``Olympic games`` do
                sortBy game.``End date``
                for city in game.``Host City`` do 
                select city
                head }

    firstOlympicCity.Name |> should equal "Athens"


[<Test>]
let ``Can execute query that compares to null``() =
    let p1 = query {
        for p in data.Commons.People.Persons do
        where (p.Name.ApproximatelyMatches "^Evelyn ")
        where (p.Gender = null)
        head
    }
    let p2 = query {
        for p in data.Commons.People.Persons do
        where (p.Name.ApproximatelyMatches "^Evelyn ")
        where (p.Gender <> null)
        head
    }
    p1.Name |> should equal "Evelyn Escalante"
    p2.Name |> should equal "Evelyn Waugh"

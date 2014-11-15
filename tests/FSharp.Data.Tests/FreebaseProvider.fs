#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.FreebaseProvider
#endif

open NUnit.Framework
open FsUnit
open System
open System.Linq
open System.Net
open FSharp.Data
open FSharp.Data.FreebaseOperators
open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames

//alow tests to work when you're behind a proxy
WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials

//Safe set the key environment variable to value (or delete it if value = "") only for this context
let environmentVariable key value = 
    let old = Environment.GetEnvironmentVariable(key)
    Environment.SetEnvironmentVariable(key, value)
    { new IDisposable with
          member x.Dispose() = Environment.SetEnvironmentVariable(key, old) }

[<Test>]
let ``Should not use api key if FREEBASE_API_KEY environment variable not set``() =
    use v = environmentVariable "FREEBASE_API_KEY" ""
    let data = FreebaseData.GetDataContext()
    data.DataContext.ApiKey |> should equal None

[<Test>]
let ``Should use api key from FREEBASE_API_KEY environment variable``() =
    use v = environmentVariable "FREEBASE_API_KEY" "KEY1234"
    let data = FreebaseData.GetDataContext()
    data.DataContext.ApiKey |> should equal (Some "KEY1234")

let data = FreebaseData.GetDataContext()

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

[<Test>]
let ``Can access specific properties for hydrogen individual``() =

    let elements = data.``Science and Technology``.Chemistry.``Chemical Elements``

    let hydrogen = elements.Individuals.Hydrogen
    hydrogen.``Atomic number`` |> should equal 1
    hydrogen.``Atomic mass``.Mass.HasValue |> should equal true
    abs (1.0 - hydrogen.``Atomic mass``.Mass.Value / 1.713498e-27<kilogram>) < 0.00001 |> should equal true
    abs (1.0 - hydrogen.``Boiling Point`` / 20.28<kelvin>) < 0.00001 |> should equal true

let findCountryByFifaCode code = 
    query { for x in data.``Time and Space``.Location.Countries do 
            where (x.``FIFA Code``.Contains code) 
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

    webPage.Split('\n').[0] |> should equal "http://www.quantz.info/"

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
let ``Can execute query that checks for not null``() =
    let p = query {
        for p in data.Commons.People.Persons do
        where (p.Name.ApproximatelyMatches "^Evelyn ")
        where (p.Gender <> null)
        head
    }
    p.Name |> should equal "Evelyn Waugh"
    p.Gender.Name |> should equal "Male"

[<Test>]
let ``Can execute query that checks for null``() =
    let p = query {
        for p in data.Commons.People.Persons do
        where (p.Name.ApproximatelyMatches "^Evelyn ")
        where (p.Gender = null)
        head
    }
    p.Name |> should equal "Evelyn Escalante"
    p.Gender |> should equal null

[<Test>]
let ``Can execute query that gets the head of a sequence of compound objects``() =
    query {
        for p in data.Commons.People.Persons do
        where (p.Name.ApproximatelyMatches "^Evelyn ")
        select (p.Name, p.``Date of birth``)
        head
    } |> should equal ("Evelyn Waugh", "1903-10-28")
    
[<Test>]
let ``Can execute query that gets the head of a sequence of basic types``() =
    query {
        for p in data.Commons.People.Persons do
        where (p.Name.ApproximatelyMatches "^Evelyn ")
        select p.Name
        head
    } |> should equal "Evelyn Waugh"

[<Test>]
let ``tvrage_id is not unique in mql query``() =
    query {
        for p in data.Commons.People.Persons do
        select (p.Name, p.``Date of birth``)
    } 
    |> Seq.head
    |> should equal ("Jack Abramoff", "1958-02-28")

let ghanaCodes = [|"GHA"|]

[<Test>]
let ``Can handle Ghana multiple ISO 3 codes``() =
    let ghana = data.``Time and Space``.Location.Countries.Individuals.Ghana
    ghana.``ISO Alpha 3`` |> Seq.toArray |> should equal ghanaCodes

[<Test>]
let ``Check Individuals10 works for small collection``() =
    let ghana = data.``Time and Space``.Location.Countries.Individuals10.Ghana
    ghana.``ISO Alpha 3`` |> Seq.toArray |> should equal ghanaCodes

[<Test>]
let ``Check Individuals100 works for small collection``() =
    let ghana = data.``Time and Space``.Location.Countries.Individuals100.Ghana
    ghana.``ISO Alpha 3`` |> Seq.toArray |> should equal ghanaCodes

[<Test>]
let ``Check IndividualsAZ works for small collection``() =
    let ghana = data.``Time and Space``.Location.Countries.IndividualsAZ.G.Ghana
    ghana.``ISO Alpha 3`` |> Seq.toArray |> should equal ghanaCodes

[<Test>]
let ``Check IndividualsAZ good for large collections``() =
    let bible = data.``Arts and Entertainment``.Books.Books.IndividualsAZ .T.``The Bible``
    bible.Characters.Any(fun x -> x.Name = "Satan") |> should equal true
    bible.Characters.Any(fun x -> x.Name = "John the Baptist") |> should equal true
    bible.Characters.Any(fun x -> x.Name = "Jesus") |> should equal true
    // I couldn't resist....
    bible.Characters.Any(fun x -> x.Name = "Bart Simpson") |> should equal false

open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols

[<Test>]
let ``Can access meteorology with units of measure``() =
    let cyclones = data.``Science and Technology``.Meteorology.``Tropical Cyclones``
    let topWind = cyclones.Individuals10.``Hurricane Sandy``.``Highest winds``
    abs(topWind - 51.3893<metre/second>) < 1e-14<metre/second> |> should equal true

open FSharp.Data.Runtime.Freebase.FreebaseRequests
open FSharp.Data.Runtime.Freebase.FreebaseSchema

[<Test>]
let ``Wrong key gives relevant message``() =
    let fb = new FreebaseQueries("invalidKey", "https://www.googleapis.com/freebase/v1", "FreebaseSchema", "none", false)
    let fbSchema = new FreebaseSchemaConnection(fb)
    let exn =
        try
            fbSchema.GetDomainStructure() |> ignore
            ""
        with :? FreebaseWebException as e -> 
            e.Message
    exn |> should contain "Reason='keyInvalid'"

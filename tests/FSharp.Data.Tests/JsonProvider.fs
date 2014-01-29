module FSharp.Data.Tests.JsonProvider

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data

type NumericFields = JsonProvider<""" [ {"a":12.3}, {"a":1.23, "b":1999.0} ] """, SampleIsList=true>
type DecimalFields = JsonProvider<""" [ {"a":9999999999999999999999999999999999.3}, {"a":1.23, "b":1999.0} ] """, SampleIsList=true>

[<Test>]
let ``Decimal required field is read correctly`` () = 
  let prov = NumericFields.Parse(""" {"a":123} """)
  prov.A |> should equal 123M

[<Test>]
let ``Decimal optional field is read as None`` () = 
  let prov = NumericFields.Parse(""" {"a":123} """)
  prov.B |> should equal None

[<Test>]
let ``Reading a required field that is null throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"a":null, "b":123} """)
  (fun () -> prov.A |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``Reading a required field that is missing throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"b":123} """)
  (fun () -> prov.A |> ignore)|> should throw typeof<Exception>

[<Test>]
let ``Reading a required decimal that is not a valid decimal throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"a":"hello", "b":123} """)
  (fun () -> prov.A |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``Reading a required float that is not a valid float returns NaN`` () = 
  let prov = DecimalFields.Parse(""" {"a":"hello", "b":123} """)
  prov.A |> should equal Double.NaN

[<Test>]
let ``Optional int correctly infered`` () = 
  let prov = JsonProvider<""" [ {"a":123}, {"a":null} ] """>.GetSamples()
  let i = prov.[0].A.Number
  i |> should equal (Some 123)

[<Test>]
let ``SampleIsList for json correctly handled``() = 
    JsonProvider<"Data/TwitterSample.json", SampleIsList=true>.GetSamples()
    |> Seq.sumBy (fun tweet ->
        match tweet.Text with
        | Some _ -> 0
        | None -> 1)
    |> should equal 2

[<Test>]
let ``Null values correctly handled``() = 
    let tweet = JsonProvider<"Data/TwitterSample.json", SampleIsList=true>.GetSamples() |> Seq.head
    tweet.Place |> should equal None

type InlinedJSON = JsonProvider<"""{ "firstName": "Max", "lastName": "Mustermann", "age": 26, "isCool": true }""">

[<Test>]
let ``Can parse inlined properties``() = 
    let person = InlinedJSON.GetSample()

    person.FirstName
    |> should equal "Max"

    person.LastName
    |> should equal "Mustermann"

    person.Age
    |> should equal 26

    person.IsCool
    |> should equal true

[<Test>]
let ``Can parse inlined properties but read from file``() = 
    let person = InlinedJSON.Load("Data/Simple.json")

    person.FirstName
    |> should equal "John"

    person.LastName
    |> should equal "Doe"

    person.Age
    |> should equal 25

    person.IsCool
    |> should equal true


type SimpleJSON = JsonProvider<"Data/Simple.json">

let simple = SimpleJSON.GetSample()

[<Test>]
let ``Can parse properties``() = 
    simple.FirstName
    |> should equal "John"

    simple.LastName
    |> should equal "Doe"

    simple.Age
    |> should equal 25

    simple.IsCool
    |> should equal true

type NestedJSON = JsonProvider<"Data/Nested.json">

let nested = NestedJSON.GetSample()

[<Test>]
let ``Can parse nested properties``() = 
    nested.Main.FirstName
    |> should equal "John"

    nested.Main.LastName
    |> should equal "Doe"

    nested.Main.Age
    |> should equal 25

    nested.Main.IsCool
    |> should equal true

type DoubleNestedJSON = JsonProvider<"Data/DoubleNested.json">

let doubleNested = DoubleNestedJSON.GetSample()

[<Test>]
let ``Can parse double nested properties``() = 
    doubleNested.Main.Title
    |> should equal "example"

    doubleNested.Main.Nested.NestedTitle
    |> should equal "sub"

type SimpleArrayJSON = JsonProvider<"Data/SimpleArray.json">

let simpleArray = SimpleArrayJSON.GetSample()

[<Test>]
let ``Can parse simple arrays``() = 
    let items = simpleArray.Items
    items.[0].Id
    |> should equal "Open"

    items.[1].Id
    |> should equal "Pause"

type OptionalValuesInJSON = JsonProvider<"Data/OptionValues.json">

let optionalValuesInJSON = OptionalValuesInJSON.GetSample()

[<Test>]
let ``Can parse optional values in arrays``() = 
    let authors = optionalValuesInJSON.Authors
    authors.[0].Name
    |> should equal "Steffen"

    authors.[0].Age
    |> should equal (Some 29)

    authors.[1].Name
    |> should equal "Tomas"

    authors.[1].Age
    |> should equal None

[<Test>]
let ``Can compare typed JSON documents``() = 
    let simple1 = SimpleJSON.GetSample()
    let simple2 = SimpleJSON.GetSample()
    let nested = NestedJSON.GetSample()

    simple1 |> should equal simple2
    nested |> should notEqual simple2

type JsonArray = JsonProvider<"""["Adam","Eve","Bonnie","Clyde","Donald","Daisy","Han","Leia"]""">

[<Test>]
let ``Can parse simple array``() = 
    let inlined = JsonArray.GetSamples()
    inlined
      |> should equal [|"Adam";"Eve";"Bonnie";"Clyde";"Donald";"Daisy";"Han";"Leia"|]

type MultipleJsonArray = JsonProvider<"""[["Adam","Eve"],["Bonnie","Clyde"],["Donald","Daisy"],["Han","Leia"]]""">

[<Test>]
let ``Can parse multidimensional arrays``() = 
    let inlined = MultipleJsonArray.GetSamples()
    inlined
      |> should equal [| [|"Adam";"Eve"|]
                         [|"Bonnie";"Clyde"|]
                         [|"Donald";"Daisy"|]
                         [|"Han";"Leia"|] |]

type WikiSample =
    JsonProvider<
        """{  
                 "firstName": "John",
                 "lastName" : "Smith",
                 "age"      : 25,
                 "address"  :
                 {
                     "streetAddress": "21 2nd Street",
                     "city"         : "New York",
                     "state"        : "NY",
                     "postalCode"   : "10021"
                 },
                 "phoneNumber":
                 [
                     {
                       "type"  : "home",
                       "number": "212 555-1234"
                     },
                     {
                       "type"  : "fax",
                       "number": "646 555-4567"
                     }
                 ]
             }""">

[<Test>]
let ``Can parse wiki sample``() = 
    let document = WikiSample.GetSample()
    document.FirstName |> should equal "John"

    let phone = document.PhoneNumber |> Seq.head
    phone.Number |> should equal "212 555-1234"

[<Test>]
let ``Can load empty json file and fails on property access``() = 
    let document = WikiSample.Load("Data/Empty.json")
    let failed = ref false
    try
        document.Age |> ignore
    with
    | _ -> failed := true
    !failed |> should be True

let newJson = 
    """{  
            "firstName": "Jane",
            "lastName" : "Doe",
            "age"      : 23
    }"""

let newJson2 = 
    """{  
            "firstName": "Jim",
            "lastName" : "Smith",
            "age"      : 24
    }"""

let document = WikiSample.Parse(newJson)
let document2 = WikiSample.Parse(newJson2)

[<Test>]
let ``Jane should have first name of Jane``() = 
    document.FirstName |> should equal "Jane"

[<Test>]
let ``Jane should have a last name of Doe``() = 
    document.LastName |> should equal "Doe"

[<Test>]
let ``Jane should have an age of 23``() = 
    document.Age |> should equal 23

[<Test>]
let ``Jim should have a first name of Jim``() = 
    document2.FirstName |> should equal "Jim"

[<Test>]
let ``Jim should have a last name of Smith``() = 
    document2.LastName |> should equal "Smith"

[<Test>]
let ``Jim should have an age of 24``() = 
    document2.Age |> should equal 24

type Project = JsonProvider<"Data/projects.json">

[<Test>]
let ``Can access the background title``() =
    let doc = Project.GetSample()
    let background = doc.Ordercontainer.Backgrounds.Background
    let title = background.Title
    title.Text |> should equal "purple stars"

[<Test>]
let ``Can access the project title``() =
    let doc = Project.GetSample()
    let project = doc.Ordercontainer.Project
    let title = project.Title
    title.Text |> should equal "Avery"

type DateJSON = JsonProvider<"Data/Dates.json">

[<Test>]
let ``Can parse microsoft format dates``() = 
    let dates = DateJSON.GetSample()
    dates.Birthdate |> should equal (new DateTime(1997, 7, 16, 19, 20, 30, 450)) // 1997-07-16T19:20:30.45+01:00

[<Test>]
let ``Can parse ISO 8601 dates``() =
    let dates = DateJSON.GetSample()
    dates.Anniversary.ToUniversalTime() |> should equal (new DateTime(1997, 7, 16, 18, 20, 30, 450)) 

[<Test>]
let ``Can parse UTC dates``() =
    let dates = DateJSON.GetSample()
    dates.UtcTime.ToUniversalTime() |> should equal (new DateTime(1997, 7, 16, 19, 50, 30, 0)) 

[<Test>]
[<SetCulture("zh-CN")>]
let ``Can parse ISO 8601 dates in the correct culture``() =
    let dates = DateJSON.GetSample()
    dates.NoTimeZone |> should equal (new DateTime(1997, 7, 16, 19, 20, 30, 00, System.DateTimeKind.Local)) 

[<Test>]
[<SetCulture("pt-PT")>]
let ``Can parse ISO 8601 dates in the specified culture``() =
    let dates = JsonProvider<"""{"birthdate": "01/02/2000"}""">.GetSample()
    dates.Birthdate.Month |> should equal 1
    let dates = JsonProvider<"""{"birthdate": "01/02/2000"}""", Culture="pt-PT">.GetSample()
    dates.Birthdate.Month |> should equal 2

[<Test>]
let ``Parsing of values wrapped in quotes should work on heterogenous values``() =
    let objs = JsonProvider<"""[{"a": "01/02/2000"}, {"a" : "3"}]""">.GetSamples()
    objs.[0].A.DateTime |> should equal (Some (DateTime(2000,01,02)))
    objs.[0].A.Number |> should equal None
    objs.[1].A.DateTime |> should equal None
    objs.[1].A.Number |> should equal (Some 3)

[<Test>]
let ``Parsing of values wrapped in quotes should work on arrays``() =
    let objs = JsonProvider<"""["01/02/2000", "01/02/2001", "3", 4]""">.GetSample()
    objs.GetDateTimes() |> should equal [| DateTime(2000,01,02); DateTime(2001,02,02) |]
    objs.GetNumbers() |> should equal [| 3; 4 |]

module FSharp.Data.Tests.JsonProvider

open NUnit.Framework
open FsUnit
open System
open System.Globalization
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes

type SchemaBasedJson = JsonProvider<Schema="Data/PersonSchema.json">

[<Test>]
let ``Can parse JSON using JSON Schema``() = 
    let person = SchemaBasedJson.Parse("""
    {
        "firstName": "John",
        "lastName": "Doe",
        "age": 30,
        "email": "john.doe@example.com",
        "phoneNumbers": [
            {
                "type": "home",
                "number": "555-1234"
            },
            {
                "type": "mobile",
                "number": "555-5678"
            }
        ],
        "address": {
            "streetAddress": "123 Main St",
            "city": "Anytown"
        },
        "isActive": true,
        "registeredSince": "2020-01-01T00:00:00Z"
    }
    """)
    
    person.FirstName |> should equal "John"
    person.LastName |> should equal "Doe"
    person.Age |> should equal (Some 30)
    person.Email |> should equal (Some "john.doe@example.com")
    person.IsActive |> should equal (Some true)
    person.PhoneNumbers.Length |> should equal 2
    person.PhoneNumbers.[0].Type |> should equal "home"
    person.PhoneNumbers.[0].Number |> should equal "555-1234"
    person.Address.Value.StreetAddress |> should equal "123 Main St"
    person.Address.Value.City |> should equal "Anytown"
    person.Address.Value.PostalCode |> should equal None
    // Convert to UTC to handle time zone differences between test environments
    let expectedDate = DateTime.Parse("2020-01-01T00:00:00Z").ToUniversalTime()
    let actualDate = person.RegisteredSince.Value.ToUniversalTime()
    actualDate |> should equal expectedDate
    
[<Test>]
let ``Can validate JSON against schema``() =
    // Load the schema
    let schema = LoadJsonSchema "Data/PersonSchema.json"
    
    // Valid JSON
    let validJson = JsonValue.Parse("""
    {
        "firstName": "John",
        "lastName": "Doe",
        "age": 30,
        "email": "john.doe@example.com",
        "isActive": true
    }
    """)
    
    // Validate
    let validResult = ValidateJsonAgainstSchema schema validJson
    match validResult with
    | Valid -> ()  // Expected outcome for valid JSON
    | Invalid msg -> Assert.Fail($"Expected valid JSON but got error: {msg}")
    
    // Invalid JSON (missing required lastName)
    let invalidJson = JsonValue.Parse("""
    {
        "firstName": "John",
        "age": -5
    }
    """)
    
    // Validate
    let invalidResult = ValidateJsonAgainstSchema schema invalidJson
    match invalidResult with
    | Valid -> Assert.Fail("Expected invalid JSON to fail validation")
    | Invalid msg -> 
        // Check that the error message mentions the missing required field
        msg.Contains("lastName") |> should equal true

// Note: We cannot test for compile-time errors in the type provider
// with NUnit tests since the type provider runs at compile-time.
// The validation that Schema and Sample/SampleIsList cannot be used together
// is handled in the JsonProvider.fs file.

type NumericFields = JsonProvider<""" [ {"a":12.3}, {"a":1.23, "b":1999.0} ] """, SampleIsList=true>
type DecimalFields = JsonProvider<""" [ {"a":9999999999999999999999999999999999.3}, {"a":1.23, "b":1999.0} ] """, SampleIsList=true>
type EmbeddedResourceProvider = JsonProvider<"Data/TypeInference.json", EmbeddedResource = "FSharp.Data.Tests, FSharp.Data.Tests.Data.TypeInference.json">

[<Test>]
let ``Decimal required field is read correctly`` () = 
  let prov = NumericFields.Parse(""" {"a":123} """)
  prov.A |> should equal 123M

[<Test>]
let ``Decimal optional field is read as None`` () = 
  let prov = NumericFields.Parse(""" {"a":123} """)
  prov.B |> should equal None

let shouldThrow message func =
    let succeeded = 
        try 
            func() |> ignore
            true
        with e ->
            e.Message |> should equal message
            false
    if succeeded then
        Assert.Fail("Exception expected")

[<Test>]
let ``Reading a required field that is null throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"a":null, "b":123} """)
  (fun () -> prov.A) |> shouldThrow "'/a' is missing"

[<Test>]
let ``Reading a required field that is missing throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"b":123} """)
  (fun () -> prov.A) |> shouldThrow "'/a' is missing"

[<Test>]
let ``Reading a required decimal that is not a valid decimal throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"a":"hello", "b":123} """)
  (fun () -> prov.A) |> shouldThrow "Expecting a Decimal at '/a', got \"hello\""

[<Test>]
let ``Reading a required float that is not a valid float returns NaN`` () = 
  let prov = DecimalFields.Parse(""" {"a":"hello", "b":123} """)
  prov.A |> should equal Double.NaN

[<Test>]
let ``Can control type inference`` () =
  let inferred = JsonProvider<"Data/TypeInference.json", InferTypesFromValues=true>.GetSamples().[0]

  let intLike   : int  = inferred.IntLike
  let boolLike1 : bool = inferred.BoolLike1
  let boolLike2 : bool = inferred.BoolLike2

  intLike   |> should equal 123
  boolLike1 |> should equal false
  boolLike2 |> should equal true

  let notInferred = JsonProvider<"Data/TypeInference.json", InferTypesFromValues=false>.GetSamples().[0]

  let intLike   : string    = notInferred.IntLike
  let boolLike1 : decimal   = notInferred.BoolLike1
  let boolLike2 : string    = notInferred.BoolLike2

  intLike   |> should equal "123"
  boolLike1 |> should equal 0M
  boolLike2 |> should equal "1"

[<Test>]
let ``Optional int correctly inferred`` () = 
  let prov = JsonProvider<""" [ {"a":123}, {"a":null} ] """>.GetSamples()
  let i = prov.[0].A
  i |> should equal (Some 123)

[<Test>]
let ``Optional strings correctly handled when missing or null``() = 
  let withoutText, withText =
    JsonProvider<"Data/TwitterSample.json", SampleIsList=true>.GetSamples()
    |> Seq.map (fun tweet -> tweet.Text)
    |> Seq.toList
    |> List.partition ((=) None)
  withoutText.Length |> should equal 5
  withText.Length |> should equal 17

[<Test>]
let ``Optional records correctly handled when missing``() = 
  let tweets = JsonProvider<"Data/TwitterSample.json", SampleIsList=true>.GetSamples()
  tweets.[0].Place |> should equal None
  tweets.[13].Place |> should not' (equal None)
  tweets.[13].Place.Value.Id |> should equal "741e21eeea82f00a"

[<Test>]
let ``Optional records correctly handled when null``() = 
  let json = JsonProvider<"""[{"milestone":null},{"milestone":{"url":"https://api.github.com/repos/twitter/bootstrap/milestones/19","labels_url":"https://api.github.com/repos/twitter/bootstrap/milestones/19/labels","id":230651}}]""">.GetSamples()
  json.[0].Milestone.IsNone |> should equal true
  json.[0].Milestone.IsSome |> should equal false

[<Literal>]
let personJson = """
[
  {
    "firstName": "John",
    "lastName": "Doe",
    "address":
    {
        "state": "Texas",
        "city": "Dallas"
    }
  }
,
{
    "firstName": "Bas",
    "lastName": "Rutten",
    "address": ""
  }
]
"""

[<Test>]
let ``Optional records correctly handled when empty string``() =
    let j = JsonProvider<personJson>.GetSamples()
    j.[0].Address.IsSome |> should equal true
    j.[0].Address.Value.City |> should equal "Dallas"
    j.[1].Address |> should equal None

[<Test>]
let ``Optional collections correctly handled when null``() = 
  let withCoords, withoutCoords =         
    JsonProvider<"Data/TwitterStream.json", SampleIsList=true>.GetSamples()
    |> Seq.map (fun tweet -> tweet.Coordinates)
    |> Seq.toList
    |> List.partition (Option.isSome)
  withCoords.Length |> should equal 1
  withoutCoords.Length |> should equal 95

[<Test>]
let ``Optional collections correctly handled when missing``() = 
  let withoutMedia, withMedia = 
    JsonProvider<"Data/TwitterSample.json", SampleIsList=true>.GetSamples()
    |> Seq.choose (fun tweet -> tweet.RetweetedStatus)
    |> Seq.map (fun retweetedStatus -> retweetedStatus.Entities.Media)
    |> Seq.toList
    |> List.partition ((=) [| |])
  withMedia.Length |> should equal 1
  withoutMedia.Length |> should equal 9

[<Test>]
let ``Allways null properties correctly handled both when present and missing``() = 
  let contributors = 
    JsonProvider<"Data/TwitterSample.json", SampleIsList=true>.GetSamples()
    |> Seq.map (fun tweet -> tweet.Contributors : IJsonDocument)
  for c in contributors do
    c.JsonValue |> should equal JsonValue.Null

[<Test>]
let ``Nulls, Missing, and "" should make the type optional``() =
    let j = JsonProvider<"""[{"a":"","b":null},{"a":2,"b":"3.4","c":"true"}]""">.GetSamples()
    j.[0].A |> should equal None
    j.[0].B |> should equal None
    j.[0].C |> should equal None
    j.[1].A |> should equal (Some 2)
    j.[1].B |> should equal (Some 3.4m)
    j.[1].C |> should equal (Some true)

[<Test>]
let ``Heterogeneous types with Nulls, Missing, and "" should return None on all choices``() =
    let j = JsonProvider<"""[{"a":"","b":null},{"a":2,"b":"3.4","c":"true"},{"a":false,"b":"2002/10/10","c":"2"},{"a":[],"b":[1],"c":{"z":1}},{"b":"00:30:00"}]""">.GetSamples()
    j.[0].A.Boolean  |> should equal None
    j.[0].A.Number   |> should equal None
    j.[0].A.Array    |> should equal None
    j.[0].B.DateTime |> should equal None
    j.[0].B.TimeSpan |> should equal None
    j.[0].B.Number   |> should equal None
    j.[0].B.Array    |> should equal None
    j.[0].C.Boolean  |> should equal None
    j.[0].C.Number   |> should equal None
    j.[0].C.Record   |> should equal None
    
    j.[1].A.Boolean  |> should equal None
    j.[1].A.Number   |> should equal (Some 2)
    j.[1].A.Array    |> should equal None
    j.[1].B.DateTime |> should equal (Some (DateTime(DateTime.Today.Year,3,4)))
    j.[1].B.TimeSpan |> should equal None
    j.[1].B.Number   |> should equal (Some 3.4m)
    j.[1].B.Array    |> should equal None
    j.[1].C.Boolean  |> should equal (Some true)
    j.[1].C.Number   |> should equal None
    j.[1].C.Record   |> should equal None

    j.[2].A.Boolean  |> should equal (Some false)
    j.[2].A.Number   |> should equal None
    j.[2].A.Array    |> should equal None
    j.[2].B.DateTime |> should equal (Some (DateTime(2002,10,10)))
    j.[2].B.TimeSpan |> should equal None
    j.[2].B.Number   |> should equal None
    j.[2].B.Array    |> should equal None
    j.[2].C.Boolean  |> should equal None
    j.[2].C.Number   |> should equal (Some 2)
    j.[2].C.Record   |> should equal None

    j.[3].A.Boolean  |> should equal None
    j.[3].A.Number   |> should equal None
    j.[3].A.Array    |> should equal (Some (Array.zeroCreate<IJsonDocument> 0))
    j.[3].B.DateTime |> should equal None
    j.[3].B.TimeSpan |> should equal None
    j.[3].B.Number   |> should equal None
    j.[3].B.Array    |> should equal (Some [|1|])
    j.[3].C.Boolean  |> should equal None
    j.[3].C.Number   |> should equal None
    j.[3].C.Record   |> should not' (equal None)
    j.[3].C.Record.Value.Z |> should equal 1

    j.[4].B.TimeSpan |> should equal (Some (TimeSpan(0, 30, 0)))

[<Test>]
let ``SampleIsList for json correctly handled``() = 
    JsonProvider<"Data/TwitterSample.json", SampleIsList=true>.GetSamples()
    |> Seq.sumBy (fun tweet ->
        match tweet.Text with
        | Some _ -> 0
        | None -> 1)
    |> should equal 5

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

[<Test>]
let ``Can parse optional values in arrays``() = 
    let authors = JsonProvider<"Data/OptionValues.json">.GetSample().Authors
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
    nested |> should not' (equal simple2)

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
    let mutable failed = false
    try
        document.Age |> ignore
    with
    | _ -> failed <- true
    failed |> should be True

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
    dates.Anniversary |> should equal (new DateTimeOffset(1997, 7, 16, 19, 20, 30, 450, TimeSpan.FromHours 1.)) 

[<Test>]
let ``Can parse UTC dates``() =
    let dates = DateJSON.GetSample()
    dates.UtcTime |> should equal (new DateTimeOffset(1997, 7, 16, 19, 50, 30, TimeSpan.Zero)) 

let withCulture (cultureName: string) = 
    let originalCulture = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture <- CultureInfo cultureName
    { new IDisposable with 
        member _.Dispose() = 
            CultureInfo.CurrentCulture <- originalCulture }

[<Test>]
let ``Can parse ISO 8601 dates in the correct culture``() =
    use _holder = withCulture "zh-CN"
    let dates = DateJSON.GetSample()
    dates.NoTimeZone |> should equal (new DateTime(1997, 7, 16, 19, 20, 30, 00, System.DateTimeKind.Local)) 

[<Test>]
let ``Can parse ISO 8601 dates in the specified culture``() =
    use _holder = withCulture "pt-PT"
    let dates = JsonProvider<"""{"birthdate": "01/02/2000"}""">.GetSample()
    dates.Birthdate.Month |> should equal 1
    let dates = JsonProvider<"""{"birthdate": "01/02/2000"}""", Culture="pt-PT">.GetSample()
    dates.Birthdate.Month |> should equal 2

type TimeSpanJSON = JsonProvider<"Data/TimeSpans.json">

[<Test>]
let ``Can parse positive time span with day and fraction``() =
    let timeSpans = TimeSpanJSON.GetSample()
    timeSpans.PositiveWithDayWithFraction |> should equal (new TimeSpan(1, 3, 16, 50, 500))

[<Test>]
let ``Can parse positive time span without day and without fraction``() =
    let timeSpans = TimeSpanJSON.GetSample()
    timeSpans.PositiveWithoutDayWithoutFraction |> should equal (new TimeSpan(0, 30, 0))

[<Test>]
let ``Can parse negative time span with day and fraction``() =
    let timeSpans = TimeSpanJSON.GetSample()
    timeSpans.NegativeWithDayWithFraction |> should equal (new TimeSpan(-1, -3, -16, -50, -500))

[<Test>]
let ``Parses timespan greater than max as string`` () = 
    let span = TimeSpanJSON.GetSample().TimespanOneTickGreaterThanMaxValue
    span.GetType() |> should equal (typeof<string>)

[<Test>]
let ``Parses timespan less than min as string`` () = 
    let span = TimeSpanJSON.GetSample().TimespanOneTickLessThanMinValue
    span.GetType() |> should equal (typeof<string>)

[<Test>]
let ``Can parse time span in different culture``() =
    let timeSpans = JsonProvider<"""{"frTimeSpan": "1:3:16:50,5"}""", Culture="fr">.GetSample()
    timeSpans.FrTimeSpan |> should equal (new TimeSpan(1, 3, 16, 50, 500))

[<Test>]
let ``Parsing of values wrapped in quotes should work on heterogenous values``() =
    let objs = JsonProvider<"""[{"a": "01/02/2000"}, {"a" : "3"}]""">.GetSamples()
    objs.[0].A.DateTime |> should equal (Some (DateTime(2000,01,02)))
    objs.[0].A.Number |> should equal None
    objs.[1].A.DateTime |> should equal None
    objs.[1].A.Number |> should equal (Some 3)

[<Test>]
let ``Parsing of values wrapped in quotes should work on arrays``() =
    let objs = JsonProvider<"""["01/02/2000", "02/02/2001", "3", 4]""">.GetSample()
    objs.DateTimes |> should equal [| DateTime(2000,01,02); DateTime(2001,02,02) |]
    objs.Numbers |> should equal [| 3; 4 |]

[<Literal>]
let jsonSample = """[{"Facts": [{"Description": "sdfsdfsdfsdfs",
                            "Name": "sdfsdf",
                            "Unit": "kg",
                            "Value": {"a":89.00}}],
                "Name" : "sdfsdf"},
                {"Facts": [{"Description": "sdfsdfsdfsdfs",
                            "Name": "ddd",
                            "Value": {"b":100}}]}]"""

[<Test>]
let ``Test error messages``() =    
    let j = JsonProvider<jsonSample>.Parse """[{"Facts": [{"Name": "foo"}]}]"""
    let errorMessage = 
        try
            j.[0].Facts.[0].Value |> ignore
            ""
        with e ->
            e.Message
    errorMessage |> should equal """Property 'Value' not found at '[0]/Facts[0]': {"Name":"foo"}"""

type JsonWithNestedArray = JsonProvider<"""{"columns" : [ "d" ], "data" : [ [ { "data" : { "EffectiveDate" : "2013-10-04T16:28:27.1370000+00:00","Id" : "2a6f4dcf-90f8-4286-92de-78b2a687c9d7","IsSampleData" : true } } ] ]}""">

[<Test>]
let ``Can parse nested arrays``() =
    let j = JsonWithNestedArray.GetSample()
    let matrix : JsonWithNestedArray.Datum[][] = j.Data
    matrix.Length |> should equal 1
    let row = matrix.[0]
    row.Length |> should equal 1
    let cell = row.[0]
    cell.Data.IsSampleData |> should equal true

[<Test>]
let ``Can parse optional arrays``() =
    let j = JsonProvider<"Data/contacts.json">.GetSample()
    j.Ab.Persons.[0].Contacts.[0].Emails |> should equal [| |]
    j.Ab.Persons.[0].Contacts.[1].Emails.Length |> should equal 1

let normalize (str:string) =
  str.Replace("\r\n", "\n")
     .Replace("\r", "\n")

type GitHub = JsonProvider<"Data/GitHub.json", RootName="Issue">

[<Test>]
let ``Can construct complex objects``() =
    let user = GitHub.User("login", 0, "avatarUrl", Guid.Parse("{75B3E239-BF95-4FAB-ABE3-F2795D3C843B}"), "url", "htmlUrl", "folowersUrl", "followingUrl", "gistsUrl",
                           "starredUrl", "subscriptionsUrl", "organizationsUrl", "reposUrl", "eventsUrl", "receivedEventsUrl", "type")
    let pullRequest = GitHub.PullRequest(None, None, None)
    let label1 = GitHub.Label("url", "name", GitHub.FloatOrString(1.5))
    let label2 = GitHub.Label("url", "name", GitHub.FloatOrString("string"))
    let json = GitHub.Issue("url", "labelsUrl", "commentsUrl", "eventsUrl", "htmlUrl", 0, 1, "title", user, [| label1; label2 |], "state",
                            JsonValue.Null, JsonValue.Null, 2, DateTimeOffset(2013,03,15,0,0,0,TimeSpan.Zero), DateTimeOffset(2013,03,16,0,0,0,TimeSpan.Zero), JsonValue.Null, pullRequest, None)
    json.JsonValue.ToString() |> normalize |> should equal (normalize """{
  "url": "url",
  "labels_url": "labelsUrl",
  "comments_url": "commentsUrl",
  "events_url": "eventsUrl",
  "html_url": "htmlUrl",
  "id": 0,
  "number": 1,
  "title": "title",
  "user": {
    "login": "login",
    "id": 0,
    "avatar_url": "avatarUrl",
    "gravatar_id": "75b3e239-bf95-4fab-abe3-f2795d3c843b",
    "url": "url",
    "html_url": "htmlUrl",
    "followers_url": "folowersUrl",
    "following_url": "followingUrl",
    "gists_url": "gistsUrl",
    "starred_url": "starredUrl",
    "subscriptions_url": "subscriptionsUrl",
    "organizations_url": "organizationsUrl",
    "repos_url": "reposUrl",
    "events_url": "eventsUrl",
    "received_events_url": "receivedEventsUrl",
    "type": "type"
  },
  "labels": [
    {
      "url": "url",
      "name": "name",
      "color": 1.5
    },
    {
      "url": "url",
      "name": "name",
      "color": "string"
    }
  ],
  "state": "state",
  "assignee": null,
  "milestone": null,
  "comments": 2,
  "created_at": "2013-03-15T00:00:00.0000000+00:00",
  "updated_at": "2013-03-16T00:00:00.0000000+00:00",
  "closed_at": null,
  "pull_request": {
    "html_url": null,
    "diff_url": null,
    "patch_url": null
  },
  "body": null
}""")

type HeterogeneousArray = JsonProvider<"""[8, 9, false, { "a": 3 }]""">

[<Test>]
let ``Can construct heterogeneous array``() =
    let json = HeterogeneousArray.Root([| 8; 9 |], false, HeterogeneousArray.Record(3))
    json.JsonValue.ToString() |> normalize |> should equal (normalize """[
  8,
  9,
  false,
  {
    "a": 3
  }
]""")

type HeterogeneousArrayWithOptionals = JsonProvider<"""[ [{ "a": 3 }], [8, 9, false, { "a": 3 }] ]""", SampleIsList=true>

[<Test>]
let ``Can construct heterogeneous arrays with optionals``() =
    let json = HeterogeneousArrayWithOptionals.Root([| |], None, HeterogeneousArrayWithOptionals.Record(3))
    json.JsonValue.ToString() |> normalize |> should equal (normalize """[
  {
    "a": 3
  }
]""")

[<Test>]
let ``Weird UnitSystem case``() =
    let comments = JsonProvider<"Data/reddit.json">.GetSample()
    let data = comments.Data.Children.[0].Data
    data.LinkId |> should equal "t3_2424px"

[<Test>]
let ``Whitespace is preserved``() =
    let j = JsonProvider<"""{ "s": " "}""">.GetSample()
    j.S |> should equal " "

[<Test>]
let ``Getting a decimal at runtime when an integer was inferred should throw``() =
    let json = JsonProvider<"""{ "x" : 0.500, "y" : 0.000 }""">.Parse("""{ "x" : -0.250, "y" : 0.800 }""")
    (fun () -> json.Y) |> shouldThrow "Expecting an Int32 at '/y', got 0.800"

[<Test>]
let ``DateTime and DateTimeOffset mix results in DateTime`` () =
    let j = JsonProvider<"""{"dates" : ["2016-08-01T04:50:13.619+10:00", "2016-10-05T04:05:03", "2016-07-01T00:00:00.000-05:00"]}""">.GetSample()
    Array.TrueForAll(j.Dates, fun x -> x.GetType() = typeof<DateTime>) |> should equal true

[<Test>]
let ``Collection of DateTimeOffset should have the type DateTimeOffset`` () =
    let j = JsonProvider<"""{"dates" : ["2016-08-01T04:50:13.619+10:00", "/Date(123123+0600)/", "2016-07-01T00:00:00.000-05:00"]}""">.GetSample()
    Array.TrueForAll(j.Dates, fun x -> x.GetType() = typeof<DateTimeOffset>) |> should equal true

[<Test>]
let ``Getting a large decimal at runtime when an integer was inferred should throw``() =
    let json = JsonProvider<"""{ "x" : 0.500, "y" : 0.000 }""">.Parse("""{ "x" : -0.250, "y" : 12345678901234567890 }""")
    (fun () -> json.Y) |> shouldThrow "Expecting an Int32 at '/y', got 12345678901234567890"

[<Test>]
let ``ParseList return result list`` () =
  let prov = NumericFields.ParseList(""" [{"a":123}, {"a":987}] """)
  prov |> Array.map (fun v -> v.A) |> Array.sort |> should equal [|123M; 987M|]


type ServiceResponse = JsonProvider<"""[
{ "code": 0, "value": {"generic payload": "yes"}, "message": null},
{ "code": 1, "value": null, "message": "Warning"},
{ "code": 2, "value": [], "message": "Exception"}
]
""", SampleIsList = true>

type FirstPayload = JsonProvider<"""{ "x" : 0.500, "y" : 0.000 }""">
type SecondPayload = JsonProvider<"""{"user": "alice", "role": "admin", "registeredSince": "2021-11-01"}""">

[<Test>]
let ``Can re-load JsonValue`` () =
  let json = FirstPayload.Parse("""{ "x" : -0.250, "y" : 12345}""")
  FirstPayload.Load(json.JsonValue) |> should equal json

[<Test>]
let ``Can load different nested payloads`` () =
  let json1 = ServiceResponse.Parse("""{ "code": 0, "value": { "x" : -0.250, "y" : 12345}, "message": null}""")
  let json2 = ServiceResponse.Parse("""{ "code": 0, "value": {"user": "alice", "role": "admin", "registeredSince": "2021-11-01"}, "message": null}""")
  let payload1 = FirstPayload.Load(json1.Value.JsonValue)

  let payload2 = SecondPayload.Load(json2.Value.JsonValue)
  payload1.X |> should equal -0.250
  payload1.Y |> should equal 12345
  payload2.User |> should equal "alice"
  payload2.Role |> should equal "admin"
  payload2.RegisteredSince |> should equal (DateTime(2021, 11, 1))


[<Test>]
let ``Can control dictionary inference`` () =
    let notinferred = JsonProvider<"Data/DictionaryInference.json", PreferDictionaries=false>.GetSamples().[0]

    notinferred.Rec.``0``   |> should equal 111
    notinferred.Rec.``1``   |> should equal (Some 222)
    
    let inferred = JsonProvider<"Data/DictionaryInference.json", PreferDictionaries=true>.GetSamples().[0]
    
    inferred.Rec.Count |> should equal 2
    inferred.Rec.IsEmpty |> should equal false
    
    inferred.Rec.ContainsKey(false) |> should equal true
    inferred.Rec.[true] |> should equal 222
    inferred.Rec.TryFind(true) |> should equal <| Some 222

    inferred.Rec.Values |> should equal [|111; 222|]
    inferred.Rec.Keys   |> should equal [|false; true|]

open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.UnitSystems.SI.UnitNames

[<Literal>]
let ambiguousJsonWithInlineSchemas = """
[
    { "Code": "typeof<string>", "Enabled": true,  "Date": "typeof<string>",      "Length": "typeof<float<metre>>", "Obj": { "X": "typeof<int>" } },
    { "Code": "123",            "Enabled": true,  "Date": "2022-06-11",          "Length": 1.83,                   "Obj": { "X": "0" } },
    { "Code": "000",            "Enabled": false,                                                                  "Obj": null },
    { "Code": "4E5",            "Enabled": true,  "Date": "2022-06-12T01:02:03", "Length": 2.00,                   "Obj": { "X": "1" } }
]
"""

type InlineSchemasJsonDefaultInference = JsonProvider<ambiguousJsonWithInlineSchemas, SampleIsList = true>
type InlineSchemasJsonNoInference = JsonProvider<ambiguousJsonWithInlineSchemas, SampleIsList = true, InferenceMode = InferenceMode.NoInference>
type InlineSchemasJsonInlineSchemasHints = JsonProvider<ambiguousJsonWithInlineSchemas, SampleIsList = true, InferenceMode = InferenceMode.ValuesAndInlineSchemasHints>
type InlineSchemasJsonInlineSchemasOverrides = JsonProvider<ambiguousJsonWithInlineSchemas, SampleIsList = true, InferenceMode = InferenceMode.ValuesAndInlineSchemasOverrides>

[<Test>]
let ``Inline schemas are disabled by default and are recognized as strings`` () =
    // For backward compat, inline schemas are disabled by default.
    let sample = InlineSchemasJsonDefaultInference.GetSamples()
    sample[1].Code.String.GetType() |> should equal (typeof<string option>)
    sample[1].Code.Number.GetType() |> should equal (typeof<float option>)
    sample[1].Enabled.GetType() |> should equal (typeof<bool>)
    sample[1].Date.String.GetType() |> should equal (typeof<string option>)
    sample[1].Date.DateTime.GetType() |> should equal (typeof<DateTime option>)
    sample[1].Length.String.GetType() |> should equal (typeof<string option>)
    sample[1].Length.Number.GetType() |> should equal (typeof<decimal option>)
    sample[1].Obj.Value.X.String.GetType() |> should equal (typeof<string option>)
    sample[1].Obj.Value.X.Number.GetType() |> should equal (typeof<bool option>) // (There is probably a little but here. The property should be called Boolean)

[<Test>]
let ``"No inference" mode disables type inference`` () =
    let sample = InlineSchemasJsonNoInference.GetSamples()
    sample[1].Code.GetType() |> should equal (typeof<string>)
    sample[1].Enabled.GetType() |> should equal (typeof<bool>) // bool is a json type so it's detected even when inference is disabled.
    sample[1].Date.GetType() |> should equal (typeof<string option>)
    sample[1].Length.String.GetType() |> should equal (typeof<string option>)
    sample[1].Length.Number.GetType() |> should equal (typeof<decimal option>) // number is also a json type so it's detected as well.
    sample[1].Obj.Value.X.GetType() |> should equal (typeof<string>)

[<Test>]
let ``Inline schemas as hints add new types to the value-based inference`` () =
    let sample = InlineSchemasJsonInlineSchemasHints.GetSamples()
    // Same as with only value inference because the inline schemas define string types:
    sample[1].Code.String.GetType() |> should equal (typeof<string option>)
    sample[1].Code.Number.GetType() |> should equal (typeof<float option>)
    sample[1].Enabled.GetType() |> should equal (typeof<bool>)
    sample[1].Date.String.GetType() |> should equal (typeof<string option>)
    sample[1].Date.DateTime.GetType() |> should equal (typeof<DateTime option>)
    // This one is inferred as a float instead of a decimal.
    // We specified a unit but it cannot be reconciled with other values that don't have it so it's ignored.
    sample[1].Length.GetType() |> should equal (typeof<float option>)
    // This one is now inferred as an int and not a bool:
    sample[1].Obj.Value.X.GetType() |> should equal (typeof<int>)

[<Test>]
let ``Inline schemas as overrides replace value-based inference when present`` () =
    let sample = InlineSchemasJsonInlineSchemasOverrides.GetSamples()
    // We know the Code property can contain letters even though our sample only contain numbers,
    // so we added an overriding inline schema.
    // With the inline schema, the value is no longer inferred as maybe a float:
    sample[1].Code.GetType() |> should equal (typeof<string>)
    // Value inference is still used when there is no inline schema:
    sample[1].Enabled.GetType() |> should equal (typeof<bool>)
    // Let's say we want to parse dates ourselves, so we want the provider to always give us strings:
    sample[1].Date.GetType() |> should equal (typeof<string option>)
    // We now have a unit!
    sample[1].Length.GetType() |> should equal (typeof<float<metre> option>)
    sample[1].Obj.Value.X.GetType() |> should equal (typeof<int>)
    // (Note the types in the inline schemas are automatically transformed to options as needed
    // when another node does not define any value for the given property)

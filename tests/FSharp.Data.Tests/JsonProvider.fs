#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.JsonProvider
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes

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
  tweets.[13].Place |> should notEqual None
  tweets.[13].Place.Value.Id |> should equal "741e21eeea82f00a"

[<Test>]
let ``Optional records correctly handled when null``() = 
  let json = JsonProvider<"""[{"milestone":null},{"milestone":{"url":"https://api.github.com/repos/twitter/bootstrap/milestones/19","labels_url":"https://api.github.com/repos/twitter/bootstrap/milestones/19/labels","id":230651}}]""">.GetSamples()
  json.[0].Milestone.IsNone |> should equal true
  json.[0].Milestone.IsSome |> should equal false

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
    let j = JsonProvider<"""[{"a":"","b":null},{"a":2,"b":"3.4","c":"true"},{"a":false,"b":"2002/10/10","c":"2"},{"a":[],"b":[1],"c":{"z":1}}]""">.GetSamples()
    j.[0].A.Boolean  |> should equal None
    j.[0].A.Number   |> should equal None
    j.[0].A.Array    |> should equal None
    j.[0].B.DateTime |> should equal None
    j.[0].B.Number   |> should equal None
    j.[0].B.Array    |> should equal None
    j.[0].C.Boolean  |> should equal None
    j.[0].C.Number   |> should equal None
    j.[0].C.Record   |> should equal None
    
    j.[1].A.Boolean  |> should equal None
    j.[1].A.Number   |> should equal (Some 2)
    j.[1].A.Array    |> should equal None
    j.[1].B.DateTime |> should equal (Some (DateTime(DateTime.Today.Year,3,4)))
    j.[1].B.Number   |> should equal (Some 3.4m)
    j.[1].B.Array    |> should equal None
    j.[1].C.Boolean  |> should equal (Some true)
    j.[1].C.Number   |> should equal None
    j.[1].C.Record   |> should equal None

    j.[2].A.Boolean  |> should equal (Some false)
    j.[2].A.Number   |> should equal None
    j.[2].A.Array    |> should equal None
    j.[2].B.DateTime |> should equal (Some (DateTime(2002,10,10)))
    j.[2].B.Number   |> should equal None
    j.[2].B.Array    |> should equal None
    j.[2].C.Boolean  |> should equal None
    j.[2].C.Number   |> should equal (Some 2)
    j.[2].C.Record   |> should equal None

    j.[3].A.Boolean  |> should equal None
    j.[3].A.Number   |> should equal None
    j.[3].A.Array    |> should equal (Some (Array.zeroCreate<IJsonDocument> 0))
    j.[3].B.DateTime |> should equal None
    j.[3].B.Number   |> should equal None
    j.[3].B.Array    |> should equal (Some [|1|])
    j.[3].C.Boolean  |> should equal None
    j.[3].C.Number   |> should equal None
    j.[3].C.Record   |> should notEqual None
    j.[3].C.Record.Value.Z |> should equal 1

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
                            JsonValue.Null, JsonValue.Null, 2, DateTime(2013,03,15), DateTime(2013,03,16), JsonValue.Null, pullRequest, None)
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
  "created_at": "2013-03-15T00:00:00.0000000",
  "updated_at": "2013-03-16T00:00:00.0000000",
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
    data.LinkId |> shouldEqual "t3_2424px"

[<Test>]
let ``Whitespace is preserved``() =
    let j = JsonProvider<"""{ "s": " "}""">.GetSample()
    j.S |> should equal " "

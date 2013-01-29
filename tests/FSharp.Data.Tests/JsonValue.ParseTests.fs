module FSharp.Data.Tests.JsonValue.ParseTests

open NUnit.Framework
open System
open System.Globalization
open System.Threading
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FsUnit

[<Test>]
let ``Can parse empty document``() = 
    let j = JsonValue.Parse "{}"
    j |> should equal (JsonValue.Object Map.empty)

[<Test>] 
let ``Can parse document with single property``() =
    let j = JsonValue.Parse "{\"firstName\": \"John\"}"
    j?firstName.AsString() |> should equal "John"

[<Test>] 
let ``Can parse document with text and integer``() =
    let j = JsonValue.Parse "{\"firstName\": \"John\", \"lastName\": \"Smith\", \"age\": 25}"
    j?firstName.AsString() |> should equal "John"
    j?lastName.AsString() |> should equal "Smith"
    j?age.AsInteger()  |> should equal 25

[<Test>] 
let ``Can parse document with text and float``() =
    let j = JsonValue.Parse "{\"firstName\": \"John\", \"lastName\": \"Smith\", \"age\": 25.25}"
    j?age.AsFloat() |> should equal 25.25

[<Test>]
let ``Can parse document with date``() =
    let j = JsonValue.Parse "{\"anniversary\": \"\\/Date(869080830450)\\/\"}"
    j?anniversary.AsDateTime() |> should equal (new DateTime(1997, 07, 16, 19, 20, 30, 450, DateTimeKind.Utc))
    j?anniversary.AsDateTime().Kind |> should equal DateTimeKind.Utc

[<Test>]
let ``Can parse document with iso date``() =
    let j = JsonValue.Parse "{\"anniversary\": \"2009-05-19 14:39:22.500\"}"
    j?anniversary.AsDateTime() |> should equal (new DateTime(2009, 05, 19, 14, 39, 22, 500, DateTimeKind.Local))
    j?anniversary.AsDateTime().Kind |> should equal DateTimeKind.Local

[<Test>]
[<SetCulture("zh-CN")>]
let ``Can parse document with iso date in local culture``() =
    let j = JsonValue.Parse "{\"anniversary\": \"2009-05-19 14:39:22.500\"}"
    j?anniversary.AsDateTime() |> should equal (new DateTime(2009, 05, 19, 14, 39, 22, 500, DateTimeKind.Local))
    j?anniversary.AsDateTime().Kind |> should equal DateTimeKind.Local

[<Test>]
let ``Can parse document with partial iso date``() =
    let j = JsonValue.Parse "{\"anniversary\": \"2009-05-19\"}"
    j?anniversary.AsDateTime() |> should equal (new DateTime(2009, 05, 19, 0, 0, 0, DateTimeKind.Local))
    j?anniversary.AsDateTime().Kind |> should equal DateTimeKind.Local

[<Test>]
let ``Can parse document with timezone iso date``() =
    let j = JsonValue.Parse "{\"anniversary\": \"2009-05-19 14:39:22+0600\"}"
    j?anniversary.AsDateTime().ToUniversalTime() |> should equal (new DateTime(2009, 05, 19, 8, 39, 22, DateTimeKind.Utc))
    
[<Test>]
let ``Can parse document with UTC iso date``() =
    let j = JsonValue.Parse "{\"anniversary\": \"2009-05-19 14:39:22Z\"}"
    j?anniversary.AsDateTime().ToUniversalTime() |> should equal (new DateTime(2009, 05, 19, 14, 39, 22, DateTimeKind.Utc))
    j?anniversary.AsDateTime().Kind |> should equal DateTimeKind.Utc

[<Test>]    
let ``Can parse document with timezone and fraction iso date``() =
    let j = JsonValue.Parse "{\"anniversary\": \"1997-07-16T19:20:30.45+01:00\"}"
    j?anniversary.AsDateTime().ToUniversalTime() |> should equal (new DateTime(1997, 07, 16, 18, 20, 30, 450, DateTimeKind.Utc))
    
// TODO: Due to limitations in the current ISO 8601 datetime parsing these fail, and should be made to pass
//[<Test>]
//let ``Cant Yet parse document with basic iso date``() =
//    let j = JsonValue.Parse "{\"anniversary\": \"19810405\"}"
//    j?anniversary.AsDateTime |> should equal (new DateTime(1981, 04, 05))
//
//[<Test>]
//let ``Cant Yet parse weird iso date``() =
//    let j = JsonValue.Parse "{\"anniversary\": \"2010-02-18T16.5\"}"
//    j?anniversary.AsDateTime |> should equal (new DateTime(2010, 02, 18, 16, 30, 00))

[<Test>]
let ``Can parse completely invalid, but close, date as string``() =
    let j = JsonValue.Parse "{\"anniversary\": \"2010-02-18T16.5:23.35:4\"}"
    (fun () -> j?anniversary.AsDateTime() |> ignore) |> should throw typeof<Exception>
    j?anniversary.AsString() |> should equal "2010-02-18T16.5:23.35:4"

[<Test>] 
let ``Can parse document with fractional numbers``() =
    let originalCulture = Thread.CurrentThread.CurrentCulture
    // use a culture that uses ',' instead o '.' for decimal separators
    Thread.CurrentThread.CurrentCulture <- new CultureInfo("pt-PT") 
    try 
      let j = JsonValue.Parse "{ \"age\": 25.5}"
      j?age.AsFloat() |> should equal 25.5
    finally
      Thread.CurrentThread.CurrentCulture <- originalCulture

[<Test>]
let ``Can parse nested document`` () =
    let j = JsonValue.Parse "{ \"main\": { \"title\": \"example\", \"nested\": { \"nestedTitle\": \"sub\" } } }"
    let main = j?main
    main?title.AsString() |> should equal "example"
    let nested = main?nested
    nested?nestedTitle.AsString() |> should equal "sub"
                
[<Test>] 
let ``Can parse document with booleans``() =
    let j = JsonValue.Parse "{ \"hasTrue\": true, \"hasFalse\": false }"
    j?hasTrue.AsBoolean() |> should equal true
    j?hasFalse.AsBoolean() |> should equal false

[<Test>] 
let ``Can parse document with null``() =    
    let j = JsonValue.Parse "{ \"items\": [{\"id\": \"Open\"}, null, {\"id\": \"Pause\"}] }"
    let jArray = j?items
    jArray.[0]?id.AsString() |> should equal "Open"
    jArray.[1]             |> should equal JsonValue.Null
    jArray.[2]?id.AsString() |> should equal "Pause"

[<Test>] 
let ``Can parse array in outermost scope``() =
    let jArray = JsonValue.Parse "[{\"id\": \"Open\"}, null, {\"id\": \"Pause\"}]"
    jArray.[0]?id.AsString() |> should equal "Open"
    jArray.[1]             |> should equal JsonValue.Null
    jArray.[2]?id.AsString() |> should equal "Pause"

[<Test>]
let ``Can parse a string from twitter api without throwing an error``() =
    let text =        
      "[{\"in_reply_to_status_id_str\":\"115445959386861568\",\"truncated\":false,\"in_reply_to_user_id_str\":\"40453522\",\"geo\":null,\"retweet_count\":0,\"contributors\":null,\"coordinates\":null,\"user\":{\"default_profile\":false,\"statuses_count\":3638,\"favourites_count\":28,\"protected\":false,\"profile_text_color\":\"634047\",\"profile_image_url\":\"http:\\/\\/a3.twimg.com\\/profile_images\\/1280550984\\/buddy_lueneburg_normal.jpg\",\"name\":\"Steffen Forkmann\",\"profile_sidebar_fill_color\":\"E3E2DE\",\"listed_count\":46,\"following\":true,\"profile_background_tile\":false,\"utc_offset\":3600,\"description\":\"C#, F# and Dynamics NAV developer, blogger and sometimes speaker. Creator of FAKE - F# Make and NaturalSpec.\",\"location\":\"Hamburg \\/ Germany\",\"contributors_enabled\":false,\"verified\":false,\"profile_link_color\":\"088253\",\"followers_count\":471,\"url\":\"http:\\/\\/www.navision-blog.de\\/blog-mitglieder\\/steffen-forkmann-ueber-mich\\/\",\"profile_sidebar_border_color\":\"D3D2CF\",\"screen_name\":\"sforkmann\",\"default_profile_image\":false,\"notifications\":false,\"show_all_inline_media\":false,\"geo_enabled\":true,\"profile_use_background_image\":true,\"friends_count\":373,\"id_str\":\"22477880\",\"is_translator\":false,\"lang\":\"en\",\"time_zone\":\"Berlin\",\"created_at\":\"Mon Mar 02 12:04:39 +0000 2009\",\"profile_background_color\":\"EDECE9\",\"id\":22477880,\"follow_request_sent\":false,\"profile_background_image_url_https\":\"https:\\/\\/si0.twimg.com\\/images\\/themes\\/theme3\\/bg.gif\",\"profile_background_image_url\":\"http:\\/\\/a1.twimg.com\\/images\\/themes\\/theme3\\/bg.gif\",\"profile_image_url_https\":\"https:\\/\\/si0.twimg.com\\/profile_images\\/1280550984\\/buddy_lueneburg_normal.jpg\"},\"favorited\":false,\"in_reply_to_screen_name\":\"ovatsus\",\"source\":\"\\u003Ca href=\\\"http:\\/\\/www.tweetdeck.com\\\" rel=\\\"nofollow\\\"\\u003ETweetDeck\\u003C\\/a\\u003E\",\"id_str\":\"115447331628916736\",\"in_reply_to_status_id\":115445959386861568,\"id\":115447331628916736,\"created_at\":\"Sun Sep 18 15:29:23 +0000 2011\",\"place\":null,\"retweeted\":false,\"in_reply_to_user_id\":40453522,\"text\":\"@ovatsus I know it's not complete. But I don't want to add a dependency on FParsec in #FSharp.Data. Can you send me samples where it fails?\"},{\"in_reply_to_status_id_str\":null,\"truncated\":false,\"in_reply_to_user_id_str\":null,\"geo\":null,\"retweet_count\":0,\"contributors\":null,\"coordinates\":null,\"user\":{\"statuses_count\":3637,\"favourites_count\":28,\"protected\":false,\"profile_text_color\":\"634047\",\"profile_image_url\":\"http:\\/\\/a3.twimg.com\\/profile_images\\/1280550984\\/buddy_lueneburg_normal.jpg\",\"name\":\"Steffen Forkmann\",\"profile_sidebar_fill_color\":\"E3E2DE\",\"listed_count\":46,\"following\":true,\"profile_background_tile\":false,\"utc_offset\":3600,\"description\":\"C#, F# and Dynamics NAV developer, blogger and sometimes speaker. Creator of FAKE - F# Make and NaturalSpec.\",\"location\":\"Hamburg \\/ Germany\",\"contributors_enabled\":false,\"verified\":false,\"profile_link_color\":\"088253\",\"followers_count\":471,\"url\":\"http:\\/\\/www.navision-blog.de\\/blog-mitglieder\\/steffen-forkmann-ueber-mich\\/\",\"profile_sidebar_border_color\":\"D3D2CF\",\"screen_name\":\"sforkmann\",\"default_profile_image\":false,\"notifications\":false,\"show_all_inline_media\":false,\"geo_enabled\":true,\"profile_use_background_image\":true,\"friends_count\":372,\"id_str\":\"22477880\",\"is_translator\":false,\"lang\":\"en\",\"time_zone\":\"Berlin\",\"created_at\":\"Mon Mar 02 12:04:39 +0000 2009\",\"profile_background_color\":\"EDECE9\",\"id\":22477880,\"default_profile\":false,\"follow_request_sent\":false,\"profile_background_image_url_https\":\"https:\\/\\/si0.twimg.com\\/images\\/themes\\/theme3\\/bg.gif\",\"profile_background_image_url\":\"http:\\/\\/a1.twimg.com\\/images\\/themes\\/theme3\\/bg.gif\",\"profile_image_url_https\":\"https:\\/\\/si0.twimg.com\\/profile_images\\/1280550984\\/buddy_lueneburg_normal.jpg\"},\"favorited\":false,\"in_reply_to_screen_name\":null,\"source\":\"\\u003Ca href=\\\"http:\\/\\/www.tweetdeck.com\\\" rel=\\\"nofollow\\\"\\u003ETweetDeck\\u003C\\/a\\u003E\",\"id_str\":\"115444490331889664\",\"in_reply_to_status_id\":null,\"id\":115444490331889664,\"created_at\":\"Sun Sep 18 15:18:06 +0000 2011\",\"possibly_sensitive\":false,\"place\":null,\"retweeted\":false,\"in_reply_to_user_id\":null,\"text\":\"Added a simple Json parser to #FSharp.Data http:\\/\\/t.co\\/3JGI56SM - #fsharp\"}]"
    JsonValue.Parse text |> printfn "%A"

[<Test>]
let ``Can parse array of numbers``() = 
    let j = JsonValue.Parse "[1, 2, 3]"
    j.[0] |> should equal (JsonValue.Number 1m)
    j.[1] |> should equal (JsonValue.Number 2m)
    j.[2] |> should equal (JsonValue.Number 3m)

[<Test>]
let ``Quotes in strings are property escaped``() = 
    let jsonStr = "{\"short_description\":\"This a string with \\\"quotes\\\"\"}"
    let j = JsonValue.Parse jsonStr
    j.ToString() |> should equal jsonStr

[<Test>]
let ``Can parse simple array``() = 
    let j = JsonValue.Parse "[\"Adam\",\"Eve\",\"Bonnie\",\"Clyde\",\"Donald\",\"Daisy\",\"Han\",\"Leia\"]"
    j.[0] |> should equal (JsonValue.String "Adam")
    j.[1] |> should equal (JsonValue.String "Eve")
    j.[2] |> should equal (JsonValue.String "Bonnie")
    j.[3] |> should equal (JsonValue.String "Clyde")

[<Test>]
let ``Can parse nested array``() = 
    let j = JsonValue.Parse "[ [\"Adam\", \"Eve\"], [\"Bonnie\", \"Clyde\"], [\"Donald\", \"Daisy\"], [\"Han\", \"Leia\"] ]"
    j.[0].[0] |> should equal (JsonValue.String "Adam")
    j.[0].[1] |> should equal (JsonValue.String "Eve")
    j.[1].[0] |> should equal (JsonValue.String "Bonnie")
    j.[1].[1] |> should equal (JsonValue.String "Clyde")

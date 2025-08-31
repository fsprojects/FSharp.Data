module FSharp.Data.Tests.WorldBankRuntime

open NUnit.Framework
open FsUnit
open System
open System.Collections.Generic
open FSharp.Data
open FSharp.Data.Runtime.WorldBank
open FSharp.Data.Runtime.Caching

// Test data structures and mock implementations
type MockCache() =
    let cache = Dictionary<string, string>()
    
    interface ICache<string, string> with
        member _.TryRetrieve(key, ?extendCacheExpiration) = 
            ignore extendCacheExpiration
            if cache.ContainsKey(key) then Some(cache.[key]) else None
        member _.Set(key, value) = cache.[key] <- value
        member _.Remove(key) = cache.Remove(key) |> ignore

// Mock JSON responses that mimic WorldBank API responses
let mockIndicatorResponse = """
[
  {
    "page": 1,
    "pages": 1,
    "per_page": 1000,
    "total": 2
  },
  [
    {
      "id": "AG.AGR.TRAC.NO",
      "name": "Agricultural machinery, tractors",
      "source": {
        "id": "2",
        "value": "World Development Indicators"
      },
      "sourceNote": "Agricultural machinery refers to the number of wheel and crawler tractors.",
      "topics": [
        {
          "id": "9",
          "value": "Infrastructure"
        }
      ]
    },
    {
      "id": "AG.CON.FERT.ZS", 
      "name": "Fertilizer consumption (kilograms per hectare of arable land)",
      "source": {
        "id": "2",
        "value": "World Development Indicators"
      },
      "sourceNote": "Fertilizer consumption measures the quantity of plant nutrients used per unit of arable land.",
      "topics": [
        {
          "id": "6",
          "value": "Environment"
        }
      ]
    }
  ]
]
"""

let mockCountryResponse = """
[
  {
    "page": 1,
    "pages": 1,
    "per_page": 1000,
    "total": 3
  },
  [
    {
      "id": "ABW",
      "name": "Aruba",
      "capitalCity": "Oranjestad",
      "region": {
        "id": "LCN",
        "value": "Latin America & Caribbean"
      }
    },
    {
      "id": "AFG",
      "name": "Afghanistan", 
      "capitalCity": "Kabul",
      "region": {
        "id": "SAS",
        "value": "South Asia"
      }
    },
    {
      "id": "WLD",
      "name": "World",
      "capitalCity": "",
      "region": {
        "id": "NA",
        "value": "Aggregates"
      }
    }
  ]
]
"""

let mockTopicResponse = """
[
  {
    "page": 1,
    "pages": 1,
    "per_page": 1000,
    "total": 2
  },
  [
    {
      "id": "1",
      "value": "Agriculture & Rural Development",
      "sourceNote": "For the 70 percent of the world's poor who live in rural areas, agriculture is the main source of income and employment."
    },
    {
      "id": "2", 
      "value": "Aid Effectiveness",
      "sourceNote": "Aid effectiveness is the impact that aid has in reducing poverty and inequality."
    }
  ]
]
"""

let mockRegionResponse = """
[
  {
    "page": 1,
    "pages": 1,
    "per_page": 1000,
    "total": 2
  },
  [
    {
      "code": "EAS",
      "name": "East Asia & Pacific"
    },
    {
      "code": "ECS",
      "name": "Europe & Central Asia"
    }
  ]
]
"""

let mockDataResponse = """
[
  {
    "page": 1,
    "pages": 1,
    "per_page": 1000,
    "total": 3
  },
  [
    {
      "date": "2020",
      "value": "65894.86"
    },
    {
      "date": "2019", 
      "value": "62888.17"
    },
    {
      "date": "2018",
      "value": null
    }
  ]
]
"""

[<Test>]
let ``IndicatorRecord should have correct IsRegion property for regular country`` () =
    let country = { Id = "USA"; Name = "United States"; CapitalCity = "Washington"; Region = "North America" }
    country.IsRegion |> should be False

[<Test>] 
let ``IndicatorRecord should have correct IsRegion property for aggregate region`` () =
    let region = { Id = "WLD"; Name = "World"; CapitalCity = ""; Region = "Aggregates" }
    region.IsRegion |> should be True

[<Test>]
let ``IndicatorRecord should be constructed with all required fields`` () =
    let indicator = { 
        Id = "AG.AGR.TRAC.NO"
        Name = "Agricultural machinery, tractors"
        TopicIds = ["9"]
        Source = "World Development Indicators"
        Description = "Agricultural machinery refers to the number of wheel and crawler tractors."
    }
    
    indicator.Id |> should equal "AG.AGR.TRAC.NO"
    indicator.Name |> should equal "Agricultural machinery, tractors"
    indicator.TopicIds |> should equal ["9"]
    indicator.Source |> should equal "World Development Indicators"
    indicator.Description |> should equal "Agricultural machinery refers to the number of wheel and crawler tractors."

[<Test>]
let ``CountryRecord should be constructed with all required fields`` () =
    let country = { 
        Id = "USA"
        Name = "United States"
        CapitalCity = "Washington"
        Region = "North America"
    }
    
    country.Id |> should equal "USA"
    country.Name |> should equal "United States" 
    country.CapitalCity |> should equal "Washington"
    country.Region |> should equal "North America"
    country.IsRegion |> should be False

[<Test>]
let ``TopicRecord should be constructed with all required fields`` () =
    let topic = { 
        Id = "1"
        Name = "Agriculture & Rural Development"
        Description = "For the 70 percent of the world's poor who live in rural areas, agriculture is the main source of income and employment."
    }
    
    topic.Id |> should equal "1"
    topic.Name |> should equal "Agriculture & Rural Development"
    topic.Description |> should equal "For the 70 percent of the world's poor who live in rural areas, agriculture is the main source of income and employment."

[<Test>]
let ``WorldBankData should construct with sources`` () =
    let data = WorldBankData("http://api.worldbank.org", "World Development Indicators")
    data |> should not' (be Null)

[<Test>]
let ``WorldBankData should construct with multiple sources`` () =
    let data = WorldBankData("http://api.worldbank.org", "World Development Indicators;Global Financial Development")
    data |> should not' (be Null)

[<Test>]
let ``WorldBankData should handle empty sources`` () =
    let data = WorldBankData("http://api.worldbank.org", "")
    data |> should not' (be Null)

// Test Http.AppendQueryToUrl functionality used by WorldBank
[<Test>]
let ``Http.AppendQueryToUrl should construct proper URL with query parameters`` () =
    let baseUrl = "http://api.worldbank.org/indicator"
    let query = [("per_page", "1000"); ("format", "json")]
    let result = Http.AppendQueryToUrl(baseUrl, query)
    result |> should equal "http://api.worldbank.org/indicator?per_page=1000&format=json"

[<Test>]
let ``Http.AppendQueryToUrl should handle special characters`` () =
    let baseUrl = "http://api.worldbank.org/country"
    let query = [("region", "Latin America & Caribbean")]
    let result = Http.AppendQueryToUrl(baseUrl, query)
    result |> should contain "region=Latin%20America%20%26%20Caribbean"
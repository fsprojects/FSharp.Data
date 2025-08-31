module FSharp.Data.Tests.Caching

open NUnit.Framework
open FsUnit
open System
open System.Threading
open System.IO
open FSharp.Data.Runtime.Caching

[<Test>]
let ``InMemoryCache Set and TryRetrieve basic functionality``() =
    let expiration = TimeSpan.FromHours(1.0)
    let cache = createInMemoryCache expiration
    
    // Test Set and TryRetrieve
    cache.Set("key1", "value1")
    cache.TryRetrieve("key1") |> should equal (Some "value1")
    
    // Test non-existent key
    cache.TryRetrieve("nonexistent") |> should equal None

[<Test>]
let ``InMemoryCache with different data types``() =
    let expiration = TimeSpan.FromHours(1.0)
    let intCache = createInMemoryCache expiration
    let stringCache = createInMemoryCache expiration
    
    // Test with integers
    intCache.Set(42, 100)
    intCache.TryRetrieve(42) |> should equal (Some 100)
    
    // Test with strings
    stringCache.Set("testkey", "testvalue")
    stringCache.TryRetrieve("testkey") |> should equal (Some "testvalue")

[<Test>]
let ``InMemoryCache expiration behavior``() =
    let shortExpiration = TimeSpan.FromMilliseconds(50.0)
    let cache = createInMemoryCache shortExpiration
    
    cache.Set("expiring_key", "expiring_value")
    cache.TryRetrieve("expiring_key") |> should equal (Some "expiring_value")
    
    // Wait for expiration
    Thread.Sleep(60)
    
    cache.TryRetrieve("expiring_key") |> should equal None

[<Test>]
let ``InMemoryCache extend cache expiration``() =
    let shortExpiration = TimeSpan.FromMilliseconds(100.0)
    let cache = createInMemoryCache shortExpiration
    
    cache.Set("extend_key", "extend_value")
    
    // Wait some time but extend the expiration
    Thread.Sleep(30)
    cache.TryRetrieve("extend_key", extendCacheExpiration = true) |> should equal (Some "extend_value")
    
    // Wait some more time - should still be available due to extension
    Thread.Sleep(30)
    cache.TryRetrieve("extend_key") |> should equal (Some "extend_value")

[<Test>]
let ``InMemoryCache Remove functionality``() =
    let expiration = TimeSpan.FromHours(1.0)
    let cache = createInMemoryCache expiration
    
    cache.Set("remove_key", "remove_value")
    cache.TryRetrieve("remove_key") |> should equal (Some "remove_value")
    
    cache.Remove("remove_key")
    cache.TryRetrieve("remove_key") |> should equal None
    
    // Removing non-existent key should not cause error
    cache.Remove("nonexistent_key")

[<Test>]
let ``InMemoryCache multiple keys and values``() =
    let expiration = TimeSpan.FromHours(1.0)
    let cache = createInMemoryCache expiration
    
    // Set multiple values
    cache.Set("key1", "value1")
    cache.Set("key2", "value2") 
    cache.Set("key3", "value3")
    
    // Verify all are retrievable
    cache.TryRetrieve("key1") |> should equal (Some "value1")
    cache.TryRetrieve("key2") |> should equal (Some "value2") 
    cache.TryRetrieve("key3") |> should equal (Some "value3")
    
    // Remove one and verify others remain
    cache.Remove("key2")
    cache.TryRetrieve("key1") |> should equal (Some "value1")
    cache.TryRetrieve("key2") |> should equal None
    cache.TryRetrieve("key3") |> should equal (Some "value3")

[<Test>]
let ``InMemoryCache overwrite existing key``() =
    let expiration = TimeSpan.FromHours(1.0)
    let cache = createInMemoryCache expiration
    
    cache.Set("overwrite_key", "original_value")
    cache.TryRetrieve("overwrite_key") |> should equal (Some "original_value")
    
    cache.Set("overwrite_key", "new_value")
    cache.TryRetrieve("overwrite_key") |> should equal (Some "new_value")

[<Test>]
let ``InternetFileCache basic functionality``() =
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let expiration = TimeSpan.FromHours(1.0)
    
    let cache = createInternetFileCache uniquePrefix expiration
    
    // Test Set and TryRetrieve
    cache.Set("file_key1", "file_value1")
    cache.TryRetrieve("file_key1") |> should equal (Some "file_value1")
    
    // Test non-existent key
    cache.TryRetrieve("nonexistent_file") |> should equal None

[<Test>]
let ``InternetFileCache persistence across instances``() =
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let expiration = TimeSpan.FromHours(1.0)
    
    // First cache instance
    let cache1 = createInternetFileCache uniquePrefix expiration
    cache1.Set("persistent_key", "persistent_value")
    
    // Second cache instance with same prefix
    let cache2 = createInternetFileCache uniquePrefix expiration
    cache2.TryRetrieve("persistent_key") |> should equal (Some "persistent_value")

[<Test>]
let ``InternetFileCache expiration behavior``() =
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let shortExpiration = TimeSpan.FromMilliseconds(50.0)
    
    let cache = createInternetFileCache uniquePrefix shortExpiration
    cache.Set("expiring_file_key", "expiring_file_value")
    cache.TryRetrieve("expiring_file_key") |> should equal (Some "expiring_file_value")
    
    // Wait for expiration
    Thread.Sleep(60)
    
    cache.TryRetrieve("expiring_file_key") |> should equal None

[<Test>]
let ``InternetFileCache Remove functionality``() =
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let expiration = TimeSpan.FromHours(1.0)
    
    let cache = createInternetFileCache uniquePrefix expiration
    cache.Set("file_remove_key", "file_remove_value")
    cache.TryRetrieve("file_remove_key") |> should equal (Some "file_remove_value")
    
    cache.Remove("file_remove_key")
    cache.TryRetrieve("file_remove_key") |> should equal None

[<Test>]
let ``InternetFileCache with special characters in keys``() =
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let expiration = TimeSpan.FromHours(1.0)
    
    let cache = createInternetFileCache uniquePrefix expiration
    
    // Test keys with special characters that need hashing
    let specialKey = "http://example.com/api?param=value&other=123"
    cache.Set(specialKey, "special_value")
    cache.TryRetrieve(specialKey) |> should equal (Some "special_value")

[<Test>]
let ``InternetFileCache with large values``() =
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let expiration = TimeSpan.FromHours(1.0)
    
    let cache = createInternetFileCache uniquePrefix expiration
    
    // Test with large string value
    let largeValue = String.replicate 10000 "This is a large test value. "
    cache.Set("large_key", largeValue)
    cache.TryRetrieve("large_key") |> should equal (Some largeValue)

[<Test>]
let ``InternetFileCache extendCacheExpiration throws not implemented``() =
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let expiration = TimeSpan.FromHours(1.0)
    
    let cache = createInternetFileCache uniquePrefix expiration
    cache.Set("extend_test", "value")
    
    // Should throw "Not implemented" exception
    Assert.Throws<System.Exception>(fun () -> cache.TryRetrieve("extend_test", extendCacheExpiration = true) |> ignore) |> ignore

[<Test>]
let ``InternetFileCache handles empty and whitespace values``() =
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let expiration = TimeSpan.FromHours(1.0)
    
    let cache = createInternetFileCache uniquePrefix expiration
    
    // Test with empty string - should return None due to isWellFormedResult check
    cache.Set("empty_key", "")
    cache.TryRetrieve("empty_key") |> should equal None
    
    // Test with whitespace - should be accepted
    cache.Set("whitespace_key", "   ")
    cache.TryRetrieve("whitespace_key") |> should equal (Some "   ")

[<Test>]
let ``InternetFileCache multiple keys with same prefix``() =
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let expiration = TimeSpan.FromHours(1.0)
    
    let cache = createInternetFileCache uniquePrefix expiration
    
    // Set multiple values
    cache.Set("file_key1", "file_value1")
    cache.Set("file_key2", "file_value2")
    cache.Set("file_key3", "file_value3")
    
    // Verify all are retrievable
    cache.TryRetrieve("file_key1") |> should equal (Some "file_value1")
    cache.TryRetrieve("file_key2") |> should equal (Some "file_value2")
    cache.TryRetrieve("file_key3") |> should equal (Some "file_value3")

[<Test>]
let ``ICache interface consistency between implementations``() =
    let expiration = TimeSpan.FromMinutes(10.0)
    let memoryCache = createInMemoryCache expiration
    let uniquePrefix = Guid.NewGuid().ToString("N")[..8]
    let fileCache = createInternetFileCache uniquePrefix expiration
    
    // Test both implementations with same operations
    let testOperations (cache: ICache<string, string>) suffix =
        let key = "consistency_test_" + suffix
        let value = "test_value_" + suffix
        
        cache.Set(key, value)
        cache.TryRetrieve(key) |> should equal (Some value)
        
        cache.Remove(key)
        cache.TryRetrieve(key) |> should equal None
    
    testOperations memoryCache "memory"
    testOperations fileCache "file"
/// Implements caching using in-memory and local file system
module internal FSharp.Data.Runtime.Caching

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open FSharp.Data.Runtime.IO

type ICache<'TKey, 'TValue> =
    abstract Set: key: 'TKey * value: 'TValue -> unit
    abstract TryRetrieve: key: 'TKey * ?extendCacheExpiration: bool -> 'TValue option
    abstract Remove: key: 'TKey -> unit

/// Creates a cache that uses in-memory collection
let createInMemoryCache (expiration: TimeSpan) =
    let dict = ConcurrentDictionary<'TKey_, 'TValue * DateTime>()

    let rec invalidationFunction key =
        async {
            // expirations over ~24.8 days overflow Int32 milliseconds; clamp and loop
            // (the timestamp is re-checked after waking, so a short sleep just re-arms)
            do! Async.Sleep(int (min expiration.TotalMilliseconds (float Int32.MaxValue)))

            match dict.TryGetValue(key) with
            | true, ((_, timestamp) as entry) ->
                if DateTime.UtcNow - timestamp >= expiration then
                    // conditional removal so a concurrent Set with a fresh timestamp
                    // is not evicted between the check and the remove
                    if (dict :> ICollection<KeyValuePair<_, _>>).Remove(KeyValuePair(key, entry)) then
                        log (sprintf "Cache expired: %O" key)
                else
                    do! invalidationFunction key
            | _ -> ()
        }

    { new ICache<_, _> with
        member _.Set(key, value) =
            dict.[key] <- (value, DateTime.UtcNow)
            invalidationFunction key |> Async.Start

        member x.TryRetrieve(key, ?extendCacheExpiration) =
            match dict.TryGetValue(key) with
            | true, (value, timestamp) when DateTime.UtcNow - timestamp < expiration ->
                if extendCacheExpiration = Some true then
                    dict.[key] <- (value, DateTime.UtcNow)

                Some value
            | _ -> None

        member _.Remove(key) =
            match dict.TryRemove(key) with
            | true, _ -> log (sprintf "Explicitly removed from cache: %O" key)
            | _ -> () }

/// Get hash code of a string - used to determine cache file
let private hashString (plainText: string) =
    let plainTextBytes = Encoding.UTF8.GetBytes(plainText)

    let hashBytes =
#if NET5_0_OR_GREATER
        SHA1.HashData(plainTextBytes)
#else
        use sha1 = SHA1.Create()
        sha1.ComputeHash(plainTextBytes)
#endif

    let s = Convert.ToBase64String(hashBytes)
    s.Replace("ab", "abab").Replace("\\", "ab")

/// Creates a cache that stores data in a local file system
let createInternetFileCache prefix expiration =

    // %UserProfile%\AppData\Local\Microsoft\Windows\INetCache
    let cacheFolder =
        if Environment.OSVersion.Platform = PlatformID.Unix then
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            + "/.cache/fsharp-data"
        else
            Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)

    let downloadCache = Path.Combine(cacheFolder, prefix)

    // Get file name for a given string (using hash)
    let cacheFile key =
        let sha1 = hashString key
        let encoded = Uri.EscapeDataString sha1
        Path.Combine(downloadCache, encoded + ".txt")

    // A simple check for now. This is to guard against a corrupted cache file.
    let isWellFormedResult result = not (String.IsNullOrEmpty result)

    try
        // Try to create directory, if it does not exist
        if not (Directory.Exists downloadCache) then
            Directory.CreateDirectory downloadCache |> ignore

        let cache =
            { new ICache<string, string> with
                member _.Set(key, value) =
                    let cacheFile = cacheFile key

                    // write to a temp file and move it into place so that concurrent readers
                    // (other threads or processes sharing the cache folder) never observe a
                    // partially written cache entry
                    let tempFile = cacheFile + "." + Guid.NewGuid().ToString("N") + ".tmp"

                    try
                        try
                            File.WriteAllText(tempFile, value)

                            if File.Exists cacheFile then
                                File.Delete cacheFile

                            File.Move(tempFile, cacheFile)
                        with e ->
                            Debug.WriteLine(
                                "Caching: Failed to write file {0} with an exception: {1}",
                                cacheFile,
                                e.Message
                            )
                    finally
                        if File.Exists tempFile then
                            try
                                File.Delete tempFile
                            with _ ->
                                ()

                member _.TryRetrieve(key, ?extendCacheExpiration) =
                    if extendCacheExpiration = Some true then
                        failwith "Not implemented"

                    let cacheFile = cacheFile key

                    try
                        if
                            File.Exists cacheFile
                            && DateTime.UtcNow - File.GetLastWriteTimeUtc cacheFile < expiration
                        then
                            let result = File.ReadAllText cacheFile
                            if isWellFormedResult result then Some result else None
                        else
                            None
                    with e ->
                        Debug.WriteLine("Caching: Failed to read file {0} with an exception: {1}", cacheFile, e.Message)
                        None

                member _.Remove(key) =
                    let cacheFile = cacheFile key

                    try
                        File.Delete(cacheFile)
                    with e ->
                        Debug.WriteLine(
                            "Caching: Failed to delete file {0} with an exception: {1}",
                            cacheFile,
                            e.Message
                        ) }

        // Ensure that we can access the file system by writing a sample value to the cache
        cache.Set("$$$test$$$", "dummyValue")

        match cache.TryRetrieve("$$$test$$$") with
        | Some "dummyValue" ->
            cache.Remove("$$$test$$$") |> ignore
            cache
        | _ ->
            // fallback to an in memory cache
            createInMemoryCache expiration
    with e ->
        Debug.WriteLine("Caching: Fall back to memory cache, because of an exception: {0}", e.Message)
        // fallback to an in memory cache
        createInMemoryCache expiration

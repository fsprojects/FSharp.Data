/// Implements caching using in-memory and local file system 
module FSharp.Data.Runtime.Caching

open System

/// Represents a cache (various implementations are available)
type ICache<'T> = 
  abstract TryRetrieve : string -> 'T option
  abstract Set : string * 'T -> unit

/// Creates a fake cache
let createNonCachingCache() = 
  { new ICache<'T> with
      member __.Set(_, _) = ()
      member __.TryRetrieve(_) = None }

#if FX_NO_CONCURRENT

open System.Collections.Generic

/// Creates a cache that uses in-memory collection
let createInMemoryCache expiration = 
  let dict = new Dictionary<_, _>()
  { new ICache<_> with
      member __.Set(key, value) = 
        lock dict <| fun () -> dict.[key] <- (value, DateTime.UtcNow)
      member __.TryRetrieve(key) =
        lock dict <| fun () ->
          match dict.TryGetValue(key) with
          | true, (value, timestamp) when timestamp - DateTime.UtcNow < expiration -> Some value
          | _ -> None }

#else

open System.Collections.Concurrent

/// Creates a cache that uses in-memory collection
let createInMemoryCache expiration = 
  let dict = new ConcurrentDictionary<_, _>()
  { new ICache<_> with
      member __.Set(key, value) = 
        dict.[key] <- (value, DateTime.UtcNow)
      member __.TryRetrieve(key) =
        match dict.TryGetValue(key) with
        | true, (value, timestamp) when timestamp - DateTime.UtcNow < expiration -> Some value
        | _ -> None }

#endif

#if FX_NO_LOCAL_FILESYSTEM

let createInternetFileCache (_prefix:string) expiration = createInMemoryCache expiration, null

#else

open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text

/// Get hash code of a string - used to determine cache file
let private hashString (plainText:string) = 
  let plainTextBytes = Encoding.UTF8.GetBytes(plainText)
  let hash = new SHA1Managed()
  let hashBytes = hash.ComputeHash(plainTextBytes)        
  let s = Convert.ToBase64String(hashBytes)
  s.Replace("ab","abab").Replace("\\","ab")

/// Creates a cache that stores data in a local file system
let createInternetFileCache prefix expiration =

  // %UserProfile%\AppData\Local\Microsoft\Windows\INetCache
  let cacheFolder =
    if Environment.OSVersion.Platform = PlatformID.Unix
    then Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.cache/fsharp-data"
    else Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)

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
      { new ICache<string> with 
          member x.TryRetrieve(key) = 
            let cacheFile = cacheFile key
            try
              if File.Exists cacheFile && File.GetLastWriteTimeUtc cacheFile - DateTime.UtcNow < expiration then
                let result = File.ReadAllText cacheFile
                if isWellFormedResult result
                then Some result
                else None
              else None
            with e -> 
              Debug.WriteLine("Caching: Failed to read file {0} with an exception: {1}", cacheFile, e.Message)
              None
                
          member x.Set(key,value) = 
            let cacheFile = cacheFile key
            try File.WriteAllText(cacheFile,value)
            with e ->
              Debug.WriteLine("Caching: Failed to write file {0} with an exception: {1}", cacheFile, e.Message) }
    
    // Ensure that we can access the file system by writing sample thing to a cache
    cache.Set("$$$test$$$", "empty")
    if cache.TryRetrieve("$$$test$$$") <> Some "empty" then 
        createInMemoryCache expiration, null
    else 
        cache, downloadCache 
  with e -> 
    Debug.WriteLine("Caching: Fall back to memory cache, because of an exception: {0}", e.Message)
    createInMemoryCache expiration, null

#endif
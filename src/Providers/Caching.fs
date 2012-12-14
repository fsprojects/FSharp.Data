// --------------------------------------------------------------------------------------
// Implements caching using in-memory and local file system 
// --------------------------------------------------------------------------------------

module ProviderImplementation.Cache

open System
open System.IO
open System.Net
open System.Text
open System.Reflection
open System.Diagnostics
open System.Collections.Concurrent
open System.Security.Cryptography
        
// --------------------------------------------------------------------------------------

/// Represents a cache (various implementations are available)
type ICache<'T> = 
  abstract TryRetrieve : string -> 'T option
  abstract Set : string * 'T -> unit

/// Creates a cache that uses in-memory collection
let memoryCache () = 
  let dict = new ConcurrentDictionary<_, _>()
  { new ICache<_> with
      member x.Set(k, v) = 
        dict.[k] <- v
      member x.TryRetrieve(k) =
        match dict.TryGetValue(k) with
        | true, v -> Some v
        | _ -> None }

/// Get hash code of a string - used to determine cache file
let private hashString (plainText:string) = 
  let plainTextBytes = Encoding.UTF8.GetBytes(plainText)
  let hash = new SHA1Managed()
  let hashBytes = hash.ComputeHash(plainTextBytes)        
  let s = Convert.ToBase64String(hashBytes)
  s.Replace("ab","abab").Replace("\\","ab")
        
/// Creates a cache that stores data in a local file system
let createInternetFileCache prefix expiration =
  let cacheFolder = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)
  let downloadCache = Path.Combine(cacheFolder, prefix)

  // Get file name for a given string (using hash)
  let cacheFile key = 
    let sha1 = hashString key 
    let encoded = System.Uri.EscapeDataString sha1
    Path.Combine(downloadCache, encoded + ".txt")

  try
    // Try to create directory, if it does not exist
    if not(Directory.Exists(downloadCache)) then
      Directory.CreateDirectory(downloadCache) |> ignore

    let cache = 
      { new ICache<string> with 
          member x.TryRetrieve(key) = 
            let cacheFile = cacheFile key
            try
              if File.Exists(cacheFile) then
                Some(File.ReadAllText(cacheFile))
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
    if cache.TryRetrieve("$$$test$$$") <> Some "empty" then memoryCache()
    else cache 
  with e -> 
    Debug.WriteLine("Caching: Fall back to memory cache, because of an exception: {0}", e.Message)
    memoryCache()


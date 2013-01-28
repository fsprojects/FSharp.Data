// --------------------------------------------------------------------------------------
// Implements caching using in-memory and local file system 
// --------------------------------------------------------------------------------------

module internal FSharp.Data.RuntimeImplementation.Caching

/// Represents a cache (various implementations are available)
type ICache<'T> = 
  abstract TryRetrieve : string -> 'T option
  abstract Set : string * 'T -> unit

let createNonCachingCache() = 
  { new ICache<'T> with
      member __.Set(_, _) = ()
      member __.TryRetrieve(_) = None }   

#if FX_NO_CONCURRENT

open System.Collections.Generic

/// Creates a cache that uses in-memory collection
let createInMemoryCache () = 
  let dict = new Dictionary<_, _>()
  { new ICache<_> with
      member __.Set(k, v) = 
        lock dict (fun () -> dict.[k] <- v)
      member __.TryRetrieve(k) =
        lock dict (fun () ->
          match dict.TryGetValue(k) with
          | true, v -> Some v
          | _ -> None) }

#else

open System.Collections.Concurrent

/// Creates a cache that uses in-memory collection
let createInMemoryCache () = 
  let dict = new ConcurrentDictionary<_, _>()
  { new ICache<_> with
      member __.Set(k, v) = 
        dict.[k] <- v
      member __.TryRetrieve(k) =
        match dict.TryGetValue(k) with
        | true, v -> Some v
        | _ -> None }

#endif

#if FX_NO_LOCAL_FILESYSTEM

let createInternetFileCache _ = createInMemoryCache(), null

#else

open System
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
let createInternetFileCache prefix =

  // e.g. C:\Users\<user>\AppData\Local\Microsoft\Windows\Temporary Internet Files
  let cacheFolder = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)
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
              if File.Exists cacheFile then
                let result = File.ReadAllText cacheFile
                if isWellFormedResult result then
                    Some result
                else
                    None
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
        createInMemoryCache(), null
    else 
        cache, downloadCache 
  with e -> 
    Debug.WriteLine("Caching: Fall back to memory cache, because of an exception: {0}", e.Message)
    createInMemoryCache(), null

#endif
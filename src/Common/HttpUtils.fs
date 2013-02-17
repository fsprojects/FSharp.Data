// --------------------------------------------------------------------------------------
// Implementation of HTTP utils missing in the portable profile (encode JS string)
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Text

// We cannot use HttpUtility from System.Web on a portable version,
// so we reimplement the encoding of JavaScript strings...
module internal HttpUtility = 

  // Encode characters that are not valid in JS string. The implementation is based
  // on https://github.com/mono/mono/blob/master/mcs/class/System.Web/System.Web/HttpUtility.cs
  // (but we use just a single iteration and create StringBuilder as needed)
  let JavaScriptStringEncode (value : string) = 
    if String.IsNullOrEmpty value then "" else 
      let chars = value.ToCharArray() 
      
      // We only create StringBuilder when we find a character that 
      // we actually need to encode (and then we copy all skipped chars)
      let sb = ref null 
      let inline ensureBuilder i = 
        if !sb = null then 
          sb := new StringBuilder(value.Length + 5)
          (!sb).Append(value.Substring(0, i))
        else !sb

      // Iterate over characters and encode 
      for i in 0 .. chars.Length - 1 do 
        let c = int (chars.[i])
        if c >= 0 && c <= 7 || c = 11 || c >= 14 && c <= 31 || c = 38 || c = 39 || c = 60 || c = 62 then
          (ensureBuilder i).AppendFormat("\\u{0:x4}", c) |> ignore
        else 
          match c with
          | 8 -> (ensureBuilder i).Append "\\b"|> ignore
          | 9 -> (ensureBuilder i).Append "\\t"|> ignore
          | 10 -> (ensureBuilder i).Append "\\n"|> ignore
          | 12 -> (ensureBuilder i).Append "\\f"|> ignore
          | 13 -> (ensureBuilder i).Append "\\r"|> ignore
          | 34 -> (ensureBuilder i).Append "\\\""|> ignore
          | 92 -> (ensureBuilder i).Append "\\\\" |> ignore
          | _ -> if !sb <> null then (!sb).Append(char c) |> ignore
      if !sb = null then value else
        (ensureBuilder chars.Length).ToString()
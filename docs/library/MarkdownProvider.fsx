(**
---
category: Type Providers
categoryindex: 1
index: 8
---
*)
(*** condition: prepare ***)
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Http.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Json.Core.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.dll"
(*** condition: fsx ***)
#if FSX
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"
#endif
(*** condition: ipynb ***)
#if IPYNB
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"

Formatter.SetPreferredMimeTypesFor(typeof<obj>, "text/plain")
Formatter.Register(fun (x: obj) (writer: TextWriter) -> fprintfn writer "%120A" x)
#endif
(**
# Markdown Type Provider

The `MarkdownProvider` gives you statically typed access to [YAML front matter](https://jekyllrb.com/docs/front-matter/)
in Markdown files. It infers the types of front matter fields from a sample file and exposes them as
typed properties. The body of the document (everything after the front matter delimiter `---`) is
always available as a `Body: string` property.

This is particularly useful for static site generators, documentation tools, blog engines, and any
application that stores metadata in Markdown files (e.g. Hugo, Jekyll, Eleventy).

## Basic Usage

Given a Markdown file `post.md`:

```markdown
---
title: Hello World
date: 2024-01-15
author: Jane Doe
tags: [fsharp, data, markdown]
draft: false
views: 1234
---

# Hello World

This is the body of the post.
```

You can write:
*)

open FSharp.Data

type Post = MarkdownProvider<"../tests/FSharp.Data.Tests/Data/BlogPost.md">

let post = Post.GetSample()

(** The front matter fields are strongly typed: *)

let title = post.Title    // string: "Hello World"
let date = post.Date      // System.DateTime: 2024-01-15
let author = post.Author  // string: "Jane Doe"
let tags = post.Tags      // string[]: [| "fsharp"; "data"; "markdown" |]
let draft = post.Draft    // bool: false
let views = post.Views    // int: 1234

(** The body is always accessible as a `string` property: *)

let body = post.Body      // string: the markdown content after ---

(**
## Loading Multiple Files

Use `Load` to load any file matching the same schema as the sample:
*)

// let post2 = Post.Load("another-post.md")

(**
## Inline Samples

You can use an inline Markdown string as the sample:
*)

type InlineDoc =
    MarkdownProvider<"""---
title: My Title
count: 42
enabled: true
---
Body here.""">

let doc = InlineDoc.Parse("""---
title: Hello
count: 100
enabled: false
---
Different body.""")

// doc.Title    // "Hello"
// doc.Count   // 100
// doc.Enabled // false
// doc.Body    // "Different body."

(**
## Supported Front Matter Types

The `MarkdownProvider` infers types using the same rules as `JsonProvider`:

| YAML value | F# type |
|---|---|
| `title: Hello` | `string` |
| `count: 42` | `int` |
| `rating: 4.5` | `decimal` |
| `enabled: true` | `bool` |
| `date: 2024-01-15` | `System.DateTime` |
| `tags: [a, b, c]` | `string[]` |
| `weight: null` | `string option` |

## Static Parameters

| Parameter | Default | Description |
|---|---|---|
| `Sample` | `""` | Path to a sample `.md` file or an inline Markdown string |
| `RootName` | `"Root"` | Name for the generated root type |
| `Culture` | `""` | Culture for parsing dates and numbers |
| `Encoding` | `""` | File encoding (default: UTF-8) |
| `ResolutionFolder` | `""` | Directory for resolving relative file paths at design time |
| `EmbeddedResource` | `""` | Embedded resource name for the sample |
| `InferTypesFromValues` | `true` | Enable type inference from values (e.g. `"123"` → `int`) |
| `UseOriginalNames` | `false` | Use front matter key names as-is (no PascalCase normalisation) |
| `PreferOptionals` | `true` | Use option types for missing/null values |

## Notes

- Only YAML front matter (delimited by `---`) is supported. TOML front matter (`+++`) is not.
- The YAML parser handles strings, numbers, booleans, null, inline arrays (`[a, b]`), and block arrays. Nested objects are not currently supported.
- The `Body` property returns the raw Markdown text after the closing `---` delimiter (including any leading newline). Use `.Trim()` if needed.
- `With*` methods are generated for each property, enabling non-destructive updates.
*)

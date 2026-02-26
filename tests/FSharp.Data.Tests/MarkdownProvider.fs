module FSharp.Data.Tests.MarkdownProvider

open NUnit.Framework
open FsUnit
open System
open FSharp.Data
open FSharp.Data.Runtime.BaseTypes

type BlogPost = MarkdownProvider<"Data/BlogPost.md">

[<Test>]
let ``Can parse markdown front matter`` () =
    let post = BlogPost.GetSample()
    post.Title |> should equal "Hello World"
    post.Author |> should equal "Jane Doe"
    post.Draft |> should equal false
    post.Views |> should equal 1234
    post.Rating |> should equal 4.5m

[<Test>]
let ``Can parse front matter date`` () =
    let post = BlogPost.GetSample()
    post.Date |> should equal (DateTime(2024, 1, 15))

[<Test>]
let ``Can parse front matter tags array`` () =
    let post = BlogPost.GetSample()
    post.Tags |> should equal [| "fsharp"; "data"; "markdown" |]

[<Test>]
let ``Body contains markdown content after front matter`` () =
    let post = BlogPost.GetSample()
    post.Body |> should contain "Hello World"
    post.Body |> should contain "This is the body"
    post.Body |> should not' (contain "---")

[<Test>]
let ``Can parse inline markdown`` () =
    let post =
        BlogPost.Parse(
            """---
title: Test Post
date: 2024-06-01
---
# Test

Some content."""
        )

    post.Title |> should equal "Test Post"
    post.Body |> should contain "Some content"

[<Test>]
let ``Can load markdown from string with Parse`` () =
    let post =
        BlogPost.Parse(
            """---
title: Loaded Post
date: 2024-06-15
author: Bob
draft: true
views: 42
rating: 3.7
tags: [test]
---
Body text here."""
        )

    post.Title |> should equal "Loaded Post"
    post.Author |> should equal "Bob"
    post.Draft |> should equal true
    post.Views |> should equal 42
    post.Body.Trim() |> should equal "Body text here."

# Release Notes

## 6.6.0 - Unreleased

- Convert build.fsx script into a full build project at build/build.fsproj, letting developers use the .NET 6 runtime assemblies to build the project

## 6.5.0 - Mar 11 2025

- Add JSON Schema support to the JSON Type Provider
- Add JSON validation against JSON Schema
- Add documentation for working with JSON Schema

## 6.4.1 - Oct 2 2024

- WorldBank URL fixed to https @pkese
- Updated Fake and Paket @Thorium
- Tests project to .NET8 @Thorium

## 6.4.0 - Mar 12 2024

- Update FSharp.Core to 6.0.1 by @Thorium
- Fix for a bug where FSharp.Data.DesignTime stays on la ock that prevents closing VS 2022 by @Thorium

## 6.3.0 - Apr 30 2023

- Add support for plain control types by @kant2002 in <https://github.com/fsprojects/FSharp.Data/pull/1487>

## 6.2.0 - Apr 30 2023

- It's a new world, major versions abound
- New option for JSON serialization (#1482) by @bonjune in <https://github.com/fsprojects/FSharp.Data/pull/1485>
- Adhere to RFC 4180 standard by @bonjune in <https://github.com/fsprojects/FSharp.Data/pull/1484>
- Csv Column Inference ignores newline character by @bonjune in <https://github.com/fsprojects/FSharp.Data/pull/1483>

## 6.1.1-beta - Apr 12 2023

- Publish FSharp.Data.RuntimeUtilities
- Update annoying assemblyinfo stuff because fake
- Roll forward the SDK so that you can use project in codespaces

## 6.1.0-beta - Apr 4 2023

- Fix json provider PreferDictionaries for array properties | [Melvyn Laïly](https://github.com/)
- Add signature tests showing the wrong generation for array values when inferring dictionaries in json | [Melvyn Laïly](https://github.com/)
- Enable TLS 1.2 in providers to be able to use it when requesting https samples | [Melvyn Laïly](https://github.com/)
- Report complete exception from providers | [Melvyn Laïly](https://github.com/)
- Add aria-label to the list of attributes used to find a name for html provided types | [Melvyn Laïly](https://github.com/)
- Do not fail the build if the WorldBank api is offline | [Melvyn Laïly](https://github.com/)
- Move source files into the folders of the projects they belong to | [Melvyn Laïly](https://github.com/)
- Move non-http code from FSharp.Data.Http into a new FSharp.Data.Runtime.Utilities project | [Melvyn Laïly](https://github.com/)
- Format code | [Lennart Schlegge](https://github.com/)
- Refactor readLine function | [Lennart Schlegge](https://github.com/)
- Extend test with quoted strings and separators | [Lennart Schlegge](https://github.com/)
- Fix reading CSV from non seekable network stream | [Lennart Schlegge](https://github.com/)
- Use nameof for all dynamically built quotations | [Melvyn Laïly](https://github.com/)
- Update release notes | [Melvyn Laïly](https://github.com/)
- Change the namespace of the public "InferenceMode" enum | [Melvyn Laïly](https://github.com/)
- updated docs for html type provider example | [aaron-jack-manning](https://github.com/)
- Fix WorldBank.fsx docs build | [Melvyn Laïly](https://github.com/)
- Re-add solution level items missing or with broken paths | [Melvyn Laïly](https://github.com/)
- Remove explicit PackageReference in projects + include paket.references | [Melvyn Laïly](https://github.com/)

### 6.0.1-beta002 - Jan 6 2023

- Change the namespace of the `InferenceMode` enum from `FSharp.Data.Runtime.StructuralInference` to `FSharp.Data`.
- Move common runtime utilities out of `FSharp.Data.Http` and into a new `FSharp.Data.Runtime.Utilities` assembly.
- Add `aria-label` to the list of html attributes used to infer names of types provided by the HtmlProvider.
- Enable TLS 1.2 when requesting http(s) samples from the type providers.
- Fix generated code of the json provider with `PreferDictionaries` when values are arrays.

### 6.0.1-beta001 - Aug 18 2022

- There are now multiple packages
  - FSharp.Data -- includes everything
    - FSharp.Data.Http -- http types/helpers
    - FSharp.Data.Csv.Core -- csv types/helpers
    - FSharp.Data.Json.Core -- json types/helpers
    - FSharp.Data.Html.Core -- html types/helpers
    - FSharp.Data.Xml.Core -- xml types/helpers

### 5.0.2 - Aug 17 2022

- Identical to 4.2.10

### 5.0.1 - Aug 16 2022

- Reverted

### 4.2.10 - Aug 12 2022

- Implement "inline schemas": ability to add type hints into the type providers' source documents by @mlaily in <https://github.com/fsprojects/FSharp.Data/pull/1447>

### 4.2.9 - Jun 26 2022

- Typo fix, the parameter is plural ("Separators") by @nhirschey in <https://github.com/fsprojects/FSharp.Data/pull/1433>
- Typo fix, the parameter is plural ("Separators"). by @kleink in <https://github.com/fsprojects/FSharp.Data/pull/1438>
- Remove restrictions for getting innerText by @LiteracyFanatic in <https://github.com/fsprojects/FSharp.Data/pull/1435>
- pre-clean some code before applying fantomas by @dsyme in <https://github.com/fsprojects/FSharp.Data/pull/1442>
- update to .net 6 by @dsyme in <https://github.com/fsprojects/FSharp.Data/pull/1443>
- Format all code by @nojaf in <https://github.com/fsprojects/FSharp.Data/pull/1441>
- formatting: wider lists by @dsyme in <https://github.com/fsprojects/FSharp.Data/pull/1444>
- fix docs by @dsyme in <https://github.com/fsprojects/FSharp.Data/pull/1446>
- add GithubActionsTestLogger and use it in CI builds by @baronfel in <https://github.com/fsprojects/FSharp.Data/pull/1449>
- Skip json comments by @mlaily in <https://github.com/fsprojects/FSharp.Data/pull/1448>
- Some medium refactoring and little fixes by @mlaily in <https://github.com/fsprojects/FSharp.Data/pull/1450>
- Fix http tests by @mlaily in <https://github.com/fsprojects/FSharp.Data/pull/1451>

### 4.2.8 - Feb 28 2022

- [Add `PreferDictionaries` flag to JSONProvider](https://github.com/fsprojects/FSharp.Data/pull/1430)
- [Add `JsonProvider.Load(value: JsonValue)`](https://github.com/fsprojects/FSharp.Data/pull/1424)

### 4.2.7 - Jan 1 2022

- [remove extra newline before endBoundaryStream](https://github.com/fsprojects/FSharp.Data/pull/1421)

### 4.2.6 - Nov 29 2021

- Update TPSDK dependencies
- [HtmlState: restore FormattedMode if still in pre after char ref](https://github.com/fsprojects/FSharp.Data/pull/1414)
- [HtmlNode.ToString(): fix self-closing tags on empty non-void elements](https://github.com/fsprojects/FSharp.Data/pull/1413)
- [non seekable multipart form files](https://github.com/fsprojects/FSharp.Data/pull/1415)

### 4.2.5 - Nov 11 2021

- [Fix for multi-part data](https://github.com/fsprojects/FSharp.Data/pull/1397)

### 4.2.4 - Oct 13 2021

- [Fix a bug where HTML parsing could run indefinitely out with incomplete XML tags](https://github.com/fsprojects/FSharp.Data/pull/1396)

### 4.2.3 - Sep 15 2021

- [Fix a bug where HTML parsing could run indefinitely](https://github.com/fsprojects/FSharp.Data/pull/1393)

#### 4.2.2 - Aug 08 2021

- [Removing snk file from fsproj references](https://github.com/fsprojects/FSharp.Data/pull/1389)

#### 4.2.0 - July 27 2021

- [sign assemblies with strong name key file](https://github.com/fsprojects/FSharp.Data/pull/1386)

#### 4.1.1 - March 27 2021

- [set sourcelink to be a private asset so that it doesn't get added to package dependencies](https://github.com/fsprojects/FSharp.Data/pull/1377)

#### 4.1.0 - March 14 2021

- [Fix internet cache invalidation](https://github.com/fsprojects/FSharp.Data/pull/1365)
- Build against .NET 5

#### 4.0.1 - Unreleased

- Move `cssSelect` to `HtmlNode`.

#### 4.0.0 - March 2 2021

- FSharp.Data is now .NET Standard 2.0 only

Several other fixes:

- Fix [CssSelect fails when there is no html tag](https://github.com/fsprojects/FSharp.Data/pull/1290)
- [Expose InnerResponse, Http properties](https://github.com/fsprojects/FSharp.Data/pull/1291)
- [Add DateTimeOffset for Csv Schema Inference](https://github.com/fsprojects/FSharp.Data/pull/1304)
- [Modified escaping of query string parameters](https://github.com/fsprojects/FSharp.Data/pull/1316)
- [Build fix: Remove dependency on http://europa.eu/rapid/conf/RSS20.xsd and update WorldBank to API to V2](https://github.com/fsprojects/FSharp.Data/pull/1320)
- [Remove dead links to tryfsharp.org](https://github.com/fsprojects/FSharp.Data/pull/1328)
- [HTML parsing for attributes not separated by a space](https://github.com/fsprojects/FSharp.Data/pull/1327)
- [Update JsonProvider.fsx](https://github.com/fsprojects/FSharp.Data/pull/1334)
- [Pluralizer - Add "slices" to special cases](https://github.com/fsprojects/FSharp.Data/pull/1335)
- [add ResolutionFolder=ResolutionFolder to docs](https://github.com/fsprojects/FSharp.Data/pull/1344)
- [fix links in docs/tools/generate.fsx](https://github.com/fsprojects/FSharp.Data/pull/1348)
- [Revamp docs generation and move to .NET Standard 2.0 only](https://github.com/fsprojects/FSharp.Data/pull/1350)

#### 3.3.4 - January 7 2020

- Rebuild using "Release" target

#### 3.3.3 - January 7 2020

- Fix nuget package

#### 3.3.2 - September 24 2019

- [Fix StackOverflow exception caused by many ampersand and semicolons](https://github.com/fsharp/FSharp.Data/pull/1281)

#### 3.3.1 - September 24 2019

- Update latest TPSDK, fixing [Build with embedded resource broke](https://github.com/fsharp/FSharp.Data/issues/1255)

#### 3.2.4 - September 16 2019

- Make appevyor build nuget package (though we still releas via '.\build Release')

#### 3.2.1 - September 10 2019

- Fix for using on F# Interactive on .NET Core
- [Add ParseList on JsonProvider](https://github.com/fsharp/FSharp.Data/pull/1272)
- [Handle serialization of odd float values](https://github.com/fsharp/FSharp.Data/pull/1275)
- [Adding HTML active patterns to expose internals of HtmlNode](https://github.com/fsharp/FSharp.Data/pull/1227)

#### 3.1.1 - April 15 2019

- [Further fix for excessive memory usage (hold TP instances weakly in file watcher callbacks)](https://github.com/fsharp/FSharp.Data/pull/1252)
- Require VS2017 or above [(due to TPSDK fix)](https://github.com/fsprojects/FSharp.TypeProviders.SDK/pull/305)

#### 3.0.3 - March 28 2019

- [Fix for excessive memory usage (hold TP instances weakly in file watcher callbacks)](https://github.com/fsharp/FSharp.Data/pull/1252)
- [NameUtils/Pluralizer: Fix unusual singularizations for some common words (choices; releases)](https://github.com/fsharp/FSharp.Data/pull/1226)
- [Reduced cache time to partially address possible excess memory usage](https://github.com/fsharp/FSharp.Data/pull/1254)
- [Update TPSDK and paket](https://github.com/fsharp/FSharp.Data/pull/1250)
- [Fixed undisposed writers in CSV saving](https://github.com/fsharp/FSharp.Data/pull/1248)

#### 3.0.1 - March 21 2019

- Fix for excessive memory usage (incorprating latest TPSDK)

#### 3.0.0 - October 14 2018

- Add GetSchema method in XML type provider.

#### 3.0.0-rc - October 7 2018

- (Breaking Change) Added support for DateTimeOffset to JSON, XML, CSV, and HTML providers.
- (Breaking Change) Added support for TimeSpan to JSON, XML, CSV, and HTML providers.
- Map XSD date to System.DateTime and XSD dateTime to System.DateTimeOffset.
- Fixed large float values being silently converted to int.MinValue when parsing JSON.
- Improved handling of invalid cookies.
- Fixes for #1091 - tidying up regex handling in script tags.

#### 3.0.0-beta4 - July 8 2018

- (Breaking Change) Ignore culture when parsing JSON to better match the JSON spec.
- Fixed handling of empty cookie headers.
- (Breaking Change) Don't silently convert decimals and floats to integers in JsonProvider.
- Improved the performance of the type provider design time components.
- Preserve white-space when parsing XML.
- Recognise media type application/json-rpc as text.
- Fix parsing of escaped charaters in string literals within HTML script tags.
- Added constants for HTTP status codes.
- Added support for schemas (XSD) in the XmlProvider.

#### 3.0.0-beta3 - April 9 2018

- Increased type caches TTL from 10 seconds to 5 minutes.

#### 3.0.0-beta2 - April 09 2018

- Fixed memory leaks inside the the type provider design time components.
- Improved the performance of the type provider design time components.

#### 3.0.0-beta - April 04 2018

- Drop PCL Profile 259, 7, 78 support in favour of netstandard2.0.
- Support [F# RFC FST-1003 loading into .NET Core-based F# tooling](https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1003-loading-type-provider-design-time-components.md).
- Integer values for optional parameter for the `System.Text.Encoding` are only supported when the F# compiler
    is run using .NET Framework. By default, new-style .NET SDK project files run the F# compiler with .NET Core.
    To force the use of an F# compiler running with .NET Framework see [this guide](https://github.com/Microsoft/visualfsharp/issues/3303).

#### 2.4.6 - March 25 2018

- Added `ContentTypeWithEncoding` helper to `HttpRequestHeaders`.
- `JsonValue` will explicitly set content type charset to UTF-8 when making requests.
- Prevent superfluous encoding of URL parameters.

#### 2.4.5 - February 19 2018

- Add an optional parameter for the `System.Text.Encoding` to use when reading data to the CSV, HTML, and Json providers. This parameter is called `encoding` and should be present on all Load and AsyncLoad methods.
- Fix handling of multipart form data payloads whose size exceeded ~80k bytes.

#### 2.4.4 - January 20 2018

- Fix parsing of unquoted HTML attributes containing URLs.
- Fixed HTTP form body url encoding.

#### 2.4.3 - December 03 2017

- Added GetColumnIndex and TryGetColumnIndex to CsvFile.
- Fixed outdated examples in the documentation that no longer worked.
- Fixed parsing of script elements with JavaScript string literals, regular expression literals, or comments, that looked like HTML tags.
- Fixed parsing of cookie values containing the '=' character.

#### 2.4.2 - October 09 2017

- Prioritize dates over decimals in type inference.

#### 2.4.1 - September 30 2017

- Fix regression introduced in 2.4.0 in HTTP stream reading.

#### 2.4.0 - September 24 2017

- Fix css selectors not working outside the body element.
- Add support for Multipart Form Data content in the HTTP implementation.
- Added TryParse to JsonValue.
- Fix parsing of self closing HTML tags.
- FSharp.Core 4.3.0.0 (F# 3.0), .NET 4.0, and PCL profile 47 are no longer supported.

#### 2.3.3 - April 10 2017

- Specify kind on Date header to UTC.
- Support for escaped special characters in CSS selectors.
- Fix crash when saving CSV files with nulls.
- Fix leakage of connections when HTTP requests time out.
- Fixed numbers not being preserved correctly when generating names.
- Fixed DOCTYPE being dropped when saving HTML documents.
- Added omission on the API that prevented creating HTML CDATA elements.
- Improved performance when parsing CDATA in HTML documents.
- Improve performance of number and DateTime parsing.

#### 2.3.2 - July 24 2016

- Add support for HTML entities with Unicode characters above 65535.
- Improve resilience when parsing invalid Set-Cookie headers.

#### 2.3.1 - June 19 2016

- Add support for specifying timeouts when doing HTTP request.

#### 2.3.1-beta2 - May 21 2016

- Preserve response stream in case of HTTP failures.
- Handle cookies with commas in their value correctly.

#### 2.3.1-beta1 - May 2 2016

- Fix runtime parsing of optional records with empty strings in JsonProvider.
- Added HTML CSS selectors to browse the DOM of parsed HTML files using the jQuery selectors syntax.
- Fix round tripping of XmlProvider generated types.

#### 2.3.0 - May 1 2016

- Handle cookies with "http://"-prefixed domain value correctly.
- Fixed Pre and Code HTML tags loosing the formating.
- Added LINQPad samples.
- Fixed quotes not being escaped when saving CSV files.
- Fixed crash on systems where WebRequest.DefaultWebProxy is null.

#### 2.3.0-beta2 - December 21 2015

- Improved JSON parsing performance by 20%.
- Fixed dependencies of NuGet package for PCL profiles 7 and 259.

#### 2.3.0-beta1 - October 11 2015

- Support for PCL profile 7 and PCL profile 259.
- Added support for single column CSV files in CsvProvider.
- Fix saving of CSV files with cells spanning multiple lines.
- Fixed parsing of HTML tables with headers spanning multiple rows.
- Fixed parsing of HTML definition lists without description elements.

#### 2.2.5 - July 12 2015

- Fix HtmlNode.hasClass to work on multi class elements.

#### 2.2.4 - July 11 2015

- Relax the parsing of the charset field in HTTP response headers to accommodate servers not 100% compliant with RFC2616.
- Fix parsing of HTML lists with links.
- Fix parsing of HTML pages with tables and lists with the same name.
- Fix parsing of HTML documents with missing closing tags.

#### 2.2.3 - June 13 2015

- Fixed compatibility with Mono 4.0.
- Support for trailing empty columns in CsvProvider.
- Fix datetime convertion when epoch date contains positive in timezone part.

#### 2.2.2 - May 11 2015

- Allow arrays in addition to objects when detecting Json values inside Xml documents.
- Simplify generated API for collections in XmlProvider in more cases.

#### 2.2.1 - May 4 2015

- Improved performance of JsonValue.Parse().
- Fixed crash processing HTTP responses without content type.
- Fixed encoding from content type not being used on the POST requests.
- Improved compatibility with different versions of FSharp.Core.
- Added BasicAuth helper to HttpRequestHeaders.

#### 2.2.0 - March 22 2015

- Added constants for more HTTP methods.
- Added fix for `thead` element without nested `tr` element.
- Improved global inference in XmlProvider.
- Write API for CsvProvider.
- Remove Freebase provider.
- Improve support for loading big CSV files in CsvProvider.
- Fix possible stack overflow in HTML parser.
- Exclude elements with aria-hidden attribute when parsing tables in HtmlProvider.
- Use ISO-8601 format when outputing dates.
- Fix parsing of HTML closing tags with numbers.
- Fixed handling of URI's with fragment but no query.
- Fixed arrays created with XML provider having unneeded parent tags on some situations.
- Allow to parse rows in CsvProvider without having to create a CsvFile.

#### 2.1.1 - December 24 2014

- Add SkipRows parameter to CsvProvider.
- Improved parsing of numbers.
- Fixed XmlProvider so InferTypesFromValues=false works for elements in addition to attributes.
- Recognise media types application/\*+json as text.
- Workarounded Mono bug causing HTTP POST requests to hang.

#### 2.1.0 - November 2 2014

- Fixed parsing of HTML attributes without value.
- Fixed parsing of non-breaking spaces in HTML.
- Fixed parsing of CDATA in HTML script elements.
- Support for more currency symbols and percent, per mil, and basic point symbols when parsing numbers.
- Promoted TextConversions to top level.

#### 2.1.0-beta2 - October 21 2014

- Improve generated table names in HtmlProvider.
- Added support for lists in addition to tables in HtmlProvider.
- Added TBA and TBD to list of default missing values.
- Make HTML parser API more C# friendly.
- Improve API of HTML operations.

#### 2.1.0-beta - October 12 2014

- New logo.
- Added HTML parser and HtmlProvider.
- Detect and ignore trailing empty header columns in CSV/TSV files.
- Fixed strings with only whitespace being lost in JsonProvider.

#### 2.0.15 - September 23 2014

- Fixed crash when disposing CsvProvider instances.
- Add support for UTF-32 characters in JsonValue and JsonProvider.
- Simplify generated API for collections in XmlProvider.

#### 2.0.14 - August 30 2014

- Fixed handling of HTTP response cookies on some corner cases that .NET doesn't natively support.

#### 2.0.13 - August 29 2014

- Fixed handling of HTTP response cookies on some corner cases that .NET doesn't natively support.

#### 2.0.12 - August 28 2014

- Fixed crash on HTTP requests that return 0 bytes.

#### 2.0.11 - August 27 2014

- Fixed HTTP decompression throwing AccessViolationException's on Windows Phone.

#### 2.0.10 - August 21 2014

- Improved performance of JsonValue.ToString().
- Allow to serialize a JsonValue to a TextWriter.
- Fixed possible memory leak.
- Accept any MIME type in HTTP requests made by CsvProvider, JsonProvider, and XmlProvider (but still issue a preference).
- Fix usage of customizeHttpRequest on POST requests.
- Fixed problem on url creation when ampersands are used in query parameter values.
- Added InferTypesFromValues parameter to XmlProvider and JsonProvider to allow disabling infering booleans and numbers from strings.

#### 2.0.9 - June 15 2014

- Support for non-UTF8 encodings in sample files for CsvProvider, JsonProvider, and XmlProvider.
- Fixed unnecessary character escaping in JsonValue.
- Be more relaxed about mixing different versions on FSharp.Data.

#### 2.0.8 - May 10 2014

- Prevent locking of dll's when reading samples from embedded resources.
- Fixed wrong default encoding being used for HTTP requests and responses.
- Fixed parsing of some unicode characters in JsonValue and JsonProvider.
- Auto-detect files with tab separators in CsvProvider.

#### 2.0.7 - April 28 2014

- Support for reading sample CSV, JSON, and XML from an embedded resource.
- Fix wrong error messages being returned when sample files are not found.

#### 2.0.6 - April 28 2014

- Performance improvements.
- Support reuse by other type providers projects like ApiaryProvider.
- Fixed problems with HTTP requests not downloading fully.
- Added support for creating typed XML objects in XmlProvider.
- Added support for creating typed JSON and XML objects from untyped JsonValue and XElement objects.
- Fixed crash when data files used in a type provider used on a fsx file changed.
- Fixed problem parsing JSON values with keys with the double quote character.

#### 2.0.5 - March 29 2014

- Added - to the list of default missing values.
- Re-added support for specifying known HTTP headers in the wrong casing.
- Fixed sending of HTTP requests when using a portable class library version of FSharp.Data in the full .NET version.

#### 2.0.4 - March 20 2014

- Helpers for sending HTTP requests with JSON and XML content.
- Removed built-in HTTP certificates support, and moved it to a sample in the documentation.

#### 2.0.3 - March 17 2014

- Respect the order of the attributes present in the JSON in JsonProvider.

#### 2.0.2 - March 16 2014

- Always send User-Agent and Accept headers when making requests in the type providers.
- Added support for creating typed JSON objects in JsonProvider.

#### 2.0.1 - March 14 2014

- Fixed Freebase provider throwing exceptions in the absense of network connectivity even when not used.

#### 2.0.0 - March 10 2014

- Detect Json values inside Xml documents, and generate appropriate types, instead of considering a raw string.
- Performance improvements.
- Fixed bugs in naming algorithm.
- Improved documentation.

#### 2.0.0-beta3 - March 4 2014

- Remove ApiaryProvider.
- Improve error reporting in the Freebase provider.
- Add stronger typing to Http.Request parameters.
- Respect character set header in HTTP responses.
- Allow to don't throw on HTTP errors.
- Allow to customize the HTTP request by passing a function.
- Added RequestStream and AsyncRequestStream methods to Http to allow accessing the response stream directly.

#### 2.0.0-beta2 - February 27 2014

- Simplified API generated by XmlProvider.
- Fixed handling of optional elements in XmlProvider.

#### 2.0.0-beta - February 24 2014

- Mono fixes.
- Allow to set the freebase api key globally by using the environment variable FREEBASE_API_KEY
- Fixed handling of optional records in JsonProvider.
- Reduced the number of cases where heterogeneous types are used in JsonProvider.
- Fixed `<type>` option option being generated on some cases in JsonProvider.
- Treat "", null and missing values in the same way in JsonProvider.
- Fixed homogeneous arrays to have the same null skipping behaviour as heterogeneous arrays in JsonProvider.
- Fixed namespace declarations generating attributes in XmlProvider.
- Fixed CsvProvider generating column names with only a space.
- Return NaN for missing data in WorldBank indicators instead of throwing an exception.
- Don't throw exceptions in JsonValue.AsArray, JsonValue.Properties, and JsonValue.InnerText.

#### 2.0.0-alpha7 - February 20 2014

- Improved name generation algorithm to cope better with acronymns.
- Fixed wrong singularization of words ending with 'uses'.
- Fixed handling of repeated one letter names.
- Improve HTTP error messages.
- Support for more api patterns in ApiaryProvider.
- Tolerate invalid json and missing data in apiary specifications.
- Improved naming of generated types.
- Fixed 'SampleIsList' to work with xml and json spanning multiple lines.
- Fixed handling of nested arrays in JsonProvider.
- Fixed handling of optional arrays in JsonProvider.

#### 2.0.0-alpha6 - February 4 2014

- JsonValue.Post() allows to post the JSON to the specified uri using HTTP.

#### 2.0.0-alpha5 - February 3 2014

- Renamed the 'FSharp.Data.Json.Extensions' module to 'FSharp.Data.JsonExtensions'.
- Renamed the 'FSharp.Data.Csv.Extensions' module to 'FSharp.Data.CsvExtensions'.
- Moved the contents of the 'FSharp.Net', 'FSharp.Data.Csv', and 'FSharp.Data.Json' namespaces to the 'FSharp.Data' namespace.
- Reuse identical types in JsonProvider.
- Improve JsonProvider error messages to include full path of the json part that caused the problem.
- JsonValue.ToString() now formats (indents) the output by default (can be turned off by using SaveOptions.DisableFormatting).

#### 2.0.0-alpha4 - January 30 2014

- Adds specific types for Freebase individuals, so each individual X only has properties P where X.P actually returns interesting data. This makes Individuals much more useful for exploring sparse data, as you can "dot" through an individual and see exactly what properties actually have interesting data. The feature is on by default but can be turned off using UseRefinedTypes=false as a static parameter.
- Individuals10 and Individuals100 views of Freebase individuals, which increases the number of items in the table by 10x and 100x.
- IndividualsAZ view of Freebase individuals, which buckets the individuals by first character of name A-Z, with each bucket containing up to 10,000 individuals.
- Added SendingQuery event which triggers for overall Freebase MQL queries and can be run in the Freebase query editor, instead of for individual REST requests including cursor-advancing requests and documentation requests.
- Renamed CsvProvider and CsvFle 'Data' property to 'Rows'.
- Renamed CsvProvider static parameter 'SafeMode' to 'AssumeMissingValues'.
- Fixed parsing of values wrapped in quotes in arrays and heterogeneous types generated by JsonProvider.
- Added SourceLink support.

#### 2.0.0-alpha3 - December 30 2013

- Fixed the use of samples which also are valid filenames in CsvProvider.
- Allow to specify only the Schema without a Sample in CsvProvider.

#### 2.0.0-alpha2 - December 24 2013

- Support heterogeneous types at the top level in XmlProvider.
- Reference System.Xml.Linq in NuGet package.
- Filter out user domains in Freebase.
- Fix Zlib.Portable being referenced by Nuget on non PCL projects.

#### 2.0.0-alpha - December 15 2013

- Support for F# 3.1 and for new portable class library projects.
- Support for sending HTTP requests with a binary body.
- Support for HTTP compression in portable class library versions (adds dependency on Zlib.Portable).
- Fixed problem when using uri's with encoded slashes (%2F) in the sample parameter of CsvProvider, JsonProvider & XmlProvider.
- CsvProvider now has GetSample static method like the other providers in addition to the default constructor.
- Add AsyncLoad(string uri) and AsyncGetSample() to CsvProvider, JsonProvider and XmlProvider.
- Removed '.AsTuple' member from CsvProvider.
- Renamed 'SampleList' static property to 'SampleIsList'.
- Renamed 'Separator' static property to 'Separators'.
- When 'SampleIsList' is true, a 'GetSamples' method is generated.
- Fixed XmlProvider's SampleIsList not working correctly.
- Fix handling of optional elements in XmlProvider when using multiple samples.
- Fix XmlProvider handling of one letter XML tags.
- Fixed CsvProvider's SafeMode not working when there were more rows than the InferRows limit.
- Exceptions raised by CsvProvider and CsvFile were reporting the wrong line number when reading files with windows line endings.
- CsvInference is now part of the runtime so it can be reused by Deedle.
- Allow currency symbols on decimals.
- Fixed file change notification not invalidating type providers correctly.
- Fix generated code doing repeated work.
- Windows Phone 7 no longer supported.
- Added Japanese documentation.
- Prevent the NuGet package from adding a reference to FSharp.Data.DesignTime.
- Entity types generated by JsonProvider & XmlProvider are now directly below the type provider, instead of under a DomainTypes inner type.
- Source Code now builds under Mono.
- Expose optional parameters from CsvFile & Http methods as optional in C#.

#### 1.1.10 - September 12 2013

- Support for heterogeneous XML attributes.
- Make CsvFile re-entrant.
- Support for compressed HTTP responses.
- Fix JSON conversion of 0 and 1 to booleans.

#### 1.1.9 - July 21 2013

- Infer booleans for ints that only manifest 0 and 1.
- Support for partially overriding the Schema in CsvProvider.
- PreferOptionals and SafeMode parameters for CsvProvider.

#### 1.1.8 - July 01 2013

- Fixed problem with portable version of FSharp.Net.Http.

#### 1.1.7 - July 01 2013

- Fixed problem handling enumerates in FreebaseProvider.

#### 1.1.6 - June 30 2013

- Fixed runtime problem accessing optional properties with a JSON null.
- Support for client certificates in FSharp.Net.Http.
- Support for Windows Phone 7.

#### 1.1.5 - May 13 2013

- Performance improvements, support for big csv files, and support for Guid types.
- Save, Filter and Truncate operations for csv files.

#### 1.1.4 - April 13 2013

- Allow to skip rows that don't match the schema in CsvProvider.
- Support for dynamic lookup in CSV files.
- Improvements to FSharp.Net.Http to support cookies and binary files.

#### 1.1.3 - April 08 2013

- Improve Units of Measure support and allow to override the type inference in the CSV Provider.

#### 1.1.2 - March 30 2013

- Update NuGet package links and icon reference.

#### 1.1.1 - February 18 2013

- Update WorldBank internals to support more efficient implementation and FunScript.

#### 1.1.0 - February 18 2013

- Support for Portable Class Libraries and Silverlight.
- Added Freebase provider.
- Improvements to the CSV provider.
- Performance improvements when handling large files.

#### 1.0.13 - January 16 2013

- Fix boolean parsing bug and improve CSV provider.

#### 1.0.12 - January 14 2013

- Minor update in missing fields handling.

#### 1.0.11 - January 14 2013

- Support for different culture settings and CSV parsing according to RFC standard.

#### 1.0.10 - January 06 2013

- CSV provider supports alternative separators and N/A values.

#### 1.0.9 - January 04 2013

- Minor changes to support Experimental release.

#### 1.0.8 - January 04 2013

- Support global unification in XML type provider.

#### 1.0.6 - December 23 2012

- Build the library using .NET 4.0.

#### 1.0.5 - December 20 2012

- CSV provider now supports dates.

#### 1.0.4 - December 17 2012

- Improved method naming.

#### 1.0.2 - December 14 2012

- Improved method naming.

#### 1.0.1 - December 14 2012

- Latest version to match with the documentation.

#### 1.0.0 - December 13 2012

- Initial release

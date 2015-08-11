#### 2.2.5 - July 12 2015
* Fix HtmlNode.hasClass to work on multi class elements.

#### 2.2.4 - July 11 2015
* Relax the parsing of the charset field in HTTP response headers to accommodate servers not 100% compliant with RFC2616.
* Fix parsing of HTML lists with links.
* Fix parsing of HTML pages with tables and lists with the same name.
* Fix parsing of HTML documents with missing closing tags.

#### 2.2.3 - June 13 2015
* Fixed compatibility with Mono 4.0.
* Support for trailing empty columns in CsvProvider.
* Fix datetime convertion when epoch date contains positive in timezone part.

#### 2.2.2 - May 11 2015
* Allow arrays in addition to objects when detecting Json values inside Xml documents.
* Simplify generated API for collections in XmlProvider in more cases.

#### 2.2.1 - May 4 2015
* Improved performance of JsonValue.Parse().
* Fixed crash processing HTTP responses without content type.
* Fixed encoding from content type not being used on the POST requests.
* Improved compatibility with different versions of FSharp.Core.
* Added BasicAuth helper to HttpRequestHeaders.

#### 2.2.0 - March 22 2015
* Added constants for more HTTP methods.
* Added fix for `thead` element without nested `tr` element.
* Improved global inference in XmlProvider.
* Write API for CsvProvider.
* Remove Freebase provider.
* Improve support for loading big CSV files in CsvProvider.
* Fix possible stack overflow in HTML parser.
* Exclude elements with aria-hidden attribute when parsing tables in HtmlProvider.
* Use ISO-8601 format when outputing dates.
* Fix parsing of HTML closing tags with numbers.
* Fixed handling of URI's with fragment but no query.
* Fixed arrays created with XML provider having unneeded parent tags on some situations.
* Allow to parse rows in CsvProvider without having to create a CsvFile.

#### 2.1.1 - December 24 2014
* Add SkipRows parameter to CsvProvider.
* Improved parsing of numbers.
* Fixed XmlProvider so InferTypesFromValues=false works for elements in addition to attributes.
* Recognise media types application/*+json as text.
* Workarounded Mono bug causing HTTP POST requests to hang.

#### 2.1.0 - November 2 2014
* Fixed parsing of HTML attributes without value.
* Fixed parsing of non-breaking spaces in HTML.
* Fixed parsing of CDATA in HTML script elements.
* Support for more currency symbols and percent, per mil, and basic point symbols when parsing numbers.
* Promoted TextConversions to top level.

#### 2.1.0-beta2 - October 21 2014
* Improve generated table names in HtmlProvider.
* Added support for lists in addition to tables in HtmlProvider.
* Added TBA and TBD to list of default missing values.
* Make HTML parser API more C# friendly.
* Improve API of HTML operations.

#### 2.1.0-beta - October 12 2014
* New logo.
* Added HTML parser and HtmlProvider.
* Detect and ignore trailing empty header columns in CSV/TSV files. 
* Fixed strings with only whitespace being lost in JsonProvider.

#### 2.0.15 - September 23 2014
* Fixed crash when disposing CsvProvider instances.
* Add support for UTF-32 characters in JsonValue and JsonProvider.
* Simplify generated API for collections in XmlProvider.

#### 2.0.14 - August 30 2014
* Fixed handling of HTTP response cookies on some corner cases that .NET doesn't natively support.

#### 2.0.13 - August 29 2014
* Fixed handling of HTTP response cookies on some corner cases that .NET doesn't natively support.

#### 2.0.12 - August 28 2014
* Fixed crash on HTTP requests that return 0 bytes.

#### 2.0.11 - August 27 2014
* Fixed HTTP decompression throwing AccessViolationException's on Windows Phone.

#### 2.0.10 - August 21 2014
* Improved performance of JsonValue.ToString().
* Allow to serialize a JsonValue to a TextWriter.
* Fixed possible memory leak.
* Accept any MIME type in HTTP requests made by CsvProvider, JsonProvider, and XmlProvider (but still issue a preference).
* Fix usage of customizeHttpRequest on POST requests.
* Fixed problem on url creation when ampersands are used in query parameter values.
* Added InferTypesFromValues parameter to XmlProvider and JsonProvider to allow disabling infering booleans and numbers from strings.

#### 2.0.9 - June 15 2014
* Support for non-UTF8 encodings in sample files for CsvProvider, JsonProvider, and XmlProvider.
* Fixed unnecessary character escaping in JsonValue.
* Be more relaxed about mixing different versions on FSharp.Data.

#### 2.0.8 - May 10 2014
* Prevent locking of dll's when reading samples from embedded resources.
* Fixed wrong default encoding being used for HTTP requests and responses.
* Fixed parsing of some unicode characters in JsonValue and JsonProvider.
* Auto-detect files with tab separators in CsvProvider.

#### 2.0.7 - April 28 2014
* Support for reading sample CSV, JSON, and XML from an embedded resource.
* Fix wrong error messages being returned when sample files are not found.

#### 2.0.6 - April 28 2014
* Performance improvements.
* Support reuse by other type providers projects like ApiaryProvider.
* Fixed problems with HTTP requests not downloading fully.
* Added support for creating typed XML objects in XmlProvider.
* Added support for creating typed JSON and XML objects from untyped JsonValue and XElement objects.
* Fixed crash when data files used in a type provider used on a fsx file changed.
* Fixed problem parsing JSON values with keys with the double quote character.

#### 2.0.5 - March 29 2014
* Added - to the list of default missing values.
* Re-added support for specifying known HTTP headers in the wrong casing.
* Fixed sending of HTTP requests when using a portable class library version of FSharp.Data in the full .NET version.

#### 2.0.4 - March 20 2014
* Helpers for sending HTTP requests with JSON and XML content.
* Removed built-in HTTP certificates support, and moved it to a sample in the documentation.

#### 2.0.3 - March 17 2014
* Respect the order of the attributes present in the JSON in JsonProvider.

#### 2.0.2 - March 16 2014
* Always send User-Agent and Accept headers when making requests in the type providers.
* Added support for creating typed JSON objects in JsonProvider.

#### 2.0.1 - March 14 2014
* Fixed Freebase provider throwing exceptions in the absense of network connectivity even when not used.

#### 2.0.0 - March 10 2014
* Detect Json values inside Xml documents, and generate appropriate types, instead of considering a raw string.
* Performance improvements.
* Fixed bugs in naming algorithm.
* Improved documentation.

#### 2.0.0-beta3 - March 4 2014
* Remove ApiaryProvider.
* Improve error reporting in the Freebase provider.
* Add stronger typing to Http.Request parameters.
* Respect character set header in HTTP responses.
* Allow to don't throw on HTTP errors.
* Allow to customize the HTTP request by passing a function.
* Added RequestStream and AsyncRequestStream methods to Http to allow accessing the response stream directly.

#### 2.0.0-beta2 - February 27 2014
* Simplified API generated by XmlProvider.
* Fixed handling of optional elements in XmlProvider.

#### 2.0.0-beta - February 24 2014
* Mono fixes.
* Allow to set the freebase api key globally by using the environment variable FREEBASE_API_KEY
* Fixed handling of optional records in JsonProvider.
* Reduced the number of cases where heterogeneous types are used in JsonProvider.
* Fixed <type> option option being generated on some cases in JsonProvider.
* Treat "", null and missing values in the same way in JsonProvider.
* Fixed homogeneous arrays to have the same null skipping behaviour as heterogeneous arrays in JsonProvider.
* Fixed namespace declarations generating attributes in XmlProvider.
* Fixed CsvProvider generating column names with only a space.
* Return NaN for missing data in WorldBank indicators instead of throwing an exception.
* Don't throw exceptions in JsonValue.AsArray, JsonValue.Properties, and JsonValue.InnerText.

#### 2.0.0-alpha7 - February 20 2014
* Improved name generation algorithm to cope better with acronymns.
* Fixed wrong singularization of words ending with 'uses'.
* Fixed handling of repeated one letter names.
* Improve HTTP error messages.
* Support for more api patterns in ApiaryProvider.
* Tolerate invalid json and missing data in apiary specifications.
* Improved naming of generated types.
* Fixed 'SampleIsList' to work with xml and json spanning multiple lines.
* Fixed handling of nested arrays in JsonProvider.
* Fixed handling of optional arrays in JsonProvider.

#### 2.0.0-alpha6 - February 4 2014
* JsonValue.Post() allows to post the JSON to the specified uri using HTTP.

#### 2.0.0-alpha5 - February 3 2014
* Renamed the 'FSharp.Data.Json.Extensions' module to 'FSharp.Data.JsonExtensions'.
* Renamed the 'FSharp.Data.Csv.Extensions' module to 'FSharp.Data.CsvExtensions'.
* Moved the contents of the 'FSharp.Net', 'FSharp.Data.Csv', and 'FSharp.Data.Json' namespaces to the 'FSharp.Data' namespace.
* Reuse identical types in JsonProvider.
* Improve JsonProvider error messages to include full path of the json part that caused the problem.
* JsonValue.ToString() now formats (indents) the output by default (can be turned off by using SaveOptions.DisableFormatting).

#### 2.0.0-alpha4 - January 30 2014
* Adds specific types for Freebase individuals, so each individual X only has properties P where X.P actually returns interesting data. This makes Individuals much more useful for exploring sparse data, as you can "dot" through an individual and see exactly what properties actually have interesting data. The feature is on by default but can be turned off using UseRefinedTypes=false as a static parameter.
* Individuals10 and Individuals100 views of Freebase individuals, which increases the number of items in the table by 10x and 100x.
* IndividualsAZ view of Freebase individuals, which buckets the individuals by first character of name A-Z, with each bucket containing up to 10,000 individuals.
* Added SendingQuery event which triggers for overall Freebase MQL queries and can be run in the Freebase query editor, instead of for individual REST requests including cursor-advancing requests and documentation requests.
* Renamed CsvProvider and CsvFle 'Data' property to 'Rows'.
* Renamed CsvProvider static parameter 'SafeMode' to 'AssumeMissingValues'.
* Fixed parsing of values wrapped in quotes in arrays and heterogeneous types generated by JsonProvider.
* Added SourceLink support.

#### 2.0.0-alpha3 - December 30 2013
* Fixed the use of samples which also are valid filenames in CsvProvider.
* Allow to specify only the Schema without a Sample in CsvProvider.

#### 2.0.0-alpha2 - December 24 2013
* Support heterogeneous types at the top level in XmlProvider.
* Reference System.Xml.Linq in NuGet package.
* Filter out user domains in Freebase.
* Fix Zlib.Portable being referenced by Nuget on non PCL projects.

#### 2.0.0-alpha - December 15 2013
* Support for F# 3.1 and for new portable class library projects.
* Support for sending HTTP requests with a binary body.
* Support for HTTP compression in portable class library versions (adds dependency on Zlib.Portable).
* Fixed problem when using uri's with encoded slashes (%2F) in the sample parameter of CsvProvider, JsonProvider & XmlProvider.
* CsvProvider now has GetSample static method like the other providers in addition to the default constructor.
* Add AsyncLoad(string uri) and AsyncGetSample() to CsvProvider, JsonProvider and XmlProvider.
* Removed '.AsTuple' member from CsvProvider.
* Renamed 'SampleList' static property to 'SampleIsList'.
* Renamed 'Separator' static property to 'Separators'.
* When 'SampleIsList' is true, a 'GetSamples' method is generated.
* Fixed XmlProvider's SampleIsList not working correctly.
* Fix handling of optional elements in XmlProvider when using multiple samples.
* Fix XmlProvider handling of one letter XML tags.
* Fixed CsvProvider's SafeMode not working when there were more rows than the InferRows limit.
* Exceptions raised by CsvProvider and CsvFile were reporting the wrong line number when reading files with windows line endings.
* CsvInference is now part of the runtime so it can be reused by Deedle.
* Allow currency symbols on decimals.
* Fixed file change notification not invalidating type providers correctly.
* Fix generated code doing repeated work.
* Windows Phone 7 no longer supported.
* Added Japanese documentation.
* Prevent the NuGet package from adding a reference to FSharp.Data.DesignTime.
* Entity types generated by JsonProvider & XmlProvider are now directly below the type provider, instead of under a DomainTypes inner type.
* Source Code now builds under Mono.
* Expose optional parameters from CsvFile & Http methods as optional in C#.

#### 1.1.10 - September 12 2013
* Support for heterogeneous XML attributes.
* Make CsvFile re-entrant. 
* Support for compressed HTTP responses. 
* Fix JSON conversion of 0 and 1 to booleans.

#### 1.1.9 - July 21 2013
* Infer booleans for ints that only manifest 0 and 1.
* Support for partially overriding the Schema in CsvProvider.
* PreferOptionals and SafeMode parameters for CsvProvider.

#### 1.1.8 - July 01 2013
* Fixed problem with portable version of FSharp.Net.Http.

#### 1.1.7 - July 01 2013
* Fixed problem handling enumerates in FreebaseProvider.

#### 1.1.6 - June 30 2013
* Fixed runtime problem accessing optional properties with a JSON null.
* Support for client certificates in FSharp.Net.Http.
* Support for Windows Phone 7.

#### 1.1.5 - May 13 2013
* Performance improvements, support for big csv files, and support for Guid types.
* Save, Filter and Truncate operations for csv files.

#### 1.1.4 - April 13 2013
* Allow to skip rows that don't match the schema in CsvProvider.
* Support for dynamic lookup in CSV files.
* Improvements to FSharp.Net.Http to support cookies and binary files.

#### 1.1.3 - April 08 2013
* Improve Units of Measure support and allow to override the type inference in the CSV Provider.

#### 1.1.2 - March 30 2013
* Update NuGet package links and icon reference.

#### 1.1.1 - February 18 2013
* Update WorldBank internals to support more efficient implementation and FunScript.

#### 1.1.0 - February 18 2013
* Support for Portable Class Libraries and Silverlight.
* Added Freebase provider.
* Improvements to the CSV provider.
* Performance improvements when handling large files.

#### 1.0.13 - January 16 2013
* Fix boolean parsing bug and improve CSV provider.

#### 1.0.12 - January 14 2013
* Minor update in missing fields handling.

#### 1.0.11 - January 14 2013
* Support for different culture settings and CSV parsing according to RFC standard.

#### 1.0.10 - January 06 2013
* CSV provider supports alternative separators and N/A values.

#### 1.0.9 - January 04 2013
* Minor changes to support Experimental release.

#### 1.0.8 - January 04 2013
* Support global unification in XML type provider.

#### 1.0.6 - December 23 2012
* Build the library using .NET 4.0.

#### 1.0.5 - December 20 2012
* CSV provider now supports dates.

#### 1.0.4 - December 17 2012
* Improved method naming.

#### 1.0.2 - December 14 2012
* Improved method naming.

#### 1.0.1 - December 14 2012
* Latest version to match with the documentation.

#### 1.0.0 - December 13 2012
* Initial release

#### 1.0.0 - December 13 2012
* Initial release

#### 1.0.1 - December 14 2012
* Latest version to match with the documentation.

#### 1.0.2 - December 14 2012
* Improved method naming.

#### 1.0.4 - December 17 2012
* Improved method naming.

#### 1.0.5 - December 20 2012
* CSV provider now supports dates.

#### 1.0.6 - December 23 2012
* Build the library using .NET 4.0.

#### 1.0.8 - January 04 2013
* Support global unification in XML type provider.

#### 1.0.9 - January 04 2013
* Minor changes to support Experimental release.

#### 1.0.10 - January 06 2013
* CSV provider supports alternative separators and N/A values.

#### 1.0.11 - January 14 2013
* Support for different culture settings and CSV parsing according to RFC standard.

#### 1.0.12 - January 14 2013
* Minor update in missing fields handling.

#### 1.0.13 - January 16 2013
* Fix boolean parsing bug and improve CSV provider.

#### 1.1.0 - February 18 2013
* Support for Portable Class Libraries and Silverlight.
* Added Freebase provider.
* Improvements to the CSV provider.
* Performance improvements when handling large files.

#### 1.1.1 - February 18 2013
* Update WorldBank internals to support more efficient implementation and FunScript.

#### 1.1.2 - March 30 2013
* Update NuGet package links and icon reference.

#### 1.1.3 - April 08 2013
* Improve Units of Measure support and allow to override the type inference in the CSV Provider.

#### 1.1.4 - April 13 2013
* Allow to skip rows that don't match the schema in CsvProvider.
* Support for dynamic lookup in CSV files.
* Improvements to FSharp.Net.Http to support cookies and binary files.

#### 1.1.5 - May 13 2013
* Performance improvements, support for big csv files, and support for Guid types.
* Save, Filter and Truncate operations for csv files.

#### 1.1.6 - June 30 2013
* Fixed runtime problem accessing optional properties with a JSON null.
* Support for client certificates in FSharp.Net.Http.
* Support for Windows Phone 7.

#### 1.1.7 - July 01 2013
* Fixed problem handling enumerates in FreebaseProvider.

#### 1.1.8 - July 01 2013
* Fixed problem with portable version of FSharp.Net.Http.

#### 1.1.9 - July 21 2013
* Infer booleans for ints that only manifest 0 and 1.
* Support for partially overriding the Schema in CsvProvider.
* PreferOptionals and SafeMode parameters for CsvProvider.

#### 1.1.10 - September 12 2013
* Support for heterogeneous XML attributes.
* Make CsvFile re-entrant. 
* Support for compressed HTTP responses. 
* Fix JSON conversion of 0 and 1 to booleans.

#### 2.0.0-alpha - Unreleased
* Support for F# 3.1 and for new portable class library projects.
* Support for sending HTTP requests with a binary body.
* Support for HTTP compression in portable class library versions.
* Fixed problem when using uri's with encoded slashes (%2F) in the sample parameter of CsvProvider, JsonProvider & XmlProvider.
* CsvProvider now has GetSample static method like the other providers in addition to the default constructor.
* Add AsyncLoad(string uri) and AsyncGetSample() to CsvProvider, JsonProvider and XmlProvider.
* Remove .AsTuple member from CsvProvider.
* Renamed SampleList to SampleIsList.
* Renamed Separator to Separators.
* When SampleIsList is true, a GetSamples method is generated.
* Fixed XmlProvider's SampleIsList not working correctly.
* Fix handling of optional elements in XmlProvider when using multiple samples.
* Fixed CsvProvider's SafeMode not working when there were more rows than the InferRows limit.
* Exceptions raised by CsvProvider and CsvFile were reporting the wrong line number when reading files with windows line endings.
* CsvInference is now part of the runtime so it can be reused by Deedle.
* Allow currency symbols on decimals.
* Fixed file change notification not invalidating type providers correctly.
* Fix generated code doing repeated work.
* Windows Phone 7 no longer supported.
* Added Japanese documentation.

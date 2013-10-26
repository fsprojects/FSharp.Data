* 1.0.0 - Initial release.
* 1.0.1 - Latest version to match with the documentation.
* 1.0.2 - Improved method naming.
* 1.0.4 - Improved method naming.
* 1.0.5 - CSV provider now supports dates.
* 1.0.6 - Build the library using .NET 4.0.
* 1.0.8 - Support global unification in XML type provider.
* 1.0.9 - Minor changes to support Experimental release.
* 1.0.10 - CSV provider supports alternative separators and N/A values.
* 1.0.11 - Support for different culture settings and CSV parsing according to RFC standard.
* 1.0.12 - Minor update in missing fields handling.
* 1.0.13 - Fix boolean parsing bug and improve CSV provider.
* 1.1.0 - Support for Portable Class Libraries and Silverlight. Added Freebase provider. Improvements to the CSV provider. Performance improvements when handling large files.
* 1.1.1 - Update WorldBank internals to support more efficient implementation and FunScript.
* 1.1.2 - Update NuGet package links and icon reference.
* 1.1.3 - Improve Units of Measure support and allow to override the type inference in the CSV Provider.
* 1.1.4 - Allow to skip rows that don't match the schema in CsvProvider. Support for dynamic lookup in CSV files. Improvements to FSharp.Net.Http to support cookies and binary files.
* 1.1.5 - Performance improvements, support for big csv files, and support for Guid types. Save, Filter and Truncate operations for csv files.
* 1.1.6 - Fixed runtime problem accessing optional properties with a JSON null. Support for client certificates in FSharp.Net.Http. Support for Windows Phone 7.
* 1.1.7 - Fixed problem handling enumerates in FreebaseProvider.
* 1.1.8 - Fixed problem with portable version of FSharp.Net.Http.
* 1.1.9 - Infer booleans for ints that only manifest 0 and 1. Support for partially overriding the Schema in CsvProvider. PreferOptionals and SafeMode parameters for CsvProvider.
* 1.1.10 - Support for heterogeneous XML attributes. Make CsvFile re-entrant. Support for compressed HTTP responses. Fix JSON conversion of 0 and 1 to booleans. Fix XmlProvider problems with nested elements and elements with same name in different namespaces.
* Unreleased - Support for sending HTTP requests with a binary body. Allow currency symbols on decimals. Remove .AsTuple member from CsvProvider. CsvProvider now uses GetSample instead of constructor like the other providers. Renamed SampleList to SampleIsList. Renamed Separator to Separators. Add AsyncLoad(string uri) to CsvProvider, JsonProvider and XmlProvider. Fixed CsvProvider's SafeMode not working when there were more rows than the InferRows limit. Fixed XmlProvider's SampleIsList not working correctly. Exceptions raised by CsvProvider and CsvFile were reporting the wrong line number when reading files with windows line endings. Fixed file change notification not invalidating type providers correctly. Fix handling of optional elements in XmlProvider when using multiple samples.

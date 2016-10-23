
// --------------------------------------------------------------------------------------
// Bank for International Settlements (BIS) type provider - runtime components 
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime.Bis

open System.Collections.Generic
open System.IO
open System.Linq 

open Microsoft.FSharp.Core.Operators

open FSharp.Data

[<AutoOpen>]
module Implementation =
    
    // Representation of a dataset dimension
    type public Dimension(dimensionName: string, position: int, memberList: string[]) =  
        class
            member this.name = dimensionName
            member this.position = position
            member this.members = memberList
        end

    // Representation of a dataset
    type Dataset(dimensions: Dimension[], periods: string[]) =
        class 
            member this.dimensions = dimensions
            member this.periods = periods
        end
       
    // representation of a filter
    type ObservationFilter(dimension : string, dimensionPosition : int, memberFilter : option<string list>) = 
        class
            member this.dimension = dimension
            member this.dimensionPosition = dimensionPosition
            member this.memberFilter = memberFilter
        end

    type ObservationValue(periodString : string, value : option<float>) =
        class
            member this.value = value
            member this.periodString = periodString
            member this.year = System.Int32.Parse(periodString.Substring(0,4))
            member this.period = 
                let periodPosition = periodString.IndexOf '-' + 2 
                let periodLength = periodString.Length - periodPosition 
                System.Int32.Parse(periodString.Substring(periodPosition, periodLength))
        end

    // Representation of an observation an values per period
    type Observation(key : string, values : Map<string, option<float>>) =
        class
            member this.key = key
            member this.values = 
                values 
                    |> Seq.map (fun obs -> new ObservationValue(obs.Key, obs.Value))
        end
        
    // Base class for parsers
    [<AbstractClass>]
    type public Parser(filePath: string) = 
        
        abstract member headerRowCount : int
        static member separator = [|"\",\""|]
        static member timePeriodName  = "Time Period"

        member val dataset : option<Dataset> = None with get, set

        member x.replaceQuotationMarks (txt : string) =
            txt.Replace("\"", System.String.Empty)

        // Split string with comma separated dimensions to array
        member x.splitDimensions (dimensions : string, ?startPosition : int) = 
            
            let opt = System.StringSplitOptions.RemoveEmptyEntries

            if startPosition.IsSome then
                dimensions.Split (Parser.separator, startPosition.Value, opt)
            else
                dimensions.Split (Parser.separator, opt)

        member x.splitObservation (obs : string) =
            obs.Split ([|':'|], System.StringSplitOptions.RemoveEmptyEntries)

        member x.isTimePeriodColumn (column : string) =
            column = Parser.timePeriodName

        member x.skipHeader (reader:StreamReader) =
            [for i in 1 .. x.headerRowCount -> reader.ReadLine()] |> ignore

        member x.getHeader () =
            use reader = new StreamReader(filePath)
            x.skipHeader reader
            
            x.splitDimensions(reader.ReadLine()) 
                |> Seq.takeWhile (fun d -> not (x.isTimePeriodColumn d))
                |> Seq.map x.replaceQuotationMarks
                |> Seq.toArray

        member x.getPeriods () = 
            use reader = new StreamReader(filePath)
            x.skipHeader reader

            x.splitDimensions(reader.ReadLine())
                |> Seq.skipWhile (fun d -> not (x.isTimePeriodColumn d))
                |> Seq.skip 1
                |> Seq.map x.replaceQuotationMarks
                |> Array.ofSeq

        // Get dataset including the dimesions and the related memebers
        member x.getDataset () = 
                match x.dataset with
                    | Some d -> d
                    | None ->   let lines = File.ReadLines(filePath)
                                let dimNames = x.getHeader()

                                // Get observations and split by dimension separator.
                                let observations = 
                                    lines
                                        |> Seq.skip (x.headerRowCount + 1)
                                        |> Seq.map (fun o -> x.splitDimensions(o, dimNames.Length + 1))
                                        |> Array.ofSeq
                
                                // Get dimensions and related available members.
                                let dimensions = 
                                    [1 .. dimNames.Length]
                                        |> Seq.mapi
                                            (fun i d -> 
                                                observations
                                                    |> Seq.map (fun obs -> Array.get obs i)
                                                    |> Seq.distinct
                                                    |> Array.ofSeq)
                                        |> Seq.mapi (fun i dimension -> new Dimension ((Array.get dimNames i), i, dimension))
                                        |> Array.ofSeq
                                
                                x.dataset <- Some(new Dataset(dimensions, x.getPeriods()))
                                x.dataset.Value

        // Retrieve observations based on observation code part filter
        member x.filter (obsFilter : Dictionary<string, string list>) =
            let dimensions = 
                x.getHeader()
                    |> Seq.mapi (fun i dimension -> new Dimension(dimension, i, Array.empty))
                
            let filterDims = 
                dimensions
                    |> Seq.map (fun dimension -> new ObservationFilter(dimension.name, dimension.position, if obsFilter.ContainsKey(dimension.name) then Some(obsFilter.[dimension.name]) else None))
                    |> Seq.filter (fun f -> f.memberFilter.IsSome)
                    |> Seq.toArray

            let headerCount = dimensions.Count()

            let filtered = 
                File.ReadAllLines(filePath)
                    |> Seq.skip (x.headerRowCount + 1)
                    |> Seq.map x.splitDimensions
                    |> Seq.filter (fun o -> let obs = x.splitObservation(o.[headerCount])
                                            filterDims
                                                |> Seq.filter (fun obsFilter -> obsFilter.memberFilter.Value.Contains(obs.[obsFilter.dimensionPosition]))
                                                |> Seq.length = filterDims.Count())
                    |> Seq.map (fun o -> o.Skip(headerCount).ToArray())
                    |> Seq.map (fun o -> o.[o.Length - 1] <-  x.replaceQuotationMarks(o.[o.Length - 1])
                                         new Observation(o.[0], 
                                            new Map<string, option<float>>(
                                                x.getPeriods() 
                                                    |> Seq.mapi (fun i period -> 
                                                                       period, (if i+1 < o.Length && not (System.String.IsNullOrWhiteSpace o.[i+1]) then 
                                                                                   Some(System.Convert.ToDouble(o.[i+1])) 
                                                                                else 
                                                                                   None)))))
                    |> List.ofSeq

            filtered

    // CBS specific parser
    type public CbsParser(filePath) =
        inherit Parser(filePath)
        override this.headerRowCount = 8

    // LBS specific parser
    type public LbsParser(filePath) =
        inherit Parser(filePath)
        override this.headerRowCount = 7

    // Property prices long
    type public PpLongParser(filePath) =
        inherit Parser(filePath)
        override this.headerRowCount = 6

    // Property prices selected
    type public PpSelectedParser(filePath) =
        inherit Parser(filePath)
        override this.headerRowCount = 5

    // Debt. securities
    type public DebtSecurityParser(filePath) =
        inherit Parser(filePath)
        override this.headerRowCount = 10

    // Effective exchange rates
    type public EffectiveExchangeRatesParser(filePath) =
        inherit Parser(filePath)
        override this.headerRowCount = 4

    // Credit to non-financial sector
    type public CreditNonFinancialSectorParser(filePath) =
        inherit Parser(filePath)
        override this.headerRowCount = 5

    // Debt service ratios for the private non-financial sector
    type public DebtServiceRatioParser(filePath) =
        inherit Parser(filePath)
        override this.headerRowCount = 7

    // Parser factory
    let createPraser pathToDatasetFile =
        match Path.GetFileName(pathToDatasetFile).ToLower() with
            | dset when dset.Contains("_cbs_") -> new CbsParser(pathToDatasetFile) :> Parser
            | dset when dset.Contains("_lbs_") -> new LbsParser(pathToDatasetFile) :> Parser
            | dset when dset.Contains("_long_pp_") -> new PpLongParser(pathToDatasetFile) :> Parser
            | dset when dset.Contains("_selected_pp_") -> new PpSelectedParser(pathToDatasetFile) :> Parser
            | dset when dset.Contains("_debt_sec2_") -> new DebtSecurityParser(pathToDatasetFile) :> Parser
            | dset when dset.Contains("_eer_") -> new EffectiveExchangeRatesParser(pathToDatasetFile) :> Parser
            | dset when dset.Contains("_total_credit_") -> new CreditNonFinancialSectorParser(pathToDatasetFile) :> Parser
            | dset when dset.Contains("_dsr_") -> new DebtServiceRatioParser(pathToDatasetFile) :> Parser
            | _ -> failwith("Dataset not yet supported. File: " + pathToDatasetFile)

    
    let query observationFilter pathToDatasetFile =
        let fileParser = createPraser pathToDatasetFile
        fileParser.filter (observationFilter)
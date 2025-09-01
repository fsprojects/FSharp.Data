module FSharp.Data.Tests.StructuralTypes

open NUnit.Framework
open FsUnit
open System
open FSharp.Data.Runtime.StructuralTypes

[<TestFixture>]
type InferedPropertyTests() =

    [<Test>]
    member _.``InferedProperty can be created with name and type``() =
        let inferredType = InferedType.Primitive(typeof<string>, None, false, false)
        let property = { Name = "TestProperty"; Type = inferredType }
        
        property.Name |> should equal "TestProperty"
        property.Type |> should equal inferredType

    [<Test>]
    member _.``InferedProperty Type field is mutable``() =
        let initialType = InferedType.Primitive(typeof<int>, None, false, false)
        let newType = InferedType.Primitive(typeof<string>, None, false, false)
        let property = { Name = "TestProperty"; Type = initialType }
        
        property.Type <- newType
        property.Type |> should equal newType

    [<Test>]
    member _.``InferedProperty ToString produces meaningful output``() =
        let inferredType = InferedType.Primitive(typeof<string>, None, false, false)
        let property = { Name = "TestProperty"; Type = inferredType }
        
        let result = property.ToString()
        result |> should contain "TestProperty"
        result |> should contain "Primitive"

    [<Test>]
    member _.``InferedProperty with complex type structure``() =
        let recordProperties = [
            { Name = "Field1"; Type = InferedType.Primitive(typeof<int>, None, false, false) }
            { Name = "Field2"; Type = InferedType.Primitive(typeof<string>, None, false, false) }
        ]
        let recordType = InferedType.Record(Some("TestRecord"), recordProperties, false)
        let property = { Name = "ComplexProperty"; Type = recordType }
        
        property.Name |> should equal "ComplexProperty"
        match property.Type with
        | InferedType.Record(Some("TestRecord"), fields, false) -> 
            fields |> should haveLength 2
            fields.[0].Name |> should equal "Field1"
            fields.[1].Name |> should equal "Field2"
        | _ -> failwith "Expected Record type"

[<TestFixture>]
type InferedTypeTagTests() =

    [<Test>]
    member _.``InferedTypeTag NiceName returns correct values``() =
        InferedTypeTag.Number.NiceName |> should equal "Number"
        InferedTypeTag.Boolean.NiceName |> should equal "Boolean"
        InferedTypeTag.String.NiceName |> should equal "String"
        InferedTypeTag.DateTime.NiceName |> should equal "DateTime"
        InferedTypeTag.TimeSpan.NiceName |> should equal "TimeSpan"
        InferedTypeTag.DateTimeOffset.NiceName |> should equal "DateTimeOffset"
        InferedTypeTag.Guid.NiceName |> should equal "Guid"
        InferedTypeTag.Collection.NiceName |> should equal "Array"
        InferedTypeTag.Heterogeneous.NiceName |> should equal "Choice"
        InferedTypeTag.Json.NiceName |> should equal "Json"
        (InferedTypeTag.Record None).NiceName |> should equal "Record"

    [<Test>]
    member _.``InferedTypeTag NiceName for named record``() =
        (InferedTypeTag.Record (Some "TestRecord")).NiceName |> should equal "TestRecord"

    [<Test>]
    member _.``InferedTypeTag Code returns correct values``() =
        InferedTypeTag.Number.Code |> should equal "Number"
        InferedTypeTag.Boolean.Code |> should equal "Boolean"
        InferedTypeTag.String.Code |> should equal "String"
        InferedTypeTag.Collection.Code |> should equal "Array"
        (InferedTypeTag.Record None).Code |> should equal "Record"
        (InferedTypeTag.Record (Some "TestRecord")).Code |> should equal "Record@TestRecord"

    [<Test>]
    member _.``InferedTypeTag ParseCode correctly parses code values``() =
        InferedTypeTag.ParseCode("Number") |> should equal InferedTypeTag.Number
        InferedTypeTag.ParseCode("Boolean") |> should equal InferedTypeTag.Boolean
        InferedTypeTag.ParseCode("String") |> should equal InferedTypeTag.String
        InferedTypeTag.ParseCode("DateTime") |> should equal InferedTypeTag.DateTime
        InferedTypeTag.ParseCode("TimeSpan") |> should equal InferedTypeTag.TimeSpan
        InferedTypeTag.ParseCode("DateTimeOffset") |> should equal InferedTypeTag.DateTimeOffset
        InferedTypeTag.ParseCode("Guid") |> should equal InferedTypeTag.Guid
        InferedTypeTag.ParseCode("Array") |> should equal InferedTypeTag.Collection
        InferedTypeTag.ParseCode("Choice") |> should equal InferedTypeTag.Heterogeneous
        InferedTypeTag.ParseCode("Json") |> should equal InferedTypeTag.Json
        InferedTypeTag.ParseCode("Record") |> should equal (InferedTypeTag.Record None)
        InferedTypeTag.ParseCode("Record@TestRecord") |> should equal (InferedTypeTag.Record (Some "TestRecord"))

    [<Test>]
    member _.``InferedTypeTag ParseCode throws for Null``() =
        (fun () -> InferedTypeTag.ParseCode("Null") |> ignore) 
        |> should throw typeof<System.Exception>

    [<Test>]
    member _.``InferedTypeTag ParseCode throws for invalid code``() =
        (fun () -> InferedTypeTag.ParseCode("InvalidCode") |> ignore) 
        |> should throw typeof<System.Exception>

    [<Test>]
    member _.``InferedTypeTag NiceName throws for Null``() =
        (fun () -> InferedTypeTag.Null.NiceName |> ignore) 
        |> should throw typeof<System.Exception>

[<TestFixture>]
type InferedMultiplicityTests() =

    [<Test>]
    member _.``InferedMultiplicity struct values work correctly``() =
        let single = InferedMultiplicity.Single
        let optionalSingle = InferedMultiplicity.OptionalSingle
        let multiple = InferedMultiplicity.Multiple
        
        single |> should not' (equal optionalSingle)
        single |> should not' (equal multiple)
        optionalSingle |> should not' (equal multiple)

[<TestFixture>]
type InferedTypeTests() =

    [<Test>]
    member _.``InferedType IsOptional returns correct values``() =
        let primitiveOptional = InferedType.Primitive(typeof<string>, None, true, false)
        let primitiveRequired = InferedType.Primitive(typeof<string>, None, false, false)
        let recordOptional = InferedType.Record(None, [], true)
        let recordRequired = InferedType.Record(None, [], false)
        let jsonOptional = InferedType.Json(InferedType.Primitive(typeof<string>, None, false, false), true)
        let jsonRequired = InferedType.Json(InferedType.Primitive(typeof<string>, None, false, false), false)
        
        primitiveOptional.IsOptional |> should equal true
        primitiveRequired.IsOptional |> should equal false
        recordOptional.IsOptional |> should equal true
        recordRequired.IsOptional |> should equal false
        jsonOptional.IsOptional |> should equal true
        jsonRequired.IsOptional |> should equal false
        InferedType.Null.IsOptional |> should equal false
        InferedType.Top.IsOptional |> should equal false

    [<Test>]
    member _.``InferedType CanHaveEmptyValues works correctly``() =
        InferedType.CanHaveEmptyValues(typeof<string>) |> should equal true
        InferedType.CanHaveEmptyValues(typeof<float>) |> should equal true
        InferedType.CanHaveEmptyValues(typeof<int>) |> should equal false
        InferedType.CanHaveEmptyValues(typeof<bool>) |> should equal false

    [<Test>]
    member _.``InferedType EnsuresHandlesMissingValues with allowEmptyValues true``() =
        let primitiveString = InferedType.Primitive(typeof<string>, None, false, false)
        let primitiveInt = InferedType.Primitive(typeof<int>, None, false, false)
        
        let result1 = primitiveString.EnsuresHandlesMissingValues(true)
        let result2 = primitiveInt.EnsuresHandlesMissingValues(true)
        
        result1 |> should equal primitiveString  // string can have empty values, so no change
        match result2 with
        | InferedType.Primitive(typ, _, true, _) when typ = typeof<int> -> ()
        | _ -> failwith "Expected optional int primitive"

    [<Test>]
    member _.``InferedType EnsuresHandlesMissingValues with allowEmptyValues false``() =
        let primitiveString = InferedType.Primitive(typeof<string>, None, false, false)
        
        let result = primitiveString.EnsuresHandlesMissingValues(false)
        
        match result with
        | InferedType.Primitive(typ, _, true, _) when typ = typeof<string> -> ()
        | _ -> failwith "Expected optional string primitive"

    [<Test>]
    member _.``InferedType EnsuresHandlesMissingValues handles already optional types``() =
        let primitiveOptional = InferedType.Primitive(typeof<string>, None, true, false)
        let recordOptional = InferedType.Record(None, [], true)
        let jsonOptional = InferedType.Json(InferedType.Primitive(typeof<string>, None, false, false), true)
        
        primitiveOptional.EnsuresHandlesMissingValues(false) |> should equal primitiveOptional
        recordOptional.EnsuresHandlesMissingValues(false) |> should equal recordOptional
        jsonOptional.EnsuresHandlesMissingValues(false) |> should equal jsonOptional

    [<Test>]
    member _.``InferedType EnsuresHandlesMissingValues handles Null and Top``() =
        InferedType.Null.EnsuresHandlesMissingValues(false) |> should equal InferedType.Null
        
        (fun () -> InferedType.Top.EnsuresHandlesMissingValues(false) |> ignore)
        |> should throw typeof<System.Exception>

    [<Test>]
    member _.``InferedType GetDropOptionality returns correct values``() =
        let primitiveOptional = InferedType.Primitive(typeof<string>, None, true, false)
        let primitiveRequired = InferedType.Primitive(typeof<string>, None, false, false)
        
        let result1, wasOptional1 = primitiveOptional.GetDropOptionality()
        let result2, wasOptional2 = primitiveRequired.GetDropOptionality()
        
        match result1 with
        | InferedType.Primitive(typ, _, false, _) when typ = typeof<string> -> ()
        | _ -> failwith "Expected non-optional string primitive"
        wasOptional1 |> should equal true
        
        result2 |> should equal primitiveRequired
        wasOptional2 |> should equal false

    [<Test>]
    member _.``InferedType DropOptionality works correctly``() =
        let primitiveOptional = InferedType.Primitive(typeof<string>, None, true, false)
        
        let result = primitiveOptional.DropOptionality()
        
        match result with
        | InferedType.Primitive(typ, _, false, _) when typ = typeof<string> -> ()
        | _ -> failwith "Expected non-optional string primitive"

    [<Test>]
    member _.``InferedType Equals works with reference equality``() =
        let type1 = InferedType.Primitive(typeof<string>, None, false, false)
        let type2 = type1  // same reference
        let type3 = InferedType.Primitive(typeof<string>, None, false, false)  // different reference, same content
        
        type1.Equals(type2) |> should equal true
        type1.Equals(type3) |> should equal true

    [<Test>]
    member _.``InferedType Equals works with structural equality``() =
        let type1 = InferedType.Primitive(typeof<string>, None, false, false)
        let type2 = InferedType.Primitive(typeof<string>, None, false, false)
        let type3 = InferedType.Primitive(typeof<int>, None, false, false)
        
        type1.Equals(type2) |> should equal true
        type1.Equals(type3) |> should equal false

    [<Test>]
    member _.``InferedType Equals with different types returns false``() =
        let inferredType = InferedType.Primitive(typeof<string>, None, false, false)
        let otherObject = "not an InferedType"
        
        inferredType.Equals(otherObject) |> should equal false

    [<Test>]
    member _.``InferedType ToString produces meaningful output``() =
        let primitiveType = InferedType.Primitive(typeof<string>, None, false, false)
        let nullType = InferedType.Null
        let topType = InferedType.Top
        
        primitiveType.ToString() |> should contain "Primitive"
        nullType.ToString() |> should contain "Null"  
        topType.ToString() |> should contain "Top"

[<TestFixture>]
type PrimitiveInferedValueTests() =

    [<Test>]
    member _.``PrimitiveInferedValue Create with TypeWrapper works correctly``() =
        let result = PrimitiveInferedValue.Create(typeof<string>, TypeWrapper.Option, Some(typeof<int>))
        
        result.InferedType |> should equal typeof<string>
        result.RuntimeType |> should equal typeof<string>
        result.UnitOfMeasure |> should equal (Some(typeof<int>))
        result.TypeWrapper |> should equal TypeWrapper.Option

    [<Test>]
    member _.``PrimitiveInferedValue Create with optional boolean works correctly``() =
        let result = PrimitiveInferedValue.Create(typeof<float>, true, None)
        
        result.InferedType |> should equal typeof<float>
        result.RuntimeType |> should equal typeof<float>
        result.UnitOfMeasure |> should equal None
        result.TypeWrapper |> should equal TypeWrapper.Option

    [<Test>]
    member _.``PrimitiveInferedValue Create handles Bit types correctly``() =
        let bitResult = PrimitiveInferedValue.Create(typeof<Bit>, TypeWrapper.None, None)
        let bit0Result = PrimitiveInferedValue.Create(typeof<Bit0>, TypeWrapper.None, None)
        let bit1Result = PrimitiveInferedValue.Create(typeof<Bit1>, TypeWrapper.None, None)
        
        bitResult.InferedType |> should equal typeof<Bit>
        bitResult.RuntimeType |> should equal typeof<bool>
        
        bit0Result.InferedType |> should equal typeof<Bit0>
        bit0Result.RuntimeType |> should equal typeof<int>
        
        bit1Result.InferedType |> should equal typeof<Bit1>
        bit1Result.RuntimeType |> should equal typeof<int>

[<TestFixture>]
type PrimitiveInferedPropertyTests() =

    [<Test>]
    member _.``PrimitiveInferedProperty Create with TypeWrapper works correctly``() =
        let result = PrimitiveInferedProperty.Create("TestProp", typeof<string>, TypeWrapper.Nullable, Some(typeof<float>))
        
        result.Name |> should equal "TestProp"
        result.Value.InferedType |> should equal typeof<string>
        result.Value.TypeWrapper |> should equal TypeWrapper.Nullable
        result.Value.UnitOfMeasure |> should equal (Some(typeof<float>))

    [<Test>]
    member _.``PrimitiveInferedProperty Create with optional boolean works correctly``() =
        let result = PrimitiveInferedProperty.Create("OptionalProp", typeof<int>, false, None)
        
        result.Name |> should equal "OptionalProp"
        result.Value.InferedType |> should equal typeof<int>
        result.Value.TypeWrapper |> should equal TypeWrapper.None
        result.Value.UnitOfMeasure |> should equal None

[<TestFixture>]
type TypeWrapperTests() =

    [<Test>]
    member _.``TypeWrapper FromOption returns correct values``() =
        TypeWrapper.FromOption(true) |> should equal TypeWrapper.Option
        TypeWrapper.FromOption(false) |> should equal TypeWrapper.None

[<TestFixture>]
type BitTypesTests() =

    [<Test>]
    member _.``Bit types are struct types``() =
        typeof<Bit>.IsValueType |> should equal true
        typeof<Bit0>.IsValueType |> should equal true
        typeof<Bit1>.IsValueType |> should equal true

    [<Test>]
    member _.``Bit types equality works``() =
        let bit = Bit.Bit
        let bit0 = Bit0.Bit0
        let bit1 = Bit1.Bit1
        
        bit |> should equal Bit.Bit
        bit0 |> should equal Bit0.Bit0
        bit1 |> should equal Bit1.Bit1
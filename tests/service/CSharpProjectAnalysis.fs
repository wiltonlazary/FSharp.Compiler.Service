﻿
#if INTERACTIVE
#r "../../debug/fcs/net45/FSharp.Compiler.Service.dll" // note, run 'build fcs debug' to generate this, this DLL has a public API so can be used from F# Interactive
#r "../../bin/v4.5/CSharp_Analysis.dll"
#r "../../packages/NUnit.3.5.0/lib/net45/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module FSharp.Compiler.Service.Tests.CSharpProjectAnalysis
#endif


open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Collections.Generic

open Microsoft.FSharp.Compiler
open FSharp.Compiler.Service.Tests
open Microsoft.FSharp.Compiler.SourceCodeServices

open FSharp.Compiler.Service.Tests.Common

#if !FCS // disabled in FCS testing because the CSharp_Analysis.dll is not put in the right place
[<Test>]
#if NETCOREAPP2_0
[<Ignore("SKIPPED: need to check if these tests can be enabled for .NET Core testing of FSharp.Compiler.Service")>]
#endif
let ``Test that csharp references are recognized as such`` () = 
    let csharpAssembly = PathRelativeToTestAssembly "CSharp_Analysis.dll"
    let _, table = getProjectReferences("""module M""", [csharpAssembly])
    let ass = table.["CSharp_Analysis"]
    let search = ass.Contents.Entities |> Seq.tryFind (fun e -> e.DisplayName = "CSharpClass") 
    Assert.True search.IsSome
    let found = search.Value
    // this is no F# thing
    found.IsFSharp |> shouldEqual false
        
    // Check that we have members
    let members = found.MembersFunctionsAndValues |> Seq.map (fun e -> e.CompiledName, e) |> dict
    members.ContainsKey ".ctor" |> shouldEqual true
    members.ContainsKey "Method" |> shouldEqual true
    members.ContainsKey "Property" |> shouldEqual true
    members.ContainsKey "Event" |> shouldEqual true
    members.ContainsKey "InterfaceMethod" |> shouldEqual true
    members.ContainsKey "InterfaceProperty" |> shouldEqual true
    members.ContainsKey "InterfaceEvent" |> shouldEqual true
    members.["Event"].IsEvent |> shouldEqual true
    members.["Event"].EventIsStandard |> shouldEqual true
    members.["Event"].EventAddMethod.DisplayName |> shouldEqual "add_Event"
    members.["Event"].EventRemoveMethod.DisplayName |> shouldEqual "remove_Event"
    members.["Event"].EventDelegateType.ToString() |> shouldEqual "type System.EventHandler"

    //// Check that we get xml docs
    members.[".ctor"].XmlDocSig |> shouldEqual "M:FSharp.Compiler.Service.Tests.CSharpClass.#ctor(System.Int32,System.String)"
    members.["Method"].XmlDocSig |> shouldEqual "M:FSharp.Compiler.Service.Tests.CSharpClass.Method(System.String)"
    members.["Property"].XmlDocSig |> shouldEqual "P:FSharp.Compiler.Service.Tests.CSharpClass.Property"
    members.["Event"].XmlDocSig |> shouldEqual "E:FSharp.Compiler.Service.Tests.CSharpClass.Event"
    members.["InterfaceMethod"].XmlDocSig |> shouldEqual "M:FSharp.Compiler.Service.Tests.CSharpClass.InterfaceMethod(System.String)"
    members.["InterfaceProperty"].XmlDocSig |> shouldEqual "P:FSharp.Compiler.Service.Tests.CSharpClass.InterfaceProperty"
    members.["InterfaceEvent"].XmlDocSig |> shouldEqual "E:FSharp.Compiler.Service.Tests.CSharpClass.InterfaceEvent"

[<Test>]
#if NETCOREAPP2_0
[<Ignore("SKIPPED: need to check if these tests can be enabled for .NET Core testing of FSharp.Compiler.Service")>]
#endif
let ``Test that symbols of csharp inner classes/enums are reported`` () = 
    let csharpAssembly = PathRelativeToTestAssembly "CSharp_Analysis.dll"
    let content = """
module NestedEnumClass
open FSharp.Compiler.Service.Tests

let _ = CSharpOuterClass.InnerEnum.Case1
let _ = CSharpOuterClass.InnerClass.StaticMember()
"""

    let results, _ = getProjectReferences(content, [csharpAssembly])
    results.GetAllUsesOfAllSymbols()
    |> Async.RunSynchronously
    |> Array.map (fun su -> su.Symbol.ToString())
    |> shouldEqual 
          [|"FSharp"; "Compiler"; "Service"; "Tests"; "FSharp"; "InnerEnum";
            "CSharpOuterClass"; "field Case1"; "InnerClass"; "CSharpOuterClass";
            "member StaticMember"; "NestedEnumClass"|]

[<Test>]
#if NETCOREAPP2_0
[<Ignore("SKIPPED: need to check if these tests can be enabled for .NET Core testing of FSharp.Compiler.Service")>]
#endif
let ``Ctor test`` () =
    let csharpAssembly = PathRelativeToTestAssembly "CSharp_Analysis.dll"
    let content = """
module CtorTest
open FSharp.Compiler.Service.Tests

let _ = CSharpClass(0)
"""
    let results, _ = getProjectReferences(content, [csharpAssembly])
    let ctor =
            results.GetAllUsesOfAllSymbols()
            |> Async.RunSynchronously
            |> Seq.map (fun su -> su.Symbol)
            |> Seq.find (function :? FSharpMemberOrFunctionOrValue as mfv -> mfv.IsConstructor | _ -> false)
    match (ctor :?> FSharpMemberOrFunctionOrValue).DeclaringEntity with 
    | Some e ->
        let members = e.MembersFunctionsAndValues
        Seq.exists (fun (mfv : FSharpMemberOrFunctionOrValue) -> mfv.IsConstructor) members |> should be True
        Seq.exists (fun (mfv : FSharpMemberOrFunctionOrValue) -> mfv.IsEffectivelySameAs ctor) members |> should be True
    | None -> failwith "Expected Some for DeclaringEntity"

let getEntitiesUses source =
    let csharpAssembly = PathRelativeToTestAssembly "CSharp_Analysis.dll"
    let results, _ = getProjectReferences(source, [csharpAssembly])
    results.GetAllUsesOfAllSymbols()
    |> Async.RunSynchronously
    |> Seq.choose (fun su ->
        match su.Symbol with
        | :? FSharpEntity as entity -> Some entity
        | _ -> None)
    |> List.ofSeq

[<Test>]
#if NETCOREAPP2_0
[<Ignore("SKIPPED: need to check if these tests can be enabled for .NET Core testing of FSharp.Compiler.Service")>]
#endif
let ``Different types with the same short name equality check`` () =
    let source = """
module CtorTest

let (s1: System.String) = null
let (s2: FSharp.Compiler.Service.Tests.String) = null
"""

    let stringSymbols =
        getEntitiesUses source
        |> List.filter (fun entity -> entity.LogicalName = "String")

    match stringSymbols with
    | e1 :: e2 :: [] -> e1.IsEffectivelySameAs(e2) |> should be False
    | _ -> sprintf "Expecting two symbols, got %A" stringSymbols |> failwith

[<Test>]
#if NETCOREAPP2_0
[<Ignore("SKIPPED: need to check if these tests can be enabled for .NET Core testing of FSharp.Compiler.Service")>]
#endif
let ``Different namespaces with the same short name equality check`` () =
    let source = """
module CtorTest

open System.Linq
open FSharp.Compiler.Service.Tests.Linq
"""

    let stringSymbols =
        getEntitiesUses source
        |> List.filter (fun entity -> entity.LogicalName = "Linq")

    match stringSymbols with
    | e1 :: e2 :: [] -> e1.IsEffectivelySameAs(e2) |> should be False
    | _ -> sprintf "Expecting two symbols, got %A" stringSymbols |> failwith

#endif
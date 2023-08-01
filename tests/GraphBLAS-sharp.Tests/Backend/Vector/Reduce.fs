module GraphBLAS.FSharp.Tests.Backend.Vector.Reduce

open Expecto
open Expecto.Logging
open Brahma.FSharp
open GraphBLAS.FSharp
open GraphBLAS.FSharp.Backend
open GraphBLAS.FSharp.Tests
open TestCases
open GraphBLAS.FSharp.Objects.ClCellExtensions

let logger = Log.create "Vector.reduce.Tests"

let wgSize = Utils.defaultWorkGroupSize

let config = Utils.defaultConfig

let checkResult zero op (actual: 'a) (vector: 'a[]) =
    let expected = Array.fold op zero vector

    "Results should be the same" |> Expect.equal actual expected

let correctnessGenericTest isEqual zero op reduce case (array: 'a[]) =

    let vector = Utils.createVectorFromArray case.Format array (isEqual zero)

    if vector.NNZ > 0 then
        let q = case.TestContext.Queue
        let context = case.TestContext.ClContext

        let clVector = vector.ToDevice context

        let result = (reduce q clVector: ClCell<_>).ToHostAndFree q

        checkResult zero op result array

let createTest<'a when 'a: equality and 'a: struct> case isEqual (zero: 'a) plus plusQ name =
    let context = case.TestContext.ClContext

    let reduce = Vector.reduce plusQ context wgSize

    case
    |> correctnessGenericTest isEqual zero plus reduce
    |> testPropertyWithConfig config $"Correctness on %A{typeof<'a>}, %s{name} %A{case.Format}"


let testFixtures case =

    let context = case.TestContext.ClContext
    let q = case.TestContext.Queue

    q.Error.Add(fun e -> failwithf "%A" e)

    [ createTest<int> case (=) 0 (+) <@ (+) @> "add"
      createTest<byte> case (=) 0uy (+) <@ (+) @> "add"
      createTest<int> case (=) System.Int32.MinValue max <@ max @> "max"

      if Utils.isFloat64Available context.ClDevice then
          createTest case Utils.floatIsEqual System.Double.MinValue max <@ max @> "max"

      createTest<float32> case Utils.float32IsEqual System.Single.MinValue max <@ max @> "max"
      createTest<byte> case (=) System.Byte.MinValue max <@ max @> "max"
      createTest<int> case (=) System.Int32.MaxValue min <@ min @> "min"

      if Utils.isFloat64Available context.ClDevice then
          createTest case Utils.floatIsEqual System.Double.MaxValue min <@ min @> "min"

      createTest<float32> case Utils.float32IsEqual System.Single.MaxValue min <@ min @> "min"
      createTest<byte> case (=) System.Byte.MaxValue min <@ min @> "min"
      createTest<bool> case (=) false (||) <@ (||) @> "add"
      createTest<bool> case (=) true (&&) <@ (&&) @> "multiply" ]

let tests = operationGPUTests "Reduce tests" testFixtures

module GraphBLAS.FSharp.Tests.Backend.Common.ClArray.Concat

open Expecto
open Brahma.FSharp
open GraphBLAS.FSharp
open GraphBLAS.FSharp.Tests
open GraphBLAS.FSharp.Objects.ArraysExtensions
open GraphBLAS.FSharp.Objects.ClContextExtensions

let context = Context.defaultContext.ClContext

let processor = Context.defaultContext.Queue

let config = Utils.defaultConfig

let makeTest<'a> isEqual testFun (arrays: 'a[][]) =

    if Seq.length arrays > 0 && arrays |> Seq.forall (fun array -> array.Length > 0) then

        let clArrays = arrays |> Seq.map context.CreateClArray

        let clActual: ClArray<'a> = testFun processor HostInterop clArrays

        // release
        let actual = clActual.ToHostAndFree processor

        clArrays |> Seq.iter (fun array -> array.Free processor)

        let expected = Seq.concat arrays |> Seq.toArray

        "Results must be the same" |> Utils.compareArrays isEqual actual expected

let createTest<'a> isEqual =
    ClArray.concat context Utils.defaultWorkGroupSize
    |> makeTest<'a> isEqual
    |> testPropertyWithConfig config $"test on %A{typeof<'a>}"

let tests =
    [ createTest<int> (=)

      if Utils.isFloat64Available context.ClDevice then
          createTest<float> Utils.floatIsEqual

      createTest<float32> Utils.float32IsEqual
      createTest<bool> (=) ]
    |> testList "Concat"

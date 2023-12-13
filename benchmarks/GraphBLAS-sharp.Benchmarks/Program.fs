open GraphBLAS.FSharp.Benchmarks
open BenchmarkDotNet.Running

[<EntryPoint>]
let main argv =
    let benchmarks =
        BenchmarkSwitcher [| typeof<Algorithms.MSBFS.BFSWithoutTransferBenchmarkInt32> |]

    benchmarks.Run argv |> ignore
    0

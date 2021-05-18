namespace GraphBLAS.FSharp.Benchmarks

open GraphBLAS.FSharp
open GraphBLAS.FSharp.Algorithms
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Columns
open System.IO
open System
open System.Text.RegularExpressions
open Brahma.FSharp.OpenCL.WorkflowBuilder.Evaluation
open OpenCL.Net
open GraphBLAS.FSharp.IO
open QuickGraph

type BFSConfig() =
    inherit ManualConfig()

    do
        base.AddColumn(
            MatrixShapeColumn("RowCount", fun mtxReader -> mtxReader.ReadMatrixShape().RowCount) :> IColumn,
            MatrixShapeColumn("ColumnCount", fun mtxReader -> mtxReader.ReadMatrixShape().ColumnCount) :> IColumn,
            MatrixShapeColumn("NNZ", fun mtxReader -> mtxReader.ReadMatrixShape().Nnz) :> IColumn,
            TEPSColumn() :> IColumn,
            StatisticColumn.Min,
            StatisticColumn.Max
        ) |> ignore

[<IterationCount(5)>]
[<WarmupCount(3)>]
[<Config(typeof<BFSConfig>)>]
type BFSBenchmarks() =
    let random = Random()

    let mutable source = 0

    // gb
    let mutable matrix = Unchecked.defaultof<Matrix<int>>

    // qg
    let graph = AdjacencyGraph<int, Edge<int>>(false)
    let mutable bfs = Unchecked.defaultof<Algorithms.Search.BreadthFirstSearchAlgorithm<int, Edge<int>>>

    [<ParamsSource("AvaliableContextsProvider")>]
    member val OclContext = Unchecked.defaultof<ClContext> with get, set
    member this.Context =
        let (ClContext context) = this.OclContext
        context

    [<ParamsSource("InputMatricesProvider")>]
    member val InputMatrixReader = Unchecked.defaultof<MtxReader> with get, set

    [<GlobalSetup>]
    member this.BuildGraph() =
        let inputMatrix = this.InputMatrixReader.ReadMatrix(fun _ -> 1)

        matrix <-
            graphblas {
                return! Matrix.switch CSR inputMatrix
                >>= Matrix.synchronizeAndReturn
            }
            |> EvalGB.withClContext this.Context
            |> EvalGB.runSync

        match inputMatrix with
        | MatrixCSR csr -> failwith "Not implemented"
        | MatrixCOO coo ->
            for i = 0 to coo.Values.Length - 1 do
                graph.AddVerticesAndEdge(Edge(coo.Rows.[i], coo.Columns.[i])) |> ignore

        bfs <- Algorithms.Search.BreadthFirstSearchAlgorithm(graph)

    [<IterationSetup>]
    member this.SetSource() =
        source <- random.Next <| Matrix.rowCount matrix

    [<Benchmark>]
    member this.GraphblasLevelBFS() =
        BFS.levelSingleSource matrix source
        |> EvalGB.withClContext this.Context
        |> EvalGB.runSync

    [<Benchmark>]
    member this.QuickGraphBFS() =
        bfs.Compute(source)

    [<IterationCleanup>]
    member this.ClearBuffers() =
        this.Context.Provider.CloseAllBuffers()

    [<GlobalCleanup>]
    member this.ClearContext() =
        let (ClContext context) = this.OclContext
        context.Provider.Dispose()

    static member AvaliableContextsProvider =
        let pathToConfig =
            Path.Combine [|
                __SOURCE_DIRECTORY__
                "Configs"
                "Context.txt"
            |] |> Path.GetFullPath

        use reader = new StreamReader(pathToConfig)
        let platformRegex = Regex <| reader.ReadLine()
        let deviceType =
            match reader.ReadLine() with
            | "Cpu" -> DeviceType.Cpu
            | "Gpu" -> DeviceType.Gpu
            | "All" -> DeviceType.All
            | _ -> failwith "Unsupported"

        let mutable e = ErrorCode.Unknown
        Cl.GetPlatformIDs &e
        |> Array.collect (fun platform -> Cl.GetDeviceIDs(platform, deviceType, &e))
        |> Seq.ofArray
        |> Seq.distinctBy (fun device -> Cl.GetDeviceInfo(device, DeviceInfo.Name, &e).ToString())
        |> Seq.filter
            (fun device ->
                let platform = Cl.GetDeviceInfo(device, DeviceInfo.Platform, &e).CastTo<Platform>()
                let platformName = Cl.GetPlatformInfo(platform, PlatformInfo.Name, &e).ToString()
                platformRegex.IsMatch platformName
            )
        |> Seq.map
            (fun device ->
                let platform = Cl.GetDeviceInfo(device, DeviceInfo.Platform, &e).CastTo<Platform>()
                let platformName = Cl.GetPlatformInfo(platform, PlatformInfo.Name, &e).ToString()
                let deviceType = Cl.GetDeviceInfo(device, DeviceInfo.Type, &e).CastTo<DeviceType>()
                OpenCLEvaluationContext(platformName, deviceType) |> ClContext
            )

    static member InputMatricesProvider =
        "BFSBenchmarks.txt"
        |> Utils.getMatricesFilenames
        |> Seq.map
            (fun matrixFilename ->
                match Path.GetExtension matrixFilename with
                | ".mtx" -> MtxReader(Utils.getFullPathToMatrix "BFSDatasets" matrixFilename)
                | _ -> failwith "Unsupported matrix format"
            )

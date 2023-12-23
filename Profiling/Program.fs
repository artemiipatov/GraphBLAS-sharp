open GraphBLAS.FSharp
open GraphBLAS.FSharp.IO
open GraphBLAS.FSharp.Benchmarks
open GraphBLAS.FSharp.Tests
open GraphBLAS.FSharp.Backend.Quotes
open GraphBLAS.FSharp.Objects
open GraphBLAS.FSharp.Objects.MatrixExtensions

let workGroupSize = 32
let clContext = Context.defaultContext.ClContext
let queue = Context.defaultContext.Queue
let msbfs = Algorithms.MSBFS.runLevels (fst ArithmeticOperations.intAdd) (fst ArithmeticOperations.intMul) clContext 32

let matrixName = "/home/katalan/dev/main/GraphBLAS-sharp/Profiling/Datasets/roadNet-CA.mtx"

let matrixReader = MtxReader matrixName

let binaryConverter = fun _ -> Utils.nextInt (System.Random())

let stringConverter = int32

let converter =
    match matrixReader.Field with
    | Pattern -> binaryConverter
    | _ -> stringConverter

let matrix = (matrixReader.ReadMatrix converter)
                 .ToCSR.ToDevice clContext
             |> ClMatrix.CSR

let vertices = List.init 10 id

for i in 0 .. 50 do
    let result = msbfs queue matrix vertices
    printfn $"%A{result.NNZ}"
    result.Dispose queue


matrix.Dispose queue

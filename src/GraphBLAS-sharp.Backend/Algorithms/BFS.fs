namespace GraphBLAS.FSharp.Backend.Algorithms

open Brahma.FSharp
open FSharp.Quotations
open GraphBLAS.FSharp
open GraphBLAS.FSharp.Objects
open GraphBLAS.FSharp.Backend.Quotes
open GraphBLAS.FSharp.Backend.Vector.Dense
open GraphBLAS.FSharp.Objects.ClContextExtensions
open GraphBLAS.FSharp.Objects.ArraysExtensions
open GraphBLAS.FSharp.Objects.ClCellExtensions

module internal BFS =
    let singleSource
        (add: Expr<int option -> int option -> int option>)
        (mul: Expr<'a option -> int option -> int option>)
        (clContext: ClContext)
        workGroupSize
        =

        let spMVTo = Operations.SpMVInplace add mul clContext workGroupSize

        let zeroCreate = ClArray.zeroCreate clContext workGroupSize

        let ofList = Vector.ofList clContext workGroupSize

        let maskComplementedTo =
            Vector.map2InPlace Mask.complementedOp clContext workGroupSize

        let fillSubVectorTo =
            Vector.assignByMaskInPlace (Convert.assignToOption Mask.assign) clContext workGroupSize

        let containsNonZero = ClArray.exists Predicates.isSome clContext workGroupSize

        fun (queue: MailboxProcessor<Msg>) (matrix: ClMatrix<'a>) (source: int) ->
            let vertexCount = matrix.RowCount

            let levels = zeroCreate queue HostInterop vertexCount

            let frontier = ofList queue DeviceOnly Dense vertexCount [ source, 1 ]

            match frontier with
            | ClVector.Dense front ->

                let mutable level = 0
                let mutable stop = false

                while not stop do
                    level <- level + 1

                    //Assigning new level values
                    fillSubVectorTo queue levels front (clContext.CreateClCell level) levels

                    //Getting new frontier
                    spMVTo queue matrix frontier frontier

                    maskComplementedTo queue front levels front

                    //Checking if front is empty
                    stop <- not <| (containsNonZero queue front).ToHostAndFree queue

                front.Free queue

                levels
            | _ -> failwith "Not implemented"

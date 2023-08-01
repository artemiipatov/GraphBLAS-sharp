namespace GraphBLAS.FSharp.Backend.Matrix.COO

open Brahma.FSharp
open GraphBLAS.FSharp
open GraphBLAS.FSharp.Backend.Quotes
open Microsoft.FSharp.Quotations
open GraphBLAS.FSharp.Objects
open GraphBLAS.FSharp.Objects.ClMatrix
open GraphBLAS.FSharp.Objects.ClCellExtensions
open GraphBLAS.FSharp.Objects.ArraysExtensions

module Matrix =
    /// <summary>
    /// Builds a new COO matrix whose elements are the results of applying the given function
    /// to each of the elements of the matrix.
    /// </summary>
    /// <param name="op">
    /// A function to transform values of the input matrix.
    /// Operand and result types should be optional to distinguish explicit and implicit zeroes
    /// </param>
    /// <param name="clContext">OpenCL context.</param>
    /// <param name="workGroupSize">Should be a power of 2 and greater than 1.</param>
    let map = Map.run

    /// <summary>
    /// Builds a new COO matrix whose values are the results of applying the given function
    /// to the corresponding pairs of values from the two matrices.
    /// </summary>
    /// <param name="op">
    /// A function to transform pairs of values from the input matrices.
    /// Operands and result types should be optional to distinguish explicit and implicit zeroes
    /// </param>
    /// <param name="clContext">OpenCL context.</param>
    /// <param name="workGroupSize">Should be a power of 2 and greater than 1.</param>
    let map2 = Map2.run

    /// <summary>
    /// Builds a new COO matrix whose values are the results of applying the given function
    /// to the corresponding pairs of values from the two matrices.
    /// </summary>
    /// <param name="op">
    /// A function to transform pairs of values from the input matrices.
    /// Operation assumption: one of the operands should always be non-zero.
    /// </param>
    /// <param name="clContext">OpenCL context.</param>
    /// <param name="workGroupSize">Should be a power of 2 and greater than 1.</param>
    let rec map2AtLeastOne<'a, 'b, 'c when 'a: struct and 'b: struct and 'c: struct and 'c: equality>
        (clContext: ClContext)
        (opAdd: Expr<AtLeastOne<'a, 'b> -> 'c option>)
        workGroupSize
        =

        Map2.AtLeastOne.run clContext (Convert.atLeastOneToOption opAdd) workGroupSize

    /// <summary>
    /// Converts <c>COO</c> matrix format to <c>Tuple</c>.
    /// </summary>
    /// <param name="clContext">OpenCL context.</param>
    /// <param name="workGroupSize">Should be a power of 2 and greater than 1.</param>
    let getTuples (clContext: ClContext) workGroupSize =

        let copy = ClArray.copy clContext workGroupSize

        let copyData = ClArray.copy clContext workGroupSize

        fun (processor: MailboxProcessor<_>) allocationMode (matrix: ClMatrix.COO<'a>) ->

            let resultRows = copy processor allocationMode matrix.Rows

            let resultColumns = copy processor allocationMode matrix.Columns

            let resultValues = copyData processor allocationMode matrix.Values

            { Context = clContext
              RowIndices = resultRows
              ColumnIndices = resultColumns
              Values = resultValues }

    /// <summary>
    /// Converts rows of given COO matrix to rows in CSR format of the same matrix.
    /// </summary>
    /// <param name="clContext">OpenCL context.</param>
    /// <param name="workGroupSize">Should be a power of 2 and greater than 1.</param>
    let private compressRows (clContext: ClContext) workGroupSize =

        let compressRows =
            <@
                fun (ndRange: Range1D) (rows: ClArray<int>) (nnz: int) (rowPointers: ClArray<int>) ->

                    let i = ndRange.GlobalID0

                    if i < nnz then
                        let row = rows.[i]

                        if i = 0 || row <> rows.[i - 1] then
                            rowPointers.[row] <- i
            @>

        let program = clContext.Compile(compressRows)

        let create = ClArray.create clContext workGroupSize

        let scan =
            Common.PrefixSum.runBackwardsIncludeInPlace <@ min @> clContext workGroupSize

        fun (processor: MailboxProcessor<_>) allocationMode (rowIndices: ClArray<int>) rowCount ->

            let nnz = rowIndices.Length

            let rowPointers = create processor allocationMode (rowCount + 1) nnz

            let kernel = program.GetKernel()

            let ndRange = Range1D.CreateValid(nnz, workGroupSize)

            processor.Post(Msg.MsgSetArguments(fun () -> kernel.KernelFunc ndRange rowIndices nnz rowPointers))

            processor.Post(Msg.CreateRunMsg<_, _> kernel)

            (scan processor rowPointers nnz).Free processor

            rowPointers

    /// <summary>
    /// Converts the given COO matrix to CSR format.
    /// Values and columns are copied and do not depend on input COO matrix anymore.
    /// </summary>
    /// <param name="clContext">OpenCL context.</param>
    /// <param name="workGroupSize">Should be a power of 2 and greater than 1.</param>
    let toCSR (clContext: ClContext) workGroupSize =
        let prepare = compressRows clContext workGroupSize

        let copy = ClArray.copy clContext workGroupSize

        let copyData = ClArray.copy clContext workGroupSize

        fun (processor: MailboxProcessor<_>) allocationMode (matrix: ClMatrix.COO<'a>) ->
            let rowPointers = prepare processor allocationMode matrix.Rows matrix.RowCount

            let cols = copy processor allocationMode matrix.Columns

            let values = copyData processor allocationMode matrix.Values

            { Context = clContext
              RowCount = matrix.RowCount
              ColumnCount = matrix.ColumnCount
              RowPointers = rowPointers
              Columns = cols
              Values = values }

    /// <summary>
    /// Converts the given COO matrix to CSR format.
    /// Values and columns are NOT copied and still depend on the input COO matrix.
    /// </summary>
    /// <param name="clContext">OpenCL context.</param>
    /// <param name="workGroupSize">Should be a power of 2 and greater than 1.</param>
    let toCSRInPlace (clContext: ClContext) workGroupSize =
        let prepare = compressRows clContext workGroupSize

        fun (processor: MailboxProcessor<_>) allocationMode (matrix: ClMatrix.COO<'a>) ->
            let rowPointers = prepare processor allocationMode matrix.Rows matrix.RowCount

            matrix.Rows.Free processor

            { Context = clContext
              RowCount = matrix.RowCount
              ColumnCount = matrix.ColumnCount
              RowPointers = rowPointers
              Columns = matrix.Columns
              Values = matrix.Values }

    /// <summary>
    /// Transposes the given matrix and returns result.
    /// The given matrix should neither be used afterwards nor be disposed.
    /// </summary>
    /// <param name="clContext">OpenCL context.</param>
    /// <param name="workGroupSize">Should be a power of 2 and greater than 1.</param>
    let transposeInPlace (clContext: ClContext) workGroupSize =

        let sort = Common.Sort.Bitonic.sortKeyValuesInplace clContext workGroupSize

        fun (queue: MailboxProcessor<_>) (matrix: ClMatrix.COO<'a>) ->
            sort queue matrix.Columns matrix.Rows matrix.Values

            { Context = clContext
              RowCount = matrix.ColumnCount
              ColumnCount = matrix.RowCount
              Rows = matrix.Columns
              Columns = matrix.Rows
              Values = matrix.Values }

    /// <summary>
    /// Transposes the given matrix and returns result as a new matrix.
    /// </summary>
    ///<param name="clContext">OpenCL context.</param>
    ///<param name="workGroupSize">Should be a power of 2 and greater than 1.</param>
    let transpose (clContext: ClContext) workGroupSize =

        let transposeInPlace = transposeInPlace clContext workGroupSize

        let copy = ClArray.copy clContext workGroupSize

        let copyData = ClArray.copy clContext workGroupSize

        fun (queue: MailboxProcessor<_>) allocationMode (matrix: ClMatrix.COO<'a>) ->

            { Context = clContext
              RowCount = matrix.RowCount
              ColumnCount = matrix.ColumnCount
              Rows = copy queue allocationMode matrix.Rows
              Columns = copy queue allocationMode matrix.Columns
              Values = copyData queue allocationMode matrix.Values }
            |> transposeInPlace queue

﻿namespace GraphBLAS.FSharp.Backend.Objects

open Brahma.FSharp

module ArraysExtensions =

    type ClArray<'a> with
        member this.Dispose(q: MailboxProcessor<Msg>) =
            q.Post(Msg.CreateFreeMsg this)
            q.PostAndReply(Msg.MsgNotifyMe)

        member this.ToHost(q: MailboxProcessor<Msg>) =
            let dst = Array.zeroCreate this.Length
            q.PostAndReply(fun ch -> Msg.CreateToHostMsg(this, dst, ch))

        member this.Size = this.Length

    type 'a ``[]`` with
        member this.Size = this.Length

        member this.ToDevice(context: ClContext) = context.CreateClArray this

    let DenseVectorToString (array: 'a []) =
        [ sprintf "Dense Vector\n"
          sprintf "Size:    %i \n" array.Length
          sprintf "Values:  %A \n" array ]
        |> String.concat ""

    let DenseVectorFromArray (array: 'a [], isZero: 'a -> bool) =
        array
        |> Array.map (fun v -> if isZero v then None else Some v)

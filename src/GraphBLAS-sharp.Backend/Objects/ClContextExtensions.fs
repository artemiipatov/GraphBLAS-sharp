namespace GraphBLAS.FSharp.Objects

open Brahma.FSharp

module ClContextExtensions =
    type AllocationFlag =
        | DeviceOnly
        | HostInterop

    type ClContext with

        member this.CreateClArrayWithSpecificAllocationMode(mode, size: int) =
            match mode with
            | DeviceOnly ->
                this.CreateClArray(
                    size,
                    deviceAccessMode = DeviceAccessMode.ReadWrite,
                    hostAccessMode = HostAccessMode.NotAccessible,
                    allocationMode = AllocationMode.Default
                )
            | HostInterop ->
                this.CreateClArray(
                    size,
                    deviceAccessMode = DeviceAccessMode.ReadWrite,
                    hostAccessMode = HostAccessMode.ReadWrite,
                    allocationMode = AllocationMode.Default
                )

        member this.CreateClArrayWithSpecificAllocationMode(mode, array: 'a[]) =
            match mode with
            | DeviceOnly ->
                this.CreateClArray(
                    array,
                    deviceAccessMode = DeviceAccessMode.ReadWrite,
                    hostAccessMode = HostAccessMode.NotAccessible,
                    allocationMode = AllocationMode.CopyHostPtr
                )
            | HostInterop ->
                this.CreateClArray(
                    array,
                    deviceAccessMode = DeviceAccessMode.ReadWrite,
                    hostAccessMode = HostAccessMode.ReadWrite,
                    allocationMode = AllocationMode.CopyHostPtr
                )

        member this.MaxMemAllocSize =
            let error = ref Unchecked.defaultof<ClErrorCode>

            Cl
                .GetDeviceInfo(this.ClDevice.Device, OpenCL.Net.DeviceInfo.MaxMemAllocSize, error)
                .CastTo<uint64>()
            |> uint64
            |> ((*) 1UL<Byte>)

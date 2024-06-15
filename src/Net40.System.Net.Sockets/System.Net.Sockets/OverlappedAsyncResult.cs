using System.Collections.Generic;
using System.Net.Sockets.Net40;
using System.Runtime.InteropServices;

namespace System.Net.Sockets;

internal class OverlappedAsyncResult : BaseOverlappedAsyncResult
{
    private Internals.SocketAddress _socketAddress;

    internal WSABuffer _singleBuffer;

    internal WSABuffer[] _wsaBuffers;

    internal Internals.SocketAddress SocketAddress => _socketAddress;

    internal OverlappedAsyncResult(Net40.Socket socket, object asyncState, AsyncCallback asyncCallback)
        : base(socket, asyncState, asyncCallback)
    {
    }

    internal IntPtr GetSocketAddressPtr()
    {
        return Marshal.UnsafeAddrOfPinnedArrayElement(_socketAddress.Buffer, 0);
    }

    internal IntPtr GetSocketAddressSizePtr()
    {
        return Marshal.UnsafeAddrOfPinnedArrayElement(_socketAddress.Buffer, _socketAddress.GetAddressSizeOffset());
    }

    internal unsafe int GetSocketAddressSize()
    {
        return *(int*)(void*)GetSocketAddressSizePtr();
    }

    internal void SetUnmanagedStructures(byte[] buffer, int offset, int size, Internals.SocketAddress socketAddress)
    {
        _socketAddress = socketAddress;
        if (_socketAddress != null)
        {
            object[] array = null;
            array = new object[2] { buffer, null };
            _socketAddress.CopyAddressSizeIntoBuffer();
            array[1] = _socketAddress.Buffer;
            SetUnmanagedStructures(array);
        }
        else
        {
            SetUnmanagedStructures(buffer);
        }

        _singleBuffer.Length = size;
        _singleBuffer.Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, offset);
    }

    internal void SetUnmanagedStructures(IList<ArraySegment<byte>> buffers)
    {
        int count = buffers.Count;
        ArraySegment<byte>[] array = new ArraySegment<byte>[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = buffers[i];
            RangeValidationHelpers.ValidateSegment(array[i]);
        }

        _wsaBuffers = new WSABuffer[count];
        object[] array2 = new object[count];
        for (int j = 0; j < count; j++)
        {
            array2[j] = array[j].Array;
        }

        SetUnmanagedStructures(array2);
        for (int k = 0; k < count; k++)
        {
            _wsaBuffers[k].Length = array[k].Count;
            _wsaBuffers[k].Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(array[k].Array, array[k].Offset);
        }
    }

    internal override object PostCompletion(int numBytes)
    {
        if (ErrorCode == 0 && NetEventSource.IsEnabled)
        {
            LogBuffer(numBytes);
        }

        return base.PostCompletion(numBytes);
    }

    private void LogBuffer(int size)
    {
        if (size <= -1)
        {
            return;
        }

        if (_wsaBuffers != null)
        {
            WSABuffer[] wsaBuffers = _wsaBuffers;
            for (int i = 0; i < wsaBuffers.Length; i++)
            {
                WSABuffer wSABuffer = wsaBuffers[i];
                NetEventSource.DumpBuffer(this, wSABuffer.Pointer, Math.Min(wSABuffer.Length, size));
                if ((size -= wSABuffer.Length) <= 0)
                {
                    break;
                }
            }
        }
        else
        {
            NetEventSource.DumpBuffer(this, _singleBuffer.Pointer, Math.Min(_singleBuffer.Length, size));
        }
    }
}
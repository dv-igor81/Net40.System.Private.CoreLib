using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Net.Sockets.Net40;

internal sealed class DynamicWinsockMethods
{
    private static List<DynamicWinsockMethods> s_methodTable = new List<DynamicWinsockMethods>();

    private Net40.AddressFamily _addressFamily;

    private SocketType _socketType;

    private ProtocolType _protocolType;

    private object _lockObject;

    private AcceptExDelegate _acceptEx;

    private GetAcceptExSockaddrsDelegate _getAcceptExSockaddrs;

    private ConnectExDelegate _connectEx;

    private TransmitPacketsDelegate _transmitPackets;

    private DisconnectExDelegate _disconnectEx;

    private DisconnectExDelegateBlocking _disconnectExBlocking;

    private WSARecvMsgDelegate _recvMsg;

    private WSARecvMsgDelegateBlocking _recvMsgBlocking;

    public static DynamicWinsockMethods GetMethods(Net40.AddressFamily addressFamily, SocketType socketType,
        ProtocolType protocolType)
    {
        lock (s_methodTable)
        {
            DynamicWinsockMethods dynamicWinsockMethods;
            for (int i = 0; i < s_methodTable.Count; i++)
            {
                dynamicWinsockMethods = s_methodTable[i];
                if (dynamicWinsockMethods._addressFamily == addressFamily &&
                    dynamicWinsockMethods._socketType == socketType &&
                    dynamicWinsockMethods._protocolType == protocolType)
                {
                    return dynamicWinsockMethods;
                }
            }

            dynamicWinsockMethods = new DynamicWinsockMethods(addressFamily, socketType, protocolType);
            s_methodTable.Add(dynamicWinsockMethods);
            return dynamicWinsockMethods;
        }
    }

    private DynamicWinsockMethods(Net40.AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
    {
        _addressFamily = addressFamily;
        _socketType = socketType;
        _protocolType = protocolType;
        _lockObject = new object();
    }

    public T GetDelegate<T>(SafeSocketHandle socketHandle) where T : class
    {
        if (typeof(T) == typeof(AcceptExDelegate))
        {
            EnsureAcceptEx(socketHandle);
            return (T)(object)_acceptEx;
        }

        if (typeof(T) == typeof(GetAcceptExSockaddrsDelegate))
        {
            EnsureGetAcceptExSockaddrs(socketHandle);
            return (T)(object)_getAcceptExSockaddrs;
        }

        if (typeof(T) == typeof(ConnectExDelegate))
        {
            EnsureConnectEx(socketHandle);
            return (T)(object)_connectEx;
        }

        if (typeof(T) == typeof(DisconnectExDelegate))
        {
            EnsureDisconnectEx(socketHandle);
            return (T)(object)_disconnectEx;
        }

        if (typeof(T) == typeof(DisconnectExDelegateBlocking))
        {
            EnsureDisconnectEx(socketHandle);
            return (T)(object)_disconnectExBlocking;
        }

        if (typeof(T) == typeof(WSARecvMsgDelegate))
        {
            EnsureWSARecvMsg(socketHandle);
            return (T)(object)_recvMsg;
        }

        if (typeof(T) == typeof(WSARecvMsgDelegateBlocking))
        {
            EnsureWSARecvMsgBlocking(socketHandle);
            return (T)(object)_recvMsgBlocking;
        }

        if (typeof(T) == typeof(TransmitPacketsDelegate))
        {
            EnsureTransmitPackets(socketHandle);
            return (T)(object)_transmitPackets;
        }

        return null;
    }

    private unsafe IntPtr LoadDynamicFunctionPointer(SafeSocketHandle socketHandle, ref Guid guid)
    {
        IntPtr funcPtr = IntPtr.Zero;
        if (Interop.Winsock.WSAIoctl(socketHandle, -939524090, ref guid, sizeof(Guid), out funcPtr,
                sizeof(IntPtr), out var _, IntPtr.Zero, IntPtr.Zero) != 0)
        {
            throw new SocketException();
        }

        return funcPtr;
    }

    private void EnsureAcceptEx(SafeSocketHandle socketHandle)
    {
        if (_acceptEx != null)
        {
            return;
        }

        lock (_lockObject)
        {
            if (_acceptEx == null)
            {
                Guid guid = new Guid("{0xb5367df1,0xcbac,0x11cf,{0x95, 0xca, 0x00, 0x80, 0x5f, 0x48, 0xa1, 0x92}}");
                IntPtr ptr = LoadDynamicFunctionPointer(socketHandle, ref guid);
                Volatile.Write(ref _acceptEx, MarshalEx.GetDelegateForFunctionPointer<AcceptExDelegate>(ptr));
            }
        }
    }

    private void EnsureGetAcceptExSockaddrs(SafeSocketHandle socketHandle)
    {
        if (_getAcceptExSockaddrs != null)
        {
            return;
        }

        lock (_lockObject)
        {
            if (_getAcceptExSockaddrs == null)
            {
                Guid guid = new Guid("{0xb5367df2,0xcbac,0x11cf,{0x95, 0xca, 0x00, 0x80, 0x5f, 0x48, 0xa1, 0x92}}");
                IntPtr ptr = LoadDynamicFunctionPointer(socketHandle, ref guid);
                Volatile.Write(ref _getAcceptExSockaddrs,
                    MarshalEx.GetDelegateForFunctionPointer<GetAcceptExSockaddrsDelegate>(ptr));
            }
        }
    }

    private void EnsureConnectEx(SafeSocketHandle socketHandle)
    {
        if (_connectEx != null)
        {
            return;
        }

        lock (_lockObject)
        {
            if (_connectEx == null)
            {
                Guid guid = new Guid("{0x25a207b9,0x0ddf3,0x4660,{0x8e,0xe9,0x76,0xe5,0x8c,0x74,0x06,0x3e}}");
                IntPtr ptr = LoadDynamicFunctionPointer(socketHandle, ref guid);
                Volatile.Write(ref _connectEx, MarshalEx.GetDelegateForFunctionPointer<ConnectExDelegate>(ptr));
            }
        }
    }

    private void EnsureDisconnectEx(SafeSocketHandle socketHandle)
    {
        if (_disconnectEx != null)
        {
            return;
        }

        lock (_lockObject)
        {
            if (_disconnectEx == null)
            {
                Guid guid = new Guid("{0x7fda2e11,0x8630,0x436f,{0xa0, 0x31, 0xf5, 0x36, 0xa6, 0xee, 0xc1, 0x57}}");
                IntPtr ptr = LoadDynamicFunctionPointer(socketHandle, ref guid);
                _disconnectExBlocking = MarshalEx.GetDelegateForFunctionPointer<DisconnectExDelegateBlocking>(ptr);
                Volatile.Write(ref _disconnectEx, MarshalEx.GetDelegateForFunctionPointer<DisconnectExDelegate>(ptr));
            }
        }
    }

    private void EnsureWSARecvMsg(SafeSocketHandle socketHandle)
    {
        if (_recvMsg != null)
        {
            return;
        }

        lock (_lockObject)
        {
            if (_recvMsg == null)
            {
                Guid guid = new Guid("{0xf689d7c8,0x6f1f,0x436b,{0x8a,0x53,0xe5,0x4f,0xe3,0x51,0xc3,0x22}}");
                IntPtr ptr = LoadDynamicFunctionPointer(socketHandle, ref guid);
                _recvMsgBlocking = MarshalEx.GetDelegateForFunctionPointer<WSARecvMsgDelegateBlocking>(ptr);
                Volatile.Write(ref _recvMsg, MarshalEx.GetDelegateForFunctionPointer<WSARecvMsgDelegate>(ptr));
            }
        }
    }

    private void EnsureWSARecvMsgBlocking(SafeSocketHandle socketHandle)
    {
        if (_recvMsgBlocking != null)
        {
            return;
        }

        lock (_lockObject)
        {
            if (_recvMsgBlocking == null)
            {
                Guid guid = new Guid("{0xf689d7c8,0x6f1f,0x436b,{0x8a,0x53,0xe5,0x4f,0xe3,0x51,0xc3,0x22}}");
                IntPtr ptr = LoadDynamicFunctionPointer(socketHandle, ref guid);
                Volatile.Write(ref _recvMsgBlocking,
                    MarshalEx.GetDelegateForFunctionPointer<WSARecvMsgDelegateBlocking>(ptr));
            }
        }
    }

    private void EnsureTransmitPackets(SafeSocketHandle socketHandle)
    {
        if (_transmitPackets != null)
        {
            return;
        }

        lock (_lockObject)
        {
            if (_transmitPackets == null)
            {
                Guid guid = new Guid("{0xd9689da0,0x1f90,0x11d3,{0x99,0x71,0x00,0xc0,0x4f,0x68,0xc8,0x76}}");
                IntPtr ptr = LoadDynamicFunctionPointer(socketHandle, ref guid);
                Volatile.Write(ref _transmitPackets,
                    MarshalEx.GetDelegateForFunctionPointer<TransmitPacketsDelegate>(ptr));
            }
        }
    }
}
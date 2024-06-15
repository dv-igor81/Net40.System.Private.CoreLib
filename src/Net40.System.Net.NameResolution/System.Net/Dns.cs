using System.Globalization;
using System.Net.Internals.Net40;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Net40;

using AddressFamily = System.Net.Sockets.Net40.AddressFamily;

public static class Dns
{
    [Obsolete(
        "GetHostByName is obsoleted for this type, please use GetHostEntry instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static IPHostEntry GetHostByName(string hostName)
    {
        NameResolutionPal.EnsureSocketsAreInitialized();
        if (hostName == null)
        {
            throw new ArgumentNullException("hostName");
        }

        if (IPAddress.TryParse(hostName, out var address))
        {
            return NameResolutionUtilities.GetUnresolvedAnswer(address);
        }

        return InternalGetHostByName(hostName);
    }

    private static void ValidateHostName(string hostName)
    {
        if (hostName.Length > 255 || (hostName.Length == 255 && hostName[254] != '.'))
        {
            throw new ArgumentOutOfRangeException("hostName",
                SR.Format(SR.net_toolong, "hostName", 255.ToString(NumberFormatInfo.CurrentInfo)));
        }
    }

    private static IPHostEntry InternalGetHostByName(string hostName)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, hostName, "InternalGetHostByName");
        }

        IPHostEntry hostinfo = null;
        ValidateHostName(hostName);
        int nativeErrorCode;
        SocketError socketError = NameResolutionPal.TryGetAddrInfo(hostName, out hostinfo, out nativeErrorCode);
        if (socketError != 0)
        {
            throw SocketExceptionFactory.CreateSocketException(socketError, nativeErrorCode);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, hostinfo, "InternalGetHostByName");
        }

        return hostinfo;
    }

    [Obsolete(
        "GetHostByAddress is obsoleted for this type, please use GetHostEntry instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static IPHostEntry GetHostByAddress(string address)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, address, "GetHostByAddress");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        if (address == null)
        {
            throw new ArgumentNullException("address");
        }

        IPHostEntry iPHostEntry = InternalGetHostByAddress(IPAddress.Parse(address));
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, iPHostEntry, "GetHostByAddress");
        }

        return iPHostEntry;
    }

    [Obsolete(
        "GetHostByAddress is obsoleted for this type, please use GetHostEntry instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static IPHostEntry GetHostByAddress(IPAddress address)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, address, "GetHostByAddress");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        if (address == null)
        {
            throw new ArgumentNullException("address");
        }

        IPHostEntry iPHostEntry = InternalGetHostByAddress(address);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, iPHostEntry, "GetHostByAddress");
        }

        return iPHostEntry;
    }

    private static IPHostEntry InternalGetHostByAddress(IPAddress address)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(null, address, "InternalGetHostByAddress");
        }

        int nativeErrorCode;
        SocketError errorCode;
        string name = NameResolutionPal.TryGetNameInfo(address, out errorCode, out nativeErrorCode);
        if (errorCode == SocketError.Success)
        {
            errorCode = NameResolutionPal.TryGetAddrInfo(name, out var hostinfo, out nativeErrorCode);
            if (errorCode == SocketError.Success)
            {
                return hostinfo;
            }

            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(null,
                    SocketExceptionFactory.CreateSocketException(errorCode, nativeErrorCode),
                    "InternalGetHostByAddress");
            }

            return hostinfo;
        }

        throw SocketExceptionFactory.CreateSocketException(errorCode, nativeErrorCode);
    }

    public static string GetHostName()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(null, null, "GetHostName");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        return NameResolutionPal.GetHostName();
    }

    [Obsolete(
        "Resolve is obsoleted for this type, please use GetHostEntry instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static IPHostEntry Resolve(string hostName)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, hostName, "Resolve");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        if (hostName == null)
        {
            throw new ArgumentNullException("hostName");
        }

        IPHostEntry iPHostEntry;
        if (IPAddress.TryParse(hostName, out var address) && (address.AddressFamily != AddressFamily.InterNetworkV6 ||
                                                                    SocketProtocolSupportPal.OSSupportsIPv6))
        {
            try
            {
                iPHostEntry = InternalGetHostByAddress(address);
            }
            catch (SocketException message)
            {
                if (NetEventSource.IsEnabled)
                {
                    NetEventSource.Error(null, message, "Resolve");
                }

                iPHostEntry = NameResolutionUtilities.GetUnresolvedAnswer(address);
            }
        }
        else
        {
            iPHostEntry = InternalGetHostByName(hostName);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, iPHostEntry, "Resolve");
        }

        return iPHostEntry;
    }

    private static void ResolveCallback(object context)
    {
        DnsResolveAsyncResult dnsResolveAsyncResult = (DnsResolveAsyncResult)context;
        IPHostEntry result;
        try
        {
            result = ((dnsResolveAsyncResult.IpAddress == null)
                ? InternalGetHostByName(dnsResolveAsyncResult.HostName)
                : InternalGetHostByAddress(dnsResolveAsyncResult.IpAddress));
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception result2)
        {
            dnsResolveAsyncResult.InvokeCallback(result2);
            return;
        }

        dnsResolveAsyncResult.InvokeCallback(result);
    }

    private static IAsyncResult HostResolutionBeginHelper(string hostName, bool justReturnParsedIp, bool throwOnIIPAny,
        AsyncCallback requestCallback, object state)
    {
        if (hostName == null)
        {
            throw new ArgumentNullException("hostName");
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(null, hostName, "HostResolutionBeginHelper");
        }

        DnsResolveAsyncResult dnsResolveAsyncResult;
        if (IPAddress.TryParse(hostName, out var address))
        {
            if (throwOnIIPAny && (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)))
            {
                throw new ArgumentException(SR.net_invalid_ip_addr, "hostName");
            }

            dnsResolveAsyncResult = new DnsResolveAsyncResult(address, null, state, requestCallback);
            if (justReturnParsedIp)
            {
                IPHostEntry unresolvedAnswer = NameResolutionUtilities.GetUnresolvedAnswer(address);
                dnsResolveAsyncResult.StartPostingAsyncOp(lockCapture: false);
                dnsResolveAsyncResult.InvokeCallback(unresolvedAnswer);
                dnsResolveAsyncResult.FinishPostingAsyncOp();
                return dnsResolveAsyncResult;
            }
        }
        else
        {
            dnsResolveAsyncResult = new DnsResolveAsyncResult(hostName, null, state, requestCallback);
        }

        dnsResolveAsyncResult.StartPostingAsyncOp(lockCapture: false);
        if (NameResolutionPal.SupportsGetAddrInfoAsync && address == null)
        {
            ValidateHostName(hostName);
            NameResolutionPal.GetAddrInfoAsync(dnsResolveAsyncResult);
        }
        else
        {
            Task.Factory.StartNew(delegate(object s) { ResolveCallback(s); }, dnsResolveAsyncResult,
                CancellationToken.None, 
                //TaskCreationOptions.DenyChildAttach,
                TaskCreationOptions.None,
                TaskScheduler.Default);
        }

        dnsResolveAsyncResult.FinishPostingAsyncOp();
        return dnsResolveAsyncResult;
    }

    private static IAsyncResult HostResolutionBeginHelper(IPAddress address, bool flowContext,
        AsyncCallback requestCallback, object state)
    {
        if (address == null)
        {
            throw new ArgumentNullException("address");
        }

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            throw new ArgumentException(SR.net_invalid_ip_addr, "address");
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(null, address, "HostResolutionBeginHelper");
        }

        DnsResolveAsyncResult dnsResolveAsyncResult = new DnsResolveAsyncResult(address, null, state, requestCallback);
        if (flowContext)
        {
            dnsResolveAsyncResult.StartPostingAsyncOp(lockCapture: false);
        }

        Task.Factory.StartNew(delegate(object s) { ResolveCallback(s); }, dnsResolveAsyncResult, CancellationToken.None,
            // TaskCreationOptions.DenyChildAttach,
            TaskCreationOptions.None, // DIA-Замена
            TaskScheduler.Default);
        dnsResolveAsyncResult.FinishPostingAsyncOp();
        return dnsResolveAsyncResult;
    }

    private static IPHostEntry HostResolutionEndHelper(IAsyncResult asyncResult)
    {
        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        if (!(asyncResult is DnsResolveAsyncResult dnsResolveAsyncResult))
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (dnsResolveAsyncResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndResolve"));
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(null, null, "HostResolutionEndHelper");
        }

        dnsResolveAsyncResult.InternalWaitForCompletion();
        dnsResolveAsyncResult.EndCalled = true;
        if (dnsResolveAsyncResult.Result is Exception source)
        {
            ExceptionDispatchInfo.Throw(source);
        }

        return (IPHostEntry)dnsResolveAsyncResult.Result;
    }

    [Obsolete(
        "BeginGetHostByName is obsoleted for this type, please use BeginGetHostEntry instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static IAsyncResult BeginGetHostByName(string hostName, AsyncCallback requestCallback, object stateObject)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, hostName, "BeginGetHostByName");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        IAsyncResult asyncResult = HostResolutionBeginHelper(hostName, justReturnParsedIp: true, throwOnIIPAny: true,
            requestCallback, stateObject);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, asyncResult, "BeginGetHostByName");
        }

        return asyncResult;
    }

    [Obsolete(
        "EndGetHostByName is obsoleted for this type, please use EndGetHostEntry instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static IPHostEntry EndGetHostByName(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, asyncResult, "EndGetHostByName");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        IPHostEntry iPHostEntry = HostResolutionEndHelper(asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, iPHostEntry, "EndGetHostByName");
        }

        return iPHostEntry;
    }

    public static IPHostEntry GetHostEntry(string hostNameOrAddress)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, hostNameOrAddress, "GetHostEntry");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        if (hostNameOrAddress == null)
        {
            throw new ArgumentNullException("hostNameOrAddress");
        }

        IPHostEntry iPHostEntry;
        if (IPAddress.TryParse(hostNameOrAddress, out var address))
        {
            if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            {
                throw new ArgumentException(SR.Format(SR.net_invalid_ip_addr, "hostNameOrAddress"));
            }

            iPHostEntry = InternalGetHostByAddress(address);
        }
        else
        {
            iPHostEntry = InternalGetHostByName(hostNameOrAddress);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, iPHostEntry, "GetHostEntry");
        }

        return iPHostEntry;
    }

    public static IPHostEntry GetHostEntry(IPAddress address)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, address, "GetHostEntry");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        if (address == null)
        {
            throw new ArgumentNullException("address");
        }

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            throw new ArgumentException(SR.Format(SR.net_invalid_ip_addr, "address"));
        }

        IPHostEntry iPHostEntry = InternalGetHostByAddress(address);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, iPHostEntry, "GetHostEntry");
        }

        return iPHostEntry;
    }

    public static IPAddress[] GetHostAddresses(string hostNameOrAddress)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, hostNameOrAddress, "GetHostAddresses");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        if (hostNameOrAddress == null)
        {
            throw new ArgumentNullException("hostNameOrAddress");
        }

        IPAddress[] array;
        if (IPAddress.TryParse(hostNameOrAddress, out var address))
        {
            if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            {
                throw new ArgumentException(SR.Format(SR.net_invalid_ip_addr, "hostNameOrAddress"));
            }

            array = new IPAddress[1] { address };
        }
        else
        {
            array = InternalGetHostByName(hostNameOrAddress).AddressList;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, array, "GetHostAddresses");
        }

        return array;
    }

    public static IAsyncResult BeginGetHostEntry(string hostNameOrAddress, AsyncCallback requestCallback,
        object stateObject)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, hostNameOrAddress, "BeginGetHostEntry");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        IAsyncResult asyncResult = HostResolutionBeginHelper(hostNameOrAddress, justReturnParsedIp: false,
            throwOnIIPAny: true, requestCallback, stateObject);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, asyncResult, "BeginGetHostEntry");
        }

        return asyncResult;
    }

    public static IAsyncResult BeginGetHostEntry(IPAddress address, AsyncCallback requestCallback, object stateObject)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, address, "BeginGetHostEntry");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        IAsyncResult asyncResult = HostResolutionBeginHelper(address, flowContext: true, requestCallback, stateObject);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, asyncResult, "BeginGetHostEntry");
        }

        return asyncResult;
    }

    public static IPHostEntry EndGetHostEntry(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, asyncResult, "EndGetHostEntry");
        }

        IPHostEntry iPHostEntry = HostResolutionEndHelper(asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, iPHostEntry, "EndGetHostEntry");
        }

        return iPHostEntry;
    }

    public static IAsyncResult BeginGetHostAddresses(string hostNameOrAddress, AsyncCallback requestCallback,
        object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, hostNameOrAddress, "BeginGetHostAddresses");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        IAsyncResult asyncResult = HostResolutionBeginHelper(hostNameOrAddress, justReturnParsedIp: true,
            throwOnIIPAny: true, requestCallback, state);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, asyncResult, "BeginGetHostAddresses");
        }

        return asyncResult;
    }

    public static IPAddress[] EndGetHostAddresses(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, asyncResult, "EndGetHostAddresses");
        }

        IPHostEntry iPHostEntry = HostResolutionEndHelper(asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, iPHostEntry, "EndGetHostAddresses");
        }

        return iPHostEntry.AddressList;
    }

    [Obsolete(
        "BeginResolve is obsoleted for this type, please use BeginGetHostEntry instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static IAsyncResult BeginResolve(string hostName, AsyncCallback requestCallback, object stateObject)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, hostName, "BeginResolve");
        }

        NameResolutionPal.EnsureSocketsAreInitialized();
        IAsyncResult asyncResult = HostResolutionBeginHelper(hostName, justReturnParsedIp: false, throwOnIIPAny: false,
            requestCallback, stateObject);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, asyncResult, "BeginResolve");
        }

        return asyncResult;
    }

    [Obsolete(
        "EndResolve is obsoleted for this type, please use EndGetHostEntry instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static IPHostEntry EndResolve(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, asyncResult, "EndResolve");
        }

        IPHostEntry iPHostEntry;
        try
        {
            iPHostEntry = HostResolutionEndHelper(asyncResult);
        }
        catch (SocketException message)
        {
            IPAddress ipAddress = ((DnsResolveAsyncResult)asyncResult).IpAddress;
            if (ipAddress == null)
            {
                throw;
            }

            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(null, message, "EndResolve");
            }

            iPHostEntry = NameResolutionUtilities.GetUnresolvedAnswer(ipAddress);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, iPHostEntry, "EndResolve");
        }

        return iPHostEntry;
    }

    public static Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress)
    {
        NameResolutionPal.EnsureSocketsAreInitialized();
        return Task<IPAddress[]>.Factory.FromAsync(
            (string arg, AsyncCallback requestCallback, object stateObject) =>
                BeginGetHostAddresses(arg, requestCallback, stateObject),
            (IAsyncResult asyncResult) => EndGetHostAddresses(asyncResult), hostNameOrAddress, null);
    }

    public static Task<IPHostEntry> GetHostEntryAsync(IPAddress address)
    {
        NameResolutionPal.EnsureSocketsAreInitialized();
        return Task<IPHostEntry>.Factory.FromAsync(
            (IPAddress arg, AsyncCallback requestCallback, object stateObject) =>
                BeginGetHostEntry(arg, requestCallback, stateObject),
            (IAsyncResult asyncResult) => EndGetHostEntry(asyncResult), address, null);
    }

    public static Task<IPHostEntry> GetHostEntryAsync(string hostNameOrAddress)
    {
        NameResolutionPal.EnsureSocketsAreInitialized();
        return Task<IPHostEntry>.Factory.FromAsync(
            (string arg, AsyncCallback requestCallback, object stateObject) =>
                BeginGetHostEntry(arg, requestCallback, stateObject),
            (IAsyncResult asyncResult) => EndGetHostEntry(asyncResult), hostNameOrAddress, null);
    }
}
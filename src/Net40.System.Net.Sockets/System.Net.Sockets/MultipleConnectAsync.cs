using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Sockets;

using DnsEndPoint = Net.Net40.DnsEndPoint;
using Dns = Net.Net40.Dns;
using IPEndPoint = Net.Net40.IPEndPoint;
using IPAddress = Net.Net40.IPAddress;

internal abstract class MultipleConnectAsync
{
    private enum State
    {
        NotStarted,
        DnsQuery,
        ConnectAttempt,
        Completed,
        Canceled
    }

    protected Net40.SocketAsyncEventArgs _userArgs;

    protected Net40.SocketAsyncEventArgs _internalArgs;

    protected DnsEndPoint _endPoint;

    protected IPAddress[] _addressList;

    protected int _nextAddress;

    private State _state;

    private object _lockObject = new object();

    public bool StartConnectAsync(Net40.SocketAsyncEventArgs args, DnsEndPoint endPoint)
    {
        lock (_lockObject)
        {
            if (endPoint.AddressFamily != 0 && endPoint.AddressFamily != Net40.AddressFamily.InterNetwork &&
                endPoint.AddressFamily != Net40.AddressFamily.InterNetworkV6)
            {
                NetEventSource.Fail(this, $"Unexpected endpoint address family: {endPoint.AddressFamily}");
            }

            _userArgs = args;
            _endPoint = endPoint;
            if (_state == State.Canceled)
            {
                SyncFail(new Net40.SocketException(995));
                return false;
            }

            if (_state != 0)
            {
                NetEventSource.Fail(this,
                    "MultipleConnectAsync.StartConnectAsync(): Unexpected object state");
            }

            _state = State.DnsQuery;
            IAsyncResult asyncResult = Dns.BeginGetHostAddresses(endPoint.Host, DnsCallback, null);
            if (asyncResult.CompletedSynchronously)
            {
                return DoDnsCallback(asyncResult, sync: true);
            }

            return true;
        }
    }

    private void DnsCallback(IAsyncResult result)
    {
        if (!result.CompletedSynchronously)
        {
            DoDnsCallback(result, sync: false);
        }
    }

    private bool DoDnsCallback(IAsyncResult result, bool sync)
    {
        Exception ex = null;
        lock (_lockObject)
        {
            if (_state == State.Canceled)
            {
                return true;
            }

            if (_state != State.DnsQuery)
            {
                NetEventSource.Fail(this, "MultipleConnectAsync.DoDnsCallback(): Unexpected object state");
            }

            try
            {
                _addressList = Dns.EndGetHostAddresses(result);
                if (_addressList == null)
                {
                    NetEventSource.Fail(this,
                        "MultipleConnectAsync.DoDnsCallback(): EndGetHostAddresses returned null!");
                }
            }
            catch (Exception ex2)
            {
                _state = State.Completed;
                ex = ex2;
            }

            if (ex == null)
            {
                _state = State.ConnectAttempt;
                _internalArgs = new Net40.SocketAsyncEventArgs();
                _internalArgs.Completed += InternalConnectCallback;
                _internalArgs.CopyBufferFrom(_userArgs);
                ex = AttemptConnection();
                if (ex != null)
                {
                    _state = State.Completed;
                }
            }
        }

        if (ex != null)
        {
            return Fail(sync, ex);
        }

        return true;
    }

    private void InternalConnectCallback(object sender, Net40.SocketAsyncEventArgs args)
    {
        Exception ex = null;
        lock (_lockObject)
        {
            if (_state == State.Canceled)
            {
                ex = new Net40.SocketException(995);
            }
            else if (args.SocketError == Net40.SocketError.Success)
            {
                _state = State.Completed;
            }
            else if (args.SocketError == Net40.SocketError.OperationAborted)
            {
                ex = new Net40.SocketException(995);
                _state = State.Canceled;
            }
            else
            {
                Net40.SocketError socketError = args.SocketError;
                Exception ex2 = AttemptConnection();
                if (ex2 == null)
                {
                    return;
                }

                ex = ((!(ex2 is Net40.SocketException ex3) || ex3.SocketErrorCode != Net40.SocketError.NoData)
                    ? ex2
                    : new Net40.SocketException((int)socketError));
                _state = State.Completed;
            }
        }

        if (ex == null)
        {
            Succeed();
        }
        else
        {
            AsyncFail(ex);
        }
    }

    private Exception AttemptConnection()
    {
        try
        {
            Net40.Socket attemptSocket;
            IPAddress nextAddress = GetNextAddress(out attemptSocket);
            if (nextAddress == null)
            {
                return new Net40.SocketException(11004);
            }

            _internalArgs.RemoteEndPoint = new IPEndPoint(nextAddress, _endPoint.Port);
            return AttemptConnection(attemptSocket, _internalArgs);
        }
        catch (Exception ex)
        {
            if (ex is ObjectDisposedException)
            {
                NetEventSource.Fail(this, "unexpected ObjectDisposedException");
            }

            return ex;
        }
    }

    private Exception AttemptConnection(Net40.Socket attemptSocket, Net40.SocketAsyncEventArgs args)
    {
        try
        {
            if (attemptSocket == null)
            {
                NetEventSource.Fail(null, "attemptSocket is null!");
            }

            if (!attemptSocket.ConnectAsync(args))
            {
                InternalConnectCallback(null, args);
            }
        }
        catch (ObjectDisposedException)
        {
            return new Net40.SocketException(995);
        }
        catch (Exception result)
        {
            return result;
        }

        return null;
    }

    protected abstract void OnSucceed();

    private void Succeed()
    {
        OnSucceed();
        _userArgs.FinishWrapperConnectSuccess(_internalArgs.ConnectSocket, _internalArgs.BytesTransferred,
            _internalArgs.SocketFlags);
        _internalArgs.Dispose();
    }

    protected abstract void OnFail(bool abortive);

    private bool Fail(bool sync, Exception e)
    {
        if (sync)
        {
            SyncFail(e);
            return false;
        }

        AsyncFail(e);
        return true;
    }

    private void SyncFail(Exception e)
    {
        OnFail(abortive: false);
        if (_internalArgs != null)
        {
            _internalArgs.Dispose();
        }

        if (e is Net40.SocketException exception)
        {
            _userArgs.FinishConnectByNameSyncFailure(exception, 0, Net40.SocketFlags.None);
        }
        else
        {
            ExceptionDispatchInfo.Throw(e);
        }
    }

    private void AsyncFail(Exception e)
    {
        OnFail(abortive: false);
        if (_internalArgs != null)
        {
            _internalArgs.Dispose();
        }

        _userArgs.FinishConnectByNameAsyncFailure(e, 0, Net40.SocketFlags.None);
    }

    public void Cancel()
    {
        bool flag = false;
        lock (_lockObject)
        {
            switch (_state)
            {
                case State.NotStarted:
                    flag = true;
                    break;
                case State.DnsQuery:
                    Task.Factory.StartNew(delegate(object s) { CallAsyncFail(s); }, null, CancellationToken.None,
                        //TaskCreationOptions.DenyChildAttach,
                        TaskCreationOptions.None, // DIA-Замена
                        TaskScheduler.Default);
                    flag = true;
                    break;
                case State.ConnectAttempt:
                    flag = true;
                    break;
                default:
                    NetEventSource.Fail(this, "Unexpected object state");
                    break;
                case State.Completed:
                    break;
            }

            _state = State.Canceled;
        }

        if (flag)
        {
            OnFail(abortive: true);
        }
    }

    private void CallAsyncFail(object ignored)
    {
        AsyncFail(new Net40.SocketException(995));
    }

    protected abstract IPAddress GetNextAddress(out Net40.Socket attemptSocket);
}
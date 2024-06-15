namespace System.Net.Sockets;

using IPAddress = Net.Net40.IPAddress;

internal sealed class SingleSocketMultipleConnectAsync : MultipleConnectAsync
{
    private Net40.Socket _socket;

    private bool _userSocket;

    public SingleSocketMultipleConnectAsync(Net40.Socket socket, bool userSocket)
    {
        _socket = socket;
        _userSocket = userSocket;
    }

    protected override IPAddress GetNextAddress(out Net40.Socket attemptSocket)
    {
        _socket.ReplaceHandleIfNecessaryAfterFailedConnect();
        IPAddress iPAddress = null;
        do
        {
            if (_nextAddress >= _addressList.Length)
            {
                attemptSocket = null;
                return null;
            }

            iPAddress = _addressList[_nextAddress];
            _nextAddress++;
        } while (!_socket.CanTryAddressFamily(iPAddress.AddressFamily));

        attemptSocket = _socket;
        return iPAddress;
    }

    protected override void OnFail(bool abortive)
    {
        if (abortive || !_userSocket)
        {
            _socket.Dispose();
        }
    }

    protected override void OnSucceed()
    {
    }
}
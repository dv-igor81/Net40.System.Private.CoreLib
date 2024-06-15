namespace System.Net.Sockets;

using IPAddress = Net.Net40.IPAddress;

public class IPv6MulticastOption
{
    private IPAddress _group;

    private long _interface;

    public IPAddress Group
    {
        get { return _group; }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            _group = value;
        }
    }

    public long InterfaceIndex
    {
        get { return _interface; }
        set
        {
            if (value < 0 || value > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException("value");
            }

            _interface = value;
        }
    }

    public IPv6MulticastOption(IPAddress group, long ifindex)
    {
        if (group == null)
        {
            throw new ArgumentNullException("group");
        }

        if (ifindex < 0 || ifindex > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException("ifindex");
        }

        Group = group;
        InterfaceIndex = ifindex;
    }

    public IPv6MulticastOption(IPAddress group)
    {
        if (group == null)
        {
            throw new ArgumentNullException("group");
        }

        Group = group;
        InterfaceIndex = 0L;
    }
}
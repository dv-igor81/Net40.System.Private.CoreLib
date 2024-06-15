namespace System.Net.Net40;

internal sealed class SystemNetworkCredential : NetworkCredential
{
    internal static readonly SystemNetworkCredential s_defaultCredential = new SystemNetworkCredential();

    private SystemNetworkCredential()
        : base(string.Empty, string.Empty, string.Empty)
    {
    }
}
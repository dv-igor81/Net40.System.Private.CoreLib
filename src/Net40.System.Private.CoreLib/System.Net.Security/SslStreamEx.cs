using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Security;

public static class SslStreamEx
{
    public static Task AuthenticateAsClientAsync(
        this SslStream sslStream,
        SslClientAuthenticationOptions sslClientAuthenticationOptions,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (sslClientAuthenticationOptions == null)
        {
            throw new ArgumentNullException("sslClientAuthenticationOptions");
        }
        throw new NotImplementedException();
    }
}
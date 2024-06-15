using System.Resources;
using System.Runtime.CompilerServices;
using Net40.FxResources.System.Net.NameResolution;

namespace System;

internal static class SR
{
    private static ResourceManager s_resourceManager;

    internal static ResourceManager ResourceManager =>
        s_resourceManager ?? (s_resourceManager = new ResourceManager(typeof(Strings)));

    internal static string net_toolong => GetResourceString("net_toolong");

    internal static string net_io_invalidasyncresult => GetResourceString("net_io_invalidasyncresult");

    internal static string net_io_invalidendcall => GetResourceString("net_io_invalidendcall");

    internal static string net_invalid_ip_addr => GetResourceString("net_invalid_ip_addr");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool UsingResourceKeys()
    {
        return false;
    }

    internal static string GetResourceString(string resourceKey, string defaultString = null)
    {
        if (UsingResourceKeys())
        {
            return defaultString ?? resourceKey;
        }

        string text = null;
        try
        {
            text = ResourceManager.GetString(resourceKey);
        }
        catch (MissingManifestResourceException)
        {
        }

        if (defaultString != null && resourceKey.Equals(text))
        {
            return defaultString;
        }

        return text;
    }

    internal static string Format(string resourceFormat, object p1)
    {
        if (UsingResourceKeys())
        {
            return string.Join(", ", resourceFormat, p1);
        }

        return string.Format(resourceFormat, p1);
    }

    internal static string Format(string resourceFormat, object p1, object p2)
    {
        if (UsingResourceKeys())
        {
            return string.Join(", ", resourceFormat, p1, p2);
        }

        return string.Format(resourceFormat, p1, p2);
    }
}
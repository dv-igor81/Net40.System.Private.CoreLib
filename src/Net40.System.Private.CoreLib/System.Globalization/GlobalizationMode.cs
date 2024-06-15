namespace System.Globalization.Net40;

internal sealed class GlobalizationMode
{
    internal static bool Invariant { get; } = GetGlobalizationInvariantMode();

    internal static bool IsTrueStringIgnoreCase(string value)
    {
        if (value.Length == 4 && (value[0] == 't' || value[0] == 'T') && (value[1] == 'r' || value[1] == 'R') &&
            (value[2] == 'u' || value[2] == 'U'))
        {
            if (value[3] != 'e')
            {
                return value[3] == 'E';
            }
            return true;
        }
        return false;
    }


    internal static bool GetInvariantSwitchValue()
    {
        bool result = false;
        string environmentVariable = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT");
        if (environmentVariable != null)
        {
            result = IsTrueStringIgnoreCase(environmentVariable) || environmentVariable.Equals("1");
        }
        return result;
    }

    private static bool GetGlobalizationInvariantMode()
    {
        return GetInvariantSwitchValue();
    }
}

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.IO;

namespace System.Net40;

public class Uri : ISerializable
{
    [Flags]
    private enum Flags : ulong
    {
        Zero = 0uL,
        SchemeNotCanonical = 1uL,
        UserNotCanonical = 2uL,
        HostNotCanonical = 4uL,
        PortNotCanonical = 8uL,
        PathNotCanonical = 0x10uL,
        QueryNotCanonical = 0x20uL,
        FragmentNotCanonical = 0x40uL,
        CannotDisplayCanonical = 0x7FuL,
        E_UserNotCanonical = 0x80uL,
        E_HostNotCanonical = 0x100uL,
        E_PortNotCanonical = 0x200uL,
        E_PathNotCanonical = 0x400uL,
        E_QueryNotCanonical = 0x800uL,
        E_FragmentNotCanonical = 0x1000uL,
        E_CannotDisplayCanonical = 0x1F80uL,
        ShouldBeCompressed = 0x2000uL,
        FirstSlashAbsent = 0x4000uL,
        BackslashInPath = 0x8000uL,
        IndexMask = 0xFFFFuL,
        HostTypeMask = 0x70000uL,
        HostNotParsed = 0uL,
        IPv6HostType = 0x10000uL,
        IPv4HostType = 0x20000uL,
        DnsHostType = 0x30000uL,
        UncHostType = 0x40000uL,
        BasicHostType = 0x50000uL,
        UnusedHostType = 0x60000uL,
        UnknownHostType = 0x70000uL,
        UserEscaped = 0x80000uL,
        AuthorityFound = 0x100000uL,
        HasUserInfo = 0x200000uL,
        LoopbackHost = 0x400000uL,
        NotDefaultPort = 0x800000uL,
        UserDrivenParsing = 0x1000000uL,
        CanonicalDnsHost = 0x2000000uL,
        ErrorOrParsingRecursion = 0x4000000uL,
        DosPath = 0x8000000uL,
        UncPath = 0x10000000uL,
        ImplicitFile = 0x20000000uL,
        MinimalUriInfoSet = 0x40000000uL,
        AllUriInfoSet = 0x80000000uL,
        IdnHost = 0x100000000uL,
        HasUnicode = 0x200000000uL,
        HostUnicodeNormalized = 0x400000000uL,
        RestUnicodeNormalized = 0x800000000uL,
        UnicodeHost = 0x1000000000uL,
        IntranetUri = 0x2000000000uL,
        UseOrigUncdStrOffset = 0x4000000000uL,
        UserIriCanonical = 0x8000000000uL,
        PathIriCanonical = 0x10000000000uL,
        QueryIriCanonical = 0x20000000000uL,
        FragmentIriCanonical = 0x40000000000uL,
        IriCanonical = 0x78000000000uL,
        UnixPath = 0x100000000000uL
    }

    private class UriInfo
    {
        public string Host;

        public string ScopeId;

        public string String;

        public Offset Offset;

        public string DnsSafeHost;

        public MoreInfo MoreInfo;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Offset
    {
        public ushort Scheme;

        public ushort User;

        public ushort Host;

        public ushort PortValue;

        public ushort Path;

        public ushort Query;

        public ushort Fragment;

        public ushort End;
    }

    private class MoreInfo
    {
        public string Path;

        public string Query;

        public string Fragment;

        public string AbsoluteUri;

        public int Hash;

        public string RemoteUrl;
    }

    [Flags]
    private enum Check
    {
        None = 0,
        EscapedCanonical = 1,
        DisplayCanonical = 2,
        DotSlashAttn = 4,
        DotSlashEscaped = 0x80,
        BackslashInPath = 0x10,
        ReservedFound = 0x20,
        NotIriCanonical = 0x40,
        FoundNonAscii = 8
    }

    public static readonly string UriSchemeFile = UriParser.FileUri.SchemeName;

    public static readonly string UriSchemeFtp = UriParser.FtpUri.SchemeName;

    public static readonly string UriSchemeGopher = UriParser.GopherUri.SchemeName;

    public static readonly string UriSchemeHttp = UriParser.HttpUri.SchemeName;

    public static readonly string UriSchemeHttps = UriParser.HttpsUri.SchemeName;

    internal static readonly string UriSchemeWs = UriParser.WsUri.SchemeName;

    internal static readonly string UriSchemeWss = UriParser.WssUri.SchemeName;

    public static readonly string UriSchemeMailto = UriParser.MailToUri.SchemeName;

    public static readonly string UriSchemeNews = UriParser.NewsUri.SchemeName;

    public static readonly string UriSchemeNntp = UriParser.NntpUri.SchemeName;

    public static readonly string UriSchemeNetTcp = UriParser.NetTcpUri.SchemeName;

    public static readonly string UriSchemeNetPipe = UriParser.NetPipeUri.SchemeName;

    public static readonly string SchemeDelimiter = "://";

    private string _string;

    private string _originalUnicodeString;

    private UriParser _syntax;

    private string _dnsSafeHost;

    private Flags _flags;

    private UriInfo _info;

    private bool _iriParsing;

    private static volatile UriIdnScope s_IdnScope = UriIdnScope.None;

    private static volatile bool s_IriParsing = true;

    private static readonly char[] s_pathDelims = new char[5] { ':', '\\', '/', '?', '#' };

    private bool IsImplicitFile => (_flags & Flags.ImplicitFile) != 0;

    private bool IsUncOrDosPath => (_flags & (Flags.DosPath | Flags.UncPath)) != 0;

    private bool IsDosPath => (_flags & Flags.DosPath) != 0;

    private bool IsUncPath => (_flags & Flags.UncPath) != 0;

    private Flags HostType => _flags & Flags.HostTypeMask;

    private UriParser Syntax => _syntax;

    private bool IsNotAbsoluteUri => _syntax == null;

    private bool AllowIdn
    {
        get
        {
            if (_syntax != null && (_syntax.Flags & UriSyntaxFlags.AllowIdn) != 0)
            {
                if (s_IdnScope != UriIdnScope.All)
                {
                    if (s_IdnScope == UriIdnScope.AllExceptIntranet)
                    {
                        return NotAny(Flags.IntranetUri);
                    }

                    return false;
                }

                return true;
            }

            return false;
        }
    }

    internal bool UserDrivenParsing => (_flags & Flags.UserDrivenParsing) != 0;

    private ushort SecuredPathIndex
    {
        get
        {
            if (IsDosPath)
            {
                char c = _string[_info.Offset.Path];
                return (ushort)((c == '/' || c == '\\') ? 3u : 2u);
            }

            return 0;
        }
    }

    public string AbsolutePath
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            string text = PrivateAbsolutePath;
            if (IsDosPath && text[0] == '/')
            {
                text = text.Substring(1);
            }

            return text;
        }
    }

    private string PrivateAbsolutePath
    {
        get
        {
            UriInfo uriInfo = EnsureUriInfo();
            if (uriInfo.MoreInfo == null)
            {
                uriInfo.MoreInfo = new MoreInfo();
            }

            string text = uriInfo.MoreInfo.Path;
            if (text == null)
            {
                text = GetParts(UriComponents.Path | UriComponents.KeepDelimiter, UriFormat.UriEscaped);
                uriInfo.MoreInfo.Path = text;
            }

            return text;
        }
    }

    public string AbsoluteUri
    {
        get
        {
            if (_syntax == null)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            UriInfo uriInfo = EnsureUriInfo();
            if (uriInfo.MoreInfo == null)
            {
                uriInfo.MoreInfo = new MoreInfo();
            }

            string text = uriInfo.MoreInfo.AbsoluteUri;
            if (text == null)
            {
                text = GetParts(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
                uriInfo.MoreInfo.AbsoluteUri = text;
            }

            return text;
        }
    }

    public string LocalPath
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            return GetLocalPath();
        }
    }

    public string Authority
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            return GetParts(UriComponents.Host | UriComponents.Port, UriFormat.UriEscaped);
        }
    }

    public UriHostNameType HostNameType
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            if (_syntax.IsSimple)
            {
                EnsureUriInfo();
            }
            else
            {
                EnsureHostString(allowDnsOptimization: false);
            }

            return HostType switch
            {
                Flags.DnsHostType => UriHostNameType.Dns,
                Flags.IPv4HostType => UriHostNameType.IPv4,
                Flags.IPv6HostType => UriHostNameType.IPv6,
                Flags.BasicHostType => UriHostNameType.Basic,
                Flags.UncHostType => UriHostNameType.Basic,
                Flags.HostTypeMask => UriHostNameType.Unknown,
                _ => UriHostNameType.Unknown,
            };
        }
    }

    public bool IsDefaultPort
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            if (_syntax.IsSimple)
            {
                EnsureUriInfo();
            }
            else
            {
                EnsureHostString(allowDnsOptimization: false);
            }

            return NotAny(Flags.NotDefaultPort);
        }
    }

    public bool IsFile
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            return (object)_syntax.SchemeName == UriSchemeFile;
        }
    }

    public bool IsLoopback
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            EnsureHostString(allowDnsOptimization: false);
            return InFact(Flags.LoopbackHost);
        }
    }

    public string PathAndQuery
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            string text = GetParts(UriComponents.PathAndQuery, UriFormat.UriEscaped);
            if (IsDosPath && text[0] == '/')
            {
                text = text.Substring(1);
            }

            return text;
        }
    }

    public string[] Segments
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            string privateAbsolutePath = PrivateAbsolutePath;
            if (privateAbsolutePath.Length == 0)
            {
                return ArrayEx.Empty<string>();
            }

            ArrayBuilder<string> arrayBuilder = default(ArrayBuilder<string>);
            int num = 0;
            while (num < privateAbsolutePath.Length)
            {
                int num2 = privateAbsolutePath.IndexOf('/', num);
                if (num2 == -1)
                {
                    num2 = privateAbsolutePath.Length - 1;
                }

                arrayBuilder.Add(privateAbsolutePath.Substring(num, num2 - num + 1));
                num = num2 + 1;
            }

            return arrayBuilder.ToArray();
        }
    }

    public bool IsUnc
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            return IsUncPath;
        }
    }

    public string Host
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            return GetParts(UriComponents.Host, UriFormat.UriEscaped);
        }
    }

    public int Port
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            if (_syntax.IsSimple)
            {
                EnsureUriInfo();
            }
            else
            {
                EnsureHostString(allowDnsOptimization: false);
            }

            if (InFact(Flags.NotDefaultPort))
            {
                return _info.Offset.PortValue;
            }

            return _syntax.DefaultPort;
        }
    }

    public string Query
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            UriInfo uriInfo = EnsureUriInfo();
            if (uriInfo.MoreInfo == null)
            {
                uriInfo.MoreInfo = new MoreInfo();
            }

            string text = uriInfo.MoreInfo.Query;
            if (text == null)
            {
                text = GetParts(UriComponents.Query | UriComponents.KeepDelimiter, UriFormat.UriEscaped);
                uriInfo.MoreInfo.Query = text;
            }

            return text;
        }
    }

    public string Fragment
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            UriInfo uriInfo = EnsureUriInfo();
            if (uriInfo.MoreInfo == null)
            {
                uriInfo.MoreInfo = new MoreInfo();
            }

            string text = uriInfo.MoreInfo.Fragment;
            if (text == null)
            {
                text = GetParts(UriComponents.Fragment | UriComponents.KeepDelimiter, UriFormat.UriEscaped);
                uriInfo.MoreInfo.Fragment = text;
            }

            return text;
        }
    }

    public string Scheme
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            return _syntax.SchemeName;
        }
    }

    private bool OriginalStringSwitched
    {
        get
        {
            if (!_iriParsing || !InFact(Flags.HasUnicode))
            {
                if (AllowIdn)
                {
                    if (!InFact(Flags.IdnHost))
                    {
                        return InFact(Flags.UnicodeHost);
                    }

                    return true;
                }

                return false;
            }

            return true;
        }
    }

    public string OriginalString
    {
        get
        {
            if (!OriginalStringSwitched)
            {
                return _string;
            }

            return _originalUnicodeString;
        }
    }

    public string DnsSafeHost
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            if (AllowIdn && ((_flags & Flags.IdnHost) != Flags.Zero || (_flags & Flags.UnicodeHost) != Flags.Zero))
            {
                EnsureUriInfo();
                return _info.DnsSafeHost;
            }

            EnsureHostString(allowDnsOptimization: false);
            if (!string.IsNullOrEmpty(_info.DnsSafeHost))
            {
                return _info.DnsSafeHost;
            }

            if (_info.Host.Length == 0)
            {
                return string.Empty;
            }

            string text = _info.Host;
            if (HostType == Flags.IPv6HostType)
            {
                text = ((_info.ScopeId != null)
                    ? string.Concat(text.AsSpan(1, text.Length - 2), _info.ScopeId)
                    : text.Substring(1, text.Length - 2));
            }
            else if (HostType == Flags.BasicHostType && InFact(Flags.HostNotCanonical | Flags.E_HostNotCanonical))
            {
                char[] array = new char[text.Length];
                int destPosition = 0;
                UriHelper.UnescapeString(text, 0, text.Length, array, ref destPosition, '\uffff', '\uffff', '\uffff',
                    UnescapeMode.Unescape | UnescapeMode.UnescapeAll, _syntax, isQuery: false);
                text = new string(array, 0, destPosition);
            }

            _info.DnsSafeHost = text;
            return text;
        }
    }

    public string IdnHost
    {
        get
        {
            string text = DnsSafeHost;
            if (HostType == Flags.DnsHostType)
            {
                text = DomainNameHelper.IdnEquivalent(text);
            }

            return text;
        }
    }

    public bool IsAbsoluteUri => _syntax != null;

    public bool UserEscaped => InFact(Flags.UserEscaped);

    public string UserInfo
    {
        get
        {
            if (IsNotAbsoluteUri)
            {
                throw new InvalidOperationException("SR.net_uri_NotAbsolute");
            }

            return GetParts(UriComponents.UserInfo, UriFormat.UriEscaped);
        }
    }

    internal bool HasAuthority => InFact(Flags.AuthorityFound);

    internal static bool IriParsingStatic(UriParser syntax)
    {
        if (s_IriParsing)
        {
            if (syntax == null || !syntax.InFact(UriSyntaxFlags.AllowIriParsing))
            {
                return syntax == null;
            }

            return true;
        }

        return false;
    }

    private bool AllowIdnStatic(UriParser syntax, Flags flags)
    {
        if (syntax != null && (syntax.Flags & UriSyntaxFlags.AllowIdn) != 0)
        {
            if (s_IdnScope != UriIdnScope.All)
            {
                if (s_IdnScope == UriIdnScope.AllExceptIntranet)
                {
                    return StaticNotAny(flags, Flags.IntranetUri);
                }

                return false;
            }

            return true;
        }

        return false;
    }

    private bool IsIntranet(string schemeHost)
    {
        return false;
    }

    private void SetUserDrivenParsing()
    {
        _flags = Flags.UserDrivenParsing | (_flags & Flags.UserEscaped);
    }

    private bool NotAny(Flags flags)
    {
        return (_flags & flags) == 0;
    }

    private bool InFact(Flags flags)
    {
        return (_flags & flags) != 0;
    }

    private static bool StaticNotAny(Flags allFlags, Flags checkFlags)
    {
        return (allFlags & checkFlags) == 0;
    }

    private static bool StaticInFact(Flags allFlags, Flags checkFlags)
    {
        return (allFlags & checkFlags) != 0;
    }

    private UriInfo EnsureUriInfo()
    {
        Flags flags = _flags;
        if ((_flags & Flags.MinimalUriInfoSet) == Flags.Zero)
        {
            CreateUriInfo(flags);
        }

        return _info;
    }

    private void EnsureParseRemaining()
    {
        if ((_flags & Flags.AllUriInfoSet) == Flags.Zero)
        {
            ParseRemaining();
        }
    }

    private void EnsureHostString(bool allowDnsOptimization)
    {
        EnsureUriInfo();
        if (_info.Host == null && (!allowDnsOptimization || !InFact(Flags.CanonicalDnsHost)))
        {
            CreateHostString();
        }
    }

    public Uri(string uriString)
    {
        if (uriString == null)
        {
            throw new ArgumentNullException("uriString");
        }

        CreateThis(uriString, dontEscape: false, UriKind.Absolute);
    }

    [Obsolete(
        "The constructor has been deprecated. Please use new Uri(string). The dontEscape parameter is deprecated and is always false. https://go.microsoft.com/fwlink/?linkid=14202")]
    public Uri(string uriString, bool dontEscape)
    {
        if (uriString == null)
        {
            throw new ArgumentNullException("uriString");
        }

        CreateThis(uriString, dontEscape, UriKind.Absolute);
    }

    [Obsolete(
        "The constructor has been deprecated. Please new Uri(Uri, string). The dontEscape parameter is deprecated and is always false. https://go.microsoft.com/fwlink/?linkid=14202")]
    public Uri(Uri baseUri, string? relativeUri, bool dontEscape)
    {
        if (baseUri == null)
        {
            throw new ArgumentNullException("baseUri");
        }

        if (!baseUri.IsAbsoluteUri)
        {
            throw new ArgumentOutOfRangeException("baseUri");
        }

        CreateUri(baseUri, relativeUri, dontEscape);
    }

    public Uri(string uriString, UriKind uriKind)
    {
        if (uriString == null)
        {
            throw new ArgumentNullException("uriString");
        }

        CreateThis(uriString, dontEscape: false, uriKind);
    }

    public Uri(Uri baseUri, string? relativeUri)
    {
        if ((object)baseUri == null)
        {
            throw new ArgumentNullException("baseUri");
        }

        if (!baseUri.IsAbsoluteUri)
        {
            throw new ArgumentOutOfRangeException("baseUri");
        }

        CreateUri(baseUri, relativeUri, dontEscape: false);
    }

    protected Uri(SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
        string @string = serializationInfo.GetString("AbsoluteUri");
        if (@string.Length != 0)
        {
            CreateThis(@string, dontEscape: false, UriKind.Absolute);
            return;
        }

        @string = serializationInfo.GetString("RelativeUri");
        if (@string == null)
        {
            throw new ArgumentNullException("uriString");
        }

        CreateThis(@string, dontEscape: false, UriKind.Relative);
    }

    void ISerializable.GetObjectData(SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
        GetObjectData(serializationInfo, streamingContext);
    }

    protected void GetObjectData(SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
        if (IsAbsoluteUri)
        {
            serializationInfo.AddValue("AbsoluteUri",
                GetParts(UriComponents.SerializationInfoString, UriFormat.UriEscaped));
            return;
        }

        serializationInfo.AddValue("AbsoluteUri", string.Empty);
        serializationInfo.AddValue("RelativeUri",
            GetParts(UriComponents.SerializationInfoString, UriFormat.UriEscaped));
    }

    private void CreateUri(Uri baseUri, string relativeUri, bool dontEscape)
    {
        CreateThis(relativeUri, dontEscape, UriKind.RelativeOrAbsolute);
        UriFormatException parsingError;
        if (baseUri.Syntax.IsSimple)
        {
            Uri uri = ResolveHelper(baseUri, this, ref relativeUri, ref dontEscape, out parsingError);
            if (parsingError != null)
            {
                throw parsingError;
            }

            if (uri != null)
            {
                if ((object)uri != this)
                {
                    CreateThisFromUri(uri);
                }

                return;
            }
        }
        else
        {
            dontEscape = false;
            relativeUri = baseUri.Syntax.InternalResolve(baseUri, this, out parsingError);
            if (parsingError != null)
            {
                throw parsingError;
            }
        }

        _flags = Flags.Zero;
        _info = null;
        _syntax = null;
        CreateThis(relativeUri, dontEscape, UriKind.Absolute);
    }

    public Uri(Uri baseUri, Uri relativeUri)
    {
        if ((object)baseUri == null)
        {
            throw new ArgumentNullException("baseUri");
        }

        if (!baseUri.IsAbsoluteUri)
        {
            throw new ArgumentOutOfRangeException("baseUri");
        }

        CreateThisFromUri(relativeUri);
        string newUriString = null;
        bool userEscaped;
        UriFormatException parsingError;
        if (baseUri.Syntax.IsSimple)
        {
            userEscaped = InFact(Flags.UserEscaped);
            Uri uri = ResolveHelper(baseUri, this, ref newUriString, ref userEscaped, out parsingError);
            if (parsingError != null)
            {
                throw parsingError;
            }

            if (uri != null)
            {
                if ((object)uri != this)
                {
                    CreateThisFromUri(uri);
                }

                return;
            }
        }
        else
        {
            userEscaped = false;
            newUriString = baseUri.Syntax.InternalResolve(baseUri, this, out parsingError);
            if (parsingError != null)
            {
                throw parsingError;
            }
        }

        _flags = Flags.Zero;
        _info = null;
        _syntax = null;
        CreateThis(newUriString, userEscaped, UriKind.Absolute);
    }

    private static ParsingError GetCombinedString(Uri baseUri, string relativeStr, bool dontEscape, ref string result)
    {
        for (int i = 0;
             i < relativeStr.Length && relativeStr[i] != '/' && relativeStr[i] != '\\' && relativeStr[i] != '?' &&
             relativeStr[i] != '#';
             i++)
        {
            if (relativeStr[i] == ':')
            {
                if (i < 2)
                {
                    break;
                }

                UriParser syntax = null;
                if (CheckSchemeSyntax(relativeStr.AsSpan(0, i), ref syntax) != 0)
                {
                    break;
                }

                if (baseUri.Syntax == syntax)
                {
                    relativeStr = ((i + 1 >= relativeStr.Length) ? string.Empty : relativeStr.Substring(i + 1));
                    break;
                }

                result = relativeStr;
                return ParsingError.None;
            }
        }

        if (relativeStr.Length == 0)
        {
            result = baseUri.OriginalString;
            return ParsingError.None;
        }

        result = CombineUri(baseUri, relativeStr, dontEscape ? UriFormat.UriEscaped : UriFormat.SafeUnescaped);
        return ParsingError.None;
    }

    private static UriFormatException GetException(ParsingError err)
    {
        return err switch
        {
            ParsingError.None => null,
            ParsingError.BadFormat => new UriFormatException("SR.net_uri_BadFormat"),
            ParsingError.BadScheme => new UriFormatException("SR.net_uri_BadScheme"),
            ParsingError.BadAuthority => new UriFormatException("SR.net_uri_BadAuthority"),
            ParsingError.EmptyUriString => new UriFormatException("SR.net_uri_EmptyUri"),
            ParsingError.SchemeLimit => new UriFormatException("SR.net_uri_SchemeLimit"),
            ParsingError.SizeLimit => new UriFormatException("SR.net_uri_SizeLimit"),
            ParsingError.MustRootedPath => new UriFormatException("SR.net_uri_MustRootedPath"),
            ParsingError.BadHostName => new UriFormatException("SR.net_uri_BadHostName"),
            ParsingError.NonEmptyHost => new UriFormatException("SR.net_uri_BadFormat"),
            ParsingError.BadPort => new UriFormatException("SR.net_uri_BadPort"),
            ParsingError.BadAuthorityTerminator => new UriFormatException("SR.net_uri_BadAuthorityTerminator"),
            ParsingError.CannotCreateRelative => new UriFormatException("SR.net_uri_CannotCreateRelative"),
            _ => new UriFormatException("SR.net_uri_BadFormat"),
        };
    }

    private static bool StaticIsFile(UriParser syntax)
    {
        return syntax.InFact(UriSyntaxFlags.FileLikeUri);
    }

    private string GetLocalPath()
    {
        EnsureParseRemaining();
        if (IsUncOrDosPath)
        {
            EnsureHostString(allowDnsOptimization: false);
            int num;
            if (NotAny(Flags.HostNotCanonical | Flags.PathNotCanonical | Flags.ShouldBeCompressed))
            {
                num = (IsUncPath ? (_info.Offset.Host - 2) : _info.Offset.Path);
                string text =
                    ((IsImplicitFile && _info.Offset.Host == ((!IsDosPath) ? 2 : 0) &&
                      _info.Offset.Query == _info.Offset.End)
                        ? _string
                        : ((IsDosPath && (_string[num] == '/' || _string[num] == '\\'))
                            ? _string.Substring(num + 1, _info.Offset.Query - num - 1)
                            : _string.Substring(num, _info.Offset.Query - num)));
                if (IsDosPath && text[1] == '|')
                {
                    text = text.Remove(1, 1);
                    text = text.Insert(1, ":");
                }

                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '/')
                    {
                        text = text.Replace('/', '\\');
                        break;
                    }
                }

                return text;
            }

            int destPosition = 0;
            num = _info.Offset.Path;
            string host = _info.Host;
            char[] array = new char[host.Length + 3 + _info.Offset.Fragment - _info.Offset.Path];
            if (IsUncPath)
            {
                array[0] = '\\';
                array[1] = '\\';
                destPosition = 2;
                UriHelper.UnescapeString(host, 0, host.Length, array, ref destPosition, '\uffff', '\uffff', '\uffff',
                    UnescapeMode.CopyOnly, _syntax, isQuery: false);
            }
            else if (_string[num] == '/' || _string[num] == '\\')
            {
                num++;
            }

            ushort num2 = (ushort)destPosition;
            UnescapeMode unescapeMode = ((InFact(Flags.PathNotCanonical) && !IsImplicitFile)
                ? (UnescapeMode.Unescape | UnescapeMode.UnescapeAll)
                : UnescapeMode.CopyOnly);
            UriHelper.UnescapeString(_string, num, _info.Offset.Query, array, ref destPosition, '\uffff', '\uffff',
                '\uffff', unescapeMode, _syntax, isQuery: true);
            if (array[1] == '|')
            {
                array[1] = ':';
            }

            if (InFact(Flags.ShouldBeCompressed))
            {
                array = Compress(array, (ushort)(IsDosPath ? (num2 + 2) : num2), ref destPosition, _syntax);
            }

            for (ushort num3 = 0; num3 < (ushort)destPosition; num3++)
            {
                if (array[num3] == '/')
                {
                    array[num3] = '\\';
                }
            }

            return new string(array, 0, destPosition);
        }

        return GetUnescapedParts(UriComponents.Path | UriComponents.KeepDelimiter, UriFormat.Unescaped);
    }

    public static unsafe UriHostNameType CheckHostName(string? name)
    {
        if (name == null || name.Length == 0 || name.Length > 32767)
        {
            return UriHostNameType.Unknown;
        }

        int end = name.Length;
        fixed (char* name2 = name)
        {
            if (name[0] == '[' && name[name.Length - 1] == ']' && IPv6AddressHelper.IsValid(name2, 1, ref end) &&
                end == name.Length)
            {
                return UriHostNameType.IPv6;
            }

            end = name.Length;
            if (IPv4AddressHelper.IsValid(name2, 0, ref end, allowIPv6: false, notImplicitFile: false,
                    unknownScheme: false) && end == name.Length)
            {
                return UriHostNameType.IPv4;
            }

            end = name.Length;
            bool notCanonical = false;
            if (DomainNameHelper.IsValid(name2, 0, ref end, ref notCanonical, notImplicitFile: false) &&
                end == name.Length)
            {
                return UriHostNameType.Dns;
            }

            end = name.Length;
            notCanonical = false;
            if (DomainNameHelper.IsValidByIri(name2, 0, ref end, ref notCanonical, notImplicitFile: false) &&
                end == name.Length)
            {
                return UriHostNameType.Dns;
            }
        }

        end = name.Length + 2;
        name = "[" + name + "]";
        fixed (char* name3 = name)
        {
            if (IPv6AddressHelper.IsValid(name3, 1, ref end) && end == name.Length)
            {
                return UriHostNameType.IPv6;
            }
        }

        return UriHostNameType.Unknown;
    }

    public string GetLeftPart(UriPartial part)
    {
        if (IsNotAbsoluteUri)
        {
            throw new InvalidOperationException("SR.net_uri_NotAbsolute");
        }

        EnsureUriInfo();
        switch (part)
        {
            case UriPartial.Scheme:
                return GetParts(UriComponents.Scheme | UriComponents.KeepDelimiter, UriFormat.UriEscaped);
            case UriPartial.Authority:
                if (NotAny(Flags.AuthorityFound) || IsDosPath)
                {
                    return string.Empty;
                }

                return GetParts(UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.UriEscaped);
            case UriPartial.Path:
                return GetParts(UriComponents.SchemeAndServer | UriComponents.UserInfo | UriComponents.Path,
                    UriFormat.UriEscaped);
            case UriPartial.Query:
                return GetParts(UriComponents.HttpRequestUrl | UriComponents.UserInfo, UriFormat.UriEscaped);
            default:
                throw new ArgumentException(SR.Format("SR.Argument_InvalidUriSubcomponent", part), "part");
        }
    }

    public static string HexEscape(char character)
    {
        if (character > 'ÿ')
        {
            throw new ArgumentOutOfRangeException("character");
        }
        return StringExEx.Create(3, character, delegate(Span<char> chars, char c)
        {
            chars[0] = '%';
            chars[1] = UriHelper.s_hexUpperChars[(c & 0xF0) >> 4];
            chars[2] = UriHelper.s_hexUpperChars[c & 0xF];
        });
    }

    public static char HexUnescape(string pattern, ref int index)
    {
        if (index < 0 || index >= pattern.Length)
        {
            throw new ArgumentOutOfRangeException("index");
        }

        if (pattern[index] == '%' && pattern.Length - index >= 3)
        {
            char c = UriHelper.EscapedAscii(pattern[index + 1], pattern[index + 2]);
            if (c != '\uffff')
            {
                index += 3;
                return c;
            }
        }

        return pattern[index++];
    }

    public static bool IsHexEncoding(string pattern, int index)
    {
        if (pattern.Length - index >= 3 && pattern[index] == '%' && IsHexDigit(pattern[index + 1]))
        {
            return IsHexDigit(pattern[index + 2]);
        }

        return false;
    }

    public static bool CheckSchemeName(string? schemeName)
    {
        if (schemeName == null || schemeName.Length == 0 || !UriHelper.IsAsciiLetter(schemeName[0]))
        {
            return false;
        }

        for (int num = schemeName.Length - 1; num > 0; num--)
        {
            if (!UriHelper.IsAsciiLetterOrDigit(schemeName[num]) && schemeName[num] != '+' && schemeName[num] != '-' &&
                schemeName[num] != '.')
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsHexDigit(char character)
    {
        if ((uint)(character - 48) > 9u && (uint)(character - 65) > 5u)
        {
            return (uint)(character - 97) <= 5u;
        }

        return true;
    }

    public static int FromHex(char digit)
    {
        switch (digit)
        {
            default:
                throw new ArgumentException("digit");
            case 'a':
            case 'b':
            case 'c':
            case 'd':
            case 'e':
            case 'f':
                return digit - 97 + 10;
            case 'A':
            case 'B':
            case 'C':
            case 'D':
            case 'E':
            case 'F':
                return digit - 65 + 10;
            case '0':
            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
                return digit - 48;
        }
    }

    public override int GetHashCode()
    {
        if (IsNotAbsoluteUri)
        {
            return CalculateCaseInsensitiveHashCode(OriginalString);
        }

        UriInfo uriInfo = EnsureUriInfo();
        if (uriInfo.MoreInfo == null)
        {
            uriInfo.MoreInfo = new MoreInfo();
        }

        int num = uriInfo.MoreInfo.Hash;
        if (num == 0)
        {
            string text = uriInfo.MoreInfo.RemoteUrl;
            if (text == null)
            {
                text = GetParts(UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped);
            }

            num = CalculateCaseInsensitiveHashCode(text);
            if (num == 0)
            {
                num = 16777216;
            }

            uriInfo.MoreInfo.Hash = num;
        }

        return num;
    }

    public override string ToString()
    {
        if (_syntax == null)
        {
            if (!_iriParsing || !InFact(Flags.HasUnicode))
            {
                return OriginalString;
            }

            return _string;
        }

        EnsureUriInfo();
        if (_info.String == null)
        {
            if (Syntax.IsSimple)
            {
                _info.String = GetComponentsHelper(UriComponents.AbsoluteUri, (UriFormat)32767);
            }
            else
            {
                _info.String = GetParts(UriComponents.AbsoluteUri, UriFormat.SafeUnescaped);
            }
        }

        return _info.String;
    }

    public static bool operator ==(Uri? uri1, Uri? uri2)
    {
        if ((object)uri1 == uri2)
        {
            return true;
        }

        if ((object)uri1 == null || (object)uri2 == null)
        {
            return false;
        }

        return uri2.Equals(uri1);
    }

    public static bool operator !=(Uri? uri1, Uri? uri2)
    {
        if ((object)uri1 == uri2)
        {
            return false;
        }

        if ((object)uri1 == null || (object)uri2 == null)
        {
            return true;
        }

        return !uri2.Equals(uri1);
    }

    public override unsafe bool Equals(object? comparand)
    {
        //The blocks IL_00e2, IL_00f5, IL_0109, IL_010f, IL_0114, IL_0119 are reachable both inside and outside the pinned region starting at IL_00dd. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
        //The blocks IL_0381, IL_03a1, IL_03b3, IL_03b5, IL_03bb are reachable both inside and outside the pinned region starting at IL_037c. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
        if (comparand == null)
        {
            return false;
        }

        if (this == comparand)
        {
            return true;
        }

        Uri result = comparand as Uri;
        if ((object)result == null)
        {
            if (!(comparand is string uriString))
            {
                return false;
            }

            if (!TryCreate(uriString, UriKind.RelativeOrAbsolute, out result))
            {
                return false;
            }
        }

        if ((object)_string == result._string)
        {
            return true;
        }

        if (IsAbsoluteUri != result.IsAbsoluteUri)
        {
            return false;
        }

        if (IsNotAbsoluteUri)
        {
            return OriginalString.Equals(result.OriginalString);
        }

        if (NotAny(Flags.AllUriInfoSet) || result.NotAny(Flags.AllUriInfoSet))
        {
            if (!IsUncOrDosPath)
            {
                if (_string.Length == result._string.Length)
                {
                    fixed (char* ptr2 = _string)
                    {
                        string @string = result._string;
                        char* intPtr;
                        if (@string == null)
                        {
                            char* ptr;
                            intPtr = (ptr = null);
                            int num = _string.Length - 1;
                            while (num >= 0 && ptr2[num] == ptr[num])
                            {
                                num--;
                            }

                            if (num == -1)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            fixed (char* ptr3 = &@string.GetPinnableReference())
                            {
                                char* ptr;
                                intPtr = (ptr = ptr3);
                                int num = _string.Length - 1;
                                while (num >= 0 && ptr2[num] == ptr[num])
                                {
                                    num--;
                                }

                                if (num == -1)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            else if (string.Equals(_string, result._string, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        EnsureUriInfo();
        result.EnsureUriInfo();
        if (!UserDrivenParsing && !result.UserDrivenParsing && Syntax.IsSimple && result.Syntax.IsSimple)
        {
            if (InFact(Flags.CanonicalDnsHost) && result.InFact(Flags.CanonicalDnsHost))
            {
                ushort num2 = _info.Offset.Host;
                ushort num3 = _info.Offset.Path;
                ushort num4 = result._info.Offset.Host;
                ushort path = result._info.Offset.Path;
                string string2 = result._string;
                if (num3 - num2 > path - num4)
                {
                    num3 = (ushort)(num2 + path - num4);
                }

                while (num2 < num3)
                {
                    if (_string[num2] != string2[num4])
                    {
                        return false;
                    }

                    if (string2[num4] == ':')
                    {
                        break;
                    }

                    num2++;
                    num4++;
                }

                if (num2 < _info.Offset.Path && _string[num2] != ':')
                {
                    return false;
                }

                if (num4 < path && string2[num4] != ':')
                {
                    return false;
                }
            }
            else
            {
                EnsureHostString(allowDnsOptimization: false);
                result.EnsureHostString(allowDnsOptimization: false);
                if (!_info.Host.Equals(result._info.Host))
                {
                    return false;
                }
            }

            if (Port != result.Port)
            {
                return false;
            }
        }

        UriInfo info = _info;
        UriInfo info2 = result._info;
        if (info.MoreInfo == null)
        {
            info.MoreInfo = new MoreInfo();
        }

        if (info2.MoreInfo == null)
        {
            info2.MoreInfo = new MoreInfo();
        }

        string text = info.MoreInfo.RemoteUrl;
        if (text == null)
        {
            text = GetParts(UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped);
            info.MoreInfo.RemoteUrl = text;
        }

        string text2 = info2.MoreInfo.RemoteUrl;
        if (text2 == null)
        {
            text2 = result.GetParts(UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped);
            info2.MoreInfo.RemoteUrl = text2;
        }

        if (!IsUncOrDosPath)
        {
            if (text.Length != text2.Length)
            {
                return false;
            }

            fixed (char* ptr6 = text)
            {
                char* intPtr2;
                char* intPtr3;
                int num5;
                char* intPtr4;
                char* ptr5;
                char* ptr7;
                char* ptr8;
                if (text2 != null)
                {
                    fixed (char* ptr4 = &text2.GetPinnableReference())
                    {
                        intPtr2 = (ptr5 = ptr4);
                        ptr7 = ptr6 + text.Length;
                        ptr8 = ptr5 + text.Length;
                        while (ptr7 != ptr6)
                        {
                            intPtr3 = --ptr7;
                            num5 = *intPtr3;
                            intPtr4 = --ptr8;
                            if (num5 != *intPtr4)
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                }

                intPtr2 = (ptr5 = null);
                ptr7 = ptr6 + text.Length;
                ptr8 = ptr5 + text.Length;
                while (ptr7 != ptr6)
                {
                    intPtr3 = --ptr7;
                    num5 = *intPtr3;
                    intPtr4 = --ptr8;
                    if (num5 != *intPtr4)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        return string.Compare(info.MoreInfo.RemoteUrl, info2.MoreInfo.RemoteUrl,
            IsUncOrDosPath ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0;
    }

    public Uri MakeRelativeUri(Uri uri)
    {
        if ((object)uri == null)
        {
            throw new ArgumentNullException("uri");
        }

        if (IsNotAbsoluteUri || uri.IsNotAbsoluteUri)
        {
            throw new InvalidOperationException("SR.net_uri_NotAbsolute");
        }

        if (Scheme == uri.Scheme && Host == uri.Host && Port == uri.Port)
        {
            string absolutePath = uri.AbsolutePath;
            string text = PathDifference(AbsolutePath, absolutePath, !IsUncOrDosPath);
            if (CheckForColonInFirstPathSegment(text) &&
                (!uri.IsDosPath || !absolutePath.Equals(text, StringComparison.Ordinal)))
            {
                text = "./" + text;
            }

            text += uri.GetParts(UriComponents.Query | UriComponents.Fragment, UriFormat.UriEscaped);
            return new Uri(text, UriKind.Relative);
        }

        return uri;
    }

    private static bool CheckForColonInFirstPathSegment(string uriString)
    {
        int num = uriString.IndexOfAny(s_pathDelims);
        if (num >= 0)
        {
            return uriString[num] == ':';
        }

        return false;
    }

    internal static string InternalEscapeString(string rawString)
    {
        if (rawString == null)
        {
            return string.Empty;
        }

        int destPos = 0;
        char[] array = UriHelper.EscapeString(rawString, 0, rawString.Length, null, ref destPos, isUriString: true, '?',
            '#', '%');
        if (array == null)
        {
            return rawString;
        }

        return new string(array, 0, destPos);
    }

    private static unsafe ParsingError ParseScheme(string uriString, ref Flags flags, ref UriParser syntax)
    {
        int length = uriString.Length;
        if (length == 0)
        {
            return ParsingError.EmptyUriString;
        }

        if (length >= 65520)
        {
            return ParsingError.SizeLimit;
        }

        fixed (char* uriString2 = uriString)
        {
            ParsingError err = ParsingError.None;
            ushort num = ParseSchemeCheckImplicitFile(uriString2, (ushort)length, ref err, ref flags, ref syntax);
            if (err != 0)
            {
                return err;
            }

            flags |= (Flags)num;
        }

        return ParsingError.None;
    }

    internal UriFormatException ParseMinimal()
    {
        ParsingError parsingError = PrivateParseMinimal();
        if (parsingError == ParsingError.None)
        {
            return null;
        }

        _flags |= Flags.ErrorOrParsingRecursion;
        return GetException(parsingError);
    }

    private unsafe ParsingError PrivateParseMinimal()
    {
        ushort num = (ushort)(_flags & Flags.IndexMask);
        ushort num2 = (ushort)_string.Length;
        string newHost = null;
        _flags &= ~(Flags.IndexMask | Flags.UserDrivenParsing);
        fixed (char* ptr =
                   ((_iriParsing && (_flags & Flags.HasUnicode) != Flags.Zero &&
                     (_flags & Flags.HostUnicodeNormalized) == Flags.Zero)
                       ? _originalUnicodeString
                       : _string))
        {
            if (num2 > num && UriHelper.IsLWS(ptr[num2 - 1]))
            {
                num2--;
                while (num2 != num && UriHelper.IsLWS(ptr[(int)(--num2)]))
                {
                }

                num2++;
            }

            if (_syntax.IsAllSet(UriSyntaxFlags.AllowEmptyHost | UriSyntaxFlags.AllowDOSPath) &&
                NotAny(Flags.ImplicitFile) && num + 1 < num2)
            {
                ushort num3 = num;
                char c;
                while (num3 < num2 && ((c = ptr[(int)num3]) == '\\' || c == '/'))
                {
                    num3++;
                }

                if (_syntax.InFact(UriSyntaxFlags.FileLikeUri) || num3 - num <= 3)
                {
                    if (num3 - num >= 2)
                    {
                        _flags |= Flags.AuthorityFound;
                    }

                    if (num3 + 1 < num2 && ((c = ptr[num3 + 1]) == ':' || c == '|') &&
                        UriHelper.IsAsciiLetter(ptr[(int)num3]))
                    {
                        if (num3 + 2 >= num2 || ((c = ptr[num3 + 2]) != '\\' && c != '/'))
                        {
                            if (_syntax.InFact(UriSyntaxFlags.FileLikeUri))
                            {
                                return ParsingError.MustRootedPath;
                            }
                        }
                        else
                        {
                            _flags |= Flags.DosPath;
                            if (_syntax.InFact(UriSyntaxFlags.MustHaveAuthority))
                            {
                                _flags |= Flags.AuthorityFound;
                            }

                            num = ((num3 == num || num3 - num == 2) ? num3 : ((ushort)(num3 - 1)));
                        }
                    }
                    else if (_syntax.InFact(UriSyntaxFlags.FileLikeUri) && num3 - num >= 2 && num3 - num != 3 &&
                             num3 < num2 && ptr[(int)num3] != '?' && ptr[(int)num3] != '#')
                    {
                        _flags |= Flags.UncPath;
                        num = num3;
                    }
                }
            }

            if ((_flags & (Flags.DosPath | Flags.UncPath | Flags.UnixPath)) == Flags.Zero)
            {
                if (num + 2 <= num2)
                {
                    char c2 = ptr[(int)num];
                    char c3 = ptr[num + 1];
                    if (_syntax.InFact(UriSyntaxFlags.MustHaveAuthority))
                    {
                        if ((c2 != '/' && c2 != '\\') || (c3 != '/' && c3 != '\\'))
                        {
                            return ParsingError.BadAuthority;
                        }

                        _flags |= Flags.AuthorityFound;
                        num += 2;
                    }
                    else if (_syntax.InFact(UriSyntaxFlags.OptionalAuthority) &&
                             (InFact(Flags.AuthorityFound) || (c2 == '/' && c3 == '/')))
                    {
                        _flags |= Flags.AuthorityFound;
                        num += 2;
                    }
                    else if (_syntax.NotAny(UriSyntaxFlags.MailToLikeUri))
                    {
                        if (_iriParsing && (_flags & Flags.HasUnicode) != Flags.Zero &&
                            (_flags & Flags.HostUnicodeNormalized) == Flags.Zero)
                        {
                            _string = _string.Substring(0, num);
                        }

                        _flags |= (Flags)((ulong)num | 0x70000uL);
                        return ParsingError.None;
                    }
                }
                else
                {
                    if (_syntax.InFact(UriSyntaxFlags.MustHaveAuthority))
                    {
                        return ParsingError.BadAuthority;
                    }

                    if (_syntax.NotAny(UriSyntaxFlags.MailToLikeUri))
                    {
                        if (_iriParsing && (_flags & Flags.HasUnicode) != Flags.Zero &&
                            (_flags & Flags.HostUnicodeNormalized) == Flags.Zero)
                        {
                            _string = _string.Substring(0, num);
                        }

                        _flags |= (Flags)((ulong)num | 0x70000uL);
                        return ParsingError.None;
                    }
                }
            }

            if (InFact(Flags.DosPath))
            {
                _flags |= (Flags)(((_flags & Flags.AuthorityFound) != Flags.Zero) ? 327680 : 458752);
                _flags |= (Flags)num;
                return ParsingError.None;
            }

            ParsingError err = ParsingError.None;
            num = CheckAuthorityHelper(ptr, num, num2, ref err, ref _flags, _syntax, ref newHost);
            if (err != 0)
            {
                return err;
            }

            if (num < num2)
            {
                char c4 = ptr[(int)num];
                if (c4 == '\\' && NotAny(Flags.ImplicitFile) && _syntax.NotAny(UriSyntaxFlags.AllowDOSPath))
                {
                    return ParsingError.BadAuthorityTerminator;
                }
            }

            _flags |= (Flags)num;
        }

        if (s_IdnScope != 0 || _iriParsing)
        {
            PrivateParseMinimalIri(newHost, num);
        }

        return ParsingError.None;
    }

    private void PrivateParseMinimalIri(string newHost, ushort idx)
    {
        if (newHost != null)
        {
            _string = newHost;
        }

        if ((!_iriParsing && AllowIdn &&
             ((_flags & Flags.IdnHost) != Flags.Zero || (_flags & Flags.UnicodeHost) != Flags.Zero)) || (_iriParsing &&
                (_flags & Flags.HasUnicode) == Flags.Zero && AllowIdn && (_flags & Flags.IdnHost) != Flags.Zero))
        {
            _flags &= ~Flags.IndexMask;
            _flags |= (Flags)_string.Length;
            _string += _originalUnicodeString.AsSpan(idx, _originalUnicodeString.Length - idx);
        }

        if (_iriParsing && (_flags & Flags.HasUnicode) != Flags.Zero)
        {
            _flags |= Flags.UseOrigUncdStrOffset;
        }
    }

    private unsafe void CreateUriInfo(Flags cF)
    {
        UriInfo uriInfo = new UriInfo();
        uriInfo.Offset.End = (ushort)_string.Length;
        if (!UserDrivenParsing)
        {
            bool flag = false;
            ushort num;
            if ((cF & Flags.ImplicitFile) != Flags.Zero)
            {
                num = 0;
                while (UriHelper.IsLWS(_string[num]))
                {
                    num++;
                    uriInfo.Offset.Scheme++;
                }

                if (StaticInFact(cF, Flags.UncPath))
                {
                    num += 2;
                    while (num < (ushort)(cF & Flags.IndexMask) && (_string[num] == '/' || _string[num] == '\\'))
                    {
                        num++;
                    }
                }
            }
            else
            {
                num = (ushort)_syntax.SchemeName.Length;
                while (_string[num++] != ':')
                {
                    uriInfo.Offset.Scheme++;
                }

                if ((cF & Flags.AuthorityFound) != Flags.Zero)
                {
                    if (_string[num] == '\\' || _string[num + 1] == '\\')
                    {
                        flag = true;
                    }

                    num += 2;
                    if ((cF & (Flags.DosPath | Flags.UncPath)) != Flags.Zero)
                    {
                        while (num < (ushort)(cF & Flags.IndexMask) && (_string[num] == '/' || _string[num] == '\\'))
                        {
                            flag = true;
                            num++;
                        }
                    }
                }
            }

            if (_syntax.DefaultPort != -1)
            {
                uriInfo.Offset.PortValue = (ushort)_syntax.DefaultPort;
            }

            if ((cF & Flags.HostTypeMask) == Flags.HostTypeMask || StaticInFact(cF, Flags.DosPath))
            {
                uriInfo.Offset.User = (ushort)(cF & Flags.IndexMask);
                uriInfo.Offset.Host = uriInfo.Offset.User;
                uriInfo.Offset.Path = uriInfo.Offset.User;
                cF = (Flags)((ulong)cF & 0xFFFFFFFFFFFF0000uL);
                if (flag)
                {
                    cF |= Flags.SchemeNotCanonical;
                }
            }
            else
            {
                uriInfo.Offset.User = num;
                if (HostType == Flags.BasicHostType)
                {
                    uriInfo.Offset.Host = num;
                    uriInfo.Offset.Path = (ushort)(cF & Flags.IndexMask);
                    cF = (Flags)((ulong)cF & 0xFFFFFFFFFFFF0000uL);
                }
                else
                {
                    if ((cF & Flags.HasUserInfo) != Flags.Zero)
                    {
                        while (_string[num] != '@')
                        {
                            num++;
                        }

                        num++;
                        uriInfo.Offset.Host = num;
                    }
                    else
                    {
                        uriInfo.Offset.Host = num;
                    }

                    num = (ushort)(cF & Flags.IndexMask);
                    cF = (Flags)((ulong)cF & 0xFFFFFFFFFFFF0000uL);
                    if (flag)
                    {
                        cF |= Flags.SchemeNotCanonical;
                    }

                    uriInfo.Offset.Path = num;
                    bool flag2 = false;
                    bool flag3 = (cF & Flags.UseOrigUncdStrOffset) != 0;
                    cF &= ~Flags.UseOrigUncdStrOffset;
                    if (flag3)
                    {
                        uriInfo.Offset.End = (ushort)_originalUnicodeString.Length;
                    }

                    if (num < uriInfo.Offset.End)
                    {
                        fixed (char* ptr = (flag3 ? _originalUnicodeString : _string))
                        {
                            if (ptr[(int)num] == ':')
                            {
                                int num2 = 0;
                                if (++num < uriInfo.Offset.End)
                                {
                                    num2 = (ushort)(ptr[(int)num] - 48);
                                    if (num2 <= 9)
                                    {
                                        flag2 = true;
                                        if (num2 == 0)
                                        {
                                            cF |= Flags.PortNotCanonical | Flags.E_PortNotCanonical;
                                        }

                                        for (num++; num < uriInfo.Offset.End; num++)
                                        {
                                            ushort num3 = (ushort)(ptr[(int)num] - 48);
                                            if (num3 > 9)
                                            {
                                                break;
                                            }

                                            num2 = num2 * 10 + num3;
                                        }
                                    }
                                }

                                if (flag2 && uriInfo.Offset.PortValue != (ushort)num2)
                                {
                                    uriInfo.Offset.PortValue = (ushort)num2;
                                    cF |= Flags.NotDefaultPort;
                                }
                                else
                                {
                                    cF |= Flags.PortNotCanonical | Flags.E_PortNotCanonical;
                                }

                                uriInfo.Offset.Path = num;
                            }
                        }
                    }
                }
            }
        }

        cF |= Flags.MinimalUriInfoSet;
        uriInfo.DnsSafeHost = _dnsSafeHost;
        lock (_string)
        {
            if ((_flags & Flags.MinimalUriInfoSet) == Flags.Zero)
            {
                _info = uriInfo;
                _flags = (Flags)(((ulong)_flags & 0xFFFFFFFFFFFF0000uL) | (ulong)cF);
            }
        }
    }

    private unsafe void CreateHostString()
    {
        if (!_syntax.IsSimple)
        {
            lock (_info)
            {
                if (NotAny(Flags.ErrorOrParsingRecursion))
                {
                    _flags |= Flags.ErrorOrParsingRecursion;
                    GetHostViaCustomSyntax();
                    _flags &= ~Flags.ErrorOrParsingRecursion;
                    return;
                }
            }
        }

        Flags flags = _flags;
        string text =
            CreateHostStringHelper(_string, _info.Offset.Host, _info.Offset.Path, ref flags, ref _info.ScopeId);
        if (text.Length != 0)
        {
            if (HostType == Flags.BasicHostType)
            {
                ushort idx = 0;
                Check check;
                fixed (char* str = text)
                {
                    check = CheckCanonical(str, ref idx, (ushort)text.Length, '\uffff');
                }

                if ((check & Check.DisplayCanonical) == 0 &&
                    (NotAny(Flags.ImplicitFile) || (check & Check.ReservedFound) != 0))
                {
                    flags |= Flags.HostNotCanonical;
                }

                if (InFact(Flags.ImplicitFile) && (check & (Check.EscapedCanonical | Check.ReservedFound)) != 0)
                {
                    check &= ~Check.EscapedCanonical;
                }

                if ((check & (Check.EscapedCanonical | Check.BackslashInPath)) != Check.EscapedCanonical)
                {
                    flags |= Flags.E_HostNotCanonical;
                    if (NotAny(Flags.UserEscaped))
                    {
                        int destPos = 0;
                        char[] array = UriHelper.EscapeString(text, 0, text.Length, null, ref destPos,
                            isUriString: true, '?', '#', IsImplicitFile ? '\uffff' : '%');
                        if (array != null)
                        {
                            text = new string(array, 0, destPos);
                        }
                    }
                }
            }
            else if (NotAny(Flags.CanonicalDnsHost))
            {
                if (_info.ScopeId != null)
                {
                    flags |= Flags.HostNotCanonical | Flags.E_HostNotCanonical;
                }
                else
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        if (_info.Offset.Host + i >= _info.Offset.End || text[i] != _string[_info.Offset.Host + i])
                        {
                            flags |= Flags.HostNotCanonical | Flags.E_HostNotCanonical;
                            break;
                        }
                    }
                }
            }
        }

        _info.Host = text;
        lock (_info)
        {
            _flags |= flags;
        }
    }

    private static string CreateHostStringHelper(string str, ushort idx, ushort end, ref Flags flags,
        ref string scopeId)
    {
        bool loopback = false;
        string text;
        switch (flags & Flags.HostTypeMask)
        {
            case Flags.DnsHostType:
                text = DomainNameHelper.ParseCanonicalName(str, idx, end, ref loopback);
                break;
            case Flags.IPv6HostType:
                text = IPv6AddressHelper.ParseCanonicalName(str, idx, ref loopback, ref scopeId);
                break;
            case Flags.IPv4HostType:
                text = IPv4AddressHelper.ParseCanonicalName(str, idx, end, ref loopback);
                break;
            case Flags.UncHostType:
                text = UncNameHelper.ParseCanonicalName(str, idx, end, ref loopback);
                break;
            case Flags.BasicHostType:
                text = ((!StaticInFact(flags, Flags.DosPath)) ? str.Substring(idx, end - idx) : string.Empty);
                if (text.Length == 0)
                {
                    loopback = true;
                }

                break;
            case Flags.HostTypeMask:
                text = string.Empty;
                break;
            default:
                throw GetException(ParsingError.BadHostName);
        }

        if (loopback)
        {
            flags |= Flags.LoopbackHost;
        }

        return text;
    }

    private unsafe void GetHostViaCustomSyntax()
    {
        if (_info.Host != null)
        {
            return;
        }

        string text = _syntax.InternalGetComponents(this, UriComponents.Host, UriFormat.UriEscaped);
        if (_info.Host == null)
        {
            if (text.Length >= 65520)
            {
                throw GetException(ParsingError.SizeLimit);
            }

            ParsingError err = ParsingError.None;
            Flags flags = (Flags)((ulong)_flags & 0xFFFFFFFFFFF8FFFFuL);
            fixed (char* pString = text)
            {
                string newHost = null;
                if (CheckAuthorityHelper(pString, 0, (ushort)text.Length, ref err, ref flags, _syntax, ref newHost) !=
                    (ushort)text.Length)
                {
                    flags = (Flags)((ulong)flags & 0xFFFFFFFFFFF8FFFFuL);
                    flags |= Flags.HostTypeMask;
                }
            }

            if (err != 0 || (flags & Flags.HostTypeMask) == Flags.HostTypeMask)
            {
                _flags = (Flags)(((ulong)_flags & 0xFFFFFFFFFFF8FFFFuL) | 0x50000);
            }
            else
            {
                text = CreateHostStringHelper(text, 0, (ushort)text.Length, ref flags, ref _info.ScopeId);
                for (int i = 0; i < text.Length; i++)
                {
                    if (_info.Offset.Host + i >= _info.Offset.End || text[i] != _string[_info.Offset.Host + i])
                    {
                        _flags |= Flags.HostNotCanonical | Flags.E_HostNotCanonical;
                        break;
                    }
                }

                _flags = (Flags)(((ulong)_flags & 0xFFFFFFFFFFF8FFFFuL) | (ulong)(flags & Flags.HostTypeMask));
            }
        }

        string text2 = _syntax.InternalGetComponents(this, UriComponents.StrongPort, UriFormat.UriEscaped);
        int num = 0;
        if (text2 == null || text2.Length == 0)
        {
            _flags &= ~Flags.NotDefaultPort;
            _flags |= Flags.PortNotCanonical | Flags.E_PortNotCanonical;
            _info.Offset.PortValue = 0;
        }
        else
        {
            for (int j = 0; j < text2.Length; j++)
            {
                int num2 = text2[j] - 48;
                if (num2 < 0 || num2 > 9 || (num = num * 10 + num2) > 65535)
                {
                    throw new UriFormatException(SR.Format("SR.net_uri_PortOutOfRange", _syntax.GetType(), text2));
                }
            }

            if (num != _info.Offset.PortValue)
            {
                if (num == _syntax.DefaultPort)
                {
                    _flags &= ~Flags.NotDefaultPort;
                }
                else
                {
                    _flags |= Flags.NotDefaultPort;
                }

                _flags |= Flags.PortNotCanonical | Flags.E_PortNotCanonical;
                _info.Offset.PortValue = (ushort)num;
            }
        }

        _info.Host = text;
    }

    internal string GetParts(UriComponents uriParts, UriFormat formatAs)
    {
        return GetComponents(uriParts, formatAs);
    }

    private string GetEscapedParts(UriComponents uriParts)
    {
        ushort num = (ushort)(((ushort)_flags & 0x3F80) >> 6);
        if (InFact(Flags.SchemeNotCanonical))
        {
            num = (ushort)(num | 1u);
        }

        if ((uriParts & UriComponents.Path) != 0)
        {
            if (InFact(Flags.ShouldBeCompressed | Flags.FirstSlashAbsent | Flags.BackslashInPath))
            {
                num = (ushort)(num | 0x10u);
            }
            else if (IsDosPath && _string[_info.Offset.Path + SecuredPathIndex - 1] == '|')
            {
                num = (ushort)(num | 0x10u);
            }
        }

        if (((ushort)uriParts & num) == 0)
        {
            string uriPartsFromUserString = GetUriPartsFromUserString(uriParts);
            if (uriPartsFromUserString != null)
            {
                return uriPartsFromUserString;
            }
        }

        return ReCreateParts(uriParts, num, UriFormat.UriEscaped);
    }

    private string GetUnescapedParts(UriComponents uriParts, UriFormat formatAs)
    {
        ushort num = (ushort)((ushort)_flags & 0x7Fu);
        if ((uriParts & UriComponents.Path) != 0)
        {
            if ((_flags & (Flags.ShouldBeCompressed | Flags.FirstSlashAbsent | Flags.BackslashInPath)) != Flags.Zero)
            {
                num = (ushort)(num | 0x10u);
            }
            else if (IsDosPath && _string[_info.Offset.Path + SecuredPathIndex - 1] == '|')
            {
                num = (ushort)(num | 0x10u);
            }
        }

        if (((ushort)uriParts & num) == 0)
        {
            string uriPartsFromUserString = GetUriPartsFromUserString(uriParts);
            if (uriPartsFromUserString != null)
            {
                return uriPartsFromUserString;
            }
        }

        return ReCreateParts(uriParts, num, formatAs);
    }

    private unsafe string ReCreateParts(UriComponents parts, ushort nonCanonical, UriFormat formatAs)
    {
        EnsureHostString(allowDnsOptimization: false);
        string text = (((parts & UriComponents.Host) == 0) ? string.Empty : _info.Host);
        int num = (_info.Offset.End - _info.Offset.User) * ((formatAs != UriFormat.UriEscaped) ? 1 : 12);
        char[] array = new char[text.Length + num + _syntax.SchemeName.Length + 3 + 1];
        num = 0;
        if ((parts & UriComponents.Scheme) != 0)
        {
            _syntax.SchemeName.CopyTo(0, array, num, _syntax.SchemeName.Length);
            num += _syntax.SchemeName.Length;
            if (parts != UriComponents.Scheme)
            {
                array[num++] = ':';
                if (InFact(Flags.AuthorityFound))
                {
                    array[num++] = '/';
                    array[num++] = '/';
                }
            }
        }

        if ((parts & UriComponents.UserInfo) != 0 && InFact(Flags.HasUserInfo))
        {
            if ((nonCanonical & 2u) != 0)
            {
                switch (formatAs)
                {
                    case UriFormat.UriEscaped:
                        if (NotAny(Flags.UserEscaped))
                        {
                            array = UriHelper.EscapeString(_string, _info.Offset.User, _info.Offset.Host, array,
                                ref num, isUriString: true, '?', '#', '%');
                            break;
                        }

                        InFact(Flags.E_UserNotCanonical);
                        _string.CopyTo(_info.Offset.User, array, num, _info.Offset.Host - _info.Offset.User);
                        num += _info.Offset.Host - _info.Offset.User;
                        break;
                    case UriFormat.SafeUnescaped:
                        array = UriHelper.UnescapeString(_string, _info.Offset.User, _info.Offset.Host - 1, array,
                            ref num, '@', '/', '\\',
                            InFact(Flags.UserEscaped) ? UnescapeMode.Unescape : UnescapeMode.EscapeUnescape, _syntax,
                            isQuery: false);
                        array[num++] = '@';
                        break;
                    case UriFormat.Unescaped:
                        array = UriHelper.UnescapeString(_string, _info.Offset.User, _info.Offset.Host, array, ref num,
                            '\uffff', '\uffff', '\uffff', UnescapeMode.Unescape | UnescapeMode.UnescapeAll, _syntax,
                            isQuery: false);
                        break;
                    default:
                        array = UriHelper.UnescapeString(_string, _info.Offset.User, _info.Offset.Host, array, ref num,
                            '\uffff', '\uffff', '\uffff', UnescapeMode.CopyOnly, _syntax, isQuery: false);
                        break;
                }
            }
            else
            {
                UriHelper.UnescapeString(_string, _info.Offset.User, _info.Offset.Host, array, ref num, '\uffff',
                    '\uffff', '\uffff', UnescapeMode.CopyOnly, _syntax, isQuery: false);
            }

            if (parts == UriComponents.UserInfo)
            {
                num--;
            }
        }

        if ((parts & UriComponents.Host) != 0 && text.Length != 0)
        {
            UnescapeMode unescapeMode =
                ((formatAs != UriFormat.UriEscaped && HostType == Flags.BasicHostType && (nonCanonical & 4u) != 0)
                    ? ((formatAs == UriFormat.Unescaped)
                        ? (UnescapeMode.Unescape | UnescapeMode.UnescapeAll)
                        : (InFact(Flags.UserEscaped) ? UnescapeMode.Unescape : UnescapeMode.EscapeUnescape))
                    : UnescapeMode.CopyOnly);
            if ((parts & UriComponents.NormalizedHost) != 0)
            {
                fixed (char* hostname = text)
                {
                    bool allAscii = false;
                    bool atLeastOneValidIdn = false;
                    try
                    {
                        text = DomainNameHelper.UnicodeEquivalent(hostname, 0, text.Length, ref allAscii,
                            ref atLeastOneValidIdn);
                    }
                    catch (UriFormatException)
                    {
                    }
                }
            }

            array = UriHelper.UnescapeString(text, 0, text.Length, array, ref num, '/', '?', '#', unescapeMode, _syntax,
                isQuery: false);
            if (((uint)parts & 0x80000000u) != 0 && HostType == Flags.IPv6HostType && _info.ScopeId != null)
            {
                _info.ScopeId.CopyTo(0, array, num - 1, _info.ScopeId.Length);
                num += _info.ScopeId.Length;
                array[num - 1] = ']';
            }
        }

        if ((parts & UriComponents.Port) != 0)
        {
            if ((nonCanonical & 8) == 0)
            {
                if (InFact(Flags.NotDefaultPort))
                {
                    ushort num2 = _info.Offset.Path;
                    while (_string[--num2] != ':')
                    {
                    }

                    _string.CopyTo(num2, array, num, _info.Offset.Path - num2);
                    num += _info.Offset.Path - num2;
                }
                else if ((parts & UriComponents.StrongPort) != 0 && _syntax.DefaultPort != -1)
                {
                    array[num++] = ':';
                    text = _info.Offset.PortValue.ToString(CultureInfo.InvariantCulture);
                    text.CopyTo(0, array, num, text.Length);
                    num += text.Length;
                }
            }
            else if (InFact(Flags.NotDefaultPort) ||
                     ((parts & UriComponents.StrongPort) != 0 && _syntax.DefaultPort != -1))
            {
                array[num++] = ':';
                text = _info.Offset.PortValue.ToString(CultureInfo.InvariantCulture);
                text.CopyTo(0, array, num, text.Length);
                num += text.Length;
            }
        }

        if ((parts & UriComponents.Path) != 0)
        {
            array = GetCanonicalPath(array, ref num, formatAs);
            if (parts == UriComponents.Path)
            {
                ushort startIndex;
                if (InFact(Flags.AuthorityFound) && num != 0 && array[0] == '/')
                {
                    startIndex = 1;
                    num--;
                }
                else
                {
                    startIndex = 0;
                }

                if (num != 0)
                {
                    return new string(array, startIndex, num);
                }

                return string.Empty;
            }
        }

        if ((parts & UriComponents.Query) != 0 && _info.Offset.Query < _info.Offset.Fragment)
        {
            ushort startIndex = (ushort)(_info.Offset.Query + 1);
            if (parts != UriComponents.Query)
            {
                array[num++] = '?';
            }

            if ((nonCanonical & 0x20u) != 0)
            {
                switch (formatAs)
                {
                    case UriFormat.UriEscaped:
                        if (NotAny(Flags.UserEscaped))
                        {
                            array = UriHelper.EscapeString(_string, startIndex, _info.Offset.Fragment, array, ref num,
                                isUriString: true, '#', '\uffff', '%');
                        }
                        else
                        {
                            UriHelper.UnescapeString(_string, startIndex, _info.Offset.Fragment, array, ref num,
                                '\uffff', '\uffff', '\uffff', UnescapeMode.CopyOnly, _syntax, isQuery: true);
                        }

                        break;
                    case (UriFormat)32767:
                        array = UriHelper.UnescapeString(_string, startIndex, _info.Offset.Fragment, array, ref num,
                            '#', '\uffff', '\uffff',
                            (InFact(Flags.UserEscaped) ? UnescapeMode.Unescape : UnescapeMode.EscapeUnescape) |
                            UnescapeMode.V1ToStringFlag, _syntax, isQuery: true);
                        break;
                    case UriFormat.Unescaped:
                        array = UriHelper.UnescapeString(_string, startIndex, _info.Offset.Fragment, array, ref num,
                            '#', '\uffff', '\uffff', UnescapeMode.Unescape | UnescapeMode.UnescapeAll, _syntax,
                            isQuery: true);
                        break;
                    default:
                        array = UriHelper.UnescapeString(_string, startIndex, _info.Offset.Fragment, array, ref num,
                            '#', '\uffff', '\uffff',
                            InFact(Flags.UserEscaped) ? UnescapeMode.Unescape : UnescapeMode.EscapeUnescape, _syntax,
                            isQuery: true);
                        break;
                }
            }
            else
            {
                UriHelper.UnescapeString(_string, startIndex, _info.Offset.Fragment, array, ref num, '\uffff', '\uffff',
                    '\uffff', UnescapeMode.CopyOnly, _syntax, isQuery: true);
            }
        }

        if ((parts & UriComponents.Fragment) != 0 && _info.Offset.Fragment < _info.Offset.End)
        {
            ushort startIndex = (ushort)(_info.Offset.Fragment + 1);
            if (parts != UriComponents.Fragment)
            {
                array[num++] = '#';
            }

            if ((nonCanonical & 0x40u) != 0)
            {
                switch (formatAs)
                {
                    case UriFormat.UriEscaped:
                        if (NotAny(Flags.UserEscaped))
                        {
                            array = UriHelper.EscapeString(_string, startIndex, _info.Offset.End, array, ref num,
                                isUriString: true, '\uffff', '\uffff', '%');
                        }
                        else
                        {
                            UriHelper.UnescapeString(_string, startIndex, _info.Offset.End, array, ref num, '\uffff',
                                '\uffff', '\uffff', UnescapeMode.CopyOnly, _syntax, isQuery: false);
                        }

                        break;
                    case (UriFormat)32767:
                        array = UriHelper.UnescapeString(_string, startIndex, _info.Offset.End, array, ref num, '#',
                            '\uffff', '\uffff',
                            (InFact(Flags.UserEscaped) ? UnescapeMode.Unescape : UnescapeMode.EscapeUnescape) |
                            UnescapeMode.V1ToStringFlag, _syntax, isQuery: false);
                        break;
                    case UriFormat.Unescaped:
                        array = UriHelper.UnescapeString(_string, startIndex, _info.Offset.End, array, ref num, '#',
                            '\uffff', '\uffff', UnescapeMode.Unescape | UnescapeMode.UnescapeAll, _syntax,
                            isQuery: false);
                        break;
                    default:
                        array = UriHelper.UnescapeString(_string, startIndex, _info.Offset.End, array, ref num, '#',
                            '\uffff', '\uffff',
                            InFact(Flags.UserEscaped) ? UnescapeMode.Unescape : UnescapeMode.EscapeUnescape, _syntax,
                            isQuery: false);
                        break;
                }
            }
            else
            {
                UriHelper.UnescapeString(_string, startIndex, _info.Offset.End, array, ref num, '\uffff', '\uffff',
                    '\uffff', UnescapeMode.CopyOnly, _syntax, isQuery: false);
            }
        }

        return new string(array, 0, num);
    }

    private string GetUriPartsFromUserString(UriComponents uriParts)
    {
        switch (uriParts & ~UriComponents.KeepDelimiter)
        {
            case UriComponents.SchemeAndServer:
                if (!InFact(Flags.HasUserInfo))
                {
                    return _string.Substring(_info.Offset.Scheme, _info.Offset.Path - _info.Offset.Scheme);
                }

                return string.Concat(_string.AsSpan(_info.Offset.Scheme, _info.Offset.User - _info.Offset.Scheme),
                    _string.AsSpan(_info.Offset.Host, _info.Offset.Path - _info.Offset.Host));
            case UriComponents.HostAndPort:
                if (InFact(Flags.HasUserInfo))
                {
                    if (InFact(Flags.NotDefaultPort) || _syntax.DefaultPort == -1)
                    {
                        return _string.Substring(_info.Offset.Host, _info.Offset.Path - _info.Offset.Host);
                    }

                    return string.Concat(_string.AsSpan(_info.Offset.Host, _info.Offset.Path - _info.Offset.Host), ":",
                        _info.Offset.PortValue.ToString(CultureInfo.InvariantCulture));
                }

                goto case UriComponents.StrongAuthority;
            case UriComponents.AbsoluteUri:
                if (_info.Offset.Scheme == 0 && _info.Offset.End == _string.Length)
                {
                    return _string;
                }

                return _string.Substring(_info.Offset.Scheme, _info.Offset.End - _info.Offset.Scheme);
            case UriComponents.HttpRequestUrl:
                if (InFact(Flags.HasUserInfo))
                {
                    return string.Concat(_string.AsSpan(_info.Offset.Scheme, _info.Offset.User - _info.Offset.Scheme),
                        _string.AsSpan(_info.Offset.Host, _info.Offset.Fragment - _info.Offset.Host));
                }

                if (_info.Offset.Scheme == 0 && _info.Offset.Fragment == _string.Length)
                {
                    return _string;
                }

                return _string.Substring(_info.Offset.Scheme, _info.Offset.Fragment - _info.Offset.Scheme);
            case UriComponents.SchemeAndServer | UriComponents.UserInfo:
                return _string.Substring(_info.Offset.Scheme, _info.Offset.Path - _info.Offset.Scheme);
            case UriComponents.HttpRequestUrl | UriComponents.UserInfo:
                if (_info.Offset.Scheme == 0 && _info.Offset.Fragment == _string.Length)
                {
                    return _string;
                }

                return _string.Substring(_info.Offset.Scheme, _info.Offset.Fragment - _info.Offset.Scheme);
            case UriComponents.Scheme:
                if (uriParts != UriComponents.Scheme)
                {
                    return _string.Substring(_info.Offset.Scheme, _info.Offset.User - _info.Offset.Scheme);
                }

                return _syntax.SchemeName;
            case UriComponents.Host:
            {
                ushort num2 = _info.Offset.Path;
                if (InFact(Flags.PortNotCanonical | Flags.NotDefaultPort))
                {
                    while (_string[--num2] != ':')
                    {
                    }
                }

                if (num2 - _info.Offset.Host != 0)
                {
                    return _string.Substring(_info.Offset.Host, num2 - _info.Offset.Host);
                }

                return string.Empty;
            }
            case UriComponents.Path:
            {
                ushort num =
                    ((uriParts != UriComponents.Path || !InFact(Flags.AuthorityFound) ||
                      _info.Offset.End <= _info.Offset.Path || _string[_info.Offset.Path] != '/')
                        ? _info.Offset.Path
                        : ((ushort)(_info.Offset.Path + 1)));
                if (num >= _info.Offset.Query)
                {
                    return string.Empty;
                }

                return _string.Substring(num, _info.Offset.Query - num);
            }
            case UriComponents.Query:
            {
                ushort num = ((uriParts != UriComponents.Query)
                    ? _info.Offset.Query
                    : ((ushort)(_info.Offset.Query + 1)));
                if (num >= _info.Offset.Fragment)
                {
                    return string.Empty;
                }

                return _string.Substring(num, _info.Offset.Fragment - num);
            }
            case UriComponents.Fragment:
            {
                ushort num = ((uriParts != UriComponents.Fragment)
                    ? _info.Offset.Fragment
                    : ((ushort)(_info.Offset.Fragment + 1)));
                if (num >= _info.Offset.End)
                {
                    return string.Empty;
                }

                return _string.Substring(num, _info.Offset.End - num);
            }
            case UriComponents.UserInfo | UriComponents.Host | UriComponents.Port:
                if (_info.Offset.Path - _info.Offset.User != 0)
                {
                    return _string.Substring(_info.Offset.User, _info.Offset.Path - _info.Offset.User);
                }

                return string.Empty;
            case UriComponents.StrongAuthority:
                if (!InFact(Flags.NotDefaultPort) && _syntax.DefaultPort != -1)
                {
                    return string.Concat(_string.AsSpan(_info.Offset.User, _info.Offset.Path - _info.Offset.User), ":",
                        _info.Offset.PortValue.ToString(CultureInfo.InvariantCulture));
                }

                goto case UriComponents.UserInfo | UriComponents.Host | UriComponents.Port;
            case UriComponents.PathAndQuery:
                return _string.Substring(_info.Offset.Path, _info.Offset.Fragment - _info.Offset.Path);
            case UriComponents.HttpRequestUrl | UriComponents.Fragment:
                if (InFact(Flags.HasUserInfo))
                {
                    return string.Concat(_string.AsSpan(_info.Offset.Scheme, _info.Offset.User - _info.Offset.Scheme),
                        _string.AsSpan(_info.Offset.Host, _info.Offset.End - _info.Offset.Host));
                }

                if (_info.Offset.Scheme == 0 && _info.Offset.End == _string.Length)
                {
                    return _string;
                }

                return _string.Substring(_info.Offset.Scheme, _info.Offset.End - _info.Offset.Scheme);
            case UriComponents.PathAndQuery | UriComponents.Fragment:
                return _string.Substring(_info.Offset.Path, _info.Offset.End - _info.Offset.Path);
            case UriComponents.UserInfo:
            {
                if (NotAny(Flags.HasUserInfo))
                {
                    return string.Empty;
                }

                ushort num = ((uriParts != UriComponents.UserInfo)
                    ? _info.Offset.Host
                    : ((ushort)(_info.Offset.Host - 1)));
                if (_info.Offset.User >= num)
                {
                    return string.Empty;
                }

                return _string.Substring(_info.Offset.User, num - _info.Offset.User);
            }
            default:
                return null;
        }
    }

    private void GetLengthWithoutTrailingSpaces(string str, ref ushort length, int idx)
    {
        ushort num = length;
        while (num > idx && UriHelper.IsLWS(str[num - 1]))
        {
            num--;
        }

        length = num;
    }

    private unsafe void ParseRemaining()
    {
        EnsureUriInfo();
        Flags flags = Flags.Zero;
        if (!UserDrivenParsing)
        {
            bool flag = _iriParsing && (_flags & Flags.HasUnicode) != Flags.Zero &&
                        (_flags & Flags.RestUnicodeNormalized) == 0;
            ushort scheme = _info.Offset.Scheme;
            ushort length = (ushort)_string.Length;
            Check check = Check.None;
            UriSyntaxFlags flags2 = _syntax.Flags;
            fixed (char* ptr = _string)
            {
                GetLengthWithoutTrailingSpaces(_string, ref length, scheme);
                if (IsImplicitFile)
                {
                    flags |= Flags.SchemeNotCanonical;
                }
                else
                {
                    ushort num = 0;
                    ushort num2 = (ushort)_syntax.SchemeName.Length;
                    while (num < num2)
                    {
                        if (_syntax.SchemeName[num] != ptr[scheme + num])
                        {
                            flags |= Flags.SchemeNotCanonical;
                        }

                        num++;
                    }

                    if ((_flags & Flags.AuthorityFound) != Flags.Zero && (scheme + num + 3 >= length ||
                                                                          ptr[scheme + num + 1] != '/' ||
                                                                          ptr[scheme + num + 2] != '/'))
                    {
                        flags |= Flags.SchemeNotCanonical;
                    }
                }

                if ((_flags & Flags.HasUserInfo) != Flags.Zero)
                {
                    scheme = _info.Offset.User;
                    check = CheckCanonical(ptr, ref scheme, _info.Offset.Host, '@');
                    if ((check & Check.DisplayCanonical) == 0)
                    {
                        flags |= Flags.UserNotCanonical;
                    }

                    if ((check & (Check.EscapedCanonical | Check.BackslashInPath)) != Check.EscapedCanonical)
                    {
                        flags |= Flags.E_UserNotCanonical;
                    }

                    if (_iriParsing &&
                        (check & (Check.EscapedCanonical | Check.DisplayCanonical | Check.BackslashInPath |
                                  Check.NotIriCanonical | Check.FoundNonAscii)) ==
                        (Check.DisplayCanonical | Check.FoundNonAscii))
                    {
                        flags |= Flags.UserIriCanonical;
                    }
                }
            }

            scheme = _info.Offset.Path;
            ushort idx = _info.Offset.Path;
            if (flag)
            {
                if (IsDosPath)
                {
                    if (IsImplicitFile)
                    {
                        _string = string.Empty;
                    }
                    else
                    {
                        _string = _syntax.SchemeName + SchemeDelimiter;
                    }
                }

                _info.Offset.Path = (ushort)_string.Length;
                scheme = _info.Offset.Path;
                ushort start = idx;
                if (IsImplicitFile || (flags2 & (UriSyntaxFlags.MayHaveQuery | UriSyntaxFlags.MayHaveFragment)) == 0)
                {
                    FindEndOfComponent(_originalUnicodeString, ref idx, (ushort)_originalUnicodeString.Length,
                        '\uffff');
                }
                else
                {
                    FindEndOfComponent(_originalUnicodeString, ref idx, (ushort)_originalUnicodeString.Length,
                        _syntax.InFact(UriSyntaxFlags.MayHaveQuery)
                            ? '?'
                            : (_syntax.InFact(UriSyntaxFlags.MayHaveFragment) ? '#' : '\ufffe'));
                }

                string text = EscapeUnescapeIri(_originalUnicodeString, start, idx, UriComponents.Path);
                try
                {
                    _string += text;
                }
                catch (ArgumentException)
                {
                    UriFormatException exception = GetException(ParsingError.BadFormat);
                    throw exception;
                }

                if (_string.Length > 65535)
                {
                    UriFormatException exception2 = GetException(ParsingError.SizeLimit);
                    throw exception2;
                }

                length = (ushort)_string.Length;
                if (_string == _originalUnicodeString)
                {
                    GetLengthWithoutTrailingSpaces(_string, ref length, scheme);
                }
            }

            fixed (char* ptr2 = _string)
            {
                check =
                    ((!IsImplicitFile && (flags2 & (UriSyntaxFlags.MayHaveQuery | UriSyntaxFlags.MayHaveFragment)) != 0)
                        ? CheckCanonical(ptr2, ref scheme, length,
                            ((flags2 & UriSyntaxFlags.MayHaveQuery) != 0)
                                ? '?'
                                : (_syntax.InFact(UriSyntaxFlags.MayHaveFragment) ? '#' : '\ufffe'))
                        : CheckCanonical(ptr2, ref scheme, length, '\uffff'));
                if ((_flags & Flags.AuthorityFound) != Flags.Zero && (flags2 & UriSyntaxFlags.PathIsRooted) != 0 &&
                    (_info.Offset.Path == length ||
                     (ptr2[(int)_info.Offset.Path] != '/' && ptr2[(int)_info.Offset.Path] != '\\')))
                {
                    flags |= Flags.FirstSlashAbsent;
                }
            }

            bool flag2 = false;
            if (IsDosPath || ((_flags & Flags.AuthorityFound) != Flags.Zero &&
                              ((flags2 & (UriSyntaxFlags.ConvertPathSlashes | UriSyntaxFlags.CompressPath)) != 0 ||
                               _syntax.InFact(UriSyntaxFlags.UnEscapeDotsAndSlashes))))
            {
                if ((check & Check.DotSlashEscaped) != 0 && _syntax.InFact(UriSyntaxFlags.UnEscapeDotsAndSlashes))
                {
                    flags |= Flags.PathNotCanonical | Flags.E_PathNotCanonical;
                    flag2 = true;
                }

                if ((flags2 & UriSyntaxFlags.ConvertPathSlashes) != 0 && (check & Check.BackslashInPath) != 0)
                {
                    flags |= Flags.PathNotCanonical | Flags.E_PathNotCanonical;
                    flag2 = true;
                }

                if ((flags2 & UriSyntaxFlags.CompressPath) != 0 && ((flags & Flags.E_PathNotCanonical) != Flags.Zero ||
                                                                    (check & Check.DotSlashAttn) != 0))
                {
                    flags |= Flags.ShouldBeCompressed;
                }

                if ((check & Check.BackslashInPath) != 0)
                {
                    flags |= Flags.BackslashInPath;
                }
            }
            else if ((check & Check.BackslashInPath) != 0)
            {
                flags |= Flags.E_PathNotCanonical;
                flag2 = true;
            }

            if ((check & Check.DisplayCanonical) == 0 && ((_flags & Flags.ImplicitFile) == Flags.Zero ||
                                                          (_flags & Flags.UserEscaped) != Flags.Zero ||
                                                          (check & Check.ReservedFound) != 0))
            {
                flags |= Flags.PathNotCanonical;
                flag2 = true;
            }

            if ((_flags & Flags.ImplicitFile) != Flags.Zero &&
                (check & (Check.EscapedCanonical | Check.ReservedFound)) != 0)
            {
                check &= ~Check.EscapedCanonical;
            }

            if ((check & Check.EscapedCanonical) == 0)
            {
                flags |= Flags.E_PathNotCanonical;
            }

            if (_iriParsing && !flag2 &&
                (check & (Check.EscapedCanonical | Check.DisplayCanonical | Check.NotIriCanonical |
                          Check.FoundNonAscii)) == (Check.DisplayCanonical | Check.FoundNonAscii))
            {
                flags |= Flags.PathIriCanonical;
            }

            if (flag)
            {
                ushort start2 = idx;
                if (idx < _originalUnicodeString.Length && _originalUnicodeString[idx] == '?')
                {
                    idx++;
                    FindEndOfComponent(_originalUnicodeString, ref idx, (ushort)_originalUnicodeString.Length,
                        ((flags2 & UriSyntaxFlags.MayHaveFragment) != 0) ? '#' : '\ufffe');
                    string text2 = EscapeUnescapeIri(_originalUnicodeString, start2, idx, UriComponents.Query);
                    try
                    {
                        _string += text2;
                    }
                    catch (ArgumentException)
                    {
                        UriFormatException exception3 = GetException(ParsingError.BadFormat);
                        throw exception3;
                    }

                    if (_string.Length > 65535)
                    {
                        UriFormatException exception4 = GetException(ParsingError.SizeLimit);
                        throw exception4;
                    }

                    length = (ushort)_string.Length;
                    if (_string == _originalUnicodeString)
                    {
                        GetLengthWithoutTrailingSpaces(_string, ref length, scheme);
                    }
                }
            }

            _info.Offset.Query = scheme;
            fixed (char* ptr3 = _string)
            {
                if (scheme < length && ptr3[(int)scheme] == '?')
                {
                    scheme++;
                    check = CheckCanonical(ptr3, ref scheme, length,
                        ((flags2 & UriSyntaxFlags.MayHaveFragment) != 0) ? '#' : '\ufffe');
                    if ((check & Check.DisplayCanonical) == 0)
                    {
                        flags |= Flags.QueryNotCanonical;
                    }

                    if ((check & (Check.EscapedCanonical | Check.BackslashInPath)) != Check.EscapedCanonical)
                    {
                        flags |= Flags.E_QueryNotCanonical;
                    }

                    if (_iriParsing &&
                        (check & (Check.EscapedCanonical | Check.DisplayCanonical | Check.BackslashInPath |
                                  Check.NotIriCanonical | Check.FoundNonAscii)) ==
                        (Check.DisplayCanonical | Check.FoundNonAscii))
                    {
                        flags |= Flags.QueryIriCanonical;
                    }
                }
            }

            if (flag)
            {
                ushort start3 = idx;
                if (idx < _originalUnicodeString.Length && _originalUnicodeString[idx] == '#')
                {
                    idx++;
                    FindEndOfComponent(_originalUnicodeString, ref idx, (ushort)_originalUnicodeString.Length,
                        '\ufffe');
                    string text3 = EscapeUnescapeIri(_originalUnicodeString, start3, idx, UriComponents.Fragment);
                    try
                    {
                        _string += text3;
                    }
                    catch (ArgumentException)
                    {
                        UriFormatException exception5 = GetException(ParsingError.BadFormat);
                        throw exception5;
                    }

                    if (_string.Length > 65535)
                    {
                        UriFormatException exception6 = GetException(ParsingError.SizeLimit);
                        throw exception6;
                    }

                    length = (ushort)_string.Length;
                    GetLengthWithoutTrailingSpaces(_string, ref length, scheme);
                }
            }

            _info.Offset.Fragment = scheme;
            fixed (char* ptr4 = _string)
            {
                if (scheme < length && ptr4[(int)scheme] == '#')
                {
                    scheme++;
                    check = CheckCanonical(ptr4, ref scheme, length, '\ufffe');
                    if ((check & Check.DisplayCanonical) == 0)
                    {
                        flags |= Flags.FragmentNotCanonical;
                    }

                    if ((check & (Check.EscapedCanonical | Check.BackslashInPath)) != Check.EscapedCanonical)
                    {
                        flags |= Flags.E_FragmentNotCanonical;
                    }

                    if (_iriParsing &&
                        (check & (Check.EscapedCanonical | Check.DisplayCanonical | Check.BackslashInPath |
                                  Check.NotIriCanonical | Check.FoundNonAscii)) ==
                        (Check.DisplayCanonical | Check.FoundNonAscii))
                    {
                        flags |= Flags.FragmentIriCanonical;
                    }
                }
            }

            _info.Offset.End = scheme;
        }

        flags |= Flags.AllUriInfoSet;
        lock (_info)
        {
            _flags |= flags;
        }

        _flags |= Flags.RestUnicodeNormalized;
    }

    private static unsafe ushort ParseSchemeCheckImplicitFile(char* uriString, ushort length, ref ParsingError err,
        ref Flags flags, ref UriParser syntax)
    {
        ushort num = 0;
        while (num < length && UriHelper.IsLWS(uriString[(int)num]))
        {
            num++;
        }

        ushort num2 = num;
        while (num2 < length && uriString[(int)num2] != ':')
        {
            num2++;
        }

        if (IntPtr.Size == 4 && num2 != length && num2 >= num + 2 &&
            CheckKnownSchemes((long*)(uriString + (int)num), (ushort)(num2 - num), ref syntax))
        {
            return (ushort)(num2 + 1);
        }

        if (num + 2 >= length || num2 == num)
        {
            err = ParsingError.BadFormat;
            return 0;
        }

        char c;
        if ((c = uriString[num + 1]) == ':' || c == '|')
        {
            if (UriHelper.IsAsciiLetter(uriString[(int)num]))
            {
                if ((c = uriString[num + 2]) == '\\' || c == '/')
                {
                    flags |= Flags.AuthorityFound | Flags.DosPath | Flags.ImplicitFile;
                    syntax = UriParser.FileUri;
                    return num;
                }

                err = ParsingError.MustRootedPath;
                return 0;
            }

            if (c == ':')
            {
                err = ParsingError.BadScheme;
            }
            else
            {
                err = ParsingError.BadFormat;
            }

            return 0;
        }

        if ((c = uriString[(int)num]) == '/' || c == '\\')
        {
            if ((c = uriString[num + 1]) == '\\' || c == '/')
            {
                flags |= Flags.AuthorityFound | Flags.UncPath | Flags.ImplicitFile;
                syntax = UriParser.FileUri;
                num += 2;
                while (num < length && ((c = uriString[(int)num]) == '/' || c == '\\'))
                {
                    num++;
                }

                return num;
            }

            err = ParsingError.BadFormat;
            return 0;
        }

        if (num2 == length)
        {
            err = ParsingError.BadFormat;
            return 0;
        }

        err = CheckSchemeSyntax(new ReadOnlySpan<char>(uriString + (int)num, num2 - num), ref syntax);
        if (err != 0)
        {
            return 0;
        }

        return (ushort)(num2 + 1);
    }

    private static unsafe bool CheckKnownSchemes(long* lptr, ushort nChars, ref UriParser syntax)
    {
        if (nChars == 2)
        {
            if (((int)(*lptr) | 0x200020) == 7536759)
            {
                syntax = UriParser.WsUri;
                return true;
            }

            return false;
        }

        switch (*lptr | 0x20002000200020L)
        {
            case 31525695615402088L:
                switch (nChars)
                {
                    case 4:
                        syntax = UriParser.HttpUri;
                        return true;
                    case 5:
                        if ((*(ushort*)(lptr + 1) | 0x20) == 115)
                        {
                            syntax = UriParser.HttpsUri;
                            return true;
                        }

                        break;
                }

                break;
            case 16326042577993847L:
                if (nChars == 3)
                {
                    syntax = UriParser.WssUri;
                    return true;
                }

                break;
            case 28429436511125606L:
                if (nChars == 4)
                {
                    syntax = UriParser.FileUri;
                    return true;
                }

                break;
            case 16326029693157478L:
                if (nChars == 3)
                {
                    syntax = UriParser.FtpUri;
                    return true;
                }

                break;
            case 32370133429452910L:
                if (nChars == 4)
                {
                    syntax = UriParser.NewsUri;
                    return true;
                }

                break;
            case 31525695615008878L:
                if (nChars == 4)
                {
                    syntax = UriParser.NntpUri;
                    return true;
                }

                break;
            case 28147948650299509L:
                if (nChars == 4)
                {
                    syntax = UriParser.UuidUri;
                    return true;
                }

                break;
            case 29273878621519975L:
                if (nChars == 6 && (*(int*)(lptr + 1) | 0x200020) == 7471205)
                {
                    syntax = UriParser.GopherUri;
                    return true;
                }

                break;
            case 30399748462674029L:
                if (nChars == 6 && (*(int*)(lptr + 1) | 0x200020) == 7274612)
                {
                    syntax = UriParser.MailToUri;
                    return true;
                }

                break;
            case 30962711301259380L:
                if (nChars == 6 && (*(int*)(lptr + 1) | 0x200020) == 7602277)
                {
                    syntax = UriParser.TelnetUri;
                    return true;
                }

                break;
            case 12948347151515758L:
                if (nChars == 8 && (lptr[1] | 0x20002000200020L) == 28429453690994800L)
                {
                    syntax = UriParser.NetPipeUri;
                    return true;
                }

                if (nChars == 7 && (lptr[1] | 0x20002000200020L) == 16326029692043380L)
                {
                    syntax = UriParser.NetTcpUri;
                    return true;
                }

                break;
            case 31525614009974892L:
                if (nChars == 4)
                {
                    syntax = UriParser.LdapUri;
                    return true;
                }

                break;
        }

        return false;
    }

    private static unsafe ParsingError CheckSchemeSyntax(ReadOnlySpan<char> span, ref UriParser syntax)
    {
        if (span.Length == 0)
        {
            return ParsingError.BadScheme;
        }

        char c2 = span[0];
        switch (c2)
        {
            case 'A':
            case 'B':
            case 'C':
            case 'D':
            case 'E':
            case 'F':
            case 'G':
            case 'H':
            case 'I':
            case 'J':
            case 'K':
            case 'L':
            case 'M':
            case 'N':
            case 'O':
            case 'P':
            case 'Q':
            case 'R':
            case 'S':
            case 'T':
            case 'U':
            case 'V':
            case 'W':
            case 'X':
            case 'Y':
            case 'Z':
                c2 = (char)(c2 | 0x20u);
                break;
            default:
                return ParsingError.BadScheme;
            case 'a':
            case 'b':
            case 'c':
            case 'd':
            case 'e':
            case 'f':
            case 'g':
            case 'h':
            case 'i':
            case 'j':
            case 'k':
            case 'l':
            case 'm':
            case 'n':
            case 'o':
            case 'p':
            case 'q':
            case 'r':
            case 's':
            case 't':
            case 'u':
            case 'v':
            case 'w':
            case 'x':
            case 'y':
            case 'z':
                break;
        }

        switch (span.Length)
        {
            case 2:
                if (30579 == (((uint)c2 << 8) | ToLowerCaseAscii(span[1])))
                {
                    syntax = UriParser.WsUri;
                    return ParsingError.None;
                }

                break;
            case 3:
                switch ((int)(((uint)c2 << 16) | ((uint)ToLowerCaseAscii(span[1]) << 8) | ToLowerCaseAscii(span[2])))
                {
                    case 6714480:
                        syntax = UriParser.FtpUri;
                        return ParsingError.None;
                    case 7828339:
                        syntax = UriParser.WssUri;
                        return ParsingError.None;
                }

                break;
            case 4:
                switch ((int)(((uint)c2 << 24) | ((uint)ToLowerCaseAscii(span[1]) << 16) |
                              ((uint)ToLowerCaseAscii(span[2]) << 8) | ToLowerCaseAscii(span[3])))
                {
                    case 1752462448:
                        syntax = UriParser.HttpUri;
                        return ParsingError.None;
                    case 1718185061:
                        syntax = UriParser.FileUri;
                        return ParsingError.None;
                }

                break;
            case 5:
                if (1752462448 == (((uint)c2 << 24) | ((uint)ToLowerCaseAscii(span[1]) << 16) |
                                   ((uint)ToLowerCaseAscii(span[2]) << 8) | ToLowerCaseAscii(span[3])) &&
                    ToLowerCaseAscii(span[4]) == 's')
                {
                    syntax = UriParser.HttpsUri;
                    return ParsingError.None;
                }

                break;
            case 6:
                if (1835100524 == (((uint)c2 << 24) | ((uint)ToLowerCaseAscii(span[1]) << 16) |
                                   ((uint)ToLowerCaseAscii(span[2]) << 8) | ToLowerCaseAscii(span[3])) &&
                    ToLowerCaseAscii(span[4]) == 't' && ToLowerCaseAscii(span[5]) == 'o')
                {
                    syntax = UriParser.MailToUri;
                    return ParsingError.None;
                }

                break;
        }

        for (int i = 1; i < span.Length; i++)
        {
            char c3 = span[i];
            if ((uint)(c3 - 97) > 25u && (uint)(c3 - 65) > 25u && (uint)(c3 - 48) > 9u && c3 != '+' && c3 != '-' &&
                c3 != '.')
            {
                return ParsingError.BadScheme;
            }
        }

        if (span.Length > 1024)
        {
            return ParsingError.SchemeLimit;
        }

        string text = new string('\0', span.Length);
        fixed (char* pointer = text)
        {
            int num = span.ToLowerInvariant(new Span<char>(pointer, text.Length));
        }

        syntax = UriParser.FindOrFetchAsUnknownV1Syntax(text);
        return ParsingError.None;

        static char ToLowerCaseAscii(char c)
        {
            if ((uint)(c - 65) > 25u)
            {
                return c;
            }

            return (char)(c | 0x20u);
        }
    }

    private unsafe ushort CheckAuthorityHelper(char* pString, ushort idx, ushort length, ref ParsingError err,
        ref Flags flags, UriParser syntax, ref string newHost)
    {
        int i = length;
        int num = idx;
        ushort num2 = idx;
        newHost = null;
        bool justNormalized = false;
        bool flag = s_IriParsing && IriParsingStatic(syntax);
        bool flag2 = (flags & Flags.HasUnicode) != 0;
        bool flag3 = (flags & Flags.HostUnicodeNormalized) == 0;
        UriSyntaxFlags flags2 = syntax.Flags;
        char c;
        if (idx == length || (c = pString[(int)idx]) == '/' || (c == '\\' && StaticIsFile(syntax)) || c == '#' ||
            c == '?')
        {
            if (syntax.InFact(UriSyntaxFlags.AllowEmptyHost))
            {
                flags &= ~Flags.UncPath;
                if (StaticInFact(flags, Flags.ImplicitFile))
                {
                    err = ParsingError.BadHostName;
                }
                else
                {
                    flags |= Flags.BasicHostType;
                }
            }
            else
            {
                err = ParsingError.BadHostName;
            }

            if (flag2 && flag && flag3)
            {
                flags |= Flags.HostUnicodeNormalized;
            }

            return idx;
        }

        if (flag2 && flag && flag3)
        {
            newHost = _originalUnicodeString.Substring(0, num);
        }

        string text = null;
        if ((flags2 & UriSyntaxFlags.MayHaveUserInfo) != 0)
        {
            while (num2 < i)
            {
                if (num2 == i - 1 || pString[(int)num2] == '?' || pString[(int)num2] == '#' ||
                    pString[(int)num2] == '\\' || pString[(int)num2] == '/')
                {
                    num2 = idx;
                    break;
                }

                if (pString[(int)num2] == '@')
                {
                    flags |= Flags.HasUserInfo;
                    if (flag || s_IdnScope != 0)
                    {
                        if (flag && flag2 && flag3)
                        {
                            text = IriHelper.EscapeUnescapeIri(pString, num, num2 + 1, UriComponents.UserInfo);
                            newHost += text;
                            if (newHost.Length > 65535)
                            {
                                err = ParsingError.SizeLimit;
                                return idx;
                            }
                        }
                        else
                        {
                            text = new string(pString, num, num2 - num + 1);
                        }
                    }

                    num2++;
                    c = pString[(int)num2];
                    break;
                }

                num2++;
            }
        }

        bool notCanonical = (flags2 & UriSyntaxFlags.SimpleUserSyntax) == 0;
        if (c == '[' && syntax.InFact(UriSyntaxFlags.AllowIPv6Host) &&
            IPv6AddressHelper.IsValid(pString, num2 + 1, ref i))
        {
            flags |= Flags.IPv6HostType;
            _iriParsing = s_IriParsing && IriParsingStatic(syntax);
            if (flag2 && flag && flag3)
            {
                newHost += new string(pString, num2, i - num2);
                flags |= Flags.HostUnicodeNormalized;
                justNormalized = true;
            }
        }
        else if (c <= '9' && c >= '0' && syntax.InFact(UriSyntaxFlags.AllowIPv4Host) && IPv4AddressHelper.IsValid(
                     pString, num2, ref i, allowIPv6: false, StaticNotAny(flags, Flags.ImplicitFile),
                     syntax.InFact(UriSyntaxFlags.V1_UnknownUri)))
        {
            flags |= Flags.IPv4HostType;
            if (flag2 && flag && flag3)
            {
                newHost += new string(pString, num2, i - num2);
                flags |= Flags.HostUnicodeNormalized;
                justNormalized = true;
            }
        }
        else if ((flags2 & UriSyntaxFlags.AllowDnsHost) != 0 && !flag && DomainNameHelper.IsValid(pString, num2, ref i,
                     ref notCanonical, StaticNotAny(flags, Flags.ImplicitFile)))
        {
            flags |= Flags.DnsHostType;
            if (!notCanonical)
            {
                flags |= Flags.CanonicalDnsHost;
            }

            if (s_IdnScope != 0)
            {
                if (s_IdnScope == UriIdnScope.AllExceptIntranet && IsIntranet(new string(pString, 0, i)))
                {
                    flags |= Flags.IntranetUri;
                }

                if (AllowIdnStatic(syntax, flags))
                {
                    bool allAscii = true;
                    bool atLeastOneValidIdn = false;
                    string text2 =
                        DomainNameHelper.UnicodeEquivalent(pString, num2, i, ref allAscii, ref atLeastOneValidIdn);
                    if (atLeastOneValidIdn)
                    {
                        if (StaticNotAny(flags, Flags.HasUnicode))
                        {
                            _originalUnicodeString = _string;
                        }

                        flags |= Flags.IdnHost;
                        newHost = string.Concat(_originalUnicodeString.AsSpan(0, num), text, text2);
                        flags |= Flags.CanonicalDnsHost;
                        _dnsSafeHost = new string(pString, num2, i - num2);
                        justNormalized = true;
                    }

                    flags |= Flags.HostUnicodeNormalized;
                }
            }
        }
        else if ((flags2 & UriSyntaxFlags.AllowDnsHost) != 0 &&
                 ((syntax.InFact(UriSyntaxFlags.AllowIriParsing) && flag3) || syntax.InFact(UriSyntaxFlags.AllowIdn)) &&
                 DomainNameHelper.IsValidByIri(pString, num2, ref i, ref notCanonical,
                     StaticNotAny(flags, Flags.ImplicitFile)))
        {
            CheckAuthorityHelperHandleDnsIri(pString, num2, i, num, flag, flag2, syntax, text, ref flags,
                ref justNormalized, ref newHost, ref err);
        }
        else if ((flags2 & UriSyntaxFlags.AllowUncHost) != 0 &&
                 UncNameHelper.IsValid(pString, num2, ref i, StaticNotAny(flags, Flags.ImplicitFile)) &&
                 i - num2 <= 256)
        {
            flags |= Flags.UncHostType;
            if (flag2 && flag && flag3)
            {
                newHost += new string(pString, num2, i - num2);
                flags |= Flags.HostUnicodeNormalized;
                justNormalized = true;
            }
        }

        if (i < length && pString[i] == '\\' && (flags & Flags.HostTypeMask) != Flags.Zero && !StaticIsFile(syntax))
        {
            if (syntax.InFact(UriSyntaxFlags.V1_UnknownUri))
            {
                err = ParsingError.BadHostName;
                flags |= Flags.HostTypeMask;
                return (ushort)i;
            }

            flags &= ~Flags.HostTypeMask;
        }
        else if (i < length && pString[i] == ':')
        {
            if (syntax.InFact(UriSyntaxFlags.MayHavePort))
            {
                int num3 = 0;
                int num4 = i;
                idx = (ushort)(i + 1);
                while (idx < length)
                {
                    ushort num5 = (ushort)(pString[(int)idx] - 48);
                    if (num5 >= 0 && num5 <= 9)
                    {
                        if ((num3 = num3 * 10 + num5) > 65535)
                        {
                            break;
                        }

                        idx++;
                        continue;
                    }

                    if (num5 == ushort.MaxValue || num5 == 15 || num5 == 65523)
                    {
                        break;
                    }

                    if (syntax.InFact(UriSyntaxFlags.AllowAnyOtherHost) && syntax.NotAny(UriSyntaxFlags.V1_UnknownUri))
                    {
                        flags &= ~Flags.HostTypeMask;
                        break;
                    }

                    err = ParsingError.BadPort;
                    return idx;
                }

                if (num3 > 65535)
                {
                    if (!syntax.InFact(UriSyntaxFlags.AllowAnyOtherHost))
                    {
                        err = ParsingError.BadPort;
                        return idx;
                    }

                    flags &= ~Flags.HostTypeMask;
                }

                if (flag && flag2 && justNormalized)
                {
                    newHost += new string(pString, num4, idx - num4);
                }
            }
            else
            {
                flags &= ~Flags.HostTypeMask;
            }
        }

        if ((flags & Flags.HostTypeMask) == Flags.Zero)
        {
            flags &= ~Flags.HasUserInfo;
            if (syntax.InFact(UriSyntaxFlags.AllowAnyOtherHost))
            {
                flags |= Flags.BasicHostType;
                for (i = idx; i < length && pString[i] != '/' && pString[i] != '?' && pString[i] != '#'; i++)
                {
                }

                CheckAuthorityHelperHandleAnyHostIri(pString, num, i, flag, flag2, syntax, ref flags, ref newHost,
                    ref err);
            }
            else if (syntax.InFact(UriSyntaxFlags.V1_UnknownUri))
            {
                bool flag4 = false;
                int num6 = idx;
                for (i = idx;
                     i < length && (!flag4 || (pString[i] != '/' && pString[i] != '?' && pString[i] != '#'));
                     i++)
                {
                    if (i < idx + 2 && pString[i] == '.')
                    {
                        flag4 = true;
                        continue;
                    }

                    err = ParsingError.BadHostName;
                    flags |= Flags.HostTypeMask;
                    return idx;
                }

                flags |= Flags.BasicHostType;
                if (flag && flag2 && StaticNotAny(flags, Flags.HostUnicodeNormalized))
                {
                    string text3 = new string(pString, num6, i - num6);
                    try
                    {
                        newHost += text3.Normalize(NormalizationForm.FormC);
                    }
                    catch (ArgumentException)
                    {
                        err = ParsingError.BadFormat;
                        return idx;
                    }

                    flags |= Flags.HostUnicodeNormalized;
                }
            }
            else if (syntax.InFact(UriSyntaxFlags.MustHaveAuthority) || syntax.InFact(UriSyntaxFlags.MailToLikeUri))
            {
                err = ParsingError.BadHostName;
                flags |= Flags.HostTypeMask;
                return idx;
            }
        }

        return (ushort)i;
    }

    private unsafe void CheckAuthorityHelperHandleDnsIri(char* pString, ushort start, int end, int startInput,
        bool iriParsing, bool hasUnicode, UriParser syntax, string userInfoString, ref Flags flags,
        ref bool justNormalized, ref string newHost, ref ParsingError err)
    {
        flags |= Flags.DnsHostType;
        if (s_IdnScope == UriIdnScope.AllExceptIntranet && IsIntranet(new string(pString, 0, end)))
        {
            flags |= Flags.IntranetUri;
        }

        if (AllowIdnStatic(syntax, flags))
        {
            bool allAscii = true;
            bool atLeastOneValidIdn = false;
            string text = DomainNameHelper.IdnEquivalent(pString, start, end, ref allAscii, ref atLeastOneValidIdn);
            string text2 = DomainNameHelper.UnicodeEquivalent(text, pString, start, end);
            if (!allAscii)
            {
                flags |= Flags.UnicodeHost;
            }

            if (atLeastOneValidIdn)
            {
                flags |= Flags.IdnHost;
            }

            if (allAscii && atLeastOneValidIdn && StaticNotAny(flags, Flags.HasUnicode))
            {
                _originalUnicodeString = _string;
                newHost = (StaticInFact(flags, Flags.HasUserInfo)
                    ? string.Concat(_originalUnicodeString.AsSpan(0, startInput), userInfoString)
                    : _originalUnicodeString.Substring(0, startInput));
                justNormalized = true;
            }
            else if (!iriParsing && (StaticInFact(flags, Flags.UnicodeHost) || StaticInFact(flags, Flags.IdnHost)))
            {
                _originalUnicodeString = _string;
                newHost = (StaticInFact(flags, Flags.HasUserInfo)
                    ? string.Concat(_originalUnicodeString.AsSpan(0, startInput), userInfoString)
                    : _originalUnicodeString.Substring(0, startInput));
                justNormalized = true;
            }

            if (!allAscii || atLeastOneValidIdn)
            {
                _dnsSafeHost = text;
                newHost += text2;
                justNormalized = true;
            }
            else if (allAscii && !atLeastOneValidIdn && iriParsing && hasUnicode)
            {
                newHost += text2;
                justNormalized = true;
            }
        }
        else if (hasUnicode)
        {
            string text3 = UriHelper.StripBidiControlCharacter(pString, start, end - start);
            try
            {
                newHost += text3?.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                err = ParsingError.BadHostName;
            }

            justNormalized = true;
        }

        flags |= Flags.HostUnicodeNormalized;
    }

    private unsafe void CheckAuthorityHelperHandleAnyHostIri(char* pString, int startInput, int end, bool iriParsing,
        bool hasUnicode, UriParser syntax, ref Flags flags, ref string newHost, ref ParsingError err)
    {
        if (!StaticNotAny(flags, Flags.HostUnicodeNormalized) ||
            (!AllowIdnStatic(syntax, flags) && !(iriParsing && hasUnicode)))
        {
            return;
        }

        string text = new string(pString, startInput, end - startInput);
        if (AllowIdnStatic(syntax, flags))
        {
            bool allAscii = true;
            bool atLeastOneValidIdn = false;
            string text2 =
                DomainNameHelper.UnicodeEquivalent(pString, startInput, end, ref allAscii, ref atLeastOneValidIdn);
            if (((allAscii && atLeastOneValidIdn) || !allAscii) && !(iriParsing && hasUnicode))
            {
                _originalUnicodeString = _string;
                newHost = _originalUnicodeString.Substring(0, startInput);
                flags |= Flags.HasUnicode;
            }

            if (atLeastOneValidIdn || !allAscii)
            {
                newHost += text2;
                string bidiStrippedHost = null;
                _dnsSafeHost =
                    DomainNameHelper.IdnEquivalent(pString, startInput, end, ref allAscii, ref bidiStrippedHost);
                if (atLeastOneValidIdn)
                {
                    flags |= Flags.IdnHost;
                }

                if (!allAscii)
                {
                    flags |= Flags.UnicodeHost;
                }
            }
            else if (iriParsing && hasUnicode)
            {
                newHost += text;
            }
        }
        else
        {
            try
            {
                newHost += text.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                err = ParsingError.BadHostName;
            }
        }

        flags |= Flags.HostUnicodeNormalized;
    }

    private unsafe void FindEndOfComponent(string input, ref ushort idx, ushort end, char delim)
    {
        fixed (char* str = input)
        {
            FindEndOfComponent(str, ref idx, end, delim);
        }
    }

    private unsafe void FindEndOfComponent(char* str, ref ushort idx, ushort end, char delim)
    {
        char c = '\uffff';
        ushort num;
        for (num = idx; num < end; num++)
        {
            c = str[(int)num];
            if (c == delim || (delim == '?' && c == '#' && _syntax != null &&
                               _syntax.InFact(UriSyntaxFlags.MayHaveFragment)))
            {
                break;
            }
        }

        idx = num;
    }

    private unsafe Check CheckCanonical(char* str, ref ushort idx, ushort end, char delim)
    {
        Check check = Check.None;
        bool flag = false;
        bool flag2 = false;
        char c = '\uffff';
        ushort num;
        for (num = idx; num < end; num++)
        {
            c = str[(int)num];
            if (c <= '\u001f' || (c >= '\u007f' && c <= '\u009f'))
            {
                flag = true;
                flag2 = true;
                check |= Check.ReservedFound;
                continue;
            }

            if (c > '~')
            {
                if (_iriParsing)
                {
                    bool flag3 = false;
                    check |= Check.FoundNonAscii;
                    if (char.IsHighSurrogate(c))
                    {
                        if (num + 1 < end)
                        {
                            bool surrogatePair = false;
                            flag3 = IriHelper.CheckIriUnicodeRange(c, str[num + 1], ref surrogatePair, isQuery: true);
                        }
                    }
                    else
                    {
                        flag3 = IriHelper.CheckIriUnicodeRange(c, isQuery: true);
                    }

                    if (!flag3)
                    {
                        check |= Check.NotIriCanonical;
                    }
                }

                if (!flag)
                {
                    flag = true;
                }

                continue;
            }

            if (c == delim || (delim == '?' && c == '#' && _syntax != null &&
                               _syntax.InFact(UriSyntaxFlags.MayHaveFragment)))
            {
                break;
            }

            if (c == '?')
            {
                if (IsImplicitFile ||
                    (_syntax != null && !_syntax.InFact(UriSyntaxFlags.MayHaveQuery) && delim != '\ufffe'))
                {
                    check |= Check.ReservedFound;
                    flag2 = true;
                    flag = true;
                }

                continue;
            }

            if (c == '#')
            {
                flag = true;
                if (IsImplicitFile || (_syntax != null && !_syntax.InFact(UriSyntaxFlags.MayHaveFragment)))
                {
                    check |= Check.ReservedFound;
                    flag2 = true;
                }

                continue;
            }

            if (c == '/' || c == '\\')
            {
                if ((check & Check.BackslashInPath) == 0 && c == '\\')
                {
                    check |= Check.BackslashInPath;
                }

                if ((check & Check.DotSlashAttn) == 0 && num + 1 != end &&
                    (str[num + 1] == '/' || str[num + 1] == '\\'))
                {
                    check |= Check.DotSlashAttn;
                }

                continue;
            }

            if (c == '.')
            {
                if (((check & Check.DotSlashAttn) == 0 && num + 1 == end) || str[num + 1] == '.' ||
                    str[num + 1] == '/' || str[num + 1] == '\\' || str[num + 1] == '?' || str[num + 1] == '#')
                {
                    check |= Check.DotSlashAttn;
                }

                continue;
            }

            if ((c > '"' || c == '!') && (c < '[' || c > '^'))
            {
                switch (c)
                {
                    case '<':
                    case '>':
                    case '`':
                        break;
                    case '{':
                    case '|':
                    case '}':
                        flag = true;
                        continue;
                    default:
                        if (c != '%')
                        {
                            continue;
                        }

                        if (!flag2)
                        {
                            flag2 = true;
                        }

                        if (num + 2 < end && (c = UriHelper.EscapedAscii(str[num + 1], str[num + 2])) != '\uffff')
                        {
                            if (c == '.' || c == '/' || c == '\\')
                            {
                                check |= Check.DotSlashEscaped;
                            }

                            num += 2;
                        }
                        else if (!flag)
                        {
                            flag = true;
                        }

                        continue;
                }
            }

            if (!flag)
            {
                flag = true;
            }

            if ((_flags & Flags.HasUnicode) != Flags.Zero && _iriParsing)
            {
                check |= Check.NotIriCanonical;
            }
        }

        if (flag2)
        {
            if (!flag)
            {
                check |= Check.EscapedCanonical;
            }
        }
        else
        {
            check |= Check.DisplayCanonical;
            if (!flag)
            {
                check |= Check.EscapedCanonical;
            }
        }

        idx = num;
        return check;
    }

    private unsafe char[] GetCanonicalPath(char[] dest, ref int pos, UriFormat formatAs)
    {
        if (InFact(Flags.FirstSlashAbsent))
        {
            dest[pos++] = '/';
        }

        if (_info.Offset.Path == _info.Offset.Query)
        {
            return dest;
        }

        int end = pos;
        int securedPathIndex = SecuredPathIndex;
        if (formatAs == UriFormat.UriEscaped)
        {
            if (InFact(Flags.ShouldBeCompressed))
            {
                _string.CopyTo(_info.Offset.Path, dest, end, _info.Offset.Query - _info.Offset.Path);
                end += _info.Offset.Query - _info.Offset.Path;
                if (_syntax.InFact(UriSyntaxFlags.UnEscapeDotsAndSlashes) && InFact(Flags.PathNotCanonical) &&
                    !IsImplicitFile)
                {
                    fixed (char* pch = dest)
                    {
                        UnescapeOnly(pch, pos, ref end, '.', '/',
                            _syntax.InFact(UriSyntaxFlags.ConvertPathSlashes) ? '\\' : '\uffff');
                    }
                }
            }
            else if (InFact(Flags.E_PathNotCanonical) && NotAny(Flags.UserEscaped))
            {
                string text = _string;
                if (securedPathIndex != 0 && text[securedPathIndex + _info.Offset.Path - 1] == '|')
                {
                    text = text.Remove(securedPathIndex + _info.Offset.Path - 1, 1);
                    text = text.Insert(securedPathIndex + _info.Offset.Path - 1, ":");
                }

                dest = UriHelper.EscapeString(text, _info.Offset.Path, _info.Offset.Query, dest, ref end,
                    isUriString: true, '?', '#', IsImplicitFile ? '\uffff' : '%');
            }
            else
            {
                _string.CopyTo(_info.Offset.Path, dest, end, _info.Offset.Query - _info.Offset.Path);
                end += _info.Offset.Query - _info.Offset.Path;
            }
        }
        else
        {
            _string.CopyTo(_info.Offset.Path, dest, end, _info.Offset.Query - _info.Offset.Path);
            end += _info.Offset.Query - _info.Offset.Path;
            if (InFact(Flags.ShouldBeCompressed) && _syntax.InFact(UriSyntaxFlags.UnEscapeDotsAndSlashes) &&
                InFact(Flags.PathNotCanonical) && !IsImplicitFile)
            {
                fixed (char* pch2 = dest)
                {
                    UnescapeOnly(pch2, pos, ref end, '.', '/',
                        _syntax.InFact(UriSyntaxFlags.ConvertPathSlashes) ? '\\' : '\uffff');
                }
            }
        }

        if (securedPathIndex != 0 && dest[securedPathIndex + pos - 1] == '|')
        {
            dest[securedPathIndex + pos - 1] = ':';
        }

        if (InFact(Flags.ShouldBeCompressed))
        {
            dest = Compress(dest, (ushort)(pos + securedPathIndex), ref end, _syntax);
            if (dest[pos] == '\\')
            {
                dest[pos] = '/';
            }

            if (formatAs == UriFormat.UriEscaped && NotAny(Flags.UserEscaped) && InFact(Flags.E_PathNotCanonical))
            {
                string input = new string(dest, pos, end - pos);
                dest = UriHelper.EscapeString(input, 0, end - pos, dest, ref pos, isUriString: true, '?', '#',
                    IsImplicitFile ? '\uffff' : '%');
                end = pos;
            }
        }
        else if (_syntax.InFact(UriSyntaxFlags.ConvertPathSlashes) && InFact(Flags.BackslashInPath))
        {
            for (int i = pos; i < end; i++)
            {
                if (dest[i] == '\\')
                {
                    dest[i] = '/';
                }
            }
        }

        if (formatAs != UriFormat.UriEscaped && InFact(Flags.PathNotCanonical))
        {
            UnescapeMode unescapeMode;
            if (InFact(Flags.PathNotCanonical))
            {
                switch (formatAs)
                {
                    case (UriFormat)32767:
                        unescapeMode =
                            (InFact(Flags.UserEscaped) ? UnescapeMode.Unescape : UnescapeMode.EscapeUnescape) |
                            UnescapeMode.V1ToStringFlag;
                        if (IsImplicitFile)
                        {
                            unescapeMode &= ~UnescapeMode.Unescape;
                        }

                        break;
                    case UriFormat.Unescaped:
                        unescapeMode = ((!IsImplicitFile)
                            ? (UnescapeMode.Unescape | UnescapeMode.UnescapeAll)
                            : UnescapeMode.CopyOnly);
                        break;
                    default:
                        unescapeMode = (InFact(Flags.UserEscaped)
                            ? UnescapeMode.Unescape
                            : UnescapeMode.EscapeUnescape);
                        if (IsImplicitFile)
                        {
                            unescapeMode &= ~UnescapeMode.Unescape;
                        }

                        break;
                }
            }
            else
            {
                unescapeMode = UnescapeMode.CopyOnly;
            }

            char[] array = new char[dest.Length];
            Buffer.BlockCopy(dest, 0, array, 0, end * 2);
            fixed (char* pStr = array)
            {
                dest = UriHelper.UnescapeString(pStr, pos, end, dest, ref pos, '?', '#', '\uffff', unescapeMode,
                    _syntax, isQuery: false);
            }
        }
        else
        {
            pos = end;
        }

        return dest;
    }

    private static unsafe void UnescapeOnly(char* pch, int start, ref int end, char ch1, char ch2, char ch3)
    {
        if (end - start < 3)
        {
            return;
        }

        char* ptr = pch + end - 2;
        pch += start;
        char* ptr2 = null;
        while (pch < ptr)
        {
            if (*(pch++) != '%')
            {
                continue;
            }

            char c = UriHelper.EscapedAscii(*(pch++), *(pch++));
            if (c != ch1 && c != ch2 && c != ch3)
            {
                continue;
            }

            ptr2 = pch - 2;
            *(ptr2 - 1) = c;
            while (pch < ptr)
            {
                if ((*(ptr2++) = *(pch++)) == '%')
                {
                    c = UriHelper.EscapedAscii(*(ptr2++) = *(pch++), *(ptr2++) = *(pch++));
                    if (c == ch1 || c == ch2 || c == ch3)
                    {
                        ptr2 -= 2;
                        *(ptr2 - 1) = c;
                    }
                }
            }

            break;
        }

        ptr += 2;
        if (ptr2 == null)
        {
            return;
        }

        if (pch == ptr)
        {
            end -= (int)(pch - ptr2);
            return;
        }

        *(ptr2++) = *(pch++);
        if (pch == ptr)
        {
            end -= (int)(pch - ptr2);
            return;
        }

        *(ptr2++) = *(pch++);
        end -= (int)(pch - ptr2);
    }

    private static char[] Compress(char[] dest, ushort start, ref int destLength, UriParser syntax)
    {
        ushort num = 0;
        ushort num2 = 0;
        ushort num3 = 0;
        ushort num4 = 0;
        ushort num5 = (ushort)((ushort)destLength - 1);
        for (start--; num5 != start; num5--)
        {
            char c = dest[num5];
            if (c == '\\' && syntax.InFact(UriSyntaxFlags.ConvertPathSlashes))
            {
                c = (dest[num5] = '/');
            }

            if (c == '/')
            {
                num++;
            }
            else
            {
                if (num > 1)
                {
                    num2 = (ushort)(num5 + 1);
                }

                num = 0;
            }

            if (c == '.')
            {
                num3++;
                continue;
            }

            if (num3 != 0)
            {
                if ((!syntax.NotAny(UriSyntaxFlags.CanonicalizeAsFilePath) ||
                     (num3 <= 2 && c == '/' && num5 != start)) && c == '/' &&
                    (num2 == num5 + num3 + 1 || (num2 == 0 && num5 + num3 + 1 == destLength)) && num3 <= 2)
                {
                    num2 = (ushort)(num5 + 1 + num3 + ((num2 != 0) ? 1 : 0));
                    Buffer.BlockCopy(dest, num2 * 2, dest, (num5 + 1) * 2, (destLength - num2) * 2);
                    destLength -= num2 - num5 - 1;
                    num2 = num5;
                    if (num3 == 2)
                    {
                        num4++;
                    }

                    num3 = 0;
                    continue;
                }

                num3 = 0;
            }

            if (c == '/')
            {
                if (num4 != 0)
                {
                    num4--;
                    num2++;
                    Buffer.BlockCopy(dest, num2 * 2, dest, (num5 + 1) * 2, (destLength - num2) * 2);
                    destLength -= num2 - num5 - 1;
                }

                num2 = num5;
            }
        }

        start++;
        if ((ushort)destLength > start && syntax.InFact(UriSyntaxFlags.CanonicalizeAsFilePath) && num <= 1)
        {
            if (num4 != 0 && dest[start] != '/')
            {
                num2++;
                Buffer.BlockCopy(dest, num2 * 2, dest, start * 2, (destLength - num2) * 2);
                destLength -= num2;
            }
            else if (num3 != 0 && (num2 == num3 + 1 || (num2 == 0 && num3 + 1 == destLength)))
            {
                num3 = (ushort)(num3 + ((num2 != 0) ? 1 : 0));
                Buffer.BlockCopy(dest, num3 * 2, dest, start * 2, (destLength - num3) * 2);
                destLength -= num3;
            }
        }

        return dest;
    }

    internal static int CalculateCaseInsensitiveHashCode(string text)
    {
        return text.ToLowerInvariant().GetHashCode();
    }

    private static string CombineUri(Uri basePart, string relativePart, UriFormat uriFormat)
    {
        char c = relativePart[0];
        if (basePart.IsDosPath && (c == '/' || c == '\\') &&
            (relativePart.Length == 1 || (relativePart[1] != '/' && relativePart[1] != '\\')))
        {
            int num = basePart.OriginalString.IndexOf(':');
            if (basePart.IsImplicitFile)
            {
                return string.Concat(basePart.OriginalString.AsSpan(0, num + 1), relativePart);
            }

            num = basePart.OriginalString.IndexOf(':', num + 1);
            return string.Concat(basePart.OriginalString.AsSpan(0, num + 1), relativePart);
        }

        if (StaticIsFile(basePart.Syntax) && (c == '\\' || c == '/'))
        {
            if (relativePart.Length >= 2 && (relativePart[1] == '\\' || relativePart[1] == '/'))
            {
                if (!basePart.IsImplicitFile)
                {
                    return "file:" + relativePart;
                }

                return relativePart;
            }

            if (basePart.IsUnc)
            {
                string text = basePart.GetParts(UriComponents.Path | UriComponents.KeepDelimiter, UriFormat.Unescaped);
                for (int i = 1; i < text.Length; i++)
                {
                    if (text[i] == '/')
                    {
                        text = text.Substring(0, i);
                        break;
                    }
                }

                if (basePart.IsImplicitFile)
                {
                    return "\\\\" + basePart.GetParts(UriComponents.Host, UriFormat.Unescaped) + text + relativePart;
                }

                return "file://" + basePart.GetParts(UriComponents.Host, uriFormat) + text + relativePart;
            }

            return "file://" + relativePart;
        }

        bool flag = basePart.Syntax.InFact(UriSyntaxFlags.ConvertPathSlashes);
        string text2 = null;
        if (c == '/' || (c == '\\' && flag))
        {
            if (relativePart.Length >= 2 && relativePart[1] == '/')
            {
                return basePart.Scheme + ":" + relativePart;
            }

            text2 = ((basePart.HostType != Flags.IPv6HostType)
                ? basePart.GetParts(UriComponents.SchemeAndServer | UriComponents.UserInfo, uriFormat)
                : (basePart.GetParts(UriComponents.Scheme | UriComponents.UserInfo, uriFormat) + "[" +
                   basePart.DnsSafeHost + "]" +
                   basePart.GetParts(UriComponents.Port | UriComponents.KeepDelimiter, uriFormat)));
            if (!flag || c != '\\')
            {
                return text2 + relativePart;
            }

            return text2 + "/" + relativePart.AsSpan(1);
        }

        text2 = basePart.GetParts(UriComponents.Path | UriComponents.KeepDelimiter,
            basePart.IsImplicitFile ? UriFormat.Unescaped : uriFormat);
        int num2 = text2.Length;
        char[] array = new char[num2 + relativePart.Length];
        if (num2 > 0)
        {
            text2.CopyTo(0, array, 0, num2);
            while (num2 > 0)
            {
                if (array[--num2] == '/')
                {
                    num2++;
                    break;
                }
            }
        }

        relativePart.CopyTo(0, array, num2, relativePart.Length);
        c = (basePart.Syntax.InFact(UriSyntaxFlags.MayHaveQuery) ? '?' : '\uffff');
        char c2 =
            ((!basePart.IsImplicitFile && basePart.Syntax.InFact(UriSyntaxFlags.MayHaveFragment)) ? '#' : '\uffff');
        ReadOnlySpan<char> readOnlySpan = string.Empty.AsSpan();
        if (c != '\uffff' || c2 != '\uffff')
        {
            int j;
            for (j = 0; j < relativePart.Length && array[num2 + j] != c && array[num2 + j] != c2; j++)
            {
            }

            if (j == 0)
            {
                readOnlySpan = relativePart.AsSpan();
            }
            else if (j < relativePart.Length)
            {
                readOnlySpan = relativePart.AsSpan(j);
            }

            num2 += j;
        }
        else
        {
            num2 += relativePart.Length;
        }

        if (basePart.HostType == Flags.IPv6HostType)
        {
            text2 = ((!basePart.IsImplicitFile)
                ? (basePart.GetParts(UriComponents.Scheme | UriComponents.UserInfo, uriFormat) + "[" +
                   basePart.DnsSafeHost + "]" +
                   basePart.GetParts(UriComponents.Port | UriComponents.KeepDelimiter, uriFormat))
                : ("\\\\[" + basePart.DnsSafeHost + "]"));
        }
        else if (basePart.IsImplicitFile)
        {
            if (basePart.IsDosPath)
            {
                array = Compress(array, 3, ref num2, basePart.Syntax);
                return string.Concat(array.AsSpan(1, num2 - 1), readOnlySpan);
            }

            text2 = "\\\\" + basePart.GetParts(UriComponents.Host, UriFormat.Unescaped);
        }
        else
        {
            text2 = basePart.GetParts(UriComponents.SchemeAndServer | UriComponents.UserInfo, uriFormat);
        }

        array = Compress(array, basePart.SecuredPathIndex, ref num2, basePart.Syntax);
        return string.Concat(text2, array.AsSpan(0, num2), readOnlySpan);
    }

    private static string PathDifference(string path1, string path2, bool compareCase)
    {
        int num = -1;
        int i;
        for (i = 0;
             i < path1.Length && i < path2.Length && (path1[i] == path2[i] ||
                                                      (!compareCase && char.ToLowerInvariant(path1[i]) ==
                                                          char.ToLowerInvariant(path2[i])));
             i++)
        {
            if (path1[i] == '/')
            {
                num = i;
            }
        }

        if (i == 0)
        {
            return path2;
        }

        if (i == path1.Length && i == path2.Length)
        {
            return string.Empty;
        }

        StringBuilder stringBuilder = new StringBuilder();
        for (; i < path1.Length; i++)
        {
            if (path1[i] == '/')
            {
                stringBuilder.Append("../");
            }
        }

        if (stringBuilder.Length == 0 && path2.Length - 1 == num)
        {
            return "./";
        }

        return stringBuilder.Append(path2.AsSpan(num + 1)).ToString();
    }

    [Obsolete(
        "The method has been deprecated. Please use MakeRelativeUri(Uri uri). https://go.microsoft.com/fwlink/?linkid=14202")]
    public string MakeRelative(Uri toUri)
    {
        if (toUri == null)
        {
            throw new ArgumentNullException("toUri");
        }

        if (IsNotAbsoluteUri || toUri.IsNotAbsoluteUri)
        {
            throw new InvalidOperationException("SR.net_uri_NotAbsolute");
        }

        if (Scheme == toUri.Scheme && Host == toUri.Host && Port == toUri.Port)
        {
            return PathDifference(AbsolutePath, toUri.AbsolutePath, !IsUncOrDosPath);
        }

        return toUri.ToString();
    }

    [Obsolete(
        "The method has been deprecated. It is not used by the system. https://go.microsoft.com/fwlink/?linkid=14202")]
    protected virtual void Canonicalize()
    {
    }

    [Obsolete(
        "The method has been deprecated. It is not used by the system. https://go.microsoft.com/fwlink/?linkid=14202")]
    protected virtual void Parse()
    {
    }

    [Obsolete(
        "The method has been deprecated. It is not used by the system. https://go.microsoft.com/fwlink/?linkid=14202")]
    protected virtual void Escape()
    {
    }

    [Obsolete(
        "The method has been deprecated. Please use GetComponents() or static UnescapeDataString() to unescape a Uri component or a string. https://go.microsoft.com/fwlink/?linkid=14202")]
    protected virtual string Unescape(string path)
    {
        char[] dest = new char[path.Length];
        int destPosition = 0;
        dest = UriHelper.UnescapeString(path, 0, path.Length, dest, ref destPosition, '\uffff', '\uffff', '\uffff',
            UnescapeMode.Unescape | UnescapeMode.UnescapeAll, null, isQuery: false);
        return new string(dest, 0, destPosition);
    }

    [Obsolete(
        "The method has been deprecated. Please use GetComponents() or static EscapeUriString() to escape a Uri component or a string. https://go.microsoft.com/fwlink/?linkid=14202")]
    protected static string EscapeString(string? str)
    {
        if (str == null)
        {
            return string.Empty;
        }

        int destPos = 0;
        char[] array = UriHelper.EscapeString(str, 0, str.Length, null, ref destPos, isUriString: true, '?', '#', '%');
        if (array == null)
        {
            return str;
        }

        return new string(array, 0, destPos);
    }

    [Obsolete(
        "The method has been deprecated. It is not used by the system. https://go.microsoft.com/fwlink/?linkid=14202")]
    protected virtual void CheckSecurity()
    {
    }

    [Obsolete(
        "The method has been deprecated. It is not used by the system. https://go.microsoft.com/fwlink/?linkid=14202")]
    protected virtual bool IsReservedCharacter(char character)
    {
        if (character != ';' && character != '/' && character != ':' && character != '@' && character != '&' &&
            character != '=' && character != '+' && character != '$')
        {
            return character == ',';
        }

        return true;
    }

    [Obsolete(
        "The method has been deprecated. It is not used by the system. https://go.microsoft.com/fwlink/?linkid=14202")]
    protected static bool IsExcludedCharacter(char character)
    {
        if (character > ' ' && character < '\u007f' && character != '<' && character != '>' && character != '#' &&
            character != '%' && character != '"' && character != '{' && character != '}' && character != '|' &&
            character != '\\' && character != '^' && character != '[' && character != ']')
        {
            return character == '`';
        }

        return true;
    }

    [Obsolete(
        "The method has been deprecated. It is not used by the system. https://go.microsoft.com/fwlink/?linkid=14202")]
    protected virtual bool IsBadFileSystemCharacter(char character)
    {
        if (character >= ' ' && character != ';' && character != '/' && character != '?' && character != ':' &&
            character != '&' && character != '=' && character != ',' && character != '*' && character != '<' &&
            character != '>' && character != '"' && character != '|' && character != '\\')
        {
            return character == '^';
        }

        return true;
    }

    private void CreateThis(string uri, bool dontEscape, UriKind uriKind)
    {
        if (uriKind < UriKind.RelativeOrAbsolute || uriKind > UriKind.Relative)
        {
            throw new ArgumentException(SR.Format("SR.net_uri_InvalidUriKind", uriKind));
        }

        _string = ((uri == null) ? string.Empty : uri);
        if (dontEscape)
        {
            _flags |= Flags.UserEscaped;
        }

        ParsingError err = ParseScheme(_string, ref _flags, ref _syntax);
        InitializeUri(err, uriKind, out var e);
        if (e != null)
        {
            throw e;
        }
    }

    private void InitializeUri(ParsingError err, UriKind uriKind, out UriFormatException e)
    {
        if (err == ParsingError.None)
        {
            if (IsImplicitFile)
            {
                if (NotAny(Flags.DosPath) && uriKind != UriKind.Absolute && (uriKind == UriKind.Relative ||
                                                                             (_string.Length >= 2 &&
                                                                              (_string[0] != '\\' ||
                                                                               _string[1] != '\\'))))
                {
                    _syntax = null;
                    _flags &= Flags.UserEscaped;
                    e = null;
                    return;
                }

                if (uriKind == UriKind.Relative && InFact(Flags.DosPath))
                {
                    _syntax = null;
                    _flags &= Flags.UserEscaped;
                    e = null;
                    return;
                }
            }
        }
        else if (err > ParsingError.EmptyUriString)
        {
            _string = null;
            e = GetException(err);
            return;
        }

        bool flag = false;
        _iriParsing = s_IriParsing && (_syntax == null || _syntax.InFact(UriSyntaxFlags.AllowIriParsing));
        if (_iriParsing && (CheckForUnicode(_string) || CheckForEscapedUnreserved(_string)))
        {
            _flags |= Flags.HasUnicode;
            flag = true;
            _originalUnicodeString = _string;
        }

        if (_syntax != null)
        {
            if (_syntax.IsSimple)
            {
                if ((err = PrivateParseMinimal()) != 0)
                {
                    if (uriKind != UriKind.Absolute && err <= ParsingError.EmptyUriString)
                    {
                        _syntax = null;
                        e = null;
                        _flags &= Flags.UserEscaped;
                        return;
                    }

                    e = GetException(err);
                }
                else if (uriKind == UriKind.Relative)
                {
                    e = GetException(ParsingError.CannotCreateRelative);
                }
                else
                {
                    e = null;
                }

                if (_iriParsing && flag)
                {
                    try
                    {
                        EnsureParseRemaining();
                        return;
                    }
                    catch (UriFormatException ex)
                    {
                        e = ex;
                        return;
                    }
                }

                return;
            }

            _syntax = _syntax.InternalOnNewUri();
            _flags |= Flags.UserDrivenParsing;
            _syntax.InternalValidate(this, out e);
            if (e != null)
            {
                if (uriKind != UriKind.Absolute && err != 0 && err <= ParsingError.EmptyUriString)
                {
                    _syntax = null;
                    e = null;
                    _flags &= Flags.UserEscaped;
                }

                return;
            }

            if (err != 0 || InFact(Flags.ErrorOrParsingRecursion))
            {
                SetUserDrivenParsing();
            }
            else if (uriKind == UriKind.Relative)
            {
                e = GetException(ParsingError.CannotCreateRelative);
            }

            if (_iriParsing && flag)
            {
                try
                {
                    EnsureParseRemaining();
                }
                catch (UriFormatException ex2)
                {
                    e = ex2;
                }
            }
        }
        else if (err != 0 && uriKind != UriKind.Absolute && err <= ParsingError.EmptyUriString)
        {
            e = null;
            _flags &= Flags.UserEscaped | Flags.HasUnicode;
            if (_iriParsing && flag)
            {
                _string = EscapeUnescapeIri(_originalUnicodeString, 0, _originalUnicodeString.Length, (UriComponents)0);
                if (_string.Length > 65535)
                {
                    err = ParsingError.SizeLimit;
                }
            }
        }
        else
        {
            _string = null;
            e = GetException(err);
        }
    }

    private bool CheckForUnicode(string data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            char c = data[i];
            if (c == '%')
            {
                if (i + 2 < data.Length)
                {
                    if (UriHelper.EscapedAscii(data[i + 1], data[i + 2]) > '\u007f')
                    {
                        return true;
                    }

                    i += 2;
                }
            }
            else if (c > '\u007f')
            {
                return true;
            }
        }

        return false;
    }

    private unsafe bool CheckForEscapedUnreserved(string data)
    {
        fixed (char* ptr = data)
        {
            for (int i = 0; i < data.Length - 2; i++)
            {
                if (ptr[i] == '%' && IsHexDigit(ptr[i + 1]) && IsHexDigit(ptr[i + 2]) && ptr[i + 1] >= '0' &&
                    ptr[i + 1] <= '7')
                {
                    char c = UriHelper.EscapedAscii(ptr[i + 1], ptr[i + 2]);
                    if (c != '\uffff' && UriHelper.Is3986Unreserved(c))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool TryCreate(string? uriString, UriKind uriKind, [NotNullWhen(true)] out Uri? result)
    {
        if (uriString == null)
        {
            result = null;
            return false;
        }

        UriFormatException e = null;
        result = CreateHelper(uriString, dontEscape: false, uriKind, ref e);
        if (e == null)
        {
            return result != null;
        }

        return false;
    }

    public static bool TryCreate(Uri? baseUri, string? relativeUri, [NotNullWhen(true)] out Uri? result)
    {
        if (TryCreate(relativeUri, UriKind.RelativeOrAbsolute, out Uri result2))
        {
            if (!result2.IsAbsoluteUri)
            {
                return TryCreate(baseUri, result2, out result);
            }

            result = result2;
            return true;
        }

        result = null;
        return false;
    }

    public static bool TryCreate(Uri? baseUri, Uri? relativeUri, [NotNullWhen(true)] out Uri? result)
    {
        result = null;
        if ((object)baseUri == null || (object)relativeUri == null)
        {
            return false;
        }

        if (baseUri.IsNotAbsoluteUri)
        {
            return false;
        }

        string newUriString = null;
        bool userEscaped;
        UriFormatException parsingError;
        if (baseUri.Syntax.IsSimple)
        {
            userEscaped = relativeUri.UserEscaped;
            result = ResolveHelper(baseUri, relativeUri, ref newUriString, ref userEscaped, out parsingError);
        }
        else
        {
            userEscaped = false;
            newUriString = baseUri.Syntax.InternalResolve(baseUri, relativeUri, out parsingError);
        }

        if (parsingError != null)
        {
            return false;
        }

        if ((object)result == null)
        {
            result = CreateHelper(newUriString, userEscaped, UriKind.Absolute, ref parsingError);
        }

        if (parsingError == null && result != null)
        {
            return result.IsAbsoluteUri;
        }

        return false;
    }

    public string GetComponents(UriComponents components, UriFormat format)
    {
        if (((uint)components & 0x80000000u) != 0 && components != UriComponents.SerializationInfoString)
        {
            throw new ArgumentOutOfRangeException("components", components, "SR.net_uri_NotJustSerialization");
        }

        if (((uint)format & 0xFFFFFFFCu) != 0)
        {
            throw new ArgumentOutOfRangeException("format");
        }

        if (IsNotAbsoluteUri)
        {
            if (components == UriComponents.SerializationInfoString)
            {
                return GetRelativeSerializationString(format);
            }

            throw new InvalidOperationException("SR.net_uri_NotAbsolute");
        }

        if (Syntax.IsSimple)
        {
            return GetComponentsHelper(components, format);
        }

        return Syntax.InternalGetComponents(this, components, format);
    }

    public static int Compare(Uri? uri1, Uri? uri2, UriComponents partsToCompare, UriFormat compareFormat,
        StringComparison comparisonType)
    {
        if ((object)uri1 == null)
        {
            if (uri2 == null)
            {
                return 0;
            }

            return -1;
        }

        if ((object)uri2 == null)
        {
            return 1;
        }

        if (!uri1.IsAbsoluteUri || !uri2.IsAbsoluteUri)
        {
            if (!uri1.IsAbsoluteUri)
            {
                if (!uri2.IsAbsoluteUri)
                {
                    return string.Compare(uri1.OriginalString, uri2.OriginalString, comparisonType);
                }

                return -1;
            }

            return 1;
        }

        return string.Compare(uri1.GetParts(partsToCompare, compareFormat),
            uri2.GetParts(partsToCompare, compareFormat), comparisonType);
    }

    public bool IsWellFormedOriginalString()
    {
        if (IsNotAbsoluteUri || Syntax.IsSimple)
        {
            return InternalIsWellFormedOriginalString();
        }

        return Syntax.InternalIsWellFormedOriginalString(this);
    }

    public static bool IsWellFormedUriString(string? uriString, UriKind uriKind)
    {
        if (!TryCreate(uriString, uriKind, out Uri result))
        {
            return false;
        }

        return result.IsWellFormedOriginalString();
    }

    internal unsafe bool InternalIsWellFormedOriginalString()
    {
        if (UserDrivenParsing)
        {
            throw new InvalidOperationException(SR.Format("SR.net_uri_UserDrivenParsing", GetType()));
        }

        fixed (char* ptr = _string)
        {
            ushort idx = 0;
            if (!IsAbsoluteUri)
            {
                if (CheckForColonInFirstPathSegment(_string))
                {
                    return false;
                }

                return (CheckCanonical(ptr, ref idx, (ushort)_string.Length, '\ufffe') &
                        (Check.EscapedCanonical | Check.BackslashInPath)) == Check.EscapedCanonical;
            }

            if (IsImplicitFile)
            {
                return false;
            }

            EnsureParseRemaining();
            Flags flags = _flags & (Flags.E_CannotDisplayCanonical | Flags.IriCanonical);
            if ((flags & Flags.IriCanonical) != Flags.Zero)
            {
                if ((flags & (Flags.E_UserNotCanonical | Flags.UserIriCanonical)) ==
                    (Flags.E_UserNotCanonical | Flags.UserIriCanonical))
                {
                    flags &= ~(Flags.E_UserNotCanonical | Flags.UserIriCanonical);
                }

                if ((flags & (Flags.E_PathNotCanonical | Flags.PathIriCanonical)) ==
                    (Flags.E_PathNotCanonical | Flags.PathIriCanonical))
                {
                    flags &= ~(Flags.E_PathNotCanonical | Flags.PathIriCanonical);
                }

                if ((flags & (Flags.E_QueryNotCanonical | Flags.QueryIriCanonical)) ==
                    (Flags.E_QueryNotCanonical | Flags.QueryIriCanonical))
                {
                    flags &= ~(Flags.E_QueryNotCanonical | Flags.QueryIriCanonical);
                }

                if ((flags & (Flags.E_FragmentNotCanonical | Flags.FragmentIriCanonical)) ==
                    (Flags.E_FragmentNotCanonical | Flags.FragmentIriCanonical))
                {
                    flags &= ~(Flags.E_FragmentNotCanonical | Flags.FragmentIriCanonical);
                }
            }

            if ((flags & Flags.E_CannotDisplayCanonical & (Flags.E_UserNotCanonical | Flags.E_PathNotCanonical |
                                                           Flags.E_QueryNotCanonical | Flags.E_FragmentNotCanonical)) !=
                Flags.Zero)
            {
                return false;
            }

            if (InFact(Flags.AuthorityFound))
            {
                idx = (ushort)(_info.Offset.Scheme + _syntax.SchemeName.Length + 2);
                if (idx >= _info.Offset.User || _string[idx - 1] == '\\' || _string[idx] == '\\')
                {
                    return false;
                }

                if (InFact(Flags.DosPath | Flags.UncPath) && ++idx < _info.Offset.User &&
                    (_string[idx] == '/' || _string[idx] == '\\'))
                {
                    return false;
                }
            }

            if (InFact(Flags.FirstSlashAbsent) && _info.Offset.Query > _info.Offset.Path)
            {
                return false;
            }

            if (InFact(Flags.BackslashInPath))
            {
                return false;
            }

            if (IsDosPath && _string[_info.Offset.Path + SecuredPathIndex - 1] == '|')
            {
                return false;
            }

            if ((_flags & Flags.CanonicalDnsHost) == Flags.Zero && HostType != Flags.IPv6HostType)
            {
                idx = _info.Offset.User;
                Check check = CheckCanonical(ptr, ref idx, _info.Offset.Path, '/');
                if ((check & (Check.EscapedCanonical | Check.BackslashInPath | Check.ReservedFound)) !=
                    Check.EscapedCanonical && (!_iriParsing || (_iriParsing &&
                                                                (check & (Check.DisplayCanonical |
                                                                          Check.NotIriCanonical |
                                                                          Check.FoundNonAscii)) !=
                                                                (Check.DisplayCanonical | Check.FoundNonAscii))))
                {
                    return false;
                }
            }

            if ((_flags & (Flags.SchemeNotCanonical | Flags.AuthorityFound)) ==
                (Flags.SchemeNotCanonical | Flags.AuthorityFound))
            {
                idx = (ushort)_syntax.SchemeName.Length;
                while (ptr[(int)idx++] != ':')
                {
                }

                if (idx + 1 >= _string.Length || ptr[(int)idx] != '/' || ptr[idx + 1] != '/')
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static unsafe string UnescapeDataString(string stringToUnescape)
    {
        if (stringToUnescape == null)
        {
            throw new ArgumentNullException("stringToUnescape");
        }

        if (stringToUnescape.Length == 0)
        {
            return string.Empty;
        }

        fixed (char* ptr = stringToUnescape)
        {
            int i;
            for (i = 0; i < stringToUnescape.Length && ptr[i] != '%'; i++)
            {
            }

            if (i == stringToUnescape.Length)
            {
                return stringToUnescape;
            }

            UnescapeMode unescapeMode = UnescapeMode.Unescape | UnescapeMode.UnescapeAll;
            i = 0;
            char[] dest = new char[stringToUnescape.Length];
            dest = UriHelper.UnescapeString(stringToUnescape, 0, stringToUnescape.Length, dest, ref i, '\uffff',
                '\uffff', '\uffff', unescapeMode, null, isQuery: false);
            return new string(dest, 0, i);
        }
    }

    public static string EscapeUriString(string stringToEscape)
    {
        if (stringToEscape == null)
        {
            throw new ArgumentNullException("stringToEscape");
        }

        if (stringToEscape.Length == 0)
        {
            return string.Empty;
        }

        int destPos = 0;
        char[] array = UriHelper.EscapeString(stringToEscape, 0, stringToEscape.Length, null, ref destPos,
            isUriString: true, '\uffff', '\uffff', '\uffff');
        if (array == null)
        {
            return stringToEscape;
        }

        return new string(array, 0, destPos);
    }

    public static string EscapeDataString(string stringToEscape)
    {
        if (stringToEscape == null)
        {
            throw new ArgumentNullException("stringToEscape");
        }

        if (stringToEscape.Length == 0)
        {
            return string.Empty;
        }

        int destPos = 0;
        char[] array = UriHelper.EscapeString(stringToEscape, 0, stringToEscape.Length, null, ref destPos,
            isUriString: false, '\uffff', '\uffff', '\uffff');
        if (array == null)
        {
            return stringToEscape;
        }

        return new string(array, 0, destPos);
    }

    internal unsafe string EscapeUnescapeIri(string input, int start, int end, UriComponents component)
    {
        fixed (char* pInput = input)
        {
            return IriHelper.EscapeUnescapeIri(pInput, start, end, component);
        }
    }

    private Uri(Flags flags, UriParser uriParser, string uri)
    {
        _flags = flags;
        _syntax = uriParser;
        _string = uri;
    }

    internal static Uri CreateHelper(string uriString, bool dontEscape, UriKind uriKind, ref UriFormatException e)
    {
        if (uriKind < UriKind.RelativeOrAbsolute || uriKind > UriKind.Relative)
        {
            throw new ArgumentException(SR.Format("SR.net_uri_InvalidUriKind", uriKind));
        }

        UriParser syntax = null;
        Flags flags = Flags.Zero;
        ParsingError parsingError = ParseScheme(uriString, ref flags, ref syntax);
        if (dontEscape)
        {
            flags |= Flags.UserEscaped;
        }

        if (parsingError != 0)
        {
            if (uriKind != UriKind.Absolute && parsingError <= ParsingError.EmptyUriString)
            {
                return new Uri(flags & Flags.UserEscaped, null, uriString);
            }

            return null;
        }

        Uri uri = new Uri(flags, syntax, uriString);
        try
        {
            uri.InitializeUri(parsingError, uriKind, out e);
            if (e == null)
            {
                return uri;
            }

            return null;
        }
        catch (UriFormatException ex)
        {
            e = ex;
            return null;
        }
    }

    internal static Uri ResolveHelper(Uri baseUri, Uri relativeUri, ref string newUriString, ref bool userEscaped,
        out UriFormatException e)
    {
        e = null;
        string empty = string.Empty;
        if ((object)relativeUri != null)
        {
            if (relativeUri.IsAbsoluteUri)
            {
                return relativeUri;
            }

            empty = relativeUri.OriginalString;
            userEscaped = relativeUri.UserEscaped;
        }
        else
        {
            empty = string.Empty;
        }

        if (empty.Length > 0 && (UriHelper.IsLWS(empty[0]) || UriHelper.IsLWS(empty[empty.Length - 1])))
        {
            empty = empty.Trim(UriHelper.s_WSchars);
        }

        if (empty.Length == 0)
        {
            newUriString = baseUri.GetParts(UriComponents.AbsoluteUri,
                baseUri.UserEscaped ? UriFormat.UriEscaped : UriFormat.SafeUnescaped);
            return null;
        }

        if (empty[0] == '#' && !baseUri.IsImplicitFile && baseUri.Syntax.InFact(UriSyntaxFlags.MayHaveFragment))
        {
            newUriString =
                baseUri.GetParts(UriComponents.HttpRequestUrl | UriComponents.UserInfo, UriFormat.UriEscaped) + empty;
            return null;
        }

        if (empty[0] == '?' && !baseUri.IsImplicitFile && baseUri.Syntax.InFact(UriSyntaxFlags.MayHaveQuery))
        {
            newUriString = baseUri.GetParts(UriComponents.SchemeAndServer | UriComponents.UserInfo | UriComponents.Path,
                UriFormat.UriEscaped) + empty;
            return null;
        }

        if (empty.Length >= 3 && (empty[1] == ':' || empty[1] == '|') && UriHelper.IsAsciiLetter(empty[0]) &&
            (empty[2] == '\\' || empty[2] == '/'))
        {
            if (baseUri.IsImplicitFile)
            {
                newUriString = empty;
                return null;
            }

            if (baseUri.Syntax.InFact(UriSyntaxFlags.AllowDOSPath))
            {
                newUriString = string.Concat(
                    str1: (!baseUri.InFact(Flags.AuthorityFound))
                        ? (baseUri.Syntax.InFact(UriSyntaxFlags.PathIsRooted) ? ":/" : ":")
                        : (baseUri.Syntax.InFact(UriSyntaxFlags.PathIsRooted) ? ":///" : "://"), str0: baseUri.Scheme,
                    str2: empty);
                return null;
            }
        }

        ParsingError combinedString = GetCombinedString(baseUri, empty, userEscaped, ref newUriString);
        if (combinedString != 0)
        {
            e = GetException(combinedString);
            return null;
        }

        if ((object)newUriString == baseUri._string)
        {
            return baseUri;
        }

        return null;
    }

    private string GetRelativeSerializationString(UriFormat format)
    {
        switch (format)
        {
            case UriFormat.UriEscaped:
            {
                if (_string.Length == 0)
                {
                    return string.Empty;
                }

                int destPos = 0;
                char[] array = UriHelper.EscapeString(_string, 0, _string.Length, null, ref destPos, isUriString: true,
                    '\uffff', '\uffff', '%');
                if (array == null)
                {
                    return _string;
                }

                return new string(array, 0, destPos);
            }
            case UriFormat.Unescaped:
                return UnescapeDataString(_string);
            case UriFormat.SafeUnescaped:
            {
                if (_string.Length == 0)
                {
                    return string.Empty;
                }

                char[] dest = new char[_string.Length];
                int destPosition = 0;
                dest = UriHelper.UnescapeString(_string, 0, _string.Length, dest, ref destPosition, '\uffff', '\uffff',
                    '\uffff', UnescapeMode.EscapeUnescape, null, isQuery: false);
                return new string(dest, 0, destPosition);
            }
            default:
                throw new ArgumentOutOfRangeException("format");
        }
    }

    internal string GetComponentsHelper(UriComponents uriComponents, UriFormat uriFormat)
    {
        if (uriComponents == UriComponents.Scheme)
        {
            return _syntax.SchemeName;
        }

        if (((uint)uriComponents & 0x80000000u) != 0)
        {
            uriComponents |= UriComponents.AbsoluteUri;
        }

        EnsureParseRemaining();
        if ((uriComponents & UriComponents.NormalizedHost) != 0)
        {
            uriComponents |= UriComponents.Host;
        }

        if ((uriComponents & UriComponents.Host) != 0)
        {
            EnsureHostString(allowDnsOptimization: true);
        }

        if (uriComponents == UriComponents.Port || uriComponents == UriComponents.StrongPort)
        {
            if ((_flags & Flags.NotDefaultPort) != Flags.Zero ||
                (uriComponents == UriComponents.StrongPort && _syntax.DefaultPort != -1))
            {
                return _info.Offset.PortValue.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        if ((uriComponents & UriComponents.StrongPort) != 0)
        {
            uriComponents |= UriComponents.Port;
        }

        if (uriComponents == UriComponents.Host && (uriFormat == UriFormat.UriEscaped ||
                                                    (_flags & (Flags.HostNotCanonical | Flags.E_HostNotCanonical)) ==
                                                    Flags.Zero))
        {
            EnsureHostString(allowDnsOptimization: false);
            return _info.Host;
        }

        switch (uriFormat)
        {
            case UriFormat.UriEscaped:
                return GetEscapedParts(uriComponents);
            case UriFormat.Unescaped:
            case UriFormat.SafeUnescaped:
            case (UriFormat)32767:
                return GetUnescapedParts(uriComponents, uriFormat);
            default:
                throw new ArgumentOutOfRangeException("uriFormat");
        }
    }

    public bool IsBaseOf(Uri uri)
    {
        if ((object)uri == null)
        {
            throw new ArgumentNullException("uri");
        }

        if (!IsAbsoluteUri)
        {
            return false;
        }

        if (Syntax.IsSimple)
        {
            return IsBaseOfHelper(uri);
        }

        return Syntax.InternalIsBaseOf(this, uri);
    }

    internal unsafe bool IsBaseOfHelper(Uri uriLink)
    {
        //The blocks IL_00a1, IL_00bd, IL_00c5, IL_00c6 are reachable both inside and outside the pinned region starting at IL_009c. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
        if (!IsAbsoluteUri || UserDrivenParsing)
        {
            return false;
        }

        if (!uriLink.IsAbsoluteUri)
        {
            string newUriString = null;
            bool userEscaped = false;
            uriLink = ResolveHelper(this, uriLink, ref newUriString, ref userEscaped, out var e);
            if (e != null)
            {
                return false;
            }

            if ((object)uriLink == null)
            {
                uriLink = CreateHelper(newUriString, userEscaped, UriKind.Absolute, ref e);
            }

            if (e != null)
            {
                return false;
            }
        }

        if (Syntax.SchemeName != uriLink.Syntax.SchemeName)
        {
            return false;
        }

        string parts = GetParts(UriComponents.HttpRequestUrl | UriComponents.UserInfo, UriFormat.SafeUnescaped);
        string parts2 =
            uriLink.GetParts(UriComponents.HttpRequestUrl | UriComponents.UserInfo, UriFormat.SafeUnescaped);
        fixed (char* ptr3 = parts)
        {
            char* intPtr;
            char* selfPtr;
            int selfLength;
            char* otherPtr;
            int otherLength;
            int ignoreCase;
            char* ptr2;
            if (parts2 != null)
            {
                fixed (char* ptr = &parts2.GetPinnableReference())
                {
                    intPtr = (ptr2 = ptr);
                    selfPtr = ptr3;
                    selfLength = (ushort)parts.Length;
                    otherPtr = ptr2;
                    otherLength = (ushort)parts2.Length;
                    ignoreCase = ((IsUncOrDosPath || uriLink.IsUncOrDosPath) ? 1 : 0);
                    return UriHelper.TestForSubPath(selfPtr, (ushort)selfLength, otherPtr, (ushort)otherLength,
                        (byte)ignoreCase != 0);
                }
            }

            intPtr = (ptr2 = null);
            selfPtr = ptr3;
            selfLength = (ushort)parts.Length;
            otherPtr = ptr2;
            otherLength = (ushort)parts2.Length;
            ignoreCase = ((IsUncOrDosPath || uriLink.IsUncOrDosPath) ? 1 : 0);
            return UriHelper.TestForSubPath(selfPtr, (ushort)selfLength, otherPtr, (ushort)otherLength,
                (byte)ignoreCase != 0);
        }
    }

    private void CreateThisFromUri(Uri otherUri)
    {
        _info = null;
        _flags = otherUri._flags;
        if (InFact(Flags.MinimalUriInfoSet))
        {
            _flags &= ~(Flags.IndexMask | Flags.MinimalUriInfoSet | Flags.AllUriInfoSet);
            int num = otherUri._info.Offset.Path;
            if (InFact(Flags.NotDefaultPort))
            {
                while (otherUri._string[num] != ':' && num > otherUri._info.Offset.Host)
                {
                    num--;
                }

                if (otherUri._string[num] != ':')
                {
                    num = otherUri._info.Offset.Path;
                }
            }

            _flags |= (Flags)num;
        }

        _syntax = otherUri._syntax;
        _string = otherUri._string;
        _iriParsing = otherUri._iriParsing;
        if (otherUri.OriginalStringSwitched)
        {
            _originalUnicodeString = otherUri._originalUnicodeString;
        }

        if (otherUri.AllowIdn && (otherUri.InFact(Flags.IdnHost) || otherUri.InFact(Flags.UnicodeHost)))
        {
            _dnsSafeHost = otherUri._dnsSafeHost;
        }
    }
}
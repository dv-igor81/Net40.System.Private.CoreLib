/*
using System.Collections.Generic;
using System.Resources;
using System.Threading;
using DefaultNamespace;

namespace System.Globalization.Net40;

public class CultureInfo : IFormatProvider, ICloneable
{
	private bool _isReadOnly;

	private CompareInfo _compareInfo;

	private TextInfo _textInfo;

	internal NumberFormatInfo _numInfo;

	internal DateTimeFormatInfo _dateTimeInfo;

	private Calendar _calendar;

	internal CultureData _cultureData;

	internal bool _isInherited;

	private CultureInfo _consoleFallbackCulture;

	internal string _name;

	private string _nonSortName;

	private string _sortName;

	private static volatile CultureInfo s_userDefaultCulture;

	private static volatile CultureInfo s_userDefaultUICulture;

	private static readonly CultureInfo s_InvariantCultureInfo = new CultureInfo(CultureData.Invariant, isReadOnly: true);

	private static volatile CultureInfo s_DefaultThreadCurrentUICulture;

	private static volatile CultureInfo s_DefaultThreadCurrentCulture;

	[ThreadStatic]
	private static CultureInfo s_currentThreadCulture;

	[ThreadStatic]
	private static CultureInfo s_currentThreadUICulture;

	private static AsyncLocal<CultureInfo> s_asyncLocalCurrentCulture;

	private static AsyncLocal<CultureInfo> s_asyncLocalCurrentUICulture;

	private static readonly object _lock = new object();

	private static volatile Dictionary<string, CultureInfo> s_NameCachedCultures;

	private static volatile Dictionary<int, CultureInfo> s_LcidCachedCultures;

	private CultureInfo _parent;

	internal const int LOCALE_NEUTRAL = 0;

	private const int LOCALE_USER_DEFAULT = 1024;

	private const int LOCALE_SYSTEM_DEFAULT = 2048;

	internal const int LOCALE_CUSTOM_UNSPECIFIED = 4096;

	internal const int LOCALE_CUSTOM_DEFAULT = 3072;

	internal const int LOCALE_INVARIANT = 127;

	//private static /*volatile#1# WindowsRuntimeResourceManagerBase s_WindowsRuntimeResourceManager;

	[ThreadStatic]
	private static bool ts_IsDoingAppXCultureInfoLookup;

	public static CultureInfo CurrentCulture
	{
		get
		{
			// if (ApplicationModel.IsUap)
			// {
			// 	CultureInfo cultureInfoForUserPreferredLanguageInAppX = GetCultureInfoForUserPreferredLanguageInAppX();
			// 	if (cultureInfoForUserPreferredLanguageInAppX != null)
			// 	{
			// 		return cultureInfoForUserPreferredLanguageInAppX;
			// 	}
			// }
			return s_currentThreadCulture ?? s_DefaultThreadCurrentCulture ?? s_userDefaultCulture ?? InitializeUserDefaultCulture();
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			if (/*!ApplicationModel.IsUap ||#1# !SetCultureInfoForUserPreferredLanguageInAppX(value))
			{
				if (s_asyncLocalCurrentCulture == null)
				{
					Interlocked.CompareExchange(ref s_asyncLocalCurrentCulture, new AsyncLocal<CultureInfo>(AsyncLocalSetCurrentCulture), null);
				}
				s_asyncLocalCurrentCulture.Value = value;
			}
		}
	}

	public static CultureInfo CurrentUICulture
	{
		get
		{
			//if (ApplicationModel.IsUap)
			{
				CultureInfo cultureInfoForUserPreferredLanguageInAppX = GetCultureInfoForUserPreferredLanguageInAppX();
				if (cultureInfoForUserPreferredLanguageInAppX != null)
				{
					return cultureInfoForUserPreferredLanguageInAppX;
				}
			}
			return s_currentThreadUICulture ?? s_DefaultThreadCurrentUICulture ?? UserDefaultUICulture;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			VerifyCultureName(value, throwException: true);
			if (/*!ApplicationModel.IsUap ||#1# !SetCultureInfoForUserPreferredLanguageInAppX(value))
			{
				if (s_asyncLocalCurrentUICulture == null)
				{
					Interlocked.CompareExchange(ref s_asyncLocalCurrentUICulture, new AsyncLocal<CultureInfo>(AsyncLocalSetCurrentUICulture), null);
				}
				s_asyncLocalCurrentUICulture.Value = value;
			}
		}
	}

	internal static CultureInfo UserDefaultUICulture => s_userDefaultUICulture ?? InitializeUserDefaultUICulture();

	public static CultureInfo InstalledUICulture => s_userDefaultCulture ?? InitializeUserDefaultCulture();

	public static CultureInfo? DefaultThreadCurrentCulture
	{
		get
		{
			return s_DefaultThreadCurrentCulture;
		}
		set
		{
			s_DefaultThreadCurrentCulture = value;
		}
	}

	public static CultureInfo? DefaultThreadCurrentUICulture
	{
		get
		{
			return s_DefaultThreadCurrentUICulture;
		}
		set
		{
			if (value != null)
			{
				VerifyCultureName(value, throwException: true);
			}
			s_DefaultThreadCurrentUICulture = value;
		}
	}

	public static CultureInfo InvariantCulture => s_InvariantCultureInfo;

	public virtual CultureInfo Parent
	{
		get
		{
			if (_parent == null)
			{
				string parentName = _cultureData.ParentName;
				Interlocked.CompareExchange(value: (!string.IsNullOrEmpty(parentName)) ? (CreateCultureInfoNoThrow(parentName, _cultureData.UseUserOverride) ?? InvariantCulture) : InvariantCulture, location1: ref _parent, comparand: null);
			}
			return _parent;
		}
	}

	public virtual int LCID => _cultureData.LCID;

	public virtual int KeyboardLayoutId => _cultureData.KeyboardLayoutId;

	public virtual string Name
	{
		get
		{
			if (_nonSortName == null)
			{
				_nonSortName = _cultureData.Name ?? string.Empty;
			}
			return _nonSortName;
		}
	}

	internal string SortName
	{
		get
		{
			if (_sortName == null)
			{
				_sortName = _cultureData.SortName;
			}
			return _sortName;
		}
	}

	public string IetfLanguageTag => Name switch
	{
		"zh-CHT" => "zh-Hant", 
		"zh-CHS" => "zh-Hans", 
		_ => Name, 
	};

	public virtual string DisplayName => _cultureData.DisplayName;

	public virtual string NativeName => _cultureData.NativeName;

	public virtual string EnglishName => _cultureData.EnglishName;

	public virtual string TwoLetterISOLanguageName => _cultureData.TwoLetterISOLanguageName;

	public virtual string ThreeLetterISOLanguageName => _cultureData.ThreeLetterISOLanguageName;

	public virtual string ThreeLetterWindowsLanguageName => _cultureData.ThreeLetterWindowsLanguageName;

	public virtual CompareInfo CompareInfo
	{
		get
		{
			if (_compareInfo == null)
			{
				_compareInfo = (UseUserOverride ? GetCultureInfo(_name).CompareInfo : new CompareInfo(this));
			}
			return _compareInfo;
		}
	}

	public virtual TextInfo TextInfo
	{
		get
		{
			if (_textInfo == null)
			{
				TextInfo textInfo = new TextInfo(_cultureData);
				textInfo.SetReadOnlyState(_isReadOnly);
				_textInfo = textInfo;
			}
			return _textInfo;
		}
	}

	public virtual bool IsNeutralCulture => _cultureData.IsNeutralCulture;

	public CultureTypes CultureTypes
	{
		get
		{
			CultureTypes cultureTypes = (CultureTypes)0;
			cultureTypes = ((!_cultureData.IsNeutralCulture) ? (cultureTypes | CultureTypes.SpecificCultures) : (cultureTypes | CultureTypes.NeutralCultures));
			cultureTypes |= (_cultureData.IsWin32Installed ? CultureTypes.InstalledWin32Cultures : ((CultureTypes)0));
			cultureTypes |= (_cultureData.IsFramework ? CultureTypes.FrameworkCultures : ((CultureTypes)0));
			cultureTypes |= (_cultureData.IsSupplementalCustomCulture ? CultureTypes.UserCustomCulture : ((CultureTypes)0));
			return cultureTypes | (_cultureData.IsReplacementCulture ? (CultureTypes.UserCustomCulture | CultureTypes.ReplacementCultures) : ((CultureTypes)0));
		}
	}

	public virtual NumberFormatInfo NumberFormat
	{
		get
		{
			if (_numInfo == null)
			{
				NumberFormatInfo numberFormatInfo = new NumberFormatInfo(_cultureData);
				numberFormatInfo._isReadOnly = _isReadOnly;
				Interlocked.CompareExchange(ref _numInfo, numberFormatInfo, null);
			}
			return _numInfo;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			VerifyWritable();
			_numInfo = value;
		}
	}

	public virtual DateTimeFormatInfo DateTimeFormat
	{
		get
		{
			if (_dateTimeInfo == null)
			{
				DateTimeFormatInfo dateTimeFormatInfo = new DateTimeFormatInfo(_cultureData, Calendar);
				dateTimeFormatInfo._isReadOnly = _isReadOnly;
				Interlocked.CompareExchange(ref _dateTimeInfo, dateTimeFormatInfo, null);
			}
			return _dateTimeInfo;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			VerifyWritable();
			_dateTimeInfo = value;
		}
	}

	public virtual Calendar Calendar
	{
		get
		{
			if (_calendar == null)
			{
				Calendar defaultCalendar = _cultureData.DefaultCalendar;
				Interlocked.MemoryBarrier();
				defaultCalendar.SetReadOnlyState(_isReadOnly);
				_calendar = defaultCalendar;
			}
			return _calendar;
		}
	}

	public virtual Calendar[] OptionalCalendars
	{
		get
		{
			CalendarId[] calendarIds = _cultureData.CalendarIds;
			Calendar[] array = new Calendar[calendarIds.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = GetCalendarInstance(calendarIds[i]);
			}
			return array;
		}
	}

	public bool UseUserOverride => _cultureData.UseUserOverride;

	public bool IsReadOnly => _isReadOnly;

	internal bool HasInvariantCultureName => Name == InvariantCulture.Name;

	private static void AsyncLocalSetCurrentCulture(AsyncLocalValueChangedArgs<CultureInfo> args)
	{
		s_currentThreadCulture = args.CurrentValue;
	}

	private static void AsyncLocalSetCurrentUICulture(AsyncLocalValueChangedArgs<CultureInfo> args)
	{
		s_currentThreadUICulture = args.CurrentValue;
	}

	private static CultureInfo InitializeUserDefaultCulture()
	{
		Interlocked.CompareExchange(ref s_userDefaultCulture, GetUserDefaultCulture(), null);
		return s_userDefaultCulture;
	}

	private static CultureInfo InitializeUserDefaultUICulture()
	{
		Interlocked.CompareExchange(ref s_userDefaultUICulture, GetUserDefaultUICulture(), null);
		return s_userDefaultUICulture;
	}

	public CultureInfo(string name)
		: this(name, useUserOverride: true)
	{
	}

	public CultureInfo(string name, bool useUserOverride)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		CultureData cultureData = CultureData.GetCultureData(name, useUserOverride);
		if (cultureData == null)
		{
			throw new CultureNotFoundException("name", name, SR.Argument_CultureNotSupported);
		}
		_cultureData = cultureData;
		_name = _cultureData.CultureName;
		_isInherited = GetType() != typeof(CultureInfo);
	}

	private CultureInfo(CultureData cultureData, bool isReadOnly = false)
	{
		_cultureData = cultureData;
		_name = cultureData.CultureName;
		_isInherited = false;
		_isReadOnly = isReadOnly;
	}

	private static CultureInfo CreateCultureInfoNoThrow(string name, bool useUserOverride)
	{
		CultureData cultureData = CultureData.GetCultureData(name, useUserOverride);
		if (cultureData == null)
		{
			return null;
		}
		return new CultureInfo(cultureData);
	}

	public CultureInfo(int culture)
		: this(culture, useUserOverride: true)
	{
	}

	public CultureInfo(int culture, bool useUserOverride)
	{
		if (culture < 0)
		{
			throw new ArgumentOutOfRangeException("culture", SR.ArgumentOutOfRange_NeedPosNum);
		}
		switch (culture)
		{
		case 0:
		case 1024:
		case 2048:
		case 3072:
		case 4096:
			throw new CultureNotFoundException("culture", culture, SR.Argument_CultureNotSupported);
		}
		_cultureData = CultureData.GetCultureData(culture, useUserOverride);
		_isInherited = GetType() != typeof(CultureInfo);
		_name = _cultureData.CultureName;
	}

	internal CultureInfo(string cultureName, string textAndCompareCultureName)
	{
		if (cultureName == null)
		{
			throw new ArgumentNullException("cultureName", SR.ArgumentNull_String);
		}
		CultureData cultureData = CultureData.GetCultureData(cultureName, useUserOverride: false);
		if (cultureData == null)
		{
			throw new CultureNotFoundException("cultureName", cultureName, SR.Argument_CultureNotSupported);
		}
		_cultureData = cultureData;
		_name = _cultureData.CultureName;
		CultureInfo cultureInfo = GetCultureInfo(textAndCompareCultureName);
		_compareInfo = cultureInfo.CompareInfo;
		_textInfo = cultureInfo.TextInfo;
	}

	private static CultureInfo GetCultureByName(string name)
	{
		try
		{
			return new CultureInfo(name)
			{
				_isReadOnly = true
			};
		}
		catch (ArgumentException)
		{
			return InvariantCulture;
		}
	}

	public static CultureInfo CreateSpecificCulture(string name)
	{
		CultureInfo cultureInfo;
		try
		{
			cultureInfo = new CultureInfo(name);
		}
		catch (ArgumentException)
		{
			cultureInfo = null;
			for (int i = 0; i < name.Length; i++)
			{
				if ('-' == name[i])
				{
					try
					{
						cultureInfo = new CultureInfo(name.Substring(0, i));
					}
					catch (ArgumentException)
					{
						throw;
					}
					break;
				}
			}
			if (cultureInfo == null)
			{
				throw;
			}
		}
		if (!cultureInfo.IsNeutralCulture)
		{
			return cultureInfo;
		}
		return new CultureInfo(cultureInfo._cultureData.SpecificCultureName);
	}

	internal static bool VerifyCultureName(string cultureName, bool throwException)
	{
		foreach (char c in cultureName)
		{
			if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
			{
				if (throwException)
				{
					throw new ArgumentException(SR.Format(SR.Argument_InvalidResourceCultureName, cultureName));
				}
				return false;
			}
		}
		return true;
	}

	internal static bool VerifyCultureName(CultureInfo culture, bool throwException)
	{
		if (!culture._isInherited)
		{
			return true;
		}
		return VerifyCultureName(culture.Name, throwException);
	}

	public static CultureInfo[] GetCultures(CultureTypes types)
	{
		if ((types & CultureTypes.UserCustomCulture) == CultureTypes.UserCustomCulture)
		{
			types |= CultureTypes.ReplacementCultures;
		}
		return CultureData.GetCultures(types);
	}

	public override bool Equals(object? value)
	{
		if (this == value)
		{
			return true;
		}
		if (value is CultureInfo cultureInfo)
		{
			if (Name.Equals(cultureInfo.Name))
			{
				return CompareInfo.Equals(cultureInfo.CompareInfo);
			}
			return false;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Name.GetHashCode() + CompareInfo.GetHashCode();
	}

	public override string ToString()
	{
		return _name;
	}

	public virtual object? GetFormat(Type? formatType)
	{
		if (formatType == typeof(NumberFormatInfo))
		{
			return NumberFormat;
		}
		if (formatType == typeof(DateTimeFormatInfo))
		{
			return DateTimeFormat;
		}
		return null;
	}

	public void ClearCachedData()
	{
		s_userDefaultCulture = GetUserDefaultCulture();
		s_userDefaultUICulture = GetUserDefaultUICulture();
		RegionInfo.s_currentRegionInfo = null;
		TimeZone.ResetTimeZone();
		TimeZoneInfo.ClearCachedData();
		s_LcidCachedCultures = null;
		s_NameCachedCultures = null;
		CultureData.ClearCachedData();
	}

	internal static Calendar GetCalendarInstance(CalendarId calType)
	{
		if (calType == CalendarId.GREGORIAN)
		{
			return new GregorianCalendar();
		}
		return GetCalendarInstanceRare(calType);
	}

	internal static Calendar GetCalendarInstanceRare(CalendarId calType)
	{
		switch (calType)
		{
		case CalendarId.GREGORIAN_US:
		case CalendarId.GREGORIAN_ME_FRENCH:
		case CalendarId.GREGORIAN_ARABIC:
		case CalendarId.GREGORIAN_XLIT_ENGLISH:
		case CalendarId.GREGORIAN_XLIT_FRENCH:
			return new GregorianCalendar((GregorianCalendarTypes)calType);
		case CalendarId.TAIWAN:
			return new TaiwanCalendar();
		case CalendarId.JAPAN:
			return new JapaneseCalendar();
		case CalendarId.KOREA:
			return new KoreanCalendar();
		case CalendarId.THAI:
			return new ThaiBuddhistCalendar();
		case CalendarId.HIJRI:
			return new HijriCalendar();
		case CalendarId.HEBREW:
			return new HebrewCalendar();
		case CalendarId.UMALQURA:
			return new UmAlQuraCalendar();
		case CalendarId.PERSIAN:
			return new PersianCalendar();
		default:
			return new GregorianCalendar();
		}
	}

	public CultureInfo GetConsoleFallbackUICulture()
	{
		CultureInfo cultureInfo = _consoleFallbackCulture;
		if (cultureInfo == null)
		{
			cultureInfo = CreateSpecificCulture(_cultureData.SCONSOLEFALLBACKNAME);
			cultureInfo._isReadOnly = true;
			_consoleFallbackCulture = cultureInfo;
		}
		return cultureInfo;
	}

	public virtual object Clone()
	{
		CultureInfo cultureInfo = (CultureInfo)MemberwiseClone();
		cultureInfo._isReadOnly = false;
		if (!_isInherited)
		{
			if (_dateTimeInfo != null)
			{
				cultureInfo._dateTimeInfo = (DateTimeFormatInfo)_dateTimeInfo.Clone();
			}
			if (_numInfo != null)
			{
				cultureInfo._numInfo = (NumberFormatInfo)_numInfo.Clone();
			}
		}
		else
		{
			cultureInfo.DateTimeFormat = (DateTimeFormatInfo)DateTimeFormat.Clone();
			cultureInfo.NumberFormat = (NumberFormatInfo)NumberFormat.Clone();
		}
		if (_textInfo != null)
		{
			cultureInfo._textInfo = (TextInfo)_textInfo.Clone();
		}
		if (_calendar != null)
		{
			cultureInfo._calendar = (Calendar)_calendar.Clone();
		}
		return cultureInfo;
	}

	public static CultureInfo ReadOnly(CultureInfo ci)
	{
		if (ci == null)
		{
			throw new ArgumentNullException("ci");
		}
		if (ci.IsReadOnly)
		{
			return ci;
		}
		CultureInfo cultureInfo = (CultureInfo)ci.MemberwiseClone();
		if (!ci.IsNeutralCulture)
		{
			if (!ci._isInherited)
			{
				if (ci._dateTimeInfo != null)
				{
					cultureInfo._dateTimeInfo = DateTimeFormatInfo.ReadOnly(ci._dateTimeInfo);
				}
				if (ci._numInfo != null)
				{
					cultureInfo._numInfo = NumberFormatInfo.ReadOnly(ci._numInfo);
				}
			}
			else
			{
				cultureInfo.DateTimeFormat = DateTimeFormatInfo.ReadOnly(ci.DateTimeFormat);
				cultureInfo.NumberFormat = NumberFormatInfo.ReadOnly(ci.NumberFormat);
			}
		}
		if (ci._textInfo != null)
		{
			cultureInfo._textInfo = System.Globalization.TextInfo.ReadOnly(ci._textInfo);
		}
		if (ci._calendar != null)
		{
			cultureInfo._calendar = System.Globalization.Calendar.ReadOnly(ci._calendar);
		}
		cultureInfo._isReadOnly = true;
		return cultureInfo;
	}

	private void VerifyWritable()
	{
		if (_isReadOnly)
		{
			throw new InvalidOperationException(SR.InvalidOperation_ReadOnly);
		}
	}

	internal static CultureInfo GetCultureInfoHelper(int lcid, string name, string altName)
	{
		Dictionary<string, CultureInfo> dictionary = s_NameCachedCultures;
		if (name != null)
		{
			name = CultureData.AnsiToLower(name);
		}
		if (altName != null)
		{
			altName = CultureData.AnsiToLower(altName);
		}
		CultureInfo value;
		if (dictionary == null)
		{
			dictionary = new Dictionary<string, CultureInfo>();
		}
		else if (lcid == -1 || lcid == 0)
		{
			string key = ((lcid == 0) ? name : (name + "\ufffd" + altName));
			bool flag;
			lock (_lock)
			{
				flag = dictionary.TryGetValue(key, out value);
			}
			if (flag && value != null)
			{
				return value;
			}
		}
		Dictionary<int, CultureInfo> dictionary2 = s_LcidCachedCultures;
		if (dictionary2 == null)
		{
			dictionary2 = new Dictionary<int, CultureInfo>();
		}
		else if (lcid > 0)
		{
			bool flag2;
			lock (_lock)
			{
				flag2 = dictionary2.TryGetValue(lcid, out value);
			}
			if (flag2 && value != null)
			{
				return value;
			}
		}
		try
		{
			value = lcid switch
			{
				-1 => new CultureInfo(name, altName), 
				0 => new CultureInfo(name, useUserOverride: false), 
				_ => new CultureInfo(lcid, useUserOverride: false), 
			};
		}
		catch (ArgumentException)
		{
			return null;
		}
		value._isReadOnly = true;
		switch (lcid)
		{
		case -1:
		{
			string key3 = name + "\ufffd" + altName;
			lock (_lock)
			{
				dictionary[key3] = value;
			}
			value.TextInfo.SetReadOnlyState(readOnly: true);
			break;
		}
		case 0:
		{
			string key2 = CultureData.AnsiToLower(value._name);
			lock (_lock)
			{
				dictionary[key2] = value;
			}
			break;
		}
		default:
			lock (_lock)
			{
				dictionary2[lcid] = value;
			}
			break;
		}
		if (-1 != lcid)
		{
			s_LcidCachedCultures = dictionary2;
		}
		s_NameCachedCultures = dictionary;
		return value;
	}

	public static CultureInfo GetCultureInfo(int culture)
	{
		if (culture <= 0)
		{
			throw new ArgumentOutOfRangeException("culture", SR.ArgumentOutOfRange_NeedPosNum);
		}
		CultureInfo cultureInfoHelper = GetCultureInfoHelper(culture, null, null);
		if (cultureInfoHelper == null)
		{
			throw new CultureNotFoundException("culture", culture, SR.Argument_CultureNotSupported);
		}
		return cultureInfoHelper;
	}

	public static CultureInfo GetCultureInfo(string name)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		CultureInfo cultureInfoHelper = GetCultureInfoHelper(0, name, null);
		if (cultureInfoHelper == null)
		{
			throw new CultureNotFoundException("name", name, SR.Argument_CultureNotSupported);
		}
		return cultureInfoHelper;
	}

	public static CultureInfo GetCultureInfo(string name, string altName)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		if (altName == null)
		{
			throw new ArgumentNullException("altName");
		}
		CultureInfo cultureInfoHelper = GetCultureInfoHelper(-1, name, altName);
		if (cultureInfoHelper == null)
		{
			throw new CultureNotFoundException("name or altName", SR.Format(SR.Argument_OneOfCulturesNotSupported, name, altName));
		}
		return cultureInfoHelper;
	}

	public static CultureInfo GetCultureInfoByIetfLanguageTag(string name)
	{
		if (name == "zh-CHT" || name == "zh-CHS")
		{
			throw new CultureNotFoundException("name", SR.Format(SR.Argument_CultureIetfNotSupported, name));
		}
		CultureInfo cultureInfo = GetCultureInfo(name);
		if (cultureInfo.LCID > 65535 || cultureInfo.LCID == 1034)
		{
			throw new CultureNotFoundException("name", SR.Format(SR.Argument_CultureIetfNotSupported, name));
		}
		return cultureInfo;
	}

	internal static CultureInfo GetUserDefaultCulture()
	{
		if (GlobalizationMode.Invariant)
		{
			return InvariantCulture;
		}
		string localeInfoEx = CultureData.GetLocaleInfoEx(null, 92u);
		if (localeInfoEx == null)
		{
			localeInfoEx = CultureData.GetLocaleInfoEx("!x-sys-default-locale", 92u);
			if (localeInfoEx == null)
			{
				return InvariantCulture;
			}
		}
		return GetCultureByName(localeInfoEx);
	}

	private unsafe static CultureInfo GetUserDefaultUICulture()
	{
		if (GlobalizationMode.Invariant)
		{
			return InvariantCulture;
		}
		uint num = 0u;
		uint num2 = 0u;
		if (Interop.Kernel32.GetUserPreferredUILanguages(8u, &num, null, &num2) != 0)
		{
			char[] array = new char[num2];
			fixed (char* pwszLanguagesBuffer = array)
			{
				if (Interop.Kernel32.GetUserPreferredUILanguages(8u, &num, pwszLanguagesBuffer, &num2) != 0)
				{
					int i;
					for (i = 0; array[i] != 0 && i < array.Length; i++)
					{
					}
					return GetCultureByName(new string(array, 0, i));
				}
			}
		}
		return InitializeUserDefaultCulture();
	}

	internal static CultureInfo GetCultureInfoForUserPreferredLanguageInAppX()
	{
		if (ts_IsDoingAppXCultureInfoLookup)
		{
			return null;
		}
		try
		{
			ts_IsDoingAppXCultureInfoLookup = true;
			// if (s_WindowsRuntimeResourceManager == null)
			// {
			// 	s_WindowsRuntimeResourceManager = ResourceManager.GetWinRTResourceManager();
			// }
			return s_WindowsRuntimeResourceManager.GlobalResourceContextBestFitCultureInfo;
		}
		finally
		{
			ts_IsDoingAppXCultureInfoLookup = false;
		}
	}

	internal static bool SetCultureInfoForUserPreferredLanguageInAppX(CultureInfo ci)
	{
		// if (s_WindowsRuntimeResourceManager == null)
		// {
		// 	s_WindowsRuntimeResourceManager = ResourceManager.GetWinRTResourceManager();
		// }
		return s_WindowsRuntimeResourceManager.SetGlobalResourceContextDefaultCulture(ci);
	}
}
*/

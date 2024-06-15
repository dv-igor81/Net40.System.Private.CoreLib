/*using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Globalization.Net40;

internal class CultureData
{
	private enum LocaleStringData : uint
	{
		LocalizedDisplayName = 2u,
		EnglishDisplayName = 114u,
		NativeDisplayName = 115u,
		LocalizedLanguageName = 111u,
		EnglishLanguageName = 4097u,
		NativeLanguageName = 4u,
		LocalizedCountryName = 6u,
		EnglishCountryName = 4098u,
		NativeCountryName = 8u,
		AbbreviatedWindowsLanguageName = 3u,
		ListSeparator = 12u,
		DecimalSeparator = 14u,
		ThousandSeparator = 15u,
		Digits = 19u,
		MonetarySymbol = 20u,
		CurrencyEnglishName = 4103u,
		CurrencyNativeName = 4104u,
		Iso4217MonetarySymbol = 21u,
		MonetaryDecimalSeparator = 22u,
		MonetaryThousandSeparator = 23u,
		AMDesignator = 40u,
		PMDesignator = 41u,
		PositiveSign = 80u,
		NegativeSign = 81u,
		Iso639LanguageTwoLetterName = 89u,
		Iso639LanguageThreeLetterName = 103u,
		Iso639LanguageName = 89u,
		Iso3166CountryName = 90u,
		Iso3166CountryName2 = 104u,
		NaNSymbol = 105u,
		PositiveInfinitySymbol = 106u,
		NegativeInfinitySymbol = 107u,
		ParentName = 109u,
		ConsoleFallbackName = 110u,
		PercentSymbol = 118u,
		PerMilleSymbol = 119u
	}

	private enum LocaleGroupingData : uint
	{
		Digit = 16u,
		Monetary = 24u
	}

	private enum LocaleNumberData : uint
	{
		LanguageId = 1u,
		GeoId = 91u,
		DigitSubstitution = 4116u,
		MeasurementSystem = 13u,
		FractionalDigitsCount = 17u,
		NegativeNumberFormat = 4112u,
		MonetaryFractionalDigitsCount = 25u,
		PositiveMonetaryNumberFormat = 27u,
		NegativeMonetaryNumberFormat = 28u,
		CalendarType = 4105u,
		FirstDayOfWeek = 4108u,
		FirstWeekOfYear = 4109u,
		ReadingLayout = 112u,
		NegativePercentFormat = 116u,
		PositivePercentFormat = 117u,
		OemCodePage = 11u,
		AnsiCodePage = 4100u,
		MacCodePage = 4113u,
		EbcdicCodePage = 4114u
	}

	private struct EnumLocaleData
	{
		public string regionName;

		public string cultureName;
	}

	private struct EnumData
	{
		public List<string> strings;
	}

	private string _sRealName;

	private string _sWindowsName;

	private string _sName;

	private string _sParent;

	private string _sLocalizedDisplayName;

	private string _sEnglishDisplayName;

	private string _sNativeDisplayName;

	private string _sSpecificCulture;

	private string _sISO639Language;

	private string _sISO639Language2;

	private string _sLocalizedLanguage;

	private string _sEnglishLanguage;

	private string _sNativeLanguage;

	private string _sAbbrevLang;

	private string _sConsoleFallbackName;

	private int _iInputLanguageHandle = -1;

	private string _sRegionName;

	private string _sLocalizedCountry;

	private string _sEnglishCountry;

	private string _sNativeCountry;

	private string _sISO3166CountryName;

	private string _sISO3166CountryName2;

	private int _iGeoId = -1;

	private string _sPositiveSign;

	private string _sNegativeSign;

	private int _iDigits;

	private int _iNegativeNumber;

	private int[] _waGrouping;

	private string _sDecimalSeparator;

	private string _sThousandSeparator;

	private string _sNaN;

	private string _sPositiveInfinity;

	private string _sNegativeInfinity;

	private int _iNegativePercent = -1;

	private int _iPositivePercent = -1;

	private string _sPercent;

	private string _sPerMille;

	private string _sCurrency;

	private string _sIntlMonetarySymbol;

	private string _sEnglishCurrency;

	private string _sNativeCurrency;

	private int _iCurrencyDigits;

	private int _iCurrency;

	private int _iNegativeCurrency;

	private int[] _waMonetaryGrouping;

	private string _sMonetaryDecimal;

	private string _sMonetaryThousand;

	private int _iMeasure = -1;

	private string _sListSeparator;

	private string _sAM1159;

	private string _sPM2359;

	private string _sTimeSeparator;

	private volatile string[] _saLongTimes;

	private volatile string[] _saShortTimes;

	private volatile string[] _saDurationFormats;

	private int _iFirstDayOfWeek = -1;

	private int _iFirstWeekOfYear = -1;

	private volatile CalendarId[] _waCalendars;

	private CalendarData[] _calendars;

	private int _iReadingLayout = -1;

	private int _iDefaultAnsiCodePage = -1;

	private int _iDefaultOemCodePage = -1;

	private int _iDefaultMacCodePage = -1;

	private int _iDefaultEbcdicCodePage = -1;

	private int _iLanguage;

	private bool _bUseOverrides;

	private bool _bNeutral;

	private static volatile Dictionary<string, CultureData> s_cachedRegions;

	private static volatile Dictionary<string, string> s_regionNames;

	private static volatile CultureData s_Invariant;

	private static volatile Dictionary<string, CultureData> s_cachedCultures;

	private static readonly object s_lock = new object();

	private static Dictionary<string, string> RegionNames
	{
		get
		{
			if (s_regionNames == null)
			{
				Dictionary<string, string> dictionary = new Dictionary<string, string>(211);
				dictionary.Add("029", "en-029");
				dictionary.Add("AE", "ar-AE");
				dictionary.Add("AF", "prs-AF");
				dictionary.Add("AL", "sq-AL");
				dictionary.Add("AM", "hy-AM");
				dictionary.Add("AR", "es-AR");
				dictionary.Add("AT", "de-AT");
				dictionary.Add("AU", "en-AU");
				dictionary.Add("AZ", "az-Cyrl-AZ");
				dictionary.Add("BA", "bs-Latn-BA");
				dictionary.Add("BD", "bn-BD");
				dictionary.Add("BE", "nl-BE");
				dictionary.Add("BG", "bg-BG");
				dictionary.Add("BH", "ar-BH");
				dictionary.Add("BN", "ms-BN");
				dictionary.Add("BO", "es-BO");
				dictionary.Add("BR", "pt-BR");
				dictionary.Add("BY", "be-BY");
				dictionary.Add("BZ", "en-BZ");
				dictionary.Add("CA", "en-CA");
				dictionary.Add("CH", "it-CH");
				dictionary.Add("CL", "es-CL");
				dictionary.Add("CN", "zh-CN");
				dictionary.Add("CO", "es-CO");
				dictionary.Add("CR", "es-CR");
				dictionary.Add("CS", "sr-Cyrl-CS");
				dictionary.Add("CZ", "cs-CZ");
				dictionary.Add("DE", "de-DE");
				dictionary.Add("DK", "da-DK");
				dictionary.Add("DO", "es-DO");
				dictionary.Add("DZ", "ar-DZ");
				dictionary.Add("EC", "es-EC");
				dictionary.Add("EE", "et-EE");
				dictionary.Add("EG", "ar-EG");
				dictionary.Add("ES", "es-ES");
				dictionary.Add("ET", "am-ET");
				dictionary.Add("FI", "fi-FI");
				dictionary.Add("FO", "fo-FO");
				dictionary.Add("FR", "fr-FR");
				dictionary.Add("GB", "en-GB");
				dictionary.Add("GE", "ka-GE");
				dictionary.Add("GL", "kl-GL");
				dictionary.Add("GR", "el-GR");
				dictionary.Add("GT", "es-GT");
				dictionary.Add("HK", "zh-HK");
				dictionary.Add("HN", "es-HN");
				dictionary.Add("HR", "hr-HR");
				dictionary.Add("HU", "hu-HU");
				dictionary.Add("ID", "id-ID");
				dictionary.Add("IE", "en-IE");
				dictionary.Add("IL", "he-IL");
				dictionary.Add("IN", "hi-IN");
				dictionary.Add("IQ", "ar-IQ");
				dictionary.Add("IR", "fa-IR");
				dictionary.Add("IS", "is-IS");
				dictionary.Add("IT", "it-IT");
				dictionary.Add("IV", "");
				dictionary.Add("JM", "en-JM");
				dictionary.Add("JO", "ar-JO");
				dictionary.Add("JP", "ja-JP");
				dictionary.Add("KE", "sw-KE");
				dictionary.Add("KG", "ky-KG");
				dictionary.Add("KH", "km-KH");
				dictionary.Add("KR", "ko-KR");
				dictionary.Add("KW", "ar-KW");
				dictionary.Add("KZ", "kk-KZ");
				dictionary.Add("LA", "lo-LA");
				dictionary.Add("LB", "ar-LB");
				dictionary.Add("LI", "de-LI");
				dictionary.Add("LK", "si-LK");
				dictionary.Add("LT", "lt-LT");
				dictionary.Add("LU", "lb-LU");
				dictionary.Add("LV", "lv-LV");
				dictionary.Add("LY", "ar-LY");
				dictionary.Add("MA", "ar-MA");
				dictionary.Add("MC", "fr-MC");
				dictionary.Add("ME", "sr-Latn-ME");
				dictionary.Add("MK", "mk-MK");
				dictionary.Add("MN", "mn-MN");
				dictionary.Add("MO", "zh-MO");
				dictionary.Add("MT", "mt-MT");
				dictionary.Add("MV", "dv-MV");
				dictionary.Add("MX", "es-MX");
				dictionary.Add("MY", "ms-MY");
				dictionary.Add("NG", "ig-NG");
				dictionary.Add("NI", "es-NI");
				dictionary.Add("NL", "nl-NL");
				dictionary.Add("NO", "nn-NO");
				dictionary.Add("NP", "ne-NP");
				dictionary.Add("NZ", "en-NZ");
				dictionary.Add("OM", "ar-OM");
				dictionary.Add("PA", "es-PA");
				dictionary.Add("PE", "es-PE");
				dictionary.Add("PH", "en-PH");
				dictionary.Add("PK", "ur-PK");
				dictionary.Add("PL", "pl-PL");
				dictionary.Add("PR", "es-PR");
				dictionary.Add("PT", "pt-PT");
				dictionary.Add("PY", "es-PY");
				dictionary.Add("QA", "ar-QA");
				dictionary.Add("RO", "ro-RO");
				dictionary.Add("RS", "sr-Latn-RS");
				dictionary.Add("RU", "ru-RU");
				dictionary.Add("RW", "rw-RW");
				dictionary.Add("SA", "ar-SA");
				dictionary.Add("SE", "sv-SE");
				dictionary.Add("SG", "zh-SG");
				dictionary.Add("SI", "sl-SI");
				dictionary.Add("SK", "sk-SK");
				dictionary.Add("SN", "wo-SN");
				dictionary.Add("SV", "es-SV");
				dictionary.Add("SY", "ar-SY");
				dictionary.Add("TH", "th-TH");
				dictionary.Add("TJ", "tg-Cyrl-TJ");
				dictionary.Add("TM", "tk-TM");
				dictionary.Add("TN", "ar-TN");
				dictionary.Add("TR", "tr-TR");
				dictionary.Add("TT", "en-TT");
				dictionary.Add("TW", "zh-TW");
				dictionary.Add("UA", "uk-UA");
				dictionary.Add("US", "en-US");
				dictionary.Add("UY", "es-UY");
				dictionary.Add("UZ", "uz-Cyrl-UZ");
				dictionary.Add("VE", "es-VE");
				dictionary.Add("VN", "vi-VN");
				dictionary.Add("YE", "ar-YE");
				dictionary.Add("ZA", "af-ZA");
				dictionary.Add("ZW", "en-ZW");
				s_regionNames = dictionary;
			}
			return s_regionNames;
		}
	}

	internal static CultureData Invariant
	{
		get
		{
			if (s_Invariant == null)
			{
				s_Invariant = CreateCultureWithInvariantData();
			}
			return s_Invariant;
		}
	}

	internal string CultureName
	{
		get
		{
			switch (_sName)
			{
			case "zh-CHS":
			case "zh-CHT":
				return _sName;
			default:
				return _sRealName;
			}
		}
	}

	internal bool UseUserOverride => _bUseOverrides;

	internal string Name => _sName ?? string.Empty;

	internal string ParentName
	{
		get
		{
			if (_sParent == null)
			{
				_sParent = GetLocaleInfo(_sRealName, LocaleStringData.ParentName);
			}
			return _sParent;
		}
	}

	internal string DisplayName
	{
		get
		{
			if (_sLocalizedDisplayName == null)
			{
				if (IsSupplementalCustomCulture)
				{
					if (IsNeutralCulture)
					{
						_sLocalizedDisplayName = NativeLanguageName;
					}
					else
					{
						_sLocalizedDisplayName = NativeName;
					}
				}
				else
				{
					try
					{
						if (Name.Equals("zh-CHT", StringComparison.OrdinalIgnoreCase))
						{
							_sLocalizedDisplayName = GetLanguageDisplayName("zh-Hant");
						}
						else if (Name.Equals("zh-CHS", StringComparison.OrdinalIgnoreCase))
						{
							_sLocalizedDisplayName = GetLanguageDisplayName("zh-Hans");
						}
						else
						{
							_sLocalizedDisplayName = GetLanguageDisplayName(Name);
						}
					}
					catch (Exception)
					{
					}
				}
				if (string.IsNullOrEmpty(_sLocalizedDisplayName))
				{
					CultureInfo userDefaultCulture;
					if (IsNeutralCulture)
					{
						_sLocalizedDisplayName = LocalizedLanguageName;
					}
					else if (CultureInfo.DefaultThreadCurrentUICulture != null && (userDefaultCulture = GetUserDefaultCulture()) != null && !CultureInfo.DefaultThreadCurrentUICulture.Name.Equals(userDefaultCulture.Name))
					{
						_sLocalizedDisplayName = NativeName;
					}
					else
					{
						_sLocalizedDisplayName = GetLocaleInfo(LocaleStringData.LocalizedDisplayName);
					}
				}
			}
			return _sLocalizedDisplayName;
		}
	}

	internal string EnglishName
	{
		get
		{
			if (_sEnglishDisplayName == null)
			{
				if (IsNeutralCulture)
				{
					_sEnglishDisplayName = EnglishLanguageName;
					switch (_sName)
					{
					case "zh-CHS":
					case "zh-CHT":
						_sEnglishDisplayName += " Legacy";
						break;
					}
				}
				else
				{
					_sEnglishDisplayName = GetLocaleInfo(LocaleStringData.EnglishDisplayName);
					if (string.IsNullOrEmpty(_sEnglishDisplayName))
					{
						if (EnglishLanguageName[EnglishLanguageName.Length - 1] == ')')
						{
							_sEnglishDisplayName = string.Concat(EnglishLanguageName.AsSpan(0, _sEnglishLanguage.Length - 1), ", ", EnglishCountryName, ")");
						}
						else
						{
							_sEnglishDisplayName = EnglishLanguageName + " (" + EnglishCountryName + ")";
						}
					}
				}
			}
			return _sEnglishDisplayName;
		}
	}

	internal string NativeName
	{
		get
		{
			if (_sNativeDisplayName == null)
			{
				if (IsNeutralCulture)
				{
					_sNativeDisplayName = NativeLanguageName;
					switch (_sName)
					{
					case "zh-CHS":
						_sNativeDisplayName += " 旧版";
						break;
					case "zh-CHT":
						_sNativeDisplayName += " 舊版";
						break;
					}
				}
				else
				{
					_sNativeDisplayName = GetLocaleInfo(LocaleStringData.NativeDisplayName);
					if (string.IsNullOrEmpty(_sNativeDisplayName))
					{
						_sNativeDisplayName = NativeLanguageName + " (" + NativeCountryName + ")";
					}
				}
			}
			return _sNativeDisplayName;
		}
	}

	internal string SpecificCultureName => _sSpecificCulture;

	internal string TwoLetterISOLanguageName
	{
		get
		{
			if (_sISO639Language == null)
			{
				_sISO639Language = GetLocaleInfo(LocaleStringData.Iso639LanguageTwoLetterName);
			}
			return _sISO639Language;
		}
	}

	internal string ThreeLetterISOLanguageName
	{
		get
		{
			if (_sISO639Language2 == null)
			{
				_sISO639Language2 = GetLocaleInfo(LocaleStringData.Iso639LanguageThreeLetterName);
			}
			return _sISO639Language2;
		}
	}

	internal string ThreeLetterWindowsLanguageName
	{
		get
		{
			if (_sAbbrevLang == null)
			{
				_sAbbrevLang = GetThreeLetterWindowsLanguageName(_sRealName);
			}
			return _sAbbrevLang;
		}
	}

	private string LocalizedLanguageName
	{
		get
		{
			if (_sLocalizedLanguage == null)
			{
				CultureInfo userDefaultCulture;
				if (CultureInfo.DefaultThreadCurrentUICulture != null && (userDefaultCulture = GetUserDefaultCulture()) != null && !CultureInfo.DefaultThreadCurrentUICulture.Name.Equals(userDefaultCulture.Name))
				{
					_sLocalizedLanguage = NativeLanguageName;
				}
				else
				{
					_sLocalizedLanguage = GetLocaleInfo(LocaleStringData.LocalizedLanguageName);
				}
			}
			return _sLocalizedLanguage;
		}
	}

	private string EnglishLanguageName
	{
		get
		{
			if (_sEnglishLanguage == null)
			{
				_sEnglishLanguage = GetLocaleInfo(LocaleStringData.EnglishLanguageName);
			}
			return _sEnglishLanguage;
		}
	}

	private string NativeLanguageName
	{
		get
		{
			if (_sNativeLanguage == null)
			{
				_sNativeLanguage = GetLocaleInfo(LocaleStringData.NativeLanguageName);
			}
			return _sNativeLanguage;
		}
	}

	internal string RegionName
	{
		get
		{
			if (_sRegionName == null)
			{
				_sRegionName = GetLocaleInfo(LocaleStringData.Iso3166CountryName);
			}
			return _sRegionName;
		}
	}

	internal int GeoId
	{
		get
		{
			if (_iGeoId == -1)
			{
				_iGeoId = GetGeoId(_sRealName);
			}
			return _iGeoId;
		}
	}

	internal string LocalizedCountryName
	{
		get
		{
			if (_sLocalizedCountry == null)
			{
				try
				{
					_sLocalizedCountry = GetRegionDisplayName(TwoLetterISOCountryName);
				}
				catch (Exception)
				{
				}
				if (_sLocalizedCountry == null)
				{
					_sLocalizedCountry = NativeCountryName;
				}
			}
			return _sLocalizedCountry;
		}
	}

	internal string EnglishCountryName
	{
		get
		{
			if (_sEnglishCountry == null)
			{
				_sEnglishCountry = GetLocaleInfo(LocaleStringData.EnglishCountryName);
			}
			return _sEnglishCountry;
		}
	}

	internal string NativeCountryName
	{
		get
		{
			if (_sNativeCountry == null)
			{
				_sNativeCountry = GetLocaleInfo(LocaleStringData.NativeCountryName);
			}
			return _sNativeCountry;
		}
	}

	internal string TwoLetterISOCountryName
	{
		get
		{
			if (_sISO3166CountryName == null)
			{
				_sISO3166CountryName = GetLocaleInfo(LocaleStringData.Iso3166CountryName);
			}
			return _sISO3166CountryName;
		}
	}

	internal string ThreeLetterISOCountryName
	{
		get
		{
			if (_sISO3166CountryName2 == null)
			{
				_sISO3166CountryName2 = GetLocaleInfo(LocaleStringData.Iso3166CountryName2);
			}
			return _sISO3166CountryName2;
		}
	}

	internal int KeyboardLayoutId
	{
		get
		{
			if (_iInputLanguageHandle == -1)
			{
				if (IsSupplementalCustomCulture)
				{
					_iInputLanguageHandle = 1033;
				}
				else
				{
					_iInputLanguageHandle = LCID;
				}
			}
			return _iInputLanguageHandle;
		}
	}

	internal string SCONSOLEFALLBACKNAME
	{
		get
		{
			if (_sConsoleFallbackName == null)
			{
				_sConsoleFallbackName = GetConsoleFallbackName(_sRealName);
			}
			return _sConsoleFallbackName;
		}
	}

	internal int[] NumberGroupSizes
	{
		get
		{
			if (_waGrouping == null)
			{
				_waGrouping = GetLocaleInfo(LocaleGroupingData.Digit);
			}
			return _waGrouping;
		}
	}

	private string NaNSymbol
	{
		get
		{
			if (_sNaN == null)
			{
				_sNaN = GetLocaleInfo(LocaleStringData.NaNSymbol);
			}
			return _sNaN;
		}
	}

	private string PositiveInfinitySymbol
	{
		get
		{
			if (_sPositiveInfinity == null)
			{
				_sPositiveInfinity = GetLocaleInfo(LocaleStringData.PositiveInfinitySymbol);
			}
			return _sPositiveInfinity;
		}
	}

	private string NegativeInfinitySymbol
	{
		get
		{
			if (_sNegativeInfinity == null)
			{
				_sNegativeInfinity = GetLocaleInfo(LocaleStringData.NegativeInfinitySymbol);
			}
			return _sNegativeInfinity;
		}
	}

	private int PercentNegativePattern
	{
		get
		{
			if (_iNegativePercent == -1)
			{
				_iNegativePercent = GetLocaleInfo(LocaleNumberData.NegativePercentFormat);
			}
			return _iNegativePercent;
		}
	}

	private int PercentPositivePattern
	{
		get
		{
			if (_iPositivePercent == -1)
			{
				_iPositivePercent = GetLocaleInfo(LocaleNumberData.PositivePercentFormat);
			}
			return _iPositivePercent;
		}
	}

	private string PercentSymbol
	{
		get
		{
			if (_sPercent == null)
			{
				_sPercent = GetLocaleInfo(LocaleStringData.PercentSymbol);
			}
			return _sPercent;
		}
	}

	private string PerMilleSymbol
	{
		get
		{
			if (_sPerMille == null)
			{
				_sPerMille = GetLocaleInfo(LocaleStringData.PerMilleSymbol);
			}
			return _sPerMille;
		}
	}

	internal string CurrencySymbol
	{
		get
		{
			if (_sCurrency == null)
			{
				_sCurrency = GetLocaleInfo(LocaleStringData.MonetarySymbol);
			}
			return _sCurrency;
		}
	}

	internal string ISOCurrencySymbol
	{
		get
		{
			if (_sIntlMonetarySymbol == null)
			{
				_sIntlMonetarySymbol = GetLocaleInfo(LocaleStringData.Iso4217MonetarySymbol);
			}
			return _sIntlMonetarySymbol;
		}
	}

	internal string CurrencyEnglishName
	{
		get
		{
			if (_sEnglishCurrency == null)
			{
				_sEnglishCurrency = GetLocaleInfo(LocaleStringData.CurrencyEnglishName);
			}
			return _sEnglishCurrency;
		}
	}

	internal string CurrencyNativeName
	{
		get
		{
			if (_sNativeCurrency == null)
			{
				_sNativeCurrency = GetLocaleInfo(LocaleStringData.CurrencyNativeName);
			}
			return _sNativeCurrency;
		}
	}

	internal int[] CurrencyGroupSizes
	{
		get
		{
			if (_waMonetaryGrouping == null)
			{
				_waMonetaryGrouping = GetLocaleInfo(LocaleGroupingData.Monetary);
			}
			return _waMonetaryGrouping;
		}
	}

	internal int MeasurementSystem
	{
		get
		{
			if (_iMeasure == -1)
			{
				_iMeasure = GetLocaleInfo(LocaleNumberData.MeasurementSystem);
			}
			return _iMeasure;
		}
	}

	internal string ListSeparator
	{
		get
		{
			if (_sListSeparator == null)
			{
				_sListSeparator = GetLocaleInfo(LocaleStringData.ListSeparator);
			}
			return _sListSeparator;
		}
	}

	internal string AMDesignator
	{
		get
		{
			if (_sAM1159 == null)
			{
				_sAM1159 = GetLocaleInfo(LocaleStringData.AMDesignator);
			}
			return _sAM1159;
		}
	}

	internal string PMDesignator
	{
		get
		{
			if (_sPM2359 == null)
			{
				_sPM2359 = GetLocaleInfo(LocaleStringData.PMDesignator);
			}
			return _sPM2359;
		}
	}

	internal string[] LongTimes
	{
		get
		{
			if (_saLongTimes == null)
			{
				string[] timeFormats = GetTimeFormats();
				if (timeFormats == null || timeFormats.Length == 0)
				{
					_saLongTimes = Invariant._saLongTimes;
				}
				else
				{
					_saLongTimes = timeFormats;
				}
			}
			return _saLongTimes;
		}
	}

	internal string[] ShortTimes
	{
		get
		{
			if (_saShortTimes == null)
			{
				string[] array = GetShortTimeFormats();
				if (array == null || array.Length == 0)
				{
					array = DeriveShortTimesFromLong();
				}
				array = AdjustShortTimesForMac(array);
				_saShortTimes = array;
			}
			return _saShortTimes;
		}
	}

	internal int FirstDayOfWeek
	{
		get
		{
			if (_iFirstDayOfWeek == -1)
			{
				_iFirstDayOfWeek = GetFirstDayOfWeek();
			}
			return _iFirstDayOfWeek;
		}
	}

	internal int CalendarWeekRule
	{
		get
		{
			if (_iFirstWeekOfYear == -1)
			{
				_iFirstWeekOfYear = GetLocaleInfo(LocaleNumberData.FirstWeekOfYear);
			}
			return _iFirstWeekOfYear;
		}
	}

	internal CalendarId[] CalendarIds
	{
		get
		{
			if (_waCalendars == null)
			{
				CalendarId[] array = new CalendarId[23];
				int num = CalendarData.GetCalendars(_sWindowsName, _bUseOverrides, array);
				if (num == 0)
				{
					_waCalendars = Invariant._waCalendars;
				}
				else
				{
					if (_sWindowsName == "zh-TW")
					{
						bool flag = false;
						for (int i = 0; i < num; i++)
						{
							if (array[i] == CalendarId.TAIWAN)
							{
								flag = true;
								break;
							}
						}
						if (!flag)
						{
							num++;
							Array.Copy(array, 1, array, 2, 21);
							array[1] = CalendarId.TAIWAN;
						}
					}
					CalendarId[] array2 = new CalendarId[num];
					Array.Copy(array, 0, array2, 0, num);
					if (array2.Length > 1)
					{
						CalendarId calendarId = (CalendarId)GetLocaleInfo(LocaleNumberData.CalendarType);
						if (array2[1] == calendarId)
						{
							array2[1] = array2[0];
							array2[0] = calendarId;
						}
					}
					_waCalendars = array2;
				}
			}
			return _waCalendars;
		}
	}

	internal bool IsRightToLeft => ReadingLayout == 1;

	private int ReadingLayout
	{
		get
		{
			if (_iReadingLayout == -1)
			{
				_iReadingLayout = GetLocaleInfo(LocaleNumberData.ReadingLayout);
			}
			return _iReadingLayout;
		}
	}

	internal string TextInfoName => _sRealName;

	internal string SortName => _sRealName;

	internal bool IsSupplementalCustomCulture => IsCustomCultureId(LCID);

	internal int ANSICodePage
	{
		get
		{
			if (_iDefaultAnsiCodePage == -1)
			{
				_iDefaultAnsiCodePage = GetAnsiCodePage(_sRealName);
			}
			return _iDefaultAnsiCodePage;
		}
	}

	internal int OEMCodePage
	{
		get
		{
			if (_iDefaultOemCodePage == -1)
			{
				_iDefaultOemCodePage = GetOemCodePage(_sRealName);
			}
			return _iDefaultOemCodePage;
		}
	}

	internal int MacCodePage
	{
		get
		{
			if (_iDefaultMacCodePage == -1)
			{
				_iDefaultMacCodePage = GetMacCodePage(_sRealName);
			}
			return _iDefaultMacCodePage;
		}
	}

	internal int EBCDICCodePage
	{
		get
		{
			if (_iDefaultEbcdicCodePage == -1)
			{
				_iDefaultEbcdicCodePage = GetEbcdicCodePage(_sRealName);
			}
			return _iDefaultEbcdicCodePage;
		}
	}

	internal int LCID
	{
		get
		{
			if (_iLanguage == 0)
			{
				_iLanguage = LocaleNameToLCID(_sRealName);
			}
			return _iLanguage;
		}
	}

	internal bool IsNeutralCulture => _bNeutral;

	internal bool IsInvariantCulture => string.IsNullOrEmpty(Name);

	internal Calendar DefaultCalendar
	{
		get
		{
			if (GlobalizationMode.Invariant)
			{
				return CultureInfo.GetCalendarInstance(CalendarIds[0]);
			}
			CalendarId calendarId = (CalendarId)GetLocaleInfo(LocaleNumberData.CalendarType);
			if (calendarId == CalendarId.UNINITIALIZED_VALUE)
			{
				calendarId = CalendarIds[0];
			}
			return CultureInfo.GetCalendarInstance(calendarId);
		}
	}

	internal string TimeSeparator
	{
		get
		{
			if (_sTimeSeparator == null)
			{
				string text = GetTimeFormatString();
				if (string.IsNullOrEmpty(text))
				{
					text = LongTimes[0];
				}
				_sTimeSeparator = GetTimeSeparator(text);
			}
			return _sTimeSeparator;
		}
	}

	internal bool IsFramework => false;

	internal bool IsWin32Installed => true;

	internal unsafe bool IsReplacementCulture
	{
		get
		{
			EnumData value = default(EnumData);
			value.strings = new List<string>();
			Interop.Kernel32.EnumSystemLocalesEx(EnumAllSystemLocalesProc, 8u, Unsafe.AsPointer(ref value), IntPtr.Zero);
			for (int i = 0; i < value.strings.Count; i++)
			{
				if (string.Compare(value.strings[i], _sWindowsName, StringComparison.OrdinalIgnoreCase) == 0)
				{
					return true;
				}
			}
			return false;
		}
	}

	internal static CultureData GetCultureDataForRegion(string cultureName, bool useUserOverride)
	{
		if (string.IsNullOrEmpty(cultureName))
		{
			return Invariant;
		}
		CultureData value = GetCultureData(cultureName, useUserOverride);
		if (value != null && !value.IsNeutralCulture)
		{
			return value;
		}
		CultureData cultureData = value;
		string key = AnsiToLower(useUserOverride ? cultureName : (cultureName + "*"));
		Dictionary<string, CultureData> dictionary = s_cachedRegions;
		if (dictionary == null)
		{
			dictionary = new Dictionary<string, CultureData>();
		}
		else
		{
			lock (s_lock)
			{
				dictionary.TryGetValue(key, out value);
			}
			if (value != null)
			{
				return value;
			}
		}
		if ((value == null || value.IsNeutralCulture) && RegionNames.TryGetValue(cultureName, out var value2))
		{
			value = GetCultureData(value2, useUserOverride);
		}
		if (!GlobalizationMode.Invariant && (value == null || value.IsNeutralCulture))
		{
			value = GetCultureDataFromRegionName(cultureName);
		}
		if (value != null && !value.IsNeutralCulture)
		{
			lock (s_lock)
			{
				dictionary[key] = value;
			}
			s_cachedRegions = dictionary;
		}
		else
		{
			value = cultureData;
		}
		return value;
	}

	internal static void ClearCachedData()
	{
		s_cachedCultures = null;
		s_cachedRegions = null;
	}

	internal static CultureInfo[] GetCultures(CultureTypes types)
	{
		if (types <= (CultureTypes)0 || ((uint)types & 0xFFFFFF80u) != 0)
		{
			throw new ArgumentOutOfRangeException("types", SR.Format(SR.ArgumentOutOfRange_Range, CultureTypes.NeutralCultures, CultureTypes.FrameworkCultures));
		}
		if ((types & CultureTypes.WindowsOnlyCultures) != 0)
		{
			types &= ~CultureTypes.WindowsOnlyCultures;
		}
		if (GlobalizationMode.Invariant)
		{
			return new CultureInfo[1]
			{
				new CultureInfo("")
			};
		}
		return EnumCultures(types);
	}

	private static CultureData CreateCultureWithInvariantData()
	{
		CultureData cultureData = new CultureData();
		cultureData._bUseOverrides = false;
		cultureData._sRealName = "";
		cultureData._sWindowsName = "";
		cultureData._sName = "";
		cultureData._sParent = "";
		cultureData._bNeutral = false;
		cultureData._sEnglishDisplayName = "Invariant Language (Invariant Country)";
		cultureData._sNativeDisplayName = "Invariant Language (Invariant Country)";
		cultureData._sSpecificCulture = "";
		cultureData._sISO639Language = "iv";
		cultureData._sISO639Language2 = "ivl";
		cultureData._sLocalizedLanguage = "Invariant Language";
		cultureData._sEnglishLanguage = "Invariant Language";
		cultureData._sNativeLanguage = "Invariant Language";
		cultureData._sAbbrevLang = "IVL";
		cultureData._sConsoleFallbackName = "";
		cultureData._iInputLanguageHandle = 127;
		cultureData._sRegionName = "IV";
		cultureData._sEnglishCountry = "Invariant Country";
		cultureData._sNativeCountry = "Invariant Country";
		cultureData._sISO3166CountryName = "IV";
		cultureData._sISO3166CountryName2 = "ivc";
		cultureData._iGeoId = 244;
		cultureData._sPositiveSign = "+";
		cultureData._sNegativeSign = "-";
		cultureData._iDigits = 2;
		cultureData._iNegativeNumber = 1;
		cultureData._waGrouping = new int[1] { 3 };
		cultureData._sDecimalSeparator = ".";
		cultureData._sThousandSeparator = ",";
		cultureData._sNaN = "NaN";
		cultureData._sPositiveInfinity = "Infinity";
		cultureData._sNegativeInfinity = "-Infinity";
		cultureData._iNegativePercent = 0;
		cultureData._iPositivePercent = 0;
		cultureData._sPercent = "%";
		cultureData._sPerMille = "‰";
		cultureData._sCurrency = "¤";
		cultureData._sIntlMonetarySymbol = "XDR";
		cultureData._sEnglishCurrency = "International Monetary Fund";
		cultureData._sNativeCurrency = "International Monetary Fund";
		cultureData._iCurrencyDigits = 2;
		cultureData._iCurrency = 0;
		cultureData._iNegativeCurrency = 0;
		cultureData._waMonetaryGrouping = new int[1] { 3 };
		cultureData._sMonetaryDecimal = ".";
		cultureData._sMonetaryThousand = ",";
		cultureData._iMeasure = 0;
		cultureData._sListSeparator = ",";
		cultureData._sTimeSeparator = ":";
		cultureData._sAM1159 = "AM";
		cultureData._sPM2359 = "PM";
		cultureData._saLongTimes = new string[1] { "HH:mm:ss" };
		cultureData._saShortTimes = new string[4] { "HH:mm", "hh:mm tt", "H:mm", "h:mm tt" };
		cultureData._saDurationFormats = new string[1] { "HH:mm:ss" };
		cultureData._iFirstDayOfWeek = 0;
		cultureData._iFirstWeekOfYear = 0;
		cultureData._waCalendars = new CalendarId[1] { CalendarId.GREGORIAN };
		cultureData._calendars = new CalendarData[23];
		cultureData._calendars[0] = CalendarData.Invariant;
		cultureData._iReadingLayout = 0;
		cultureData._iLanguage = 127;
		cultureData._iDefaultAnsiCodePage = 1252;
		cultureData._iDefaultOemCodePage = 437;
		cultureData._iDefaultMacCodePage = 10000;
		cultureData._iDefaultEbcdicCodePage = 37;
		if (GlobalizationMode.Invariant)
		{
			cultureData._sLocalizedDisplayName = cultureData._sNativeDisplayName;
			cultureData._sLocalizedCountry = cultureData._sNativeCountry;
		}
		return cultureData;
	}

	internal static CultureData GetCultureData(string cultureName, bool useUserOverride)
	{
		if (string.IsNullOrEmpty(cultureName))
		{
			return Invariant;
		}
		string key = AnsiToLower(useUserOverride ? cultureName : (cultureName + "*"));
		Dictionary<string, CultureData> dictionary = s_cachedCultures;
		if (dictionary == null)
		{
			dictionary = new Dictionary<string, CultureData>();
		}
		else
		{
			bool flag;
			CultureData value;
			lock (s_lock)
			{
				flag = dictionary.TryGetValue(key, out value);
			}
			if (flag && value != null)
			{
				return value;
			}
		}
		CultureData cultureData = CreateCultureData(cultureName, useUserOverride);
		if (cultureData == null)
		{
			return null;
		}
		lock (s_lock)
		{
			dictionary[key] = cultureData;
		}
		s_cachedCultures = dictionary;
		return cultureData;
	}

	private static string NormalizeCultureName(string name, out bool isNeutralName)
	{
		isNeutralName = true;
		int i = 0;
		if (name.Length > 85)
		{
			throw new ArgumentException(SR.Format(SR.Argument_InvalidId, "name"));
		}
		Span<char> span = stackalloc char[name.Length];
		bool flag = false;
		for (; i < name.Length && name[i] != '-' && name[i] != '_'; i++)
		{
			if (name[i] >= 'A' && name[i] <= 'Z')
			{
				span[i] = (char)(name[i] + 32);
				flag = true;
			}
			else
			{
				span[i] = name[i];
			}
		}
		if (i < name.Length)
		{
			isNeutralName = false;
		}
		for (; i < name.Length; i++)
		{
			if (name[i] >= 'a' && name[i] <= 'z')
			{
				span[i] = (char)(name[i] - 32);
				flag = true;
			}
			else
			{
				span[i] = name[i];
			}
		}
		if (flag)
		{
			return new string(span);
		}
		return name;
	}

	private static CultureData CreateCultureData(string cultureName, bool useUserOverride)
	{
		if (GlobalizationMode.Invariant)
		{
			if (cultureName.Length > 85 || !CultureInfo.VerifyCultureName(cultureName, throwException: false))
			{
				return null;
			}
			CultureData cultureData = CreateCultureWithInvariantData();
			cultureData._bUseOverrides = useUserOverride;
			cultureData._sName = NormalizeCultureName(cultureName, out cultureData._bNeutral);
			cultureData._sRealName = cultureData._sName;
			cultureData._sWindowsName = cultureData._sName;
			cultureData._iLanguage = 4096;
			return cultureData;
		}
		if (cultureName.Length == 1 && (cultureName[0] == 'C' || cultureName[0] == 'c'))
		{
			return Invariant;
		}
		CultureData cultureData2 = new CultureData();
		cultureData2._bUseOverrides = useUserOverride;
		cultureData2._sRealName = cultureName;
		if (!cultureData2.InitCultureData() && !cultureData2.InitCompatibilityCultureData())
		{
			return null;
		}
		return cultureData2;
	}

	private bool InitCompatibilityCultureData()
	{
		string sRealName = _sRealName;
		string text;
		string sName;
		switch (AnsiToLower(sRealName))
		{
		case "zh-chs":
			text = "zh-Hans";
			sName = "zh-CHS";
			break;
		case "zh-cht":
			text = "zh-Hant";
			sName = "zh-CHT";
			break;
		default:
			return false;
		}
		_sRealName = text;
		if (!InitCultureData())
		{
			return false;
		}
		_sName = sName;
		_sParent = text;
		return true;
	}

	internal static CultureData GetCultureData(int culture, bool bUseUserOverride)
	{
		string text = null;
		CultureData cultureData = null;
		if (culture == 127)
		{
			return Invariant;
		}
		if (GlobalizationMode.Invariant)
		{
			throw new CultureNotFoundException("culture", culture, SR.Argument_CultureNotSupported);
		}
		text = LCIDToLocaleName(culture);
		if (!string.IsNullOrEmpty(text))
		{
			cultureData = GetCultureData(text, bUseUserOverride);
		}
		if (cultureData == null)
		{
			throw new CultureNotFoundException("culture", culture, SR.Argument_CultureNotSupported);
		}
		return cultureData;
	}

	private string[] AdjustShortTimesForMac(string[] shortTimes)
	{
		return shortTimes;
	}

	private string[] DeriveShortTimesFromLong()
	{
		string[] longTimes = LongTimes;
		string[] array = new string[longTimes.Length];
		for (int i = 0; i < longTimes.Length; i++)
		{
			array[i] = StripSecondsFromPattern(longTimes[i]);
		}
		return array;
	}

	private static string StripSecondsFromPattern(string time)
	{
		bool flag = false;
		int num = -1;
		for (int i = 0; i < time.Length; i++)
		{
			if (time[i] == '\'')
			{
				flag = !flag;
			}
			else if (time[i] == '\\')
			{
				i++;
			}
			else
			{
				if (flag)
				{
					continue;
				}
				switch (time[i])
				{
				case 's':
				{
					if (i - num <= 4 && i - num > 1 && time[num + 1] != '\'' && time[i - 1] != '\'' && num >= 0)
					{
						i = num + 1;
					}
					bool containsSpace;
					int indexOfNextTokenAfterSeconds = GetIndexOfNextTokenAfterSeconds(time, i, out containsSpace);
					time = string.Concat(str1: (!containsSpace) ? "" : " ", str0: time.AsSpan(0, i), str2: time.AsSpan(indexOfNextTokenAfterSeconds));
					break;
				}
				case 'H':
				case 'h':
				case 'm':
					num = i;
					break;
				}
			}
		}
		return time;
	}

	private static int GetIndexOfNextTokenAfterSeconds(string time, int index, out bool containsSpace)
	{
		bool flag = false;
		containsSpace = false;
		while (index < time.Length)
		{
			switch (time[index])
			{
			case '\'':
				flag = !flag;
				break;
			case '\\':
				index++;
				if (time[index] == ' ')
				{
					containsSpace = true;
				}
				break;
			case ' ':
				containsSpace = true;
				break;
			case 'H':
			case 'h':
			case 'm':
			case 't':
				if (!flag)
				{
					return index;
				}
				break;
			}
			index++;
		}
		containsSpace = false;
		return index;
	}

	internal string[] ShortDates(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saShortDates;
	}

	internal string[] LongDates(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saLongDates;
	}

	internal string[] YearMonths(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saYearMonths;
	}

	internal string[] DayNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saDayNames;
	}

	internal string[] AbbreviatedDayNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saAbbrevDayNames;
	}

	internal string[] SuperShortDayNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saSuperShortDayNames;
	}

	internal string[] MonthNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saMonthNames;
	}

	internal string[] GenitiveMonthNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saMonthGenitiveNames;
	}

	internal string[] AbbreviatedMonthNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saAbbrevMonthNames;
	}

	internal string[] AbbreviatedGenitiveMonthNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saAbbrevMonthGenitiveNames;
	}

	internal string[] LeapYearMonthNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saLeapYearMonthNames;
	}

	internal string MonthDay(CalendarId calendarId)
	{
		return GetCalendar(calendarId).sMonthDay;
	}

	internal string CalendarName(CalendarId calendarId)
	{
		return GetCalendar(calendarId).sNativeName;
	}

	internal CalendarData GetCalendar(CalendarId calendarId)
	{
		int num = (int)(calendarId - 1);
		if (_calendars == null)
		{
			_calendars = new CalendarData[23];
		}
		CalendarData calendarData = _calendars[num];
		if (calendarData == null)
		{
			calendarData = new CalendarData(_sWindowsName, calendarId, UseUserOverride);
			_calendars[num] = calendarData;
		}
		return calendarData;
	}

	internal string[] EraNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saEraNames;
	}

	internal string[] AbbrevEraNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saAbbrevEraNames;
	}

	internal string[] AbbreviatedEnglishEraNames(CalendarId calendarId)
	{
		return GetCalendar(calendarId).saAbbrevEnglishEraNames;
	}

	internal string DateSeparator(CalendarId calendarId)
	{
		if (calendarId == CalendarId.JAPAN && !LocalAppContextSwitches.EnforceLegacyJapaneseDateParsing)
		{
			return "/";
		}
		return GetDateSeparator(ShortDates(calendarId)[0]);
	}

	private static string UnescapeNlsString(string str, int start, int end)
	{
		StringBuilder stringBuilder = null;
		for (int i = start; i < str.Length && i <= end; i++)
		{
			switch (str[i])
			{
			case '\'':
				if (stringBuilder == null)
				{
					stringBuilder = new StringBuilder(str, start, i - start, str.Length);
				}
				break;
			case '\\':
				if (stringBuilder == null)
				{
					stringBuilder = new StringBuilder(str, start, i - start, str.Length);
				}
				i++;
				if (i < str.Length)
				{
					stringBuilder.Append(str[i]);
				}
				break;
			default:
				stringBuilder?.Append(str[i]);
				break;
			}
		}
		if (stringBuilder == null)
		{
			return str.Substring(start, end - start + 1);
		}
		return stringBuilder.ToString();
	}

	private static string GetTimeSeparator(string format)
	{
		return GetSeparator(format, "Hhms");
	}

	private static string GetDateSeparator(string format)
	{
		return GetSeparator(format, "dyM");
	}

	private static string GetSeparator(string format, string timeParts)
	{
		int num = IndexOfTimePart(format, 0, timeParts);
		if (num != -1)
		{
			char c = format[num];
			do
			{
				num++;
			}
			while (num < format.Length && format[num] == c);
			int num2 = num;
			if (num2 < format.Length)
			{
				int num3 = IndexOfTimePart(format, num2, timeParts);
				if (num3 != -1)
				{
					return UnescapeNlsString(format, num2, num3 - 1);
				}
			}
		}
		return string.Empty;
	}

	private static int IndexOfTimePart(string format, int startIndex, string timeParts)
	{
		bool flag = false;
		for (int i = startIndex; i < format.Length; i++)
		{
			if (!flag && timeParts.Contains(format[i]))
			{
				return i;
			}
			switch (format[i])
			{
			case '\\':
				if (i + 1 < format.Length)
				{
					i++;
					char c = format[i];
					if (c != '\'' && c != '\\')
					{
						i--;
					}
				}
				break;
			case '\'':
				flag = !flag;
				break;
			}
		}
		return -1;
	}

	internal static bool IsCustomCultureId(int cultureId)
	{
		if (cultureId != 3072)
		{
			return cultureId == 4096;
		}
		return true;
	}

	internal void GetNFIValues(NumberFormatInfo nfi)
	{
		if (GlobalizationMode.Invariant || IsInvariantCulture)
		{
			nfi._positiveSign = _sPositiveSign;
			nfi._negativeSign = _sNegativeSign;
			nfi._numberGroupSeparator = _sThousandSeparator;
			nfi._numberDecimalSeparator = _sDecimalSeparator;
			nfi._numberDecimalDigits = _iDigits;
			nfi._numberNegativePattern = _iNegativeNumber;
			nfi._currencySymbol = _sCurrency;
			nfi._currencyGroupSeparator = _sMonetaryThousand;
			nfi._currencyDecimalSeparator = _sMonetaryDecimal;
			nfi._currencyDecimalDigits = _iCurrencyDigits;
			nfi._currencyNegativePattern = _iNegativeCurrency;
			nfi._currencyPositivePattern = _iCurrency;
		}
		else
		{
			nfi._positiveSign = GetLocaleInfo(LocaleStringData.PositiveSign);
			nfi._negativeSign = GetLocaleInfo(LocaleStringData.NegativeSign);
			nfi._numberDecimalSeparator = GetLocaleInfo(LocaleStringData.DecimalSeparator);
			nfi._numberGroupSeparator = GetLocaleInfo(LocaleStringData.ThousandSeparator);
			nfi._currencyGroupSeparator = GetLocaleInfo(LocaleStringData.MonetaryThousandSeparator);
			nfi._currencyDecimalSeparator = GetLocaleInfo(LocaleStringData.MonetaryDecimalSeparator);
			nfi._currencySymbol = GetLocaleInfo(LocaleStringData.MonetarySymbol);
			nfi._numberDecimalDigits = GetLocaleInfo(LocaleNumberData.FractionalDigitsCount);
			nfi._currencyDecimalDigits = GetLocaleInfo(LocaleNumberData.MonetaryFractionalDigitsCount);
			nfi._currencyPositivePattern = GetLocaleInfo(LocaleNumberData.PositiveMonetaryNumberFormat);
			nfi._currencyNegativePattern = GetLocaleInfo(LocaleNumberData.NegativeMonetaryNumberFormat);
			nfi._numberNegativePattern = GetLocaleInfo(LocaleNumberData.NegativeNumberFormat);
			string localeInfo = GetLocaleInfo(LocaleStringData.Digits);
			nfi._nativeDigits = new string[10];
			for (int i = 0; i < nfi._nativeDigits.Length; i++)
			{
				nfi._nativeDigits[i] = char.ToString(localeInfo[i]);
			}
			nfi._digitSubstitution = GetDigitSubstitution(_sRealName);
		}
		nfi._numberGroupSizes = NumberGroupSizes;
		nfi._currencyGroupSizes = CurrencyGroupSizes;
		nfi._percentNegativePattern = PercentNegativePattern;
		nfi._percentPositivePattern = PercentPositivePattern;
		nfi._percentSymbol = PercentSymbol;
		nfi._perMilleSymbol = PerMilleSymbol;
		nfi._negativeInfinitySymbol = NegativeInfinitySymbol;
		nfi._positiveInfinitySymbol = PositiveInfinitySymbol;
		nfi._nanSymbol = NaNSymbol;
		nfi._percentDecimalDigits = nfi._numberDecimalDigits;
		nfi._percentDecimalSeparator = nfi._numberDecimalSeparator;
		nfi._percentGroupSizes = nfi._numberGroupSizes;
		nfi._percentGroupSeparator = nfi._numberGroupSeparator;
		if (nfi._positiveSign == null || nfi._positiveSign.Length == 0)
		{
			nfi._positiveSign = "+";
		}
		if (nfi._currencyDecimalSeparator == null || nfi._currencyDecimalSeparator.Length == 0)
		{
			nfi._currencyDecimalSeparator = nfi._numberDecimalSeparator;
		}
	}

	internal static string AnsiToLower(string testString)
	{
		return TextInfo.ToLowerAsciiInvariant(testString);
	}

	private unsafe bool InitCultureData()
	{
		string sRealName = _sRealName;
		char* ptr = stackalloc char[85];
		int localeInfoEx = GetLocaleInfoEx(sRealName, 92u, ptr, 85);
		if (localeInfoEx == 0)
		{
			return false;
		}
		_sRealName = new string(ptr, 0, localeInfoEx - 1);
		sRealName = _sRealName;
		if (GetLocaleInfoEx(sRealName, 536871025u, ptr, 2) == 0)
		{
			return false;
		}
		_bNeutral = *(uint*)ptr != 0;
		_sWindowsName = sRealName;
		if (_bNeutral)
		{
			_sName = sRealName;
			localeInfoEx = Interop.Kernel32.ResolveLocaleName(sRealName, ptr, 85);
			if (localeInfoEx < 1)
			{
				return false;
			}
			_sSpecificCulture = new string(ptr, 0, localeInfoEx - 1);
		}
		else
		{
			_sSpecificCulture = sRealName;
			_sName = sRealName;
			if (GetLocaleInfoEx(sRealName, 536870913u, ptr, 2) == 0)
			{
				return false;
			}
			_iLanguage = *(int*)ptr;
			if (!IsCustomCultureId(_iLanguage))
			{
				int num = sRealName.IndexOf('_');
				if (num > 0 && num < sRealName.Length)
				{
					_sName = sRealName.Substring(0, num);
				}
			}
		}
		return true;
	}

	internal static unsafe string GetLocaleInfoEx(string localeName, uint field)
	{
		char* ptr = stackalloc char[530];
		int localeInfoEx = GetLocaleInfoEx(localeName, field, ptr, 530);
		if (localeInfoEx > 0)
		{
			return new string(ptr);
		}
		return null;
	}

	internal static unsafe int GetLocaleInfoExInt(string localeName, uint field)
	{
		field |= 0x20000000u;
		int result = 0;
		GetLocaleInfoEx(localeName, field, (char*)(&result), 4);
		return result;
	}

	internal static unsafe int GetLocaleInfoEx(string lpLocaleName, uint lcType, char* lpLCData, int cchData)
	{
		return Interop.Kernel32.GetLocaleInfoEx(lpLocaleName, lcType, lpLCData, cchData);
	}

	private string GetLocaleInfo(LocaleStringData type)
	{
		return GetLocaleInfo(_sWindowsName, type);
	}

	private string GetLocaleInfo(string localeName, LocaleStringData type)
	{
		return GetLocaleInfoFromLCType(localeName, (uint)type, UseUserOverride);
	}

	private int GetLocaleInfo(LocaleNumberData type)
	{
		uint num = (uint)type;
		if (!UseUserOverride)
		{
			num |= 0x80000000u;
		}
		return GetLocaleInfoExInt(_sWindowsName, num);
	}

	private int[] GetLocaleInfo(LocaleGroupingData type)
	{
		return ConvertWin32GroupString(GetLocaleInfoFromLCType(_sWindowsName, (uint)type, UseUserOverride));
	}

	private string GetTimeFormatString()
	{
		return ReescapeWin32String(GetLocaleInfoFromLCType(_sWindowsName, 4099u, UseUserOverride));
	}

	private int GetFirstDayOfWeek()
	{
		int localeInfoExInt = GetLocaleInfoExInt(_sWindowsName, 0x100Cu | ((!UseUserOverride) ? 2147483648u : 0u));
		return ConvertFirstDayOfWeekMonToSun(localeInfoExInt);
	}

	private string[] GetTimeFormats()
	{
		return ReescapeWin32Strings(nativeEnumTimeFormats(_sWindowsName, 0u, UseUserOverride));
	}

	private string[] GetShortTimeFormats()
	{
		return ReescapeWin32Strings(nativeEnumTimeFormats(_sWindowsName, 2u, UseUserOverride));
	}

	private static unsafe CultureData GetCultureDataFromRegionName(string regionName)
	{
		EnumLocaleData value = default(EnumLocaleData);
		value.cultureName = null;
		value.regionName = regionName;
		Interop.Kernel32.EnumSystemLocalesEx(EnumSystemLocalesProc, 34u, Unsafe.AsPointer(ref value), IntPtr.Zero);
		if (value.cultureName != null)
		{
			return GetCultureData(value.cultureName, useUserOverride: true);
		}
		return null;
	}

	private string GetLanguageDisplayName(string cultureName)
	{
		CultureInfo userDefaultCulture;
		if (CultureInfo.DefaultThreadCurrentUICulture != null && (userDefaultCulture = GetUserDefaultCulture()) != null && !CultureInfo.DefaultThreadCurrentUICulture.Name.Equals(userDefaultCulture.Name))
		{
			return NativeName;
		}
		return GetLocaleInfo(cultureName, LocaleStringData.LocalizedDisplayName);
	}

	private string GetRegionDisplayName(string isoCountryCode)
	{
		if (CultureInfo.CurrentUICulture.Name.Equals(CultureInfo.UserDefaultUICulture.Name))
		{
			return GetLocaleInfo(LocaleStringData.LocalizedCountryName);
		}
		return NativeCountryName;
	}

	private static CultureInfo GetUserDefaultCulture()
	{
		return CultureInfo.GetUserDefaultCulture();
	}

	private static string GetLocaleInfoFromLCType(string localeName, uint lctype, bool useUserOveride)
	{
		if (!useUserOveride)
		{
			lctype |= 0x80000000u;
		}
		return GetLocaleInfoEx(localeName, lctype) ?? string.Empty;
	}

	[return: NotNullIfNotNull("str")]
	internal static string ReescapeWin32String(string str)
	{
		if (str == null)
		{
			return null;
		}
		StringBuilder stringBuilder = null;
		bool flag = false;
		for (int i = 0; i < str.Length; i++)
		{
			if (str[i] == '\'')
			{
				if (flag)
				{
					if (i + 1 < str.Length && str[i + 1] == '\'')
					{
						if (stringBuilder == null)
						{
							stringBuilder = new StringBuilder(str, 0, i, str.Length * 2);
						}
						stringBuilder.Append("\\'");
						i++;
						continue;
					}
					flag = false;
				}
				else
				{
					flag = true;
				}
			}
			else if (str[i] == '\\')
			{
				if (stringBuilder == null)
				{
					stringBuilder = new StringBuilder(str, 0, i, str.Length * 2);
				}
				stringBuilder.Append("\\\\");
				continue;
			}
			stringBuilder?.Append(str[i]);
		}
		if (stringBuilder == null)
		{
			return str;
		}
		return stringBuilder.ToString();
	}

	[return: NotNullIfNotNull("array")]
	internal static string[] ReescapeWin32Strings(string[] array)
	{
		if (array != null)
		{
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReescapeWin32String(array[i]);
			}
		}
		return array;
	}

	private static int[] ConvertWin32GroupString(string win32Str)
	{
		if (win32Str == null || win32Str.Length == 0)
		{
			return new int[1] { 3 };
		}
		if (win32Str[0] == '0')
		{
			return new int[1];
		}
		int[] array;
		if (win32Str[win32Str.Length - 1] == '0')
		{
			array = new int[win32Str.Length / 2];
		}
		else
		{
			array = new int[win32Str.Length / 2 + 2];
			array[^1] = 0;
		}
		int num = 0;
		int num2 = 0;
		while (num < win32Str.Length && num2 < array.Length)
		{
			if (win32Str[num] < '1' || win32Str[num] > '9')
			{
				return new int[1] { 3 };
			}
			array[num2] = win32Str[num] - 48;
			num += 2;
			num2++;
		}
		return array;
	}

	private static int ConvertFirstDayOfWeekMonToSun(int iTemp)
	{
		iTemp++;
		if (iTemp > 6)
		{
			iTemp = 0;
		}
		return iTemp;
	}

	private static unsafe Interop.BOOL EnumSystemLocalesProc(char* lpLocaleString, uint flags, void* contextHandle)
	{
		ref EnumLocaleData reference = ref Unsafe.As<byte, EnumLocaleData>(ref *(byte*)contextHandle);
		try
		{
			string text = new string(lpLocaleString);
			string localeInfoEx = GetLocaleInfoEx(text, 90u);
			if (localeInfoEx != null && localeInfoEx.Equals(reference.regionName, StringComparison.OrdinalIgnoreCase))
			{
				reference.cultureName = text;
				return Interop.BOOL.FALSE;
			}
			return Interop.BOOL.TRUE;
		}
		catch (Exception)
		{
			return Interop.BOOL.FALSE;
		}
	}

	private static unsafe Interop.BOOL EnumAllSystemLocalesProc(char* lpLocaleString, uint flags, void* contextHandle)
	{
		ref EnumData reference = ref Unsafe.As<byte, EnumData>(ref *(byte*)contextHandle);
		try
		{
			reference.strings.Add(new string(lpLocaleString));
			return Interop.BOOL.TRUE;
		}
		catch (Exception)
		{
			return Interop.BOOL.FALSE;
		}
	}

	private static unsafe Interop.BOOL EnumTimeCallback(char* lpTimeFormatString, void* lParam)
	{
		ref EnumData reference = ref Unsafe.As<byte, EnumData>(ref *(byte*)lParam);
		try
		{
			reference.strings.Add(new string(lpTimeFormatString));
			return Interop.BOOL.TRUE;
		}
		catch (Exception)
		{
			return Interop.BOOL.FALSE;
		}
	}

	private static unsafe string[] nativeEnumTimeFormats(string localeName, uint dwFlags, bool useUserOverride)
	{
		EnumData value = default(EnumData);
		value.strings = new List<string>();
		Interop.Kernel32.EnumTimeFormatsEx(EnumTimeCallback, localeName, dwFlags, Unsafe.AsPointer(ref value));
		if (value.strings.Count > 0)
		{
			string[] array = value.strings.ToArray();
			if (!useUserOverride && value.strings.Count > 1)
			{
				uint lctype = ((dwFlags == 2) ? 121u : 4099u);
				string localeInfoFromLCType = GetLocaleInfoFromLCType(localeName, lctype, useUserOverride);
				if (localeInfoFromLCType != "")
				{
					string text = array[0];
					if (localeInfoFromLCType != text)
					{
						array[0] = array[1];
						array[1] = text;
					}
				}
			}
			return array;
		}
		return null;
	}

	private static int LocaleNameToLCID(string cultureName)
	{
		return Interop.Kernel32.LocaleNameToLCID(cultureName, 134217728u);
	}

	private static unsafe string LCIDToLocaleName(int culture)
	{
		char* ptr = stackalloc char[86];
		int num = Interop.Kernel32.LCIDToLocaleName(culture, ptr, 86, 134217728u);
		if (num > 0)
		{
			return new string(ptr);
		}
		return null;
	}

	private int GetAnsiCodePage(string cultureName)
	{
		return GetLocaleInfo(LocaleNumberData.AnsiCodePage);
	}

	private int GetOemCodePage(string cultureName)
	{
		return GetLocaleInfo(LocaleNumberData.OemCodePage);
	}

	private int GetMacCodePage(string cultureName)
	{
		return GetLocaleInfo(LocaleNumberData.MacCodePage);
	}

	private int GetEbcdicCodePage(string cultureName)
	{
		return GetLocaleInfo(LocaleNumberData.EbcdicCodePage);
	}

	private int GetGeoId(string cultureName)
	{
		return GetLocaleInfo(LocaleNumberData.GeoId);
	}

	private int GetDigitSubstitution(string cultureName)
	{
		return GetLocaleInfo(LocaleNumberData.DigitSubstitution);
	}

	private string GetThreeLetterWindowsLanguageName(string cultureName)
	{
		return GetLocaleInfo(cultureName, LocaleStringData.AbbreviatedWindowsLanguageName);
	}

	private static unsafe CultureInfo[] EnumCultures(CultureTypes types)
	{
		uint num = 0u;
		if ((types & (CultureTypes.InstalledWin32Cultures | CultureTypes.ReplacementCultures | CultureTypes.FrameworkCultures)) != 0)
		{
			num |= 0x30u;
		}
		if ((types & CultureTypes.NeutralCultures) != 0)
		{
			num |= 0x10u;
		}
		if ((types & CultureTypes.SpecificCultures) != 0)
		{
			num |= 0x20u;
		}
		if ((types & CultureTypes.UserCustomCulture) != 0)
		{
			num |= 2u;
		}
		if ((types & CultureTypes.ReplacementCultures) != 0)
		{
			num |= 2u;
		}
		EnumData value = default(EnumData);
		value.strings = new List<string>();
		
		// Interop.Kernel32.EnumSystemLocalesEx(EnumAllSystemLocalesProc, num, Unsafe.AsPointer(ref value), IntPtr.Zero);
		
		CultureInfo[] array = new CultureInfo[value.strings.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = new CultureInfo(value.strings[i]);
		}
		return array;
	}

	private string GetConsoleFallbackName(string cultureName)
	{
		return GetLocaleInfo(cultureName, LocaleStringData.ConsoleFallbackName);
	}
}*/
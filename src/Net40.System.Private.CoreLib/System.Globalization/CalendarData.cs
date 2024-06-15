/*using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DefaultNamespace;

namespace System.Globalization.Net40;

internal class CalendarData
{
	private struct EnumData
	{
		public string userOverride;

		public List<string> strings;
	}

	private struct EnumCalendarsData
	{
		public int userOverride;

		public List<int> calendars;
	}

	internal string sNativeName;

	internal string[] saShortDates;

	internal string[] saYearMonths;

	internal string[] saLongDates;

	internal string sMonthDay;

	internal string[] saEraNames;

	internal string[] saAbbrevEraNames;

	internal string[] saAbbrevEnglishEraNames;

	internal string[] saDayNames;

	internal string[] saAbbrevDayNames;

	internal string[] saSuperShortDayNames;

	internal string[] saMonthNames;

	internal string[] saAbbrevMonthNames;

	internal string[] saMonthGenitiveNames;

	internal string[] saAbbrevMonthGenitiveNames;

	internal string[] saLeapYearMonthNames;

	internal int iTwoDigitYearMax = 2029;

	internal int iCurrentEra;

	internal bool bUseUserOverrides;

	internal static readonly CalendarData Invariant = CreateInvariant();

	private CalendarData()
	{
	}

	private static CalendarData CreateInvariant()
	{
		CalendarData calendarData = new CalendarData();
		calendarData.sNativeName = "Gregorian Calendar";
		calendarData.iTwoDigitYearMax = 2029;
		calendarData.iCurrentEra = 1;
		calendarData.saShortDates = new string[2] { "MM/dd/yyyy", "yyyy-MM-dd" };
		calendarData.saLongDates = new string[1] { "dddd, dd MMMM yyyy" };
		calendarData.saYearMonths = new string[1] { "yyyy MMMM" };
		calendarData.sMonthDay = "MMMM dd";
		calendarData.saEraNames = new string[1] { "A.D." };
		calendarData.saAbbrevEraNames = new string[1] { "AD" };
		calendarData.saAbbrevEnglishEraNames = new string[1] { "AD" };
		calendarData.saDayNames = new string[7] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
		calendarData.saAbbrevDayNames = new string[7] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
		calendarData.saSuperShortDayNames = new string[7] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
		calendarData.saMonthNames = new string[13]
		{
			"January",
			"February",
			"March",
			"April",
			"May",
			"June",
			"July",
			"August",
			"September",
			"October",
			"November",
			"December",
			string.Empty
		};
		calendarData.saAbbrevMonthNames = new string[13]
		{
			"Jan",
			"Feb",
			"Mar",
			"Apr",
			"May",
			"Jun",
			"Jul",
			"Aug",
			"Sep",
			"Oct",
			"Nov",
			"Dec",
			string.Empty
		};
		calendarData.saMonthGenitiveNames = calendarData.saMonthNames;
		calendarData.saAbbrevMonthGenitiveNames = calendarData.saAbbrevMonthNames;
		calendarData.saLeapYearMonthNames = calendarData.saMonthNames;
		calendarData.bUseUserOverrides = false;
		return calendarData;
	}

	internal CalendarData(string localeName, CalendarId calendarId, bool bUseUserOverrides)
	{
		this.bUseUserOverrides = bUseUserOverrides;
		if (!LoadCalendarDataFromSystem(localeName, calendarId))
		{
			if (sNativeName == null)
			{
				sNativeName = string.Empty;
			}
			if (saShortDates == null)
			{
				saShortDates = Invariant.saShortDates;
			}
			if (saYearMonths == null)
			{
				saYearMonths = Invariant.saYearMonths;
			}
			if (saLongDates == null)
			{
				saLongDates = Invariant.saLongDates;
			}
			if (sMonthDay == null)
			{
				sMonthDay = Invariant.sMonthDay;
			}
			if (saEraNames == null)
			{
				saEraNames = Invariant.saEraNames;
			}
			if (saAbbrevEraNames == null)
			{
				saAbbrevEraNames = Invariant.saAbbrevEraNames;
			}
			if (saAbbrevEnglishEraNames == null)
			{
				saAbbrevEnglishEraNames = Invariant.saAbbrevEnglishEraNames;
			}
			if (saDayNames == null)
			{
				saDayNames = Invariant.saDayNames;
			}
			if (saAbbrevDayNames == null)
			{
				saAbbrevDayNames = Invariant.saAbbrevDayNames;
			}
			if (saSuperShortDayNames == null)
			{
				saSuperShortDayNames = Invariant.saSuperShortDayNames;
			}
			if (saMonthNames == null)
			{
				saMonthNames = Invariant.saMonthNames;
			}
			if (saAbbrevMonthNames == null)
			{
				saAbbrevMonthNames = Invariant.saAbbrevMonthNames;
			}
		}
		if (calendarId == CalendarId.TAIWAN)
		{
			if (SystemSupportsTaiwaneseCalendar())
			{
				sNativeName = "中華民國曆";
			}
			else
			{
				sNativeName = string.Empty;
			}
		}
		if (saMonthGenitiveNames == null || saMonthGenitiveNames.Length == 0 || string.IsNullOrEmpty(saMonthGenitiveNames[0]))
		{
			saMonthGenitiveNames = saMonthNames;
		}
		if (saAbbrevMonthGenitiveNames == null || saAbbrevMonthGenitiveNames.Length == 0 || string.IsNullOrEmpty(saAbbrevMonthGenitiveNames[0]))
		{
			saAbbrevMonthGenitiveNames = saAbbrevMonthNames;
		}
		if (saLeapYearMonthNames == null || saLeapYearMonthNames.Length == 0 || string.IsNullOrEmpty(saLeapYearMonthNames[0]))
		{
			saLeapYearMonthNames = saMonthNames;
		}
		InitializeEraNames(localeName, calendarId);
		InitializeAbbreviatedEraNames(localeName, calendarId);
		if (calendarId == CalendarId.JAPAN)
		{
			saAbbrevEnglishEraNames = JapaneseCalendar.EnglishEraNames();
		}
		else
		{
			saAbbrevEnglishEraNames = new string[1] { "" };
		}
		iCurrentEra = saEraNames.Length;
	}

	private void InitializeEraNames(string localeName, CalendarId calendarId)
	{
		switch (calendarId)
		{
		case CalendarId.GREGORIAN:
			if (saEraNames == null || saEraNames.Length == 0 || string.IsNullOrEmpty(saEraNames[0]))
			{
				saEraNames = new string[1] { "A.D." };
			}
			break;
		case CalendarId.GREGORIAN_US:
		case CalendarId.JULIAN:
			saEraNames = new string[1] { "A.D." };
			break;
		case CalendarId.HEBREW:
			saEraNames = new string[1] { "C.E." };
			break;
		case CalendarId.HIJRI:
		case CalendarId.UMALQURA:
			if (localeName == "dv-MV")
			{
				saEraNames = new string[1] { "ހ\u07a8ޖ\u07b0ރ\u07a9" };
			}
			else
			{
				saEraNames = new string[1] { "بعد الهجرة" };
			}
			break;
		case CalendarId.GREGORIAN_ARABIC:
		case CalendarId.GREGORIAN_XLIT_ENGLISH:
		case CalendarId.GREGORIAN_XLIT_FRENCH:
			saEraNames = new string[1] { "م" };
			break;
		case CalendarId.GREGORIAN_ME_FRENCH:
			saEraNames = new string[1] { "ap. J.-C." };
			break;
		case CalendarId.TAIWAN:
			if (SystemSupportsTaiwaneseCalendar())
			{
				saEraNames = new string[1] { "中華民國" };
			}
			else
			{
				saEraNames = new string[1] { string.Empty };
			}
			break;
		case CalendarId.KOREA:
			saEraNames = new string[1] { "단기" };
			break;
		case CalendarId.THAI:
			saEraNames = new string[1] { "พ.ศ." };
			break;
		case CalendarId.JAPAN:
		case CalendarId.JAPANESELUNISOLAR:
			saEraNames = JapaneseCalendar.EraNames();
			break;
		case CalendarId.PERSIAN:
			if (saEraNames == null || saEraNames.Length == 0 || string.IsNullOrEmpty(saEraNames[0]))
			{
				saEraNames = new string[1] { "ه.ش" };
			}
			break;
		default:
			saEraNames = Invariant.saEraNames;
			break;
		}
	}

	private void InitializeAbbreviatedEraNames(string localeName, CalendarId calendarId)
	{
		switch (calendarId)
		{
		case CalendarId.GREGORIAN:
			if (saAbbrevEraNames == null || saAbbrevEraNames.Length == 0 || string.IsNullOrEmpty(saAbbrevEraNames[0]))
			{
				saAbbrevEraNames = new string[1] { "AD" };
			}
			break;
		case CalendarId.GREGORIAN_US:
		case CalendarId.JULIAN:
			saAbbrevEraNames = new string[1] { "AD" };
			break;
		case CalendarId.JAPAN:
		case CalendarId.JAPANESELUNISOLAR:
			saAbbrevEraNames = JapaneseCalendar.AbbrevEraNames();
			break;
		case CalendarId.HIJRI:
		case CalendarId.UMALQURA:
			if (localeName == "dv-MV")
			{
				saAbbrevEraNames = new string[1] { "ހ." };
			}
			else
			{
				saAbbrevEraNames = new string[1] { "هـ" };
			}
			break;
		case CalendarId.TAIWAN:
			saAbbrevEraNames = new string[1];
			if (saEraNames[0].Length == 4)
			{
				saAbbrevEraNames[0] = saEraNames[0].Substring(2, 2);
			}
			else
			{
				saAbbrevEraNames[0] = saEraNames[0];
			}
			break;
		case CalendarId.PERSIAN:
			if (saAbbrevEraNames == null || saAbbrevEraNames.Length == 0 || string.IsNullOrEmpty(saAbbrevEraNames[0]))
			{
				saAbbrevEraNames = saEraNames;
			}
			break;
		default:
			saAbbrevEraNames = saEraNames;
			break;
		}
	}

	internal static CalendarData GetCalendarData(CalendarId calendarId)
	{
		string name = CalendarIdToCultureName(calendarId);
		return CultureInfo.GetCultureInfo(name)._cultureData.GetCalendar(calendarId);
	}

	private static string CalendarIdToCultureName(CalendarId calendarId)
	{
		switch (calendarId)
		{
		case CalendarId.GREGORIAN_US:
			return "fa-IR";
		case CalendarId.JAPAN:
			return "ja-JP";
		case CalendarId.TAIWAN:
			return "zh-TW";
		case CalendarId.KOREA:
			return "ko-KR";
		case CalendarId.HIJRI:
		case CalendarId.GREGORIAN_ARABIC:
		case CalendarId.UMALQURA:
			return "ar-SA";
		case CalendarId.THAI:
			return "th-TH";
		case CalendarId.HEBREW:
			return "he-IL";
		case CalendarId.GREGORIAN_ME_FRENCH:
			return "ar-DZ";
		case CalendarId.GREGORIAN_XLIT_ENGLISH:
		case CalendarId.GREGORIAN_XLIT_FRENCH:
			return "ar-IQ";
		default:
			return "en-US";
		}
	}

	private bool LoadCalendarDataFromSystem(string localeName, CalendarId calendarId)
	{
		bool flag = true;
		uint num = ((!bUseUserOverrides) ? 2147483648u : 0u);
		switch (calendarId)
		{
		case CalendarId.JAPANESELUNISOLAR:
			calendarId = CalendarId.JAPAN;
			break;
		case CalendarId.JULIAN:
		case CalendarId.CHINESELUNISOLAR:
		case CalendarId.SAKA:
		case CalendarId.LUNAR_ETO_CHN:
		case CalendarId.LUNAR_ETO_KOR:
		case CalendarId.LUNAR_ETO_ROKUYOU:
		case CalendarId.KOREANLUNISOLAR:
		case CalendarId.TAIWANLUNISOLAR:
			calendarId = CalendarId.GREGORIAN_US;
			break;
		}
		CheckSpecialCalendar(ref calendarId, ref localeName);
		flag &= CallGetCalendarInfoEx(localeName, calendarId, 0x30u | num, out iTwoDigitYearMax);
		flag &= CallGetCalendarInfoEx(localeName, calendarId, 2u, out sNativeName);
		flag &= CallGetCalendarInfoEx(localeName, calendarId, 0x38u | num, out sMonthDay);
		flag &= CallEnumCalendarInfo(localeName, calendarId, 5u, 0x1Fu | num, out saShortDates);
		flag &= CallEnumCalendarInfo(localeName, calendarId, 6u, 0x20u | num, out saLongDates);
		flag &= CallEnumCalendarInfo(localeName, calendarId, 47u, 4102u, out saYearMonths);
		flag &= GetCalendarDayInfo(localeName, calendarId, 13u, out saDayNames);
		flag &= GetCalendarDayInfo(localeName, calendarId, 20u, out saAbbrevDayNames);
		flag &= GetCalendarMonthInfo(localeName, calendarId, 21u, out saMonthNames);
		flag &= GetCalendarMonthInfo(localeName, calendarId, 34u, out saAbbrevMonthNames);
		GetCalendarDayInfo(localeName, calendarId, 55u, out saSuperShortDayNames);
		if (calendarId == CalendarId.GREGORIAN)
		{
			GetCalendarMonthInfo(localeName, calendarId, 268435477u, out saMonthGenitiveNames);
			GetCalendarMonthInfo(localeName, calendarId, 268435490u, out saAbbrevMonthGenitiveNames);
		}
		CallEnumCalendarInfo(localeName, calendarId, 4u, 0u, out saEraNames);
		CallEnumCalendarInfo(localeName, calendarId, 57u, 0u, out saAbbrevEraNames);
		saShortDates = CultureData.ReescapeWin32Strings(saShortDates);
		saLongDates = CultureData.ReescapeWin32Strings(saLongDates);
		saYearMonths = CultureData.ReescapeWin32Strings(saYearMonths);
		sMonthDay = CultureData.ReescapeWin32String(sMonthDay);
		return flag;
	}

	internal static int GetTwoDigitYearMax(CalendarId calendarId)
	{
		if (GlobalizationMode.Invariant)
		{
			return Invariant.iTwoDigitYearMax;
		}
		int data = -1;
		if (!CallGetCalendarInfoEx((string)null, calendarId, 48u, out data))
		{
			data = -1;
		}
		return data;
	}

	internal static unsafe int GetCalendars(string localeName, bool useUserOverride, CalendarId[] calendars)
	{
		EnumCalendarsData value = default(EnumCalendarsData);
		value.userOverride = 0;
		value.calendars = new List<int>();
		if (useUserOverride)
		{
			int localeInfoExInt = CultureData.GetLocaleInfoExInt(localeName, 4105u);
			if (localeInfoExInt != 0)
			{
				value.userOverride = localeInfoExInt;
				value.calendars.Add(localeInfoExInt);
			}
		}
		//Interop.Kernel32.EnumCalendarInfoExEx(EnumCalendarsCallback, localeName, uint.MaxValue, null, 1u, Unsafe.AsPointer(ref value));
		for (int i = 0; i < Math.Min(calendars.Length, value.calendars.Count); i++)
		{
			calendars[i] = (CalendarId)value.calendars[i];
		}
		return value.calendars.Count;
	}

	private static bool SystemSupportsTaiwaneseCalendar()
	{
		string data;
		return CallGetCalendarInfoEx("zh-TW", CalendarId.TAIWAN, 2u, out data);
	}

	private static void CheckSpecialCalendar(ref CalendarId calendar, ref string localeName)
	{
		switch (calendar)
		{
		case CalendarId.GREGORIAN_US:
		{
			if (!CallGetCalendarInfoEx(localeName, calendar, 2u, out string data))
			{
				localeName = "fa-IR";
			}
			if (!CallGetCalendarInfoEx(localeName, calendar, 2u, out data))
			{
				localeName = "en-US";
				calendar = CalendarId.GREGORIAN;
			}
			break;
		}
		case CalendarId.TAIWAN:
			if (!SystemSupportsTaiwaneseCalendar())
			{
				calendar = CalendarId.GREGORIAN;
			}
			break;
		}
	}

	private static bool CallGetCalendarInfoEx(string localeName, CalendarId calendar, uint calType, out int data)
	{
		//return Interop.Kernel32.GetCalendarInfoEx(localeName, (uint)calendar, IntPtr.Zero, calType | 0x20000000u, IntPtr.Zero, 0, out data) != 0;
		return false;
	}

	private static unsafe bool CallGetCalendarInfoEx(string localeName, CalendarId calendar, uint calType, out string data)
	{
		char* ptr = stackalloc char[80];
		int num = Interop.Kernel32.GetCalendarInfoEx(localeName, (uint)calendar, IntPtr.Zero, calType, (IntPtr)ptr, 80, IntPtr.Zero);
		if (num > 0)
		{
			if (ptr[num - 1] == '\0')
			{
				num--;
			}
			data = new string(ptr, 0, num);
			return true;
		}
		data = "";
		return false;
	}

	private static unsafe Interop.BOOL EnumCalendarInfoCallback(char* lpCalendarInfoString, uint calendar, IntPtr pReserved, void* lParam)
	{
		ref EnumData reference = ref Unsafe.As<byte, EnumData>(ref *(byte*)lParam);
		try
		{
			string text = new string(lpCalendarInfoString);
			if (reference.userOverride != text)
			{
				reference.strings.Add(text);
			}
			return Interop.BOOL.TRUE;
		}
		catch (Exception)
		{
			return Interop.BOOL.FALSE;
		}
	}

	private static unsafe bool CallEnumCalendarInfo(string localeName, CalendarId calendar, uint calType, uint lcType, out string[] data)
	{
		EnumData value = default(EnumData);
		value.userOverride = null;
		value.strings = new List<string>();
		if (lcType != 0 && (lcType & 0x80000000u) == 0 && GetUserDefaultLocaleName() == localeName)
		{
			CalendarId calendarId = (CalendarId)CultureData.GetLocaleInfoExInt(localeName, 4105u);
			if (calendarId == calendar)
			{
				string localeInfoEx = CultureData.GetLocaleInfoEx(localeName, lcType);
				if (localeInfoEx != null)
				{
					value.userOverride = localeInfoEx;
					value.strings.Add(localeInfoEx);
				}
			}
		}
		Interop.Kernel32.EnumCalendarInfoExEx(EnumCalendarInfoCallback, localeName, (uint)calendar, null, calType, Unsafe.AsPointer(ref value));
		if (value.strings.Count == 0)
		{
			data = null;
			return false;
		}
		string[] array = value.strings.ToArray();
		if (calType == 57 || calType == 4)
		{
			Array.Reverse(array, 0, array.Length);
		}
		data = array;
		return true;
	}

	private static bool GetCalendarDayInfo(string localeName, CalendarId calendar, uint calType, out string[] outputStrings)
	{
		bool flag = true;
		string[] array = new string[7];
		int num = 0;
		while (num < 7)
		{
			flag &= CallGetCalendarInfoEx(localeName, calendar, calType, out array[num]);
			if (num == 0)
			{
				calType -= 7;
			}
			num++;
			calType++;
		}
		outputStrings = array;
		return flag;
	}

	private static bool GetCalendarMonthInfo(string localeName, CalendarId calendar, uint calType, out string[] outputStrings)
	{
		string[] array = new string[13];
		int num = 0;
		while (num < 13)
		{
			if (!CallGetCalendarInfoEx(localeName, calendar, calType, out array[num]))
			{
				array[num] = "";
			}
			num++;
			calType++;
		}
		outputStrings = array;
		return true;
	}

	private static unsafe Interop.BOOL EnumCalendarsCallback(char* lpCalendarInfoString, uint calendar, IntPtr reserved, void* lParam)
	{
		ref EnumCalendarsData reference = ref Unsafe.As<byte, EnumCalendarsData>(ref *(byte*)lParam);
		try
		{
			if (reference.userOverride != calendar)
			{
				reference.calendars.Add((int)calendar);
			}
			return Interop.BOOL.TRUE;
		}
		catch (Exception)
		{
			return Interop.BOOL.FALSE;
		}
	}

	private static unsafe string GetUserDefaultLocaleName()
	{
		char* ptr = stackalloc char[85];
		int localeInfoEx = CultureData.GetLocaleInfoEx(null, 92u, ptr, 85);
		if (localeInfoEx > 0)
		{
			return new string(ptr, 0, localeInfoEx - 1);
		}
		return "";
	}
}*/
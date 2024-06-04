#define DEBUG
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;

namespace System.Diagnostics.Tracing;

internal class ManifestBuilder
{
	private static readonly string[] s_escapes = new string[8] { "&amp;", "&lt;", "&gt;", "&apos;", "&quot;", "%r", "%n", "%t" };

	private Dictionary<int, string> opcodeTab;

	private Dictionary<int, string>? taskTab;

	private Dictionary<ulong, string>? keywordTab;

	private Dictionary<string, Type>? mapsTab;

	private Dictionary<string, string> stringTab;

	private StringBuilder sb;

	private StringBuilder events;

	private StringBuilder templates;

	private ResourceManager? resources;

	private EventManifestOptions flags;

	private IList<string> errors;

	private Dictionary<string, List<int>> perEventByteArrayArgIndices;

	private string? eventName;

	private int numParams;

	private List<int>? byteArrArgIndices;

	public IList<string> Errors => errors;

	public ManifestBuilder(string providerName, Guid providerGuid, string? dllName, ResourceManager? resources, EventManifestOptions flags)
	{
		this.flags = flags;
		this.resources = resources;
		sb = new StringBuilder();
		events = new StringBuilder();
		templates = new StringBuilder();
		opcodeTab = new Dictionary<int, string>();
		stringTab = new Dictionary<string, string>();
		errors = new List<string>();
		perEventByteArrayArgIndices = new Dictionary<string, List<int>>();
		sb.AppendLine("<instrumentationManifest xmlns=\"http://schemas.microsoft.com/win/2004/08/events\">");
		sb.AppendLine(" <instrumentation xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:win=\"http://manifests.microsoft.com/win/2004/08/windows/events\">");
		sb.AppendLine("  <events xmlns=\"http://schemas.microsoft.com/win/2004/08/events\">");
		sb.Append("<provider name=\"").Append(providerName).Append("\" guid=\"{")
			.Append(providerGuid.ToString())
			.Append("}");
		if (dllName != null)
		{
			sb.Append("\" resourceFileName=\"").Append(dllName).Append("\" messageFileName=\"")
				.Append(dllName);
		}
		string symbolsName = providerName.Replace("-", "").Replace('.', '_');
		sb.Append("\" symbol=\"").Append(symbolsName);
		sb.Append("\">").AppendLine();
	}

	public void AddOpcode(string name, int value)
	{
		if ((flags & EventManifestOptions.Strict) != 0)
		{
			if (value <= 10 || value >= 239)
			{
				ManifestError(SR.Format(SR.EventSource_IllegalOpcodeValue, name, value));
			}
			if (opcodeTab.TryGetValue(value, out string prevName) && !name.Equals(prevName, StringComparison.Ordinal))
			{
				ManifestError(SR.Format(SR.EventSource_OpcodeCollision, name, prevName, value));
			}
		}
		opcodeTab[value] = name;
	}

	public void AddTask(string name, int value)
	{
		if ((flags & EventManifestOptions.Strict) != 0)
		{
			if (value <= 0 || value >= 65535)
			{
				ManifestError(SR.Format(SR.EventSource_IllegalTaskValue, name, value));
			}
			if (taskTab != null && taskTab.TryGetValue(value, out string prevName) && !name.Equals(prevName, StringComparison.Ordinal))
			{
				ManifestError(SR.Format(SR.EventSource_TaskCollision, name, prevName, value));
			}
		}
		if (taskTab == null)
		{
			taskTab = new Dictionary<int, string>();
		}
		taskTab[value] = name;
	}

	public void AddKeyword(string name, ulong value)
	{
		if ((value & (value - 1)) != 0)
		{
			ManifestError(SR.Format(SR.EventSource_KeywordNeedPowerOfTwo, "0x" + value.ToString("x", CultureInfo.CurrentCulture), name), runtimeCritical: true);
		}
		if ((flags & EventManifestOptions.Strict) != 0)
		{
			if (value >= 17592186044416L && !name.StartsWith("Session", StringComparison.Ordinal))
			{
				ManifestError(SR.Format(SR.EventSource_IllegalKeywordsValue, name, "0x" + value.ToString("x", CultureInfo.CurrentCulture)));
			}
			if (keywordTab != null && keywordTab.TryGetValue(value, out string prevName) && !name.Equals(prevName, StringComparison.Ordinal))
			{
				ManifestError(SR.Format(SR.EventSource_KeywordCollision, name, prevName, "0x" + value.ToString("x", CultureInfo.CurrentCulture)));
			}
		}
		if (keywordTab == null)
		{
			keywordTab = new Dictionary<ulong, string>();
		}
		keywordTab[value] = name;
	}

	public void StartEvent(string eventName, EventAttribute eventAttribute)
	{
		Debug.Assert(numParams == 0);
		Debug.Assert(this.eventName == null);
		this.eventName = eventName;
		numParams = 0;
		byteArrArgIndices = null;
		events.Append("  <event").Append(" value=\"").Append(eventAttribute.EventId)
			.Append("\"")
			.Append(" version=\"")
			.Append(eventAttribute.Version)
			.Append("\"")
			.Append(" level=\"")
			.Append(GetLevelName(eventAttribute.Level))
			.Append("\"")
			.Append(" symbol=\"")
			.Append(eventName)
			.Append("\"");
		WriteMessageAttrib(events, "event", eventName, eventAttribute.Message);
		if (eventAttribute.Keywords != EventKeywords.None)
		{
			events.Append(" keywords=\"").Append(GetKeywords((ulong)eventAttribute.Keywords, eventName)).Append("\"");
		}
		if (eventAttribute.Opcode != 0)
		{
			events.Append(" opcode=\"").Append(GetOpcodeName(eventAttribute.Opcode, eventName)).Append("\"");
		}
		if (eventAttribute.Task != 0)
		{
			events.Append(" task=\"").Append(GetTaskName(eventAttribute.Task, eventName)).Append("\"");
		}
	}

	public void AddEventParameter(Type type, string name)
	{
		if (numParams == 0)
		{
			templates.Append("  <template tid=\"").Append(eventName).Append("Args\">")
				.AppendLine();
		}
		if (type == typeof(byte[]))
		{
			if (byteArrArgIndices == null)
			{
				byteArrArgIndices = new List<int>(4);
			}
			byteArrArgIndices.Add(numParams);
			numParams++;
			templates.Append("   <data name=\"").Append(name).Append("Size\" inType=\"win:UInt32\"/>")
				.AppendLine();
		}
		numParams++;
		templates.Append("   <data name=\"").Append(name).Append("\" inType=\"")
			.Append(GetTypeName(type))
			.Append("\"");
		if ((type.IsArray || type.IsPointer) && type.GetElementType() == typeof(byte))
		{
			templates.Append(" length=\"").Append(name).Append("Size\"");
		}
		if (type.IsEnum() && Enum.GetUnderlyingType(type) != typeof(ulong) && Enum.GetUnderlyingType(type) != typeof(long))
		{
			templates.Append(" map=\"").Append(type.Name).Append("\"");
			if (mapsTab == null)
			{
				mapsTab = new Dictionary<string, Type>();
			}
			if (!mapsTab.ContainsKey(type.Name))
			{
				mapsTab.Add(type.Name, type);
			}
		}
		templates.Append("/>").AppendLine();
	}

	public void EndEvent()
	{
		Debug.Assert(eventName != null);
		if (numParams > 0)
		{
			templates.Append("  </template>").AppendLine();
			events.Append(" template=\"").Append(eventName).Append("Args\"");
		}
		events.Append("/>").AppendLine();
		if (byteArrArgIndices != null)
		{
			perEventByteArrayArgIndices[eventName] = byteArrArgIndices;
		}
		if (stringTab.TryGetValue("event_" + eventName, out string msg))
		{
			msg = TranslateToManifestConvention(msg, eventName);
			stringTab["event_" + eventName] = msg;
		}
		eventName = null;
		numParams = 0;
		byteArrArgIndices = null;
	}

	public byte[] CreateManifest()
	{
		string str = CreateManifestString();
		return Encoding.UTF8.GetBytes(str);
	}

	public void ManifestError(string msg, bool runtimeCritical = false)
	{
		if ((flags & EventManifestOptions.Strict) != 0)
		{
			errors.Add(msg);
		}
		else if (runtimeCritical)
		{
			throw new ArgumentException(msg);
		}
	}

	private string CreateManifestString()
	{
		if (taskTab != null)
		{
			sb.Append(" <tasks>").AppendLine();
			List<int> sortedTasks = new List<int>(taskTab.Keys);
			sortedTasks.Sort();
			foreach (int task in sortedTasks)
			{
				sb.Append("  <task");
				WriteNameAndMessageAttribs(sb, "task", taskTab[task]);
				sb.Append(" value=\"").Append(task).Append("\"/>")
					.AppendLine();
			}
			sb.Append(" </tasks>").AppendLine();
		}
		if (mapsTab != null)
		{
			sb.Append(" <maps>").AppendLine();
			foreach (Type enumType in mapsTab.Values)
			{
				bool isbitmap = EventSource.GetCustomAttributeHelper(enumType, typeof(FlagsAttribute), flags) != null;
				string mapKind = (isbitmap ? "bitMap" : "valueMap");
				sb.Append("  <").Append(mapKind).Append(" name=\"")
					.Append(enumType.Name)
					.Append("\">")
					.AppendLine();
				FieldInfo[] staticFields = enumType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
				bool anyValuesWritten = false;
				FieldInfo[] array = staticFields;
				foreach (FieldInfo staticField in array)
				{
					object constantValObj = staticField.GetRawConstantValue();
					if (constantValObj != null)
					{
						ulong hexValue = ((!(constantValObj is ulong)) ? ((ulong)Convert.ToInt64(constantValObj)) : ((ulong)constantValObj));
						if (!isbitmap || ((hexValue & (hexValue - 1)) == 0L && hexValue != 0))
						{
							sb.Append("   <map value=\"0x").Append(hexValue.ToString("x", CultureInfo.InvariantCulture)).Append("\"");
							WriteMessageAttrib(sb, "map", enumType.Name + "." + staticField.Name, staticField.Name);
							sb.Append("/>").AppendLine();
							anyValuesWritten = true;
						}
					}
				}
				if (!anyValuesWritten)
				{
					sb.Append("   <map value=\"0x0\"");
					WriteMessageAttrib(sb, "map", enumType.Name + ".None", "None");
					sb.Append("/>").AppendLine();
				}
				sb.Append("  </").Append(mapKind).Append(">")
					.AppendLine();
			}
			sb.Append(" </maps>").AppendLine();
		}
		sb.Append(" <opcodes>").AppendLine();
		List<int> sortedOpcodes = new List<int>(opcodeTab.Keys);
		sortedOpcodes.Sort();
		foreach (int opcode in sortedOpcodes)
		{
			sb.Append("  <opcode");
			WriteNameAndMessageAttribs(sb, "opcode", opcodeTab[opcode]);
			sb.Append(" value=\"").Append(opcode).Append("\"/>")
				.AppendLine();
		}
		sb.Append(" </opcodes>").AppendLine();
		if (keywordTab != null)
		{
			sb.Append(" <keywords>").AppendLine();
			List<ulong> sortedKeywords = new List<ulong>(keywordTab.Keys);
			sortedKeywords.Sort();
			foreach (ulong keyword in sortedKeywords)
			{
				sb.Append("  <keyword");
				WriteNameAndMessageAttribs(sb, "keyword", keywordTab[keyword]);
				sb.Append(" mask=\"0x").Append(keyword.ToString("x", CultureInfo.InvariantCulture)).Append("\"/>")
					.AppendLine();
			}
			sb.Append(" </keywords>").AppendLine();
		}
		sb.Append(" <events>").AppendLine();
		sb.Append(events);
		sb.Append(" </events>").AppendLine();
		sb.Append(" <templates>").AppendLine();
		if (templates.Length > 0)
		{
			sb.Append(templates);
		}
		else
		{
			sb.Append("    <template tid=\"_empty\"></template>").AppendLine();
		}
		sb.Append(" </templates>").AppendLine();
		sb.Append("</provider>").AppendLine();
		sb.Append("</events>").AppendLine();
		sb.Append("</instrumentation>").AppendLine();
		sb.Append("<localization>").AppendLine();
		List<CultureInfo> cultures = null;
		if (resources != null && (flags & EventManifestOptions.AllCultures) != 0)
		{
			cultures = GetSupportedCultures(resources);
		}
		else
		{
			cultures = new List<CultureInfo>();
			cultures.Add(CultureInfo.CurrentUICulture);
		}
		string[] sortedStrings = new string[stringTab.Keys.Count];
		stringTab.Keys.CopyTo(sortedStrings, 0);
		Array.Sort(sortedStrings, 0, sortedStrings.Length);
		foreach (CultureInfo ci in cultures)
		{
			sb.Append(" <resources culture=\"").Append(ci.Name).Append("\">")
				.AppendLine();
			sb.Append("  <stringTable>").AppendLine();
			string[] array2 = sortedStrings;
			foreach (string stringKey in array2)
			{
				string val = GetLocalizedMessage(stringKey, ci, etwFormat: true);
				sb.Append("   <string id=\"").Append(stringKey).Append("\" value=\"")
					.Append(val)
					.Append("\"/>")
					.AppendLine();
			}
			sb.Append("  </stringTable>").AppendLine();
			sb.Append(" </resources>").AppendLine();
		}
		sb.Append("</localization>").AppendLine();
		sb.AppendLine("</instrumentationManifest>");
		return sb.ToString();
	}

	private void WriteNameAndMessageAttribs(StringBuilder stringBuilder, string elementName, string name)
	{
		stringBuilder.Append(" name=\"").Append(name).Append("\"");
		WriteMessageAttrib(sb, elementName, name, name);
	}

	private void WriteMessageAttrib(StringBuilder stringBuilder, string elementName, string name, string? value)
	{
		string key = elementName + "_" + name;
		if (resources != null)
		{
			string localizedString = resources.GetString(key, CultureInfo.InvariantCulture);
			if (localizedString != null)
			{
				value = localizedString;
			}
		}
		if (value != null)
		{
			stringBuilder.Append(" message=\"$(string.").Append(key).Append(")\"");
			if (stringTab.TryGetValue(key, out string prevValue) && !prevValue.Equals(value))
			{
				ManifestError(SR.Format(SR.EventSource_DuplicateStringKey, key), runtimeCritical: true);
			}
			else
			{
				stringTab[key] = value;
			}
		}
	}

	internal string? GetLocalizedMessage(string key, CultureInfo ci, bool etwFormat)
	{
		string value = null;
		if (resources != null)
		{
			string localizedString = resources.GetString(key, ci);
			if (localizedString != null)
			{
				value = localizedString;
				if (etwFormat && key.StartsWith("event_", StringComparison.Ordinal))
				{
					string evtName = key.Substring("event_".Length);
					value = TranslateToManifestConvention(value, evtName);
				}
			}
		}
		if (etwFormat && value == null)
		{
			stringTab.TryGetValue(key, out value);
		}
		return value;
	}

	private static List<CultureInfo> GetSupportedCultures(ResourceManager resources)
	{
		List<CultureInfo> cultures = new List<CultureInfo>();
		if (!cultures.Contains(CultureInfo.CurrentUICulture))
		{
			cultures.Insert(0, CultureInfo.CurrentUICulture);
		}
		return cultures;
	}

	private static string GetLevelName(EventLevel level)
	{
		return ((level >= (EventLevel)16) ? "" : "win:") + level;
	}

	private string GetTaskName(EventTask task, string eventName)
	{
		if (task == EventTask.None)
		{
			return "";
		}
		if (taskTab == null)
		{
			taskTab = new Dictionary<int, string>();
		}
		if (!taskTab.TryGetValue((int)task, out string ret))
		{
			string text2 = (taskTab[(int)task] = eventName);
			ret = text2;
		}
		return ret;
	}

	private string? GetOpcodeName(EventOpcode opcode, string eventName)
	{
		switch (opcode)
		{
		case EventOpcode.Info:
			return "win:Info";
		case EventOpcode.Start:
			return "win:Start";
		case EventOpcode.Stop:
			return "win:Stop";
		case EventOpcode.DataCollectionStart:
			return "win:DC_Start";
		case EventOpcode.DataCollectionStop:
			return "win:DC_Stop";
		case EventOpcode.Extension:
			return "win:Extension";
		case EventOpcode.Reply:
			return "win:Reply";
		case EventOpcode.Resume:
			return "win:Resume";
		case EventOpcode.Suspend:
			return "win:Suspend";
		case EventOpcode.Send:
			return "win:Send";
		case EventOpcode.Receive:
			return "win:Receive";
		default:
		{
			if (opcodeTab == null || !opcodeTab.TryGetValue((int)opcode, out string ret))
			{
				ManifestError(SR.Format(SR.EventSource_UndefinedOpcode, opcode, eventName), runtimeCritical: true);
				ret = null;
			}
			return ret;
		}
		}
	}

	private string GetKeywords(ulong keywords, string eventName)
	{
		string ret = "";
		for (ulong bit = 1uL; bit != 0; bit <<= 1)
		{
			if ((keywords & bit) != 0)
			{
				string keyword = null;
				if ((keywordTab == null || !keywordTab.TryGetValue(bit, out keyword)) && bit >= 281474976710656L)
				{
					keyword = string.Empty;
				}
				if (keyword == null)
				{
					ManifestError(SR.Format(SR.EventSource_UndefinedKeyword, "0x" + bit.ToString("x", CultureInfo.CurrentCulture), eventName), runtimeCritical: true);
					keyword = string.Empty;
				}
				if (ret.Length != 0 && keyword.Length != 0)
				{
					ret += " ";
				}
				ret += keyword;
			}
		}
		return ret;
	}

	private string GetTypeName(Type type)
	{
		if (type.IsEnum())
		{
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			string typeName = GetTypeName(fields[0].FieldType);
			return typeName.Replace("win:Int", "win:UInt");
		}
		switch (type.GetTypeCode())
		{
		case TypeCode.Boolean:
			return "win:Boolean";
		case TypeCode.Byte:
			return "win:UInt8";
		case TypeCode.Char:
		case TypeCode.UInt16:
			return "win:UInt16";
		case TypeCode.UInt32:
			return "win:UInt32";
		case TypeCode.UInt64:
			return "win:UInt64";
		case TypeCode.SByte:
			return "win:Int8";
		case TypeCode.Int16:
			return "win:Int16";
		case TypeCode.Int32:
			return "win:Int32";
		case TypeCode.Int64:
			return "win:Int64";
		case TypeCode.String:
			return "win:UnicodeString";
		case TypeCode.Single:
			return "win:Float";
		case TypeCode.Double:
			return "win:Double";
		case TypeCode.DateTime:
			return "win:FILETIME";
		default:
			if (type == typeof(Guid))
			{
				return "win:GUID";
			}
			if (type == typeof(IntPtr))
			{
				return "win:Pointer";
			}
			if ((type.IsArray || type.IsPointer) && type.GetElementType() == typeof(byte))
			{
				return "win:Binary";
			}
			ManifestError(SR.Format(SR.EventSource_UnsupportedEventTypeInManifest, type.Name), runtimeCritical: true);
			return string.Empty;
		}
	}

	private static void UpdateStringBuilder([NotNull] ref StringBuilder? stringBuilder, string eventMessage, int startIndex, int count)
	{
		if (stringBuilder == null)
		{
			stringBuilder = new StringBuilder();
		}
		stringBuilder.Append(eventMessage, startIndex, count);
	}

	private string TranslateToManifestConvention(string eventMessage, string evtName)
	{
		StringBuilder stringBuilder = null;
		int writtenSoFar = 0;
		int chIdx = -1;
		int i = 0;
		while (i < eventMessage.Length)
		{
			if (eventMessage[i] == '%')
			{
				UpdateStringBuilder(ref stringBuilder, eventMessage, writtenSoFar, i - writtenSoFar);
				stringBuilder.Append("%%");
				i++;
				writtenSoFar = i;
			}
			else if (i < eventMessage.Length - 1 && ((eventMessage[i] == '{' && eventMessage[i + 1] == '{') || (eventMessage[i] == '}' && eventMessage[i + 1] == '}')))
			{
				UpdateStringBuilder(ref stringBuilder, eventMessage, writtenSoFar, i - writtenSoFar);
				stringBuilder.Append(eventMessage[i]);
				i++;
				i++;
				writtenSoFar = i;
			}
			else if (eventMessage[i] == '{')
			{
				int leftBracket = i;
				i++;
				int argNum = 0;
				for (; i < eventMessage.Length && char.IsDigit(eventMessage[i]); i++)
				{
					argNum = argNum * 10 + eventMessage[i] - 48;
				}
				if (i < eventMessage.Length && eventMessage[i] == '}')
				{
					i++;
					UpdateStringBuilder(ref stringBuilder, eventMessage, writtenSoFar, leftBracket - writtenSoFar);
					int manIndex = TranslateIndexToManifestConvention(argNum, evtName);
					stringBuilder.Append('%').Append(manIndex);
					if (i < eventMessage.Length && eventMessage[i] == '!')
					{
						i++;
						stringBuilder.Append("%!");
					}
					writtenSoFar = i;
				}
				else
				{
					ManifestError(SR.Format(SR.EventSource_UnsupportedMessageProperty, evtName, eventMessage));
				}
			}
			else if ((chIdx = "&<>'\"\r\n\t".IndexOf(eventMessage[i])) >= 0)
			{
				UpdateStringBuilder(ref stringBuilder, eventMessage, writtenSoFar, i - writtenSoFar);
				i++;
				stringBuilder.Append(s_escapes[chIdx]);
				writtenSoFar = i;
			}
			else
			{
				i++;
			}
		}
		if (stringBuilder == null)
		{
			return eventMessage;
		}
		UpdateStringBuilder(ref stringBuilder, eventMessage, writtenSoFar, i - writtenSoFar);
		return stringBuilder.ToString();
	}

	private int TranslateIndexToManifestConvention(int idx, string evtName)
	{
		if (perEventByteArrayArgIndices.TryGetValue(evtName, out List<int> byteArrArgIndices))
		{
			foreach (int byArrIdx in byteArrArgIndices)
			{
				if (idx >= byArrIdx)
				{
					idx++;
					continue;
				}
				break;
			}
		}
		return idx + 1;
	}
}

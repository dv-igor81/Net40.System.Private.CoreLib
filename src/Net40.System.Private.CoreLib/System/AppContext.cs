using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Threading;

namespace System;

public static class AppContext
{
	private static readonly Dictionary<string, object> s_dataStore = new Dictionary<string, object>();

	private static Dictionary<string, bool> s_switches;

	private static string s_defaultBaseDirectory;

	public static string BaseDirectory => ((string)GetData("APP_CONTEXT_BASE_DIRECTORY")) ?? s_defaultBaseDirectory ?? (s_defaultBaseDirectory = GetBaseDirectoryCore());

	public static string? TargetFrameworkName
	{
		get
		{
			Assembly entryAssembly = Assembly.GetEntryAssembly();
			return ((object)entryAssembly == null) ? null : CustomAttributeExtensions.GetCustomAttribute<TargetFrameworkAttribute>(entryAssembly)?.FrameworkName;
		}
	}

	public static event UnhandledExceptionEventHandler? UnhandledException;

	public static event EventHandler<FirstChanceExceptionEventArgs>? FirstChanceException;

	public static event EventHandler? ProcessExit;

	internal unsafe static void Setup(char** pNames, char** pValues, int count)
	{
		for (int i = 0; i < count; i++)
		{
			s_dataStore.Add(new string(pNames[i]), new string(pValues[i]));
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static bool IsDirectorySeparator(char c)
	{
		if (c != '\\')
		{
			return c == '/';
		}
		return true;
	}

	public static bool EndsInDirectorySeparator(string path)
	{
		if (path != null && path.Length > 0)
		{
			return IsDirectorySeparator(path[path.Length - 1]);
		}
		return false;
	}

	private static string GetBaseDirectoryCore()
	{
		string text = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
		if (text != null && !PathEx.EndsInDirectorySeparator(text))
		{
			text += "\\";
		}
		return text ?? string.Empty;
	}

	public static object? GetData(string name)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		lock (s_dataStore)
		{
			s_dataStore.TryGetValue(name, out object value);
			return value;
		}
	}

	public static void SetData(string name, object? data)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		lock (s_dataStore)
		{
			s_dataStore[name] = data;
		}
	}

	internal static void OnProcessExit()
	{
		AppContext.ProcessExit?.Invoke(AppDomain.CurrentDomain, EventArgs.Empty);
	}

	public static bool TryGetSwitch(string switchName, out bool isEnabled)
	{
		if (switchName == null)
		{
			throw new ArgumentNullException("switchName");
		}
		if (switchName.Length == 0)
		{
			throw new ArgumentException("SR.Argument_EmptyName", "switchName");
		}
		if (s_switches != null)
		{
			lock (s_switches)
			{
				if (s_switches.TryGetValue(switchName, out isEnabled))
				{
					return true;
				}
			}
		}
		if (GetData(switchName) is string value && bool.TryParse(value, out isEnabled))
		{
			return true;
		}
		isEnabled = false;
		return false;
	}

	public static void SetSwitch(string switchName, bool isEnabled)
	{
		if (switchName == null)
		{
			throw new ArgumentNullException("switchName");
		}
		if (switchName.Length == 0)
		{
			throw new ArgumentException("SR.Argument_EmptyName", "switchName");
		}
		if (s_switches == null)
		{
			Interlocked.CompareExchange(ref s_switches, new Dictionary<string, bool>(), null);
		}
		lock (s_switches)
		{
			s_switches[switchName] = isEnabled;
		}
	}
}

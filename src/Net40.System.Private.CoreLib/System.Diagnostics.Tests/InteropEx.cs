using System.Runtime.InteropServices;

namespace System.Diagnostics.Tests;

public class InteropEx
{
	[StructLayout(LayoutKind.Sequential, Size = 40)]
	public struct PROCESS_MEMORY_COUNTERS
	{
		public uint cb;

		public uint PageFaultCount;

		public uint PeakWorkingSetSize;

		public uint WorkingSetSize;

		public uint QuotaPeakPagedPoolUsage;

		public uint QuotaPagedPoolUsage;

		public uint QuotaPeakNonPagedPoolUsage;

		public uint QuotaNonPagedPoolUsage;

		public uint PagefileUsage;

		public uint PeakPagefileUsage;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct USER_INFO_1
	{
		public string usri1_name;

		public string usri1_password;

		public uint usri1_password_age;

		public uint usri1_priv;

		public string usri1_home_dir;

		public string usri1_comment;

		public uint usri1_flags;

		public string usri1_script_path;
	}

	public struct TOKEN_USER
	{
		public SID_AND_ATTRIBUTES User;
	}

	public struct SID_AND_ATTRIBUTES
	{
		public IntPtr Sid;

		public int Attributes;
	}

	[DllImport("kernel32.dll")]
	public static extern int GetCurrentProcessId();
}

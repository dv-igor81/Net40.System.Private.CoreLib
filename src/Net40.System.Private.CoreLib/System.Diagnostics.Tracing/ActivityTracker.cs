#define DEBUG
using System.Threading;

namespace System.Diagnostics.Tracing;

internal class ActivityTracker
{
	private class ActivityInfo
	{
		private enum NumberListCodes : byte
		{
			End = 0,
			LastImmediateValue = 10,
			PrefixCode = 11,
			MultiByte1 = 12
		}

		internal readonly string m_name;

		private readonly long m_uniqueId;

		internal readonly Guid m_guid;

		internal readonly int m_activityPathGuidOffset;

		internal readonly int m_level;

		internal readonly EventActivityOptions m_eventOptions;

		internal long m_lastChildID;

		internal int m_stopped;

		internal readonly ActivityInfo? m_creator;

		internal readonly Guid m_activityIdToRestore;

		public Guid ActivityId => m_guid;

		public ActivityInfo(string name, long uniqueId, ActivityInfo? creator, Guid activityIDToRestore, EventActivityOptions options)
		{
			m_name = name;
			m_eventOptions = options;
			m_creator = creator;
			m_uniqueId = uniqueId;
			m_level = ((creator != null) ? (creator.m_level + 1) : 0);
			m_activityIdToRestore = activityIDToRestore;
			CreateActivityPathGuid(out m_guid, out m_activityPathGuidOffset);
		}

		public static string Path(ActivityInfo? activityInfo)
		{
			if (activityInfo == null)
			{
				return "";
			}
			return Path(activityInfo.m_creator) + "/" + activityInfo.m_uniqueId;
		}

		public override string ToString()
		{
			return m_name + "(" + Path(this) + ((m_stopped != 0) ? ",DEAD)" : ")");
		}

		public static string LiveActivities(ActivityInfo? list)
		{
			if (list == null)
			{
				return "";
			}
			return list.ToString() + ";" + LiveActivities(list.m_creator);
		}

		public bool CanBeOrphan()
		{
			if ((m_eventOptions & EventActivityOptions.Detachable) != 0)
			{
				return true;
			}
			return false;
		}

		private unsafe void CreateActivityPathGuid(out Guid idRet, out int activityPathGuidOffset)
		{
			fixed (Guid* outPtr = &idRet)
			{
				int activityPathGuidOffsetStart = 0;
				if (m_creator != null)
				{
					activityPathGuidOffsetStart = m_creator.m_activityPathGuidOffset;
					idRet = m_creator.m_guid;
				}
				else
				{
					int appDomainID = 0;
					appDomainID = Thread.GetDomainID();
					activityPathGuidOffsetStart = AddIdToGuid(outPtr, activityPathGuidOffsetStart, (uint)appDomainID);
				}
				activityPathGuidOffset = AddIdToGuid(outPtr, activityPathGuidOffsetStart, (uint)m_uniqueId);
				if (12 < activityPathGuidOffset)
				{
					CreateOverflowGuid(outPtr);
				}
			}
		}

		private unsafe void CreateOverflowGuid(Guid* outPtr)
		{
			for (ActivityInfo ancestor = m_creator; ancestor != null; ancestor = ancestor.m_creator)
			{
				if (ancestor.m_activityPathGuidOffset <= 10)
				{
					uint id = (uint)Interlocked.Increment(ref ancestor.m_lastChildID);
					*outPtr = ancestor.m_guid;
					int endId = AddIdToGuid(outPtr, ancestor.m_activityPathGuidOffset, id, overflow: true);
					if (endId <= 12)
					{
						break;
					}
				}
			}
		}

		private static unsafe int AddIdToGuid(Guid* outPtr, int whereToAddId, uint id, bool overflow = false)
		{
			byte* ptr = (byte*)outPtr;
			byte* endPtr = ptr + 12;
			ptr += whereToAddId;
			if (endPtr <= ptr)
			{
				return 13;
			}
			if (0 < id && id <= 10 && !overflow)
			{
				WriteNibble(ref ptr, endPtr, id);
			}
			else
			{
				uint len = 4u;
				if (id <= 255)
				{
					len = 1u;
				}
				else if (id <= 65535)
				{
					len = 2u;
				}
				else if (id <= 16777215)
				{
					len = 3u;
				}
				if (overflow)
				{
					if (endPtr <= ptr + 2)
					{
						return 13;
					}
					WriteNibble(ref ptr, endPtr, 11u);
				}
				WriteNibble(ref ptr, endPtr, 12 + (len - 1));
				if (ptr < endPtr && *ptr != 0)
				{
					if (id < 4096)
					{
						*ptr = (byte)(192 + (id >> 8));
						id &= 0xFFu;
					}
					ptr++;
				}
				while (0 < len)
				{
					if (endPtr <= ptr)
					{
						ptr++;
						break;
					}
					*(ptr++) = (byte)id;
					id >>= 8;
					len--;
				}
			}
			*(uint*)((byte*)outPtr + (nint)3 * (nint)4) = (*(uint*)outPtr + *(uint*)((byte*)outPtr + 4) + *(uint*)((byte*)outPtr + (nint)2 * (nint)4) + 1503500717) ^ EventSource.s_currentPid;
			return (int)(ptr - (byte*)outPtr);
		}

		private static unsafe void WriteNibble(ref byte* ptr, byte* endPtr, uint value)
		{
			Debug.Assert(value < 16);
			Debug.Assert(ptr < endPtr);
			if (*ptr != 0)
			{
				byte* intPtr = ptr++;
				*intPtr |= (byte)value;
			}
			else
			{
				*ptr = (byte)(value << 4);
			}
		}
	}

	private AsyncLocal<ActivityInfo?>? m_current;

	private bool m_checkedForEnable;

	private static System.Diagnostics.Tracing.ActivityTracker s_activityTrackerInstance = new System.Diagnostics.Tracing.ActivityTracker();

	private static long m_nextId = 0L;

	private const ushort MAX_ACTIVITY_DEPTH = 100;

	public static System.Diagnostics.Tracing.ActivityTracker Instance => s_activityTrackerInstance;

	public void OnStart(string providerName, string activityName, int task, ref Guid activityId, ref Guid relatedActivityId, EventActivityOptions options)
	{
		if (m_current == null)
		{
			if (m_checkedForEnable)
			{
				return;
			}
			m_checkedForEnable = true;
			if (m_current == null)
			{
				return;
			}
		}
		Debug.Assert((options & EventActivityOptions.Disable) == 0);
		ActivityInfo currentActivity = m_current.Value;
		string fullActivityName = NormalizeActivityName(providerName, activityName, task);
		if (currentActivity != null)
		{
			if (currentActivity.m_level >= 100)
			{
				activityId = Guid.Empty;
				relatedActivityId = Guid.Empty;
				return;
			}
			if ((options & EventActivityOptions.Recursive) == 0)
			{
				ActivityInfo existingActivity = FindActiveActivity(fullActivityName, currentActivity);
				if (existingActivity != null)
				{
					OnStop(providerName, activityName, task, ref activityId);
					currentActivity = m_current.Value;
				}
			}
		}
		long id = ((currentActivity != null) ? Interlocked.Increment(ref currentActivity.m_lastChildID) : Interlocked.Increment(ref m_nextId));
		relatedActivityId = EventSource.CurrentThreadActivityId;
		ActivityInfo newActivity = new ActivityInfo(fullActivityName, id, currentActivity, relatedActivityId, options);
		m_current.Value = newActivity;
		activityId = newActivity.ActivityId;
	}

	public void OnStop(string providerName, string activityName, int task, ref Guid activityId)
	{
		if (m_current == null)
		{
			return;
		}
		string fullActivityName = NormalizeActivityName(providerName, activityName, task);
		ActivityInfo newCurrentActivity;
		ActivityInfo activityToStop;
		do
		{
			ActivityInfo currentActivity = m_current.Value;
			newCurrentActivity = null;
			activityToStop = FindActiveActivity(fullActivityName, currentActivity);
			if (activityToStop == null)
			{
				activityId = Guid.Empty;
				return;
			}
			activityId = activityToStop.ActivityId;
			ActivityInfo orphan = currentActivity;
			while (orphan != activityToStop && orphan != null)
			{
				if (orphan.m_stopped != 0)
				{
					orphan = orphan.m_creator;
					continue;
				}
				if (orphan.CanBeOrphan())
				{
					if (newCurrentActivity == null)
					{
						newCurrentActivity = orphan;
					}
				}
				else
				{
					orphan.m_stopped = 1;
					Debug.Assert(orphan.m_stopped != 0);
				}
				orphan = orphan.m_creator;
			}
		}
		while (Interlocked.CompareExchange(ref activityToStop.m_stopped, 1, 0) != 0);
		if (newCurrentActivity == null)
		{
			newCurrentActivity = activityToStop.m_creator;
		}
		m_current.Value = newCurrentActivity;
	}

	public void Enable()
	{
		if (m_current == null)
		{
			try
			{
				m_current = new AsyncLocal<ActivityInfo>(ActivityChanging);
			}
			catch (NotImplementedException)
			{
				Debugger.Log(0, null, "Activity Enabled() called but AsyncLocals Not Supported (pre V4.6).  Ignoring Enable");
			}
		}
	}

	private ActivityInfo? FindActiveActivity(string name, ActivityInfo? startLocation)
	{
		for (ActivityInfo activity = startLocation; activity != null; activity = activity.m_creator)
		{
			if (name == activity.m_name && activity.m_stopped == 0)
			{
				return activity;
			}
		}
		return null;
	}

	private string NormalizeActivityName(string providerName, string activityName, int task)
	{
		if (activityName.EndsWith("Start", StringComparison.Ordinal))
		{
			return providerName + activityName.Substring(0, activityName.Length - "Start".Length);
		}
		if (activityName.EndsWith("Stop", StringComparison.Ordinal))
		{
			return providerName + activityName.Substring(0, activityName.Length - "Stop".Length);
		}
		if (task != 0)
		{
			return providerName + "task" + task;
		}
		return providerName + activityName;
	}

	private void ActivityChanging(AsyncLocalValueChangedArgs<ActivityInfo?> args)
	{
		ActivityInfo cur = args.CurrentValue;
		ActivityInfo prev = args.PreviousValue;
		if (prev == null || prev.m_creator != cur || (cur != null && !(prev.m_activityIdToRestore != cur.ActivityId)))
		{
			while (cur != null && cur.m_stopped != 0)
			{
				cur = cur.m_creator;
			}
		}
	}
}

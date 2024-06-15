using System.Collections.Generic;
using System.Threading;

namespace System.Diagnostics.Tracing;

internal class EventCounterGroup
{
	[EventData]
	private class PayloadType
	{
		public EventCounterPayload Payload { get; set; }

		public PayloadType(EventCounterPayload payload)
		{
			Payload = payload;
		}
	}

	private readonly EventSource _eventSource;

	private readonly List<EventCounter> _eventCounters;

	private static WeakReference<EventCounterGroup>[] s_eventCounterGroups;

	private static readonly object s_eventCounterGroupsLock = new object();

	private DateTime _timeStampSinceCollectionStarted;

	private int _pollingIntervalInMilliseconds;

	private Timer _pollingTimer;

	internal EventCounterGroup(EventSource eventSource)
	{
		_eventSource = eventSource;
		_eventCounters = new List<EventCounter>();
		RegisterCommandCallback();
	}

	internal void Add(EventCounter eventCounter)
	{
		lock (this)
		{
			_eventCounters.Add(eventCounter);
		}
	}

	internal void Remove(EventCounter eventCounter)
	{
		lock (this)
		{
			_eventCounters.Remove(eventCounter);
		}
	}

	private void RegisterCommandCallback()
	{
		_eventSource.EventCommandExecuted += OnEventSourceCommand;
	}

	private void OnEventSourceCommand(object sender, EventCommandEventArgs e)
	{
		if ((e.Command == EventCommand.Enable || e.Command == EventCommand.Update) && e.Arguments.TryGetValue("EventCounterIntervalSec", out var value) && float.TryParse(value, out var result))
		{
			lock (this)
			{
				EnableTimer(result);
			}
		}
	}

	private static void EnsureEventSourceIndexAvailable(int eventSourceIndex)
	{
		if (s_eventCounterGroups == null)
		{
			s_eventCounterGroups = new WeakReference<EventCounterGroup>[eventSourceIndex + 1];
		}
		else if (eventSourceIndex >= s_eventCounterGroups.Length)
		{
			WeakReference<EventCounterGroup>[] destinationArray = new WeakReference<EventCounterGroup>[eventSourceIndex + 1];
			Array.Copy(s_eventCounterGroups, 0, destinationArray, 0, s_eventCounterGroups.Length);
			s_eventCounterGroups = destinationArray;
		}
	}

	internal static EventCounterGroup GetEventCounterGroup(EventSource eventSource)
	{
		lock (s_eventCounterGroupsLock)
		{
			int num = EventListener.EventSourceIndex(eventSource);
			EnsureEventSourceIndexAvailable(num);
			WeakReference<EventCounterGroup> weakReference = s_eventCounterGroups[num];
			EventCounterGroup target = null;
			if (weakReference == null || !weakReference.TryGetTarget(out target))
			{
				target = new EventCounterGroup(eventSource);
				s_eventCounterGroups[num] = new WeakReference<EventCounterGroup>(target);
			}
			return target;
		}
	}

	private void DisposeTimer()
	{
		if (_pollingTimer != null)
		{
			_pollingTimer.Dispose();
			_pollingTimer = null;
		}
	}

	private void EnableTimer(float pollingIntervalInSeconds)
	{
		if (pollingIntervalInSeconds <= 0f)
		{
			DisposeTimer();
			_pollingIntervalInMilliseconds = 0;
		}
		else if (_pollingIntervalInMilliseconds == 0 || pollingIntervalInSeconds * 1000f < (float)_pollingIntervalInMilliseconds)
		{
			_pollingIntervalInMilliseconds = (int)(pollingIntervalInSeconds * 1000f);
			DisposeTimer();
			_timeStampSinceCollectionStarted = DateTime.UtcNow;
			bool flag = false;
			try
			{
				if (!ExecutionContext.IsFlowSuppressed())
				{
					ExecutionContext.SuppressFlow();
					flag = true;
				}
				_pollingTimer = new Timer(delegate(object s)
				{
					((EventCounterGroup)s).OnTimer(null);
				}, this, _pollingIntervalInMilliseconds, _pollingIntervalInMilliseconds);
			}
			finally
			{
				if (flag)
				{
					ExecutionContext.RestoreFlow();
				}
			}
		}
		OnTimer(null);
	}

	private void OnTimer(object state)
	{
		lock (this)
		{
			if (_eventSource.IsEnabled())
			{
				DateTime utcNow = DateTime.UtcNow;
				TimeSpan timeSpan = utcNow - _timeStampSinceCollectionStarted;
				foreach (EventCounter eventCounter in _eventCounters)
				{
					EventCounterPayload eventCounterPayload = eventCounter.GetEventCounterPayload();
					eventCounterPayload.IntervalSec = (float)timeSpan.TotalSeconds;
					_eventSource.Write("EventCounters", new EventSourceOptions
					{
						Level = EventLevel.LogAlways
					}, new PayloadType(eventCounterPayload));
				}
				_timeStampSinceCollectionStarted = utcNow;
			}
			else
			{
				DisposeTimer();
			}
		}
	}
}
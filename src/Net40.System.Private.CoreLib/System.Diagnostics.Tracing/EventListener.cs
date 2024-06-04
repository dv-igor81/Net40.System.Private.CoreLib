#define DEBUG
using System.Collections.Generic;
using System.Threading;

namespace System.Diagnostics.Tracing;

public class EventListener : IDisposable
{
	internal volatile EventListener? m_Next;

	internal static EventListener? s_Listeners;

	internal static List<WeakReference>? s_EventSources;

	private static bool s_CreatingListener;

	[ThreadStatic]
	private static bool s_ConnectingEventSourcesAndListener;

	private static bool s_EventSourceShutdownRegistered;

	internal static object EventListenersLock
	{
		get
		{
			if (s_EventSources == null)
			{
				Interlocked.CompareExchange(ref s_EventSources, new List<WeakReference>(2), null);
			}
			return s_EventSources;
		}
	}

	private event EventHandler<EventSourceCreatedEventArgs>? _EventSourceCreated;

	public event EventHandler<EventSourceCreatedEventArgs>? EventSourceCreated
	{
		add
		{
			CallBackForExistingEventSources(addToListenersList: false, value);
			this._EventSourceCreated = (EventHandler<EventSourceCreatedEventArgs>)Delegate.Combine(this._EventSourceCreated, value);
		}
		remove
		{
			this._EventSourceCreated = (EventHandler<EventSourceCreatedEventArgs>)Delegate.Remove(this._EventSourceCreated, value);
		}
	}

	public event EventHandler<EventWrittenEventArgs>? EventWritten;

	static EventListener()
	{
		s_CreatingListener = false;
		s_ConnectingEventSourcesAndListener = false;
		s_EventSourceShutdownRegistered = false;
	}

	public EventListener()
	{
		CallBackForExistingEventSources(addToListenersList: true, delegate(object obj, EventSourceCreatedEventArgs args)
		{
			args.EventSource.AddListener((EventListener)obj);
		});
	}

	public virtual void Dispose()
	{
		lock (EventListenersLock)
		{
			if (s_Listeners != null)
			{
				if (this == s_Listeners)
				{
					EventListener cur = s_Listeners;
					s_Listeners = m_Next;
					RemoveReferencesToListenerInEventSources(cur);
				}
				else
				{
					EventListener prev = s_Listeners;
					while (true)
					{
						EventListener cur2 = prev.m_Next;
						if (cur2 == null)
						{
							break;
						}
						if (cur2 == this)
						{
							prev.m_Next = cur2.m_Next;
							RemoveReferencesToListenerInEventSources(cur2);
							break;
						}
						prev = cur2;
					}
				}
			}
			Validate();
		}
	}

	public void EnableEvents(EventSource eventSource, EventLevel level)
	{
		EnableEvents(eventSource, level, EventKeywords.None);
	}

	public void EnableEvents(EventSource eventSource, EventLevel level, EventKeywords matchAnyKeyword)
	{
		EnableEvents(eventSource, level, matchAnyKeyword, null);
	}

	public void EnableEvents(EventSource eventSource, EventLevel level, EventKeywords matchAnyKeyword, IDictionary<string, string?>? arguments)
	{
		if (eventSource == null)
		{
			throw new ArgumentNullException("eventSource");
		}
		eventSource.SendCommand(this, EventProviderType.None, 0, 0, EventCommand.Update, enable: true, level, matchAnyKeyword, arguments);
	}

	public void DisableEvents(EventSource eventSource)
	{
		if (eventSource == null)
		{
			throw new ArgumentNullException("eventSource");
		}
		eventSource.SendCommand(this, EventProviderType.None, 0, 0, EventCommand.Update, enable: false, EventLevel.LogAlways, EventKeywords.None, null);
	}

	public static int EventSourceIndex(EventSource eventSource)
	{
		return eventSource.m_id;
	}

	protected internal virtual void OnEventSourceCreated(EventSource eventSource)
	{
		EventHandler<EventSourceCreatedEventArgs> callBack = this._EventSourceCreated;
		if (callBack != null)
		{
			EventSourceCreatedEventArgs args = new EventSourceCreatedEventArgs();
			args.EventSource = eventSource;
			callBack(this, args);
		}
	}

	protected internal virtual void OnEventWritten(EventWrittenEventArgs eventData)
	{
		this.EventWritten?.Invoke(this, eventData);
	}

	internal static void AddEventSource(EventSource newEventSource)
	{
		lock (EventListenersLock)
		{
			if (s_EventSources == null)
			{
				s_EventSources = new List<WeakReference>(2);
			}
			if (!s_EventSourceShutdownRegistered)
			{
				s_EventSourceShutdownRegistered = true;
				AppContext.ProcessExit += DisposeOnShutdown;
			}
			int newIndex = -1;
			if (s_EventSources.Count % 64 == 63)
			{
				int i = s_EventSources.Count;
				while (0 < i)
				{
					i--;
					WeakReference weakRef = s_EventSources[i];
					if (!weakRef.IsAlive)
					{
						newIndex = i;
						weakRef.Target = newEventSource;
						break;
					}
				}
			}
			if (newIndex < 0)
			{
				newIndex = s_EventSources.Count;
				s_EventSources.Add(new WeakReference(newEventSource));
			}
			newEventSource.m_id = newIndex;
			bool previousValue = s_ConnectingEventSourcesAndListener;
			s_ConnectingEventSourcesAndListener = true;
			try
			{
				for (EventListener listener = s_Listeners; listener != null; listener = listener.m_Next)
				{
					newEventSource.AddListener(listener);
				}
			}
			finally
			{
				s_ConnectingEventSourcesAndListener = previousValue;
			}
			Validate();
		}
	}

	private static void DisposeOnShutdown(object? sender, EventArgs e)
	{
		lock (EventListenersLock)
		{
			Debug.Assert(s_EventSources != null);
			foreach (WeakReference esRef in s_EventSources)
			{
				if (esRef.Target is EventSource es)
				{
					es.Dispose();
				}
			}
		}
	}

	private static void RemoveReferencesToListenerInEventSources(EventListener listenerToRemove)
	{
		Debug.Assert(s_EventSources != null);
		foreach (WeakReference eventSourceRef in s_EventSources)
		{
			if (!(eventSourceRef.Target is EventSource eventSource))
			{
				continue;
			}
			Debug.Assert(eventSource.m_Dispatchers != null);
			if (eventSource.m_Dispatchers.m_Listener == listenerToRemove)
			{
				eventSource.m_Dispatchers = eventSource.m_Dispatchers.m_Next;
				continue;
			}
			System.Diagnostics.Tracing.EventDispatcher prev = eventSource.m_Dispatchers;
			while (true)
			{
				System.Diagnostics.Tracing.EventDispatcher cur = prev.m_Next;
				if (cur == null)
				{
					Debug.Fail("EventSource did not have a registered EventListener!");
					break;
				}
				if (cur.m_Listener == listenerToRemove)
				{
					prev.m_Next = cur.m_Next;
					break;
				}
				prev = cur;
			}
		}
	}

	[Conditional("DEBUG")]
	internal static void Validate()
	{
		if (s_ConnectingEventSourcesAndListener)
		{
			return;
		}
		lock (EventListenersLock)
		{
			Debug.Assert(s_EventSources != null);
			Dictionary<EventListener, bool> allListeners = new Dictionary<EventListener, bool>();
			for (EventListener cur = s_Listeners; cur != null; cur = cur.m_Next)
			{
				allListeners.Add(cur, value: true);
			}
			int id = -1;
			foreach (WeakReference eventSourceRef in s_EventSources)
			{
				id++;
				if (!(eventSourceRef.Target is EventSource eventSource))
				{
					continue;
				}
				Debug.Assert(eventSource.m_id == id, "Unexpected event source ID.");
				for (System.Diagnostics.Tracing.EventDispatcher dispatcher = eventSource.m_Dispatchers; dispatcher != null; dispatcher = dispatcher.m_Next)
				{
					Debug.Assert(allListeners.ContainsKey(dispatcher.m_Listener), "EventSource has a listener not on the global list.");
				}
				foreach (EventListener listener in allListeners.Keys)
				{
					System.Diagnostics.Tracing.EventDispatcher dispatcher = eventSource.m_Dispatchers;
					while (true)
					{
						Debug.Assert(dispatcher != null, "Listener is not on all eventSources.");
						if (dispatcher.m_Listener == listener)
						{
							break;
						}
						dispatcher = dispatcher.m_Next;
					}
				}
			}
		}
	}

	private void CallBackForExistingEventSources(bool addToListenersList, EventHandler<EventSourceCreatedEventArgs>? callback)
	{
		lock (EventListenersLock)
		{
			Debug.Assert(s_EventSources != null);
			if (s_CreatingListener)
			{
				throw new InvalidOperationException(SR.EventSource_ListenerCreatedInsideCallback);
			}
			try
			{
				s_CreatingListener = true;
				if (addToListenersList)
				{
					m_Next = s_Listeners;
					s_Listeners = this;
				}
				if (callback != null)
				{
					WeakReference[] eventSourcesSnapshot = s_EventSources.ToArray();
					bool previousValue = s_ConnectingEventSourcesAndListener;
					s_ConnectingEventSourcesAndListener = true;
					try
					{
						foreach (WeakReference eventSourceRef in eventSourcesSnapshot)
						{
							if (eventSourceRef.Target is EventSource eventSource)
							{
								EventSourceCreatedEventArgs args = new EventSourceCreatedEventArgs();
								args.EventSource = eventSource;
								callback(this, args);
							}
						}
					}
					finally
					{
						s_ConnectingEventSourcesAndListener = previousValue;
					}
				}
				Validate();
			}
			finally
			{
				s_CreatingListener = false;
			}
		}
	}
}

#define DEBUG
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;

namespace System.Diagnostics.Tracing;

public class EventWrittenEventArgs : EventArgs
{
	private string? m_message;

	private string? m_eventName;

	private EventSource m_eventSource;

	private ReadOnlyCollection<string>? m_payloadNames;

	private Guid m_activityId;

	private long? m_osThreadId;

	internal EventTags m_tags;

	internal EventOpcode m_opcode;

	internal EventLevel m_level;

	internal EventKeywords m_keywords;

	public string? EventName
	{
		get
		{
			if (m_eventName != null || EventId < 0)
			{
				return m_eventName;
			}
			Debug.Assert(m_eventSource.m_eventData != null);
			return m_eventSource.m_eventData[EventId].Name;
		}
		internal set
		{
			m_eventName = value;
		}
	}

	public int EventId { get; internal set; }

	public Guid ActivityId
	{
		get
		{
			Guid activityId = m_activityId;
			if (activityId == Guid.Empty)
			{
				//activityId = System.Diagnostics.Tracing.EventSource.CurrentThreadActivityId;
			}
			return activityId;
		}
		internal set
		{
			m_activityId = value;
		}
	}

	public Guid RelatedActivityId { get; internal set; }

	public ReadOnlyCollection<object?>? Payload { get; internal set; }

	public ReadOnlyCollection<string>? PayloadNames
	{
		get
		{
			if (EventId >= 0 && m_payloadNames == null)
			{
				List<string> names = new List<string>();
				Debug.Assert(m_eventSource.m_eventData != null);
				ParameterInfo[] parameters = m_eventSource.m_eventData[EventId].Parameters;
				foreach (ParameterInfo parameter in parameters)
				{
					names.Add(parameter.Name);
				}
				m_payloadNames = new ReadOnlyCollection<string>(names);
			}
			return m_payloadNames;
		}
		internal set
		{
			m_payloadNames = value;
		}
	}

	public EventSource EventSource => m_eventSource;

	public EventKeywords Keywords
	{
		get
		{
			if (EventId < 0)
			{
				return m_keywords;
			}
			Debug.Assert(m_eventSource.m_eventData != null);
			return (EventKeywords)m_eventSource.m_eventData[EventId].Descriptor.Keywords;
		}
	}

	public EventOpcode Opcode
	{
		get
		{
			if (EventId <= 0)
			{
				return m_opcode;
			}
			Debug.Assert(m_eventSource.m_eventData != null);
			return (EventOpcode)m_eventSource.m_eventData[EventId].Descriptor.Opcode;
		}
	}

	public EventTask Task
	{
		get
		{
			if (EventId <= 0)
			{
				return EventTask.None;
			}
			Debug.Assert(m_eventSource.m_eventData != null);
			return (EventTask)m_eventSource.m_eventData[EventId].Descriptor.Task;
		}
	}

	public EventTags Tags
	{
		get
		{
			if (EventId <= 0)
			{
				return m_tags;
			}
			Debug.Assert(m_eventSource.m_eventData != null);
			return m_eventSource.m_eventData[EventId].Tags;
		}
	}

	public string? Message
	{
		get
		{
			if (EventId <= 0)
			{
				return m_message;
			}
			Debug.Assert(m_eventSource.m_eventData != null);
			return m_eventSource.m_eventData[EventId].Message;
		}
		internal set
		{
			m_message = value;
		}
	}

	public byte Version
	{
		get
		{
			if (EventId <= 0)
			{
				return 0;
			}
			Debug.Assert(m_eventSource.m_eventData != null);
			return m_eventSource.m_eventData[EventId].Descriptor.Version;
		}
	}

	public EventLevel Level
	{
		get
		{
			if (EventId <= 0)
			{
				return m_level;
			}
			Debug.Assert(m_eventSource.m_eventData != null);
			return (EventLevel)m_eventSource.m_eventData[EventId].Descriptor.Level;
		}
	}

	public long OSThreadId
	{
		get
		{
			if (!m_osThreadId.HasValue)
			{
				m_osThreadId = Thread.CurrentThread.ManagedThreadId;
			}
			return m_osThreadId.Value;
		}
		internal set
		{
			m_osThreadId = value;
		}
	}

	public DateTime TimeStamp { get; internal set; }

	internal EventWrittenEventArgs(EventSource eventSource)
	{
		m_eventSource = eventSource;
		TimeStamp = DateTime.UtcNow;
	}
}

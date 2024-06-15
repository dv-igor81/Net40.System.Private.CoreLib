﻿using System.Collections.Generic;
using System.Text;

namespace System.Diagnostics.Tracing;

public abstract class DiagnosticCounter : IDisposable
{
    private string _displayName = "";

    private string _displayUnits = "";

    private CounterGroup _group;

    private Dictionary<string, string> _metadata;

    public string DisplayName
    {
        get { return _displayName; }
        set
        {
            if (value == null)
            {
                throw new ArgumentException("Cannot set null as DisplayName");
            }

            _displayName = value;
        }
    }

    public string DisplayUnits
    {
        get { return _displayUnits; }
        set
        {
            if (value == null)
            {
                throw new ArgumentException("Cannot set null as DisplayUnits");
            }

            _displayUnits = value;
        }
    }

    public string Name { get; }

    public EventSource EventSource { get; }

    internal DiagnosticCounter(string name, EventSource eventSource)
    {
        if (name == null)
        {
            throw new ArgumentNullException("Name");
        }

        if (eventSource == null)
        {
            throw new ArgumentNullException("EventSource");
        }

        _group = CounterGroup.GetCounterGroup(eventSource);
        _group.Add(this);
        Name = name;
        DisplayUnits = string.Empty;
        EventSource = eventSource;
    }

    public void Dispose()
    {
        if (_group != null)
        {
            _group.Remove(this);
            _group = null;
        }
    }

    public void AddMetadata(string key, string? value)
    {
        lock (this)
        {
            if (_metadata == null)
            {
                _metadata = new Dictionary<string, string>();
            }

            _metadata.Add(key, value);
        }
    }

    internal abstract void WritePayload(float intervalSec, int pollingIntervalMillisec);

    internal void ReportOutOfBandMessage(string message)
    {
        EventSource.ReportOutOfBandMessage(message, flush: true);
    }

    internal string GetMetadataString()
    {
        if (_metadata == null)
        {
            return "";
        }

        Dictionary<string, string>.Enumerator enumerator = _metadata.GetEnumerator();
        bool flag = enumerator.MoveNext();
        KeyValuePair<string, string> current = enumerator.Current;
        if (!enumerator.MoveNext())
        {
            return current.Key + ":" + current.Value;
        }

        StringBuilder stringBuilder = new StringBuilder().Append(current.Key).Append(':').Append(current.Value);
        do
        {
            current = enumerator.Current;
            stringBuilder.Append(',').Append(current.Key).Append(':')
                .Append(current.Value);
        } while (enumerator.MoveNext());

        return stringBuilder.ToString();
    }
}
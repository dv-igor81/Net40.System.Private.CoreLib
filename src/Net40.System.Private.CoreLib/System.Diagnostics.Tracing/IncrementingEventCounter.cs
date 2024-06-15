namespace System.Diagnostics.Tracing;

public class IncrementingEventCounter : DiagnosticCounter
{
    private double _increment;

    private double _prevIncrement;

    public TimeSpan DisplayRateTimeScale { get; set; }

    public IncrementingEventCounter(string name, EventSource eventSource)
        : base(name, eventSource)
    {
    }

    public void Increment(double increment = 1.0)
    {
        lock (this)
        {
            _increment += increment;
        }
    }

    public override string ToString()
    {
        return $"IncrementingEventCounter '{base.Name}' Increment {_increment}";
    }

    internal override void WritePayload(float intervalSec, int pollingIntervalMillisec)
    {
        lock (this)
        {
            IncrementingCounterPayload incrementingCounterPayload = new IncrementingCounterPayload();
            incrementingCounterPayload.Name = base.Name;
            incrementingCounterPayload.IntervalSec = intervalSec;
            incrementingCounterPayload.DisplayName = base.DisplayName ?? "";
            incrementingCounterPayload.DisplayRateTimeScale = ((DisplayRateTimeScale == TimeSpan.Zero) ? "" : DisplayRateTimeScale.ToString("c"));
            incrementingCounterPayload.Series = $"Interval={pollingIntervalMillisec}";
            incrementingCounterPayload.CounterType = "Sum";
            incrementingCounterPayload.Metadata = GetMetadataString();
            incrementingCounterPayload.Increment = _increment - _prevIncrement;
            incrementingCounterPayload.DisplayUnits = base.DisplayUnits ?? "";
            _prevIncrement = _increment;
            base.EventSource.Write("EventCounters", new EventSourceOptions
            {
                Level = EventLevel.LogAlways
            }, new IncrementingEventCounterPayloadType(incrementingCounterPayload));
        }
    }

    internal void UpdateMetric()
    {
        lock (this)
        {
            _prevIncrement = _increment;
        }
    }
}

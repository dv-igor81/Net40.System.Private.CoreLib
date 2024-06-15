namespace System.Diagnostics.Tracing;

public class PollingCounter : DiagnosticCounter
{
    private Func<double> _metricProvider;

    private double _lastVal;

    public PollingCounter(string name, EventSource eventSource, Func<double> metricProvider)
        : base(name, eventSource)
    {
        if (metricProvider == null)
        {
            throw new ArgumentNullException("metricProvider");
        }
        _metricProvider = metricProvider;
    }

    public override string ToString()
    {
        return string.Format("PollingCounter '{0}' Count {1} Mean {2}", base.Name, 1, _lastVal.ToString("n3"));
    }

    internal override void WritePayload(float intervalSec, int pollingIntervalMillisec)
    {
        lock (this)
        {
            double num = 0.0;
            try
            {
                num = _metricProvider();
            }
            catch (Exception ex)
            {
                ReportOutOfBandMessage("ERROR: Exception during EventCounter " + base.Name + " metricProvider callback: " + ex.Message);
            }
            CounterPayload counterPayload = new CounterPayload();
            counterPayload.Name = base.Name;
            counterPayload.DisplayName = base.DisplayName ?? "";
            counterPayload.Count = 1;
            counterPayload.IntervalSec = intervalSec;
            counterPayload.Series = $"Interval={pollingIntervalMillisec}";
            counterPayload.CounterType = "Mean";
            counterPayload.Mean = num;
            counterPayload.Max = num;
            counterPayload.Min = num;
            counterPayload.Metadata = GetMetadataString();
            counterPayload.StandardDeviation = 0.0;
            counterPayload.DisplayUnits = base.DisplayUnits ?? "";
            _lastVal = num;
            base.EventSource.Write("EventCounters", new EventSourceOptions
            {
                Level = EventLevel.LogAlways
            }, new PollingPayloadType(counterPayload));
        }
    }
}
namespace System.Diagnostics.Tracing;

[EventData]
internal class PollingPayloadType
{
    public CounterPayload Payload { get; set; }

    public PollingPayloadType(CounterPayload payload)
    {
        Payload = payload;
    }
}
namespace System.Diagnostics.Tracing;

[EventData]
internal class IncrementingPollingCounterPayloadType
{
    public IncrementingCounterPayload Payload { get; set; }

    public IncrementingPollingCounterPayloadType(IncrementingCounterPayload payload)
    {
        Payload = payload;
    }
}

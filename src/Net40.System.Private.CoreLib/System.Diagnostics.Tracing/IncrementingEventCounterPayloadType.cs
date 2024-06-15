namespace System.Diagnostics.Tracing;

[EventData]
internal class IncrementingEventCounterPayloadType
{
    public IncrementingCounterPayload Payload { get; set; }

    public IncrementingEventCounterPayloadType(IncrementingCounterPayload payload)
    {
        Payload = payload;
    }
}
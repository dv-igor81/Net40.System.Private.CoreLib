using System.Collections;
using System.Collections.Generic;

namespace System.Diagnostics.Tracing;

[EventData]
internal class EventCounterPayload : IEnumerable<KeyValuePair<string, object>>, IEnumerable
{
    public string Name { get; set; }

    public float Mean { get; set; }

    public float StandardDeviation { get; set; }

    public int Count { get; set; }

    public float Min { get; set; }

    public float Max { get; set; }

    public float IntervalSec { get; internal set; }

    private IEnumerable<KeyValuePair<string, object>> ForEnumeration
    {
        get
        {
            yield return new KeyValuePair<string, object>("Name", Name);
            yield return new KeyValuePair<string, object>("Mean", Mean);
            yield return new KeyValuePair<string, object>("StandardDeviation", StandardDeviation);
            yield return new KeyValuePair<string, object>("Count", Count);
            yield return new KeyValuePair<string, object>("Min", Min);
            yield return new KeyValuePair<string, object>("Max", Max);
        }
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        return ForEnumeration.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ForEnumeration.GetEnumerator();
    }
}
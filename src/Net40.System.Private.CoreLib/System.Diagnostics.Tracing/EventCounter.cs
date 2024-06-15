using System.Threading;

namespace System.Diagnostics.Tracing;

public class EventCounter : IDisposable
{
	private readonly string _name;

	private EventCounterGroup _group;

	private volatile float[] _bufferedValues;

	private volatile int _bufferedValuesIndex;

	private int _count;

	private float _sum;

	private float _sumSquared;

	private float _min;

	private float _max;

	private object MyLock => _bufferedValues;

	public EventCounter(string name, EventSource eventSource)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		if (eventSource == null)
		{
			throw new ArgumentNullException("eventSource");
		}
		InitializeBuffer();
		_name = name;
		_group = EventCounterGroup.GetEventCounterGroup(eventSource);
		_group.Add(this);
		_min = float.PositiveInfinity;
		_max = float.NegativeInfinity;
	}

	public void WriteMetric(float value)
	{
		Enqueue(value);
	}

	public void Dispose()
	{
		EventCounterGroup group = _group;
		if (group != null)
		{
			group.Remove(this);
			_group = null;
		}
	}

	public override string ToString()
	{
		return "EventCounter '" + _name + "' Count " + _count + " Mean " + ((double)_sum / (double)_count).ToString("n3");
	}

	private void InitializeBuffer()
	{
		_bufferedValues = new float[10];
		for (int i = 0; i < _bufferedValues.Length; i++)
		{
			_bufferedValues[i] = float.NegativeInfinity;
		}
	}

	private void Enqueue(float value)
	{
		int num = _bufferedValuesIndex;
		float num2;
		do
		{
			num2 = Interlocked.CompareExchange(ref _bufferedValues[num], value, float.NegativeInfinity);
			num++;
			if (_bufferedValues.Length <= num)
			{
				lock (MyLock)
				{
					Flush();
				}
				num = 0;
			}
		}
		while (num2 != float.NegativeInfinity);
		_bufferedValuesIndex = num;
	}

	private void Flush()
	{
		for (int i = 0; i < _bufferedValues.Length; i++)
		{
			float num = Interlocked.Exchange(ref _bufferedValues[i], float.NegativeInfinity);
			if (num != float.NegativeInfinity)
			{
				OnMetricWritten(num);
			}
		}
		_bufferedValuesIndex = 0;
	}

	private void OnMetricWritten(float value)
	{
		_sum += value;
		_sumSquared += value * value;
		if (value > _max)
		{
			_max = value;
		}
		if (value < _min)
		{
			_min = value;
		}
		_count++;
	}

	internal EventCounterPayload GetEventCounterPayload()
	{
		lock (MyLock)
		{
			Flush();
			EventCounterPayload eventCounterPayload = new EventCounterPayload();
			eventCounterPayload.Name = _name;
			eventCounterPayload.Count = _count;
			if (0 < _count)
			{
				eventCounterPayload.Mean = _sum / (float)_count;
				eventCounterPayload.StandardDeviation = (float)Math.Sqrt(_sumSquared / (float)_count - _sum * _sum / (float)_count / (float)_count);
			}
			else
			{
				eventCounterPayload.Mean = 0f;
				eventCounterPayload.StandardDeviation = 0f;
			}
			eventCounterPayload.Min = _min;
			eventCounterPayload.Max = _max;
			ResetStatistics();
			return eventCounterPayload;
		}
	}

	private void ResetStatistics()
	{
		_count = 0;
		_sum = 0f;
		_sumSquared = 0f;
		_min = float.PositiveInfinity;
		_max = float.NegativeInfinity;
	}
}
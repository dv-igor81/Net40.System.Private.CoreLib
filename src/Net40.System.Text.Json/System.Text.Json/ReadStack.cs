#define DEBUG
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Json;

[DebuggerDisplay("Path:{JsonPath()} Current: ClassType.{Current.JsonClassInfo.ClassType}, {Current.JsonClassInfo.Type.Name}")]
internal struct ReadStack
{
	internal static readonly char[] SpecialCharacters = new char[18]
	{
		'.', ' ', '\'', '/', '"', '[', ']', '(', ')', '\t',
		'\n', '\r', '\f', '\b', '\\', '\u0085', '\u2028', '\u2029'
	};

	public ReadStackFrame Current;

	private List<ReadStackFrame> _previous;

	public int _index;

	public long BytesConsumed;

	internal bool ReadAhead;

	public bool IsLastFrame => _index == 0;

	public void Push()
	{
		if (_previous == null)
		{
			_previous = new List<ReadStackFrame>();
		}
		if (_index == _previous.Count)
		{
			_previous.Add(Current);
		}
		else
		{
			Debug.Assert(_index < _previous.Count);
			_previous[_index] = Current;
		}
		Current.Reset();
		_index++;
	}

	public void Pop()
	{
		Debug.Assert(_index > 0);
		Current = _previous[--_index];
	}

	public string JsonPath()
	{
		StringBuilder sb = new StringBuilder("$");
		for (int i = 0; i < _index; i++)
		{
			ReadStackFrame frame = _previous[i];
			AppendStackFrame(sb, in frame);
		}
		AppendStackFrame(sb, in Current);
		return sb.ToString();
	}

	private void AppendStackFrame(StringBuilder sb, in ReadStackFrame frame)
	{
		string propertyName = GetPropertyName(in frame);
		AppendPropertyName(sb, propertyName);
		if (frame.JsonClassInfo == null)
		{
			return;
		}
		if (frame.IsProcessingDictionary())
		{
			AppendPropertyName(sb, frame.KeyName);
		}
		else if (frame.IsProcessingEnumerable())
		{
			IList list = frame.TempEnumerableValues;
			if (list == null && frame.ReturnValue != null)
			{
				list = (IList)(frame.JsonPropertyInfo?.GetValueAsObject(frame.ReturnValue));
			}
			if (list != null)
			{
				sb.Append("[");
				sb.Append(list.Count);
				sb.Append("]");
			}
		}
	}

	private void AppendPropertyName(StringBuilder sb, string propertyName)
	{
		if (propertyName != null)
		{
			if (propertyName.IndexOfAny(SpecialCharacters) != -1)
			{
				sb.Append("['");
				sb.Append(propertyName);
				sb.Append("']");
			}
			else
			{
				sb.Append('.');
				sb.Append(propertyName);
			}
		}
	}

	private string GetPropertyName(in ReadStackFrame frame)
	{
		byte[] utf8PropertyName = frame.JsonPropertyName;
		if (utf8PropertyName == null)
		{
			utf8PropertyName = frame.JsonPropertyInfo?.JsonPropertyName;
		}
		if (utf8PropertyName != null)
		{
			return JsonHelpers.Utf8GetString(utf8PropertyName);
		}
		return null;
	}
}

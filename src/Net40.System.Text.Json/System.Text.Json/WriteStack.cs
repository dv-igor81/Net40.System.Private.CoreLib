#define DEBUG
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Json;

[DebuggerDisplay("Path:{PropertyPath()} Current: ClassType.{Current.JsonClassInfo.ClassType}, {Current.JsonClassInfo.Type.Name}")]
internal struct WriteStack
{
	public WriteStackFrame Current;

	private List<WriteStackFrame> _previous;

	private int _index;

	public void Push()
	{
		if (_previous == null)
		{
			_previous = new List<WriteStackFrame>();
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

	public void Push(JsonClassInfo nextClassInfo, object nextValue)
	{
		Push();
		Current.JsonClassInfo = nextClassInfo;
		Current.CurrentValue = nextValue;
		ClassType classType = nextClassInfo.ClassType;
		if (classType == ClassType.Enumerable || nextClassInfo.ClassType == ClassType.Dictionary)
		{
			Current.PopStackOnEndCollection = true;
			Current.JsonPropertyInfo = Current.JsonClassInfo.PolicyProperty;
		}
		else if (classType == ClassType.IDictionaryConstructible)
		{
			Current.PopStackOnEndCollection = true;
			Current.JsonPropertyInfo = Current.JsonClassInfo.PolicyProperty;
			Current.IsIDictionaryConstructible = true;
		}
		else
		{
			Debug.Assert(nextClassInfo.ClassType == ClassType.Object || nextClassInfo.ClassType == ClassType.Unknown);
			Current.PopStackOnEndObject = true;
		}
	}

	public void Pop()
	{
		Debug.Assert(_index > 0);
		Current = _previous[--_index];
	}

	public string PropertyPath()
	{
		StringBuilder sb = new StringBuilder("$");
		for (int i = 0; i < _index; i++)
		{
			WriteStackFrame frame = _previous[i];
			AppendStackFrame(sb, in frame);
		}
		AppendStackFrame(sb, in Current);
		return sb.ToString();
	}

	private void AppendStackFrame(StringBuilder sb, in WriteStackFrame frame)
	{
		string propertyName = frame.JsonPropertyInfo?.PropertyInfo?.Name;
		AppendPropertyName(sb, propertyName);
	}

	private void AppendPropertyName(StringBuilder sb, string propertyName)
	{
		if (propertyName != null)
		{
			if (propertyName.IndexOfAny(ReadStack.SpecialCharacters) != -1)
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
}

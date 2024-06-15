#define DEBUG
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace System.Text.Json;

internal struct WriteStackFrame
{
	public object CurrentValue;

	public JsonClassInfo JsonClassInfo;

	public string KeyName;

	public IEnumerator CollectionEnumerator;

	public bool PopStackOnEndCollection;

	public bool IsIDictionaryConstructible;

	public bool IsIDictionaryConstructibleProperty;

	public bool PopStackOnEndObject;

	public bool StartObjectWritten;

	public bool MoveToNextProperty;

	public int PropertyEnumeratorIndex;

	public ExtensionDataWriteStatus ExtensionDataStatus;

	public JsonPropertyInfo JsonPropertyInfo;

	public void Initialize(Type type, JsonSerializerOptions options)
	{
		JsonClassInfo = options.GetOrAddClass(type);
		if (JsonClassInfo.ClassType == ClassType.Value || JsonClassInfo.ClassType == ClassType.Enumerable || JsonClassInfo.ClassType == ClassType.Dictionary)
		{
			JsonPropertyInfo = JsonClassInfo.PolicyProperty;
		}
		else if (JsonClassInfo.ClassType == ClassType.IDictionaryConstructible)
		{
			JsonPropertyInfo = JsonClassInfo.PolicyProperty;
			IsIDictionaryConstructible = true;
		}
	}

	public void WriteObjectOrArrayStart(ClassType classType, Utf8JsonWriter writer, JsonSerializerOptions options, bool writeNull = false)
	{
		JsonPropertyInfo jsonPropertyInfo = JsonPropertyInfo;
		if (jsonPropertyInfo != null && jsonPropertyInfo.EscapedName.HasValue)
		{
			WriteObjectOrArrayStart(classType, JsonPropertyInfo.EscapedName.Value, writer, writeNull);
			return;
		}
		if (KeyName != null)
		{
			JsonEncodedText propertyName = JsonEncodedText.Encode(KeyName, options.Encoder);
			WriteObjectOrArrayStart(classType, propertyName, writer, writeNull);
			return;
		}
		Debug.Assert(!writeNull);
		if (classType == ClassType.Object || classType == ClassType.Dictionary || classType == ClassType.IDictionaryConstructible)
		{
			writer.WriteStartObject();
			StartObjectWritten = true;
		}
		else
		{
			Debug.Assert(classType == ClassType.Enumerable);
			writer.WriteStartArray();
		}
	}

	private void WriteObjectOrArrayStart(ClassType classType, JsonEncodedText propertyName, Utf8JsonWriter writer, bool writeNull)
	{
		if (writeNull)
		{
			writer.WriteNull(propertyName);
		}
		else if (classType == ClassType.Object || classType == ClassType.Dictionary || classType == ClassType.IDictionaryConstructible)
		{
			writer.WriteStartObject(propertyName);
			StartObjectWritten = true;
		}
		else
		{
			Debug.Assert(classType == ClassType.Enumerable);
			writer.WriteStartArray(propertyName);
		}
	}

	public void Reset()
	{
		CurrentValue = null;
		CollectionEnumerator = null;
		ExtensionDataStatus = ExtensionDataWriteStatus.NotStarted;
		IsIDictionaryConstructible = false;
		JsonClassInfo = null;
		PropertyEnumeratorIndex = 0;
		PopStackOnEndCollection = false;
		PopStackOnEndObject = false;
		StartObjectWritten = false;
		EndProperty();
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void EndProperty()
	{
		IsIDictionaryConstructibleProperty = false;
		JsonPropertyInfo = null;
		KeyName = null;
		MoveToNextProperty = false;
	}

	public void EndDictionary()
	{
		CollectionEnumerator = null;
		PopStackOnEndCollection = false;
	}

	public void EndArray()
	{
		CollectionEnumerator = null;
		PopStackOnEndCollection = false;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void NextProperty()
	{
		EndProperty();
		int maxPropertyIndex = JsonClassInfo.PropertyCacheArray.Length;
		PropertyEnumeratorIndex++;
		if (PropertyEnumeratorIndex >= maxPropertyIndex)
		{
			if (PropertyEnumeratorIndex > maxPropertyIndex)
			{
				ExtensionDataStatus = ExtensionDataWriteStatus.Finished;
			}
			else if (JsonClassInfo.DataExtensionProperty != null)
			{
				ExtensionDataStatus = ExtensionDataWriteStatus.Writing;
			}
		}
	}
}

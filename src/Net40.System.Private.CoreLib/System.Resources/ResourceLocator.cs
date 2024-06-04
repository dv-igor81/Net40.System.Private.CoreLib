namespace System.Resources;

internal struct ResourceLocator
{
	internal object _value;

	internal int _dataPos;

	internal int DataPosition => _dataPos;

	internal object Value
	{
		get
		{
			return _value;
		}
		set
		{
			_value = value;
		}
	}

	internal ResourceLocator(int dataPos, object value)
	{
		_dataPos = dataPos;
		_value = value;
	}

	internal static bool CanCache(System.Resources.ResourceTypeCode value)
	{
		return value <= System.Resources.ResourceTypeCode.TimeSpan;
	}
}

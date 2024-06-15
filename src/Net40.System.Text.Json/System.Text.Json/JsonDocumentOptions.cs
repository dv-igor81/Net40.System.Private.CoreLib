#define DEBUG
using System.Diagnostics;

namespace System.Text.Json;

public struct JsonDocumentOptions
{
	internal const int DefaultMaxDepth = 64;

	private int _maxDepth;

	private JsonCommentHandling _commentHandling;

	public JsonCommentHandling CommentHandling
	{
		readonly get
		{
			return _commentHandling;
		}
		set
		{
			Debug.Assert((int)value >= 0);
			if ((int)value > 1)
			{
				throw new ArgumentOutOfRangeException("value", "SR.JsonDocumentDoesNotSupportComments");
			}
			_commentHandling = value;
		}
	}

	public int MaxDepth
	{
		readonly get
		{
			return _maxDepth;
		}
		set
		{
			if (value < 0)
			{
				throw ThrowHelper.GetArgumentOutOfRangeException_MaxDepthMustBePositive("value");
			}
			_maxDepth = value;
		}
	}

	public bool AllowTrailingCommas { get; set; }

	internal JsonReaderOptions GetReaderOptions()
	{
		JsonReaderOptions result = default(JsonReaderOptions);
		result.AllowTrailingCommas = AllowTrailingCommas;
		result.CommentHandling = CommentHandling;
		result.MaxDepth = MaxDepth;
		return result;
	}
}

using System.Text.Unicode;

namespace System.Text.Encodings.Web;

public abstract class JavaScriptEncoder : TextEncoder
{
	public static JavaScriptEncoder Default => DefaultJavaScriptEncoder.Singleton;

	public static JavaScriptEncoder UnsafeRelaxedJsonEscaping => UnsafeRelaxedJavaScriptEncoder.s_singleton;

	public static JavaScriptEncoder Create(TextEncoderSettings settings)
	{
		return new DefaultJavaScriptEncoder(settings);
	}

	public static JavaScriptEncoder Create(params UnicodeRange[] allowedRanges)
	{
		return new DefaultJavaScriptEncoder(allowedRanges);
	}
}

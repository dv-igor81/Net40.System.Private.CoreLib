using System.Text.Unicode;

namespace System.Text.Encodings.Web;

public abstract class UrlEncoder : TextEncoder
{
	public static UrlEncoder Default => DefaultUrlEncoder.Singleton;

	public static UrlEncoder Create(TextEncoderSettings settings)
	{
		return new DefaultUrlEncoder(settings);
	}

	public static UrlEncoder Create(params UnicodeRange[] allowedRanges)
	{
		return new DefaultUrlEncoder(allowedRanges);
	}
}

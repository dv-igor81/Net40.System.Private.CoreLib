#define DEBUG
using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace System.Text.Encodings.Web;

internal static class TextEncoderExtensions
{
	private delegate OperationStatus EncodeUtf8Del(TextEncoder encoder, ReadOnlySpan<byte> utf8Source, Span<byte> utf8Destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock);

	private delegate int FindFirstCharacterToEncodeUtf8Del(TextEncoder encoder, ReadOnlySpan<byte> utf8Text);

	private static readonly EncodeUtf8Del s_encodeUtf8Fn = CreateEncodeUtf8Fn();

	private static readonly FindFirstCharacterToEncodeUtf8Del s_findFirstCharToEncodeUtf8Fn = CreateFindFirstCharToEncodeUtf8Fn();

	private static EncodeUtf8Del CreateEncodeUtf8Fn()
	{
		MethodInfo methodInfo = typeof(TextEncoder).GetMethod("EncodeUtf8Shim", BindingFlags.Static | BindingFlags.NonPublic);
		Debug.Assert(methodInfo != null);
		EncodeUtf8Del del = (EncodeUtf8Del)MethodInfoTheraotExtensions.CreateDelegate(methodInfo, typeof(EncodeUtf8Del));
		del(HtmlEncoder.Default, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, out var _, out var _, isFinalBlock: false);
		return (EncodeUtf8Del)MethodInfoTheraotExtensions.CreateDelegate(methodInfo, typeof(EncodeUtf8Del));
	}

	private static FindFirstCharacterToEncodeUtf8Del CreateFindFirstCharToEncodeUtf8Fn()
	{
		MethodInfo methodInfo = typeof(TextEncoder).GetMethod("FindFirstCharacterToEncodeUtf8Shim", BindingFlags.Static | BindingFlags.NonPublic);
		Debug.Assert(methodInfo != null);
		FindFirstCharacterToEncodeUtf8Del del = (FindFirstCharacterToEncodeUtf8Del)MethodInfoTheraotExtensions.CreateDelegate(methodInfo, typeof(FindFirstCharacterToEncodeUtf8Del));
		del(HtmlEncoder.Default, ReadOnlySpan<byte>.Empty);
		return (FindFirstCharacterToEncodeUtf8Del)MethodInfoTheraotExtensions.CreateDelegate(methodInfo, typeof(FindFirstCharacterToEncodeUtf8Del));
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static OperationStatus EncodeUtf8(this TextEncoder encoder, ReadOnlySpan<byte> utf8Source, Span<byte> utf8Destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock = true)
	{
		return s_encodeUtf8Fn(encoder, utf8Source, utf8Destination, out bytesConsumed, out bytesWritten, isFinalBlock);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static int FindFirstCharacterToEncodeUtf8(this TextEncoder encoder, ReadOnlySpan<byte> utf8Text)
	{
		return s_findFirstCharToEncodeUtf8Fn(encoder, utf8Text);
	}
}

using System.Runtime.InteropServices;
using Microsoft.IO;

namespace System.Text;

public static class EncodingEx
{
    
    public static unsafe int GetBytes(this Encoding encoding, string str, Span<byte> bytes)
    {
        return encoding.GetBytes(str.ToCharArray(), bytes);
    }
    
    public static unsafe int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        fixed (char* chars2 = &MemoryMarshal.GetNonNullPinnableReference(chars))
        {
            fixed (byte* bytes2 = &MemoryMarshal.GetNonNullPinnableReference<byte>(bytes))
            {
                return encoding.GetBytes(chars2, chars.Length, bytes2, bytes.Length);
            }
        }
    }
    
    public static unsafe string GetString(this Encoding encoding, byte* bytes, int byteCount)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException("bytes", SR.ArgumentNull_Array);
        }
        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException("byteCount", SR.ArgumentOutOfRange_NeedNonNegNum);
        }
        
        //encoding.GetString()
        //StringExtensions.Create()
        
        return CreateStringFromEncoding(bytes, byteCount, encoding);
    }

    private static unsafe string CreateStringFromEncoding(byte* bytes, int byteLength, Encoding encoding)
    {
        int charCount = encoding.GetCharCount(bytes, byteLength);
        if (charCount == 0)
        {
            return string.Empty;
        }
        //string text = FastAllocateString(charCount);
        string text = new string(new char[charCount]);
        fixed (char* chars = text)
        {
            int chars2 = encoding.GetChars(bytes, byteLength, chars, charCount);
        }
        return text;
    }


}
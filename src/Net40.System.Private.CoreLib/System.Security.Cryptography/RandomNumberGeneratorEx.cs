namespace System.Security.Cryptography;

public static class RandomNumberGeneratorEx
{
    public static void Fill(Span<byte> data)
    {
        if (data.Length > 0)
        {
            RandomNumberGenerator generator = RandomNumberGenerator.Create();
            byte[] dataArr = new byte[data.Length];
            generator.GetBytes(dataArr);
            data.Clear();
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = dataArr[i];
            }
        }
    }
}
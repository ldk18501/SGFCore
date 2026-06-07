public static class ConfigBinaryCodec
{
    public const byte DefaultXorKey = 0x55;

    public static void Encode(byte[] data)
    {
        Xor(data, DefaultXorKey);
    }

    public static void Decode(byte[] data)
    {
        Xor(data, DefaultXorKey);
    }

    public static void Xor(byte[] data, byte key)
    {
        if (data == null)
        {
            return;
        }

        for (int i = 0; i < data.Length; i++)
        {
            data[i] ^= key;
        }
    }
}

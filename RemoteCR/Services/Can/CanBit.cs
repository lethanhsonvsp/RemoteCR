namespace RemoteCR.Services.Can;

public static class CanBit
{
    public static ulong Get(byte[] d, int start, int len)
    {
        ulong v = 0;
        for (int i = 0; i < len; i++)
        {
            int bit = start + i;
            if ((d[bit / 8] & (1 << (bit % 8))) != 0)
                v |= 1UL << i;
        }
        return v;
    }

    public static void Set(byte[] d, int start, int len, ulong value)
    {
        for (int i = 0; i < len; i++)
        {
            int bit = start + i;
            if (((value >> i) & 1) == 1)
                d[bit / 8] |= (byte)(1 << (bit % 8));
            else
                d[bit / 8] &= (byte)~(1 << (bit % 8));
        }
    }
}
public enum CanBaudRate
{
    K125 = 0,
    K250 = 1,
    K500 = 2,
    K1000 = 3,
    K800 = 4
}
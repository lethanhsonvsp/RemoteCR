namespace RemoteCR.Services.Can;

public class CanFrame
{
    public uint Id { get; set; }
    public byte Dlc { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime Ts { get; set; } = DateTime.Now;

    public string Hex =>
        BitConverter.ToString(Data).Replace("-", " ");
}

namespace RemoteCR.Services.Can;

public class ConfigReportA
{
    public uint SerialNumber;
    public byte SwMajor;
    public byte SwMinor;
    public byte SwDebug;
    public byte McuId;
}

public class ConfigReportB
{
    public uint DeltaPN;
    public byte Week;
    public byte Year;
    public byte HwMajor;
    public byte HwMinor;
}

public class CommInfoReport
{
    public byte Channel;
    public byte CommId;
    public double SuccessRate;
}

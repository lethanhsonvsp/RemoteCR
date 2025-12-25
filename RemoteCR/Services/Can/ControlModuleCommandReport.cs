namespace RemoteCR.Services.Can;

public class ControlModuleCommandReport
{
    public double DemandVoltage_V { get; set; }
    public double DemandCurrent_A { get; set; }

    public bool PowerEnable { get; set; }
    public bool ClearFaults { get; set; }

    public bool[] PowerStages { get; set; } = new bool[9];
}

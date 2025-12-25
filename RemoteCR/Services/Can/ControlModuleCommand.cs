namespace RemoteCR.Services.Can;

public class ControlModuleCommand
{
    public double DemandVoltage_V;
    public double DemandCurrent_A;
    public bool PowerStage1;
    public bool ClearFaults;
    public bool[] PowerStages { get; } = new bool[9];
}

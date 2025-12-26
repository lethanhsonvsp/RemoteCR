namespace RemoteCR.Services.Can;

public sealed class ControlModuleCommandReport
{
    public double DemandVoltage_V { get; init; }
    public double DemandCurrent_A { get; init; }
    public bool ClearFaults { get; init; }
    public bool PowerStage1 { get; init; }
    public DateTime Timestamp { get; init; }

}

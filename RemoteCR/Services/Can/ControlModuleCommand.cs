namespace RemoteCR.Services.Can;

public class ControlModuleCommand
{
    public double DemandVoltage_V;
    public double DemandCurrent_A;

    // 🔴 MASTER ENABLE (BIT 20)
    public bool PowerEnable;

    // 🟢 POWER STAGE 1 (BIT 22)
    public bool PowerStage1;

    public bool ClearFaults;
}

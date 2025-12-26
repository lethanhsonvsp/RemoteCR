namespace RemoteCR.Services.Can;

public class ControlModuleCommand
{
    // Demand_Voltage (bit 0..19, 0.001 V)
    public double Demand_Voltage;

    // Demand_Current (bit 32..49, 0.001 A)
    public double Demand_Current;

    // Demand_PowerStage1 (bit 20)
    public bool Demand_PowerStage1;

    // Demand_ClearFaults (bit 21)
    public bool Demand_ClearFaults;

    // Demand_PowerStage2~10 (bit 22..30)
    // standalone → chỉ dùng [0]
    public bool[] Demand_PowerStages { get; } = new bool[9];
}

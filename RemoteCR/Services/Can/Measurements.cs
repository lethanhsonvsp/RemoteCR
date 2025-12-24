namespace RemoteCR.Services.Can;

public class PowerMeasurement
{
    public double Voltage_V;
    public double Current_A;
    public bool Charging;
    public bool Fault;
}

public class AcMeasurement
{
    public double Voltage_V;
    public double Current_A;
    public double Frequency_Hz;
}

public class WirelessStatus
{
    public int Gap_mm;
    public double Efficiency_pct;
}

public class TemperatureReport
{
    public double Secondary_C;
    public double Primary_C;
}

namespace RemoteCR.Services.Can;

public class ChargingSummaryModel
{
    // ===== POWER =====
    public double Voltage_V { get; set; }
    public double Current_A { get; set; }
    public bool Charging { get; set; }
    public bool Fault { get; set; }

    // ===== STATUS (FIX LỖI Ở ĐÂY) =====
    public StatusReport? Status { get; set; }

    // ===== AC =====
    public AcMeasurement? Ac { get; set; }

    // ===== WIRELESS =====
    public WirelessStatus? Wireless { get; set; }
    public WirelessStatusReport? WirelessStatusReport { get; set; }

    // ===== TEMP =====
    public TemperatureReport? Temperature { get; set; }

    // ===== LIFE =====
    public LifeReportA? LifeA { get; set; }
    public LifeReportB? LifeB { get; set; }
    public LifeReportC? LifeC { get; set; }

    // ===== CONFIG =====
    public ConfigReportA? ConfigA { get; set; }
    public ConfigReportB? ConfigB { get; set; }

    public CanBaudRate? CanBaud { get; set; }
    public ControlModuleCommandReport ControlCmd { get; internal set; }

    // ===== COMM INFO =====

    public CommInfoReport? CommInfo;


    /* ===== UPDATE HELPERS ===== */
    public void Update(PowerMeasurement p)
    {
        Voltage_V = p.Voltage_V;
        Current_A = p.Current_A;
        Charging = p.Charging;
        Fault = p.Fault;
    }

    public void Update(StatusReport s)
    {
        Status = s;
        Fault = s.Fault;
    }
}

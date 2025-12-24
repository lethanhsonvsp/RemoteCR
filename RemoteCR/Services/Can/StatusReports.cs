namespace RemoteCR.Services.Can;

public enum ChargerState
{
    Uninit = 0x00,
    Standby = 0x01,
    Charging = 0x04,
    Fault = 0x10
}

public class StatusReport
{
    public ChargerState State;
    public bool Fault;
    public bool Ocp;
    public bool Ovp;
    public bool Watchdog;
}

public class WirelessStatusReport
{
    public bool UnderCurrent;
    public bool WirelessOk;
}

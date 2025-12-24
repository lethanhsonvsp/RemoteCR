namespace RemoteCR.Services.Can;

public static class CanMessageDecoder
{
    public static void Decode(uint canId, byte[] d, ChargingSummaryModel m)
    {
        switch (canId)
        {
            /* ================= DC ================= */
            case 0x311:
                m.Update(new PowerMeasurement
                {
                    Voltage_V = CanBit.Get(d, 0, 20) * 0.001,
                    Current_A = CanBit.Get(d, 20, 18) * 0.001,
                    Fault = CanBit.Get(d, 38, 1) == 1,
                    Charging = CanBit.Get(d, 39, 1) == 1
                });
                break;

            /* ================= STATUS ================= */
            case 0x321:
                m.Update(new StatusReport
                {
                    State = (ChargerState)CanBit.Get(d, 0, 6),
                    Fault = CanBit.Get(d, 12, 1) == 1,
                    Ocp = CanBit.Get(d, 18, 1) == 1,
                    Ovp = CanBit.Get(d, 21, 1) == 1 || CanBit.Get(d, 22, 1) == 1,
                    Watchdog = CanBit.Get(d, 24, 1) == 1
                });
                break;

            /* ================= AC INPUT ================= */
            case 0x3C1:
                m.Ac = new AcMeasurement
                {
                    Voltage_V = CanBit.Get(d, 0, 20) * 0.001,
                    Current_A = CanBit.Get(d, 20, 18) * 0.001,
                    Frequency_Hz = CanBit.Get(d, 38, 10) * 0.1
                };
                break;

            /* ================= WIRELESS ================= */
            case 0x3E1:
                m.Wireless = new WirelessStatus
                {
                    Efficiency_pct = CanBit.Get(d, 16, 10) * 0.1,
                    Gap_mm = (int)CanBit.Get(d, 32, 8)
                };
                break;

            /* ================= TEMPERATURE ================= */
            case 0x3F1:
                m.Temperature = new TemperatureReport
                {
                    Secondary_C = (short)CanBit.Get(d, 0, 16) * 0.005,
                    Primary_C = (short)CanBit.Get(d, 16, 16) * 0.005
                };
                break;

            /* ================= LIFE ================= */
            case 0x511:
                m.LifeA = new LifeReportA
                {
                    AhDelivered = CanBit.Get(d, 0, 32) * 0.1,
                    ChargeCycles = (uint)CanBit.Get(d, 32, 32)
                };
                break;

            case 0x521:
                m.LifeB = new LifeReportB
                {
                    UptimeSec = (uint)CanBit.Get(d, 0, 32)
                };
                break;

            case 0x531:
                m.LifeC = new LifeReportC
                {
                    LoadTimeSec = (uint)CanBit.Get(d, 0, 32),
                    IdleTimeSec = (uint)CanBit.Get(d, 32, 32)
                };
                break;

            /* ================= WIRELESS STATUS ================= */
            case 0x5F1:
                m.WirelessStatusReport = new WirelessStatusReport
                {
                    UnderCurrent = CanBit.Get(d, 6, 1) == 1,
                    WirelessOk = CanBit.Get(d, 7, 1) == 1
                };
                break;

            /* ================= CONFIG ================= */
            case 0x721:
                m.ConfigA = new ConfigReportA
                {
                    SerialNumber = (uint)CanBit.Get(d, 0, 32),
                    SwMajor = (byte)CanBit.Get(d, 32, 8),
                    SwMinor = (byte)CanBit.Get(d, 40, 8),
                    SwDebug = (byte)CanBit.Get(d, 48, 8),
                    McuId = (byte)CanBit.Get(d, 60, 4)
                };
                break;

            case 0x731:
                m.ConfigB = new ConfigReportB
                {
                    DeltaPN = (uint)CanBit.Get(d, 0, 32),
                    Week = (byte)CanBit.Get(d, 32, 8),
                    Year = (byte)CanBit.Get(d, 40, 8),
                    HwMinor = (byte)CanBit.Get(d, 48, 4),
                    HwMajor = (byte)CanBit.Get(d, 52, 4)
                };
                break;

            /* ================= COMM ================= */
            case 0x771:
                m.CommInfo = new CommInfoReport
                {
                    Channel = (byte)CanBit.Get(d, 0, 8),
                    CommId = (byte)CanBit.Get(d, 8, 8),
                    SuccessRate = CanBit.Get(d, 16, 32) * 0.000001
                };
                break;

            /* ================= CAN BAUD ================= */
            case 0x781:
                m.CanBaud = (CanBaudRate)CanBit.Get(d, 0, 4);
                break;
        }
    }
}

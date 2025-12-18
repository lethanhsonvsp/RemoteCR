using System;
namespace RemoteCR.Services.Can;

public class DeltaDecoder
{
    private readonly CanStateContainer state;

    public DeltaDecoder(CanStateContainer state)
    {
        this.state = state;
    }

    public void Decode(uint id, byte dlc, byte[] d)
    {
        state.AddFrame(id, dlc, d);

        if (d == null || d.Length == 0 || dlc == 0)
            return;

        uint baseId = id & 0xFF0;

        switch (baseId)
        {
            case 0x310: if (dlc >= 4) Decode_311(d); break;
            case 0x320: if (dlc >= 2) Decode_321(d); break;
            case 0x3C0: if (dlc >= 6) Decode_3C1(d); break;
            case 0x3E0: if (dlc >= 2) Decode_3E1(d); break;
            case 0x3F0: if (dlc >= 4) Decode_3F1(d); break;

            case 0x520: if (dlc >= 2) Decode_521(d); break;

            case 0x5F0: if (dlc >= 4) Decode_5F1(d); break;
            case 0x770: if (dlc >= 7) Decode_771(d); break;
        }

        state.NotifyChanged();
    }

    // ================= OUTPUT =================
    private void Decode_311(byte[] d)
    {
        state.Voltage = (d[0] | (d[1] << 8)) * 0.001;
        state.Current = (d[2] | (d[3] << 8)) * 0.001;
    }

    private void Decode_321(byte[] d)
    {
        state.Gap = (sbyte)d[0];
        state.Misalignment = (sbyte)d[1];
    }

    // ================= AC INPUT =================
    private void Decode_3C1(byte[] d)
    {
        state.AcVoltage = (d[0] | (d[1] << 8)) * 0.1;
        state.AcCurrent = (d[2] | (d[3] << 8)) * 0.01;
        state.AcFreq = d[4];
        state.AcPower = state.AcVoltage * state.AcCurrent;
    }

    // ================= TEMP =================
    private void Decode_3E1(byte[] d)
    {
        state.PriTemp = d[0] - 40;
        state.SecTemp = d[1] - 40;
    }

    // ================= RF =================
    private void Decode_3F1(byte[] d)
    {
        state.Coupling = (d[0] | (d[1] << 8)) * 0.1;
        state.RfPower = (d[2] | (d[3] << 8)) * 0.1;
    }

    private void Decode_521(byte[] d)
    {
        state.Rssi = (short)(d[0] | (d[1] << 8));
    }

    // ================= FAULT (FIXED) =================
    private void Decode_5F1(byte[] d)
    {
        state.FaultFlags =
            (ChargerFault)(
                (uint)d[0] |
                ((uint)d[1] << 8) |
                ((uint)d[2] << 16) |
                ((uint)d[3] << 24)
            );
    }

    // ================= STATUS + FW =================
    private void Decode_771(byte[] d)
    {
        state.StatusFlags = ChargerStatus.None;

        for (int i = 0; i < 4; i++)
            state.StatusFlags |= (ChargerStatus)(d[i] << (i * 8));

        state.RevMCU1 = d[4].ToString();
        state.RevMCU2 = d[5].ToString();
        state.RevDSP = d[6].ToString();
    }
}

[Flags]
public enum ChargerFault : uint
{
    None = 0,

    // -------- d[0] --------
    OVP = 1 << 0,
    OCP = 1 << 1,
    ShortCircuit = 1 << 2,
    OTP_Primary = 1 << 3,
    OTP_Secondary = 1 << 4,
    OutputOpen = 1 << 5,
    PolarityReverse = 1 << 6,
    OutputDisabled = 1 << 7,

    // -------- d[1] --------
    AC_UV = 1 << 8,
    AC_OV = 1 << 9,
    AC_Freq = 1 << 10,
    PhaseLoss = 1 << 11,
    Surge = 1 << 12,
    Ripple = 1 << 13,
    EMI = 1 << 14,
    Isolation = 1 << 15,

    // -------- d[2] --------
    GapTooHigh = 1 << 16,
    MisalignmentHigh = 1 << 17,
    ForeignObject = 1 << 18,
    CommTimeout = 1 << 19,
    Derating = 1 << 20,
    TX_Fault = 1 << 21,
    RX_Fault = 1 << 22,
    RF_Fault = 1 << 23,

    // -------- d[3] --------
    BMS_NoCmd = 1 << 24,
    BMS_Stop = 1 << 25,
    ProfileErr = 1 << 26,
    SafetyStop = 1 << 27,
    InternalComm = 1 << 28,
    EEPROM = 1 << 29,
    SensorFault = 1 << 30,
    ChargerLocked = 1u << 31
}
[Flags]
public enum ChargerStatus : uint
{
    None = 0,

    // -------- BYTE 0 (0–7) --------
    Input_OK = 1 << 0,
    Charging_Enabled = 1 << 1,
    CC_Mode = 1 << 2,
    CV_Mode = 1 << 3,

    // -------- BYTE 1 (8–15) --------
    Misalignment_Warning = 1 << 8,
    FOD_Warning = 1 << 9,
    RF_Link_Warning = 1 << 10,
    Thermal_Derating = 1 << 11,

    // -------- BYTE 2 (16–23) --------
    FOD_Active = 1 << 22,
    Internal_Protection = 1 << 23,

    // -------- BYTE 3 (24–31) --------
    RX_Fault = 1 << 25,
    TX_Fault = 1 << 27,
    EEPROM_Fault = 1 << 29,
    Comm_Fault = 1 << 30,
    Charger_Locked = 1u << 31,
    Safety_Stop = 1 << 24   // ✅ BỔ SUNG
}


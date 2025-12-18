namespace RemoteCR.Services.Can;

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


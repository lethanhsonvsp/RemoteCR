namespace RemoteCR.Services.Can;

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


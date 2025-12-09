using System;
using System.Collections.Generic;

namespace RemoteCR.Services.Can;

public class DeltaDecoder
{
    private readonly CanStateContainer state;
    private static readonly string[] stringArray = new[]
        {
            "Reserved_24","RX_Fault","Reserved_26","TX_Fault","EEPROM_Fault","Comm_Fault","Charger_Locked","Safety_Stop"
        };

    public DeltaDecoder(CanStateContainer state)
    {
        this.state = state;
    }

    public void Decode(uint id, byte dlc, byte[] d)
    {
        if (d == null || d.Length == 0 || dlc == 0)
            return;

        uint baseId = id & 0xFF0;

        switch (baseId)
        {
            case 0x310:  // 311h — Voltage & Current
                if (dlc >= 4) Decode_311(d);
                break;

            case 0x320:  // 321h — Gap
                if (dlc >= 1) Decode_321(d);
                break;

            case 0x3E0:  // 3Ex — Temperatures
                if (dlc >= 2) Decode_3Ex(d);
                break;

            case 0x5F0:  // 5Fx — Fault bits
                if (dlc >= 4) Decode_5Fx(d);
                break;

            case 0x770:  // 77x — Status + Firmware
                if (dlc >= 7) Decode_77x(d);
                break;
        }

        state.NotifyChanged();
    }

    // --------------------------------------------------------------------
    // 311h – Voltage + Current
    // --------------------------------------------------------------------
    private void Decode_311(byte[] d)
    {
        state.Voltage = (d[0] | (d[1] << 8)) * 0.001;
        state.Current = (d[2] | (d[3] << 8)) * 0.001;
    }

    // --------------------------------------------------------------------
    // 321h – Gap (signed)
    // --------------------------------------------------------------------
    private void Decode_321(byte[] d)
    {
        state.Gap = (sbyte)d[0];
    }

    // --------------------------------------------------------------------
    // 3Ex – Temperature sensors (value - 40)
    // --------------------------------------------------------------------
    private void Decode_3Ex(byte[] d)
    {
        state.PriTemp = d[0] - 40;
        state.SecTemp = d[1] - 40;
    }

    // --------------------------------------------------------------------
    // 5Fx – Fault bit groups
    // --------------------------------------------------------------------
    private void Decode_5Fx(byte[] d)
    {
        state.Faults.Clear();

        DecodeBits(d[0], new[]
        {
            "OVP","OCP","ShortCircuit","OTP_Primary","OTP_Secondary",
            "OutputOpen","PolarityReverse","OutputDisabled"
        });

        DecodeBits(d[1], new[]
        {
            "AC_UV","AC_OV","AC_Freq","PhaseLoss","Surge","Ripple","EMI","Isolation"
        });

        DecodeBits(d[2], new[]
        {
            "GapTooHigh","MisalignmentHigh","ForeignObject","CommTimeout","Derating","TX_Fault","RX_Fault","RF_Fault"
        });

        DecodeBits(d[3], new[]
        {
            "BMS_NoCmd","BMS_Stop","ProfileErr","SafetyStop","InternalComm","EEPROM","SensorFault","ChargerLocked"
        });
    }

    // --------------------------------------------------------------------
    // 77x – Status + Firmware (CHUẨN theo frame thật bạn gửi)
    // --------------------------------------------------------------------
    private void Decode_77x(byte[] d)
    {
        state.Status.Clear();

        // BYTE 0
        DecodeStatusBits(d[0], new[]
        {
            "Input_OK","Charging_Enabled","CC_Mode","CV_Mode","Reserved_4","Reserved_5","Reserved_6","Reserved_7"
        });

        // BYTE 1
        DecodeStatusBits(d[1],
        [
            "Misalignment_Warning","FOD_Warning","RF_Link_Warning","Thermal_Derating","Reserved_12","Reserved_13","Reserved_14","Reserved_15"
        ]);

        // BYTE 2
        DecodeStatusBits(d[2],
        [
            "Reserved_16","Reserved_17","Reserved_18","Reserved_19","Reserved_20","Reserved_21","FOD_Active","Internal_Protection"
        ]);

        // BYTE 3
        DecodeStatusBits(d[3], stringArray);

        // Firmware revisions
        state.RevMCU1 = d[4].ToString();
        state.RevMCU2 = d[5].ToString();
        state.RevDSP = d[6].ToString();

        // Byte 7 = sequence counter (không lưu)
    }

    // --------------------------------------------------------------------
    // Helper: Fault decode
    // --------------------------------------------------------------------
    private void DecodeBits(byte val, string[] labels)
    {
        for (int i = 0; i < 8; i++)
            if ((val & (1 << i)) != 0)
                state.Faults.Add(labels[i]);
    }

    // --------------------------------------------------------------------
    // Helper: Status decode
    // --------------------------------------------------------------------
    private void DecodeStatusBits(byte val, string[] labels)
    {
        for (int i = 0; i < 8; i++)
            if ((val & (1 << i)) != 0)
                state.Status.Add(labels[i]);
    }
}

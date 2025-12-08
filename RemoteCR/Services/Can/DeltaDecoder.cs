using System;
using System.Collections.Generic;

namespace RemoteCR.Services.Can;

public class DeltaDecoder
{
    private readonly CanStateContainer _state;

    public DeltaDecoder(CanStateContainer state)
    {
        _state = state;
    }

    public void Decode(uint id, byte dlc, byte[] d)
    {
        uint baseId = id & 0xFF0;

        switch (baseId)
        {
            case 0x310: Decode_311(d); break;
            case 0x320: Decode_321(d); break;
            case 0x3E0: Decode_3Ex(d); break;
            case 0x5F0: Decode_5Fx(d); break;
        }

        _state.NotifyChanged();
    }

    // -----------------------------
    //   MESSAGE 311h — Voltage + Current
    // -----------------------------
    private void Decode_311(byte[] d)
    {
        _state.Voltage = (d[0] | (d[1] << 8)) * 0.001;
        _state.Current = (d[2] | (d[3] << 8)) * 0.001;
    }

    // -----------------------------
    //   MESSAGE 321h — Gap value
    // -----------------------------
    private void Decode_321(byte[] d)
    {
        _state.Gap = d[0];
    }

    // -----------------------------
    //   MESSAGE 3E? — Temperatures
    // -----------------------------
    private void Decode_3Ex(byte[] d)
    {
        _state.PriTemp = d[0] - 40;
        _state.SecTemp = d[1] - 40;
    }

    // -----------------------------
    //   MESSAGE 5F? — Fault Bits
    // -----------------------------
    private void Decode_5Fx(byte[] d)
    {
        _state.Faults.Clear();

        DecodeBits(d[0], new[] {
            "OVP","OCP","ShortCircuit","OTP_Primary","OTP_Secondary",
            "OutputOpen","PolarityReverse","OutputDisabled"});

        DecodeBits(d[1], new[] {
            "AC_UV","AC_OV","AC_Freq","PhaseLoss","Surge",
            "Ripple","EMI","Isolation"});

        DecodeBits(d[2], new[] {
            "GapTooHigh","MisalignmentHigh","ForeignObject",
            "CommTimeout","Derating","TX_Fault","RX_Fault","RF_Fault"});

        DecodeBits(d[3], new[] {
            "BMS_NoCmd","BMS_Stop","ProfileErr","SafetyStop",
            "InternalComm","EEPROM","SensorFault","ChargerLocked"});
    }

    // -----------------------------
    //   Helper: Add fault text
    // -----------------------------
    private void DecodeBits(byte val, string[] labels)
    {
        for (int i = 0; i < 8; i++)
            if ((val & (1 << i)) != 0)
                _state.Faults.Add(labels[i]);
    }
}

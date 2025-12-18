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


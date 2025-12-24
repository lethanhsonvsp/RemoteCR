namespace RemoteCR.Services.Can;

public static class ControlModuleEncoder
{
    public static byte[] Encode(ControlModuleCommand c)
    {
        var d = new byte[8];

        // Voltage demand [0..19] (0.001 V)
        CanBit.Set(d, 0, 20, (ulong)(c.DemandVoltage_V / 0.001));

        // Master power enable [20]
        CanBit.Set(d, 20, 1, c.PowerStage1 ? 1UL : 0UL);

        // Clear fault [21]
        CanBit.Set(d, 21, 1, c.ClearFaults ? 1UL : 0UL);

        // Power stages [22..30]
        for (int i = 0; i < c.PowerStages.Length && i < 9; i++)
            CanBit.Set(d, 22 + i, 1, c.PowerStages[i] ? 1UL : 0UL);

        // Current demand [32..49] (0.001 A)
        CanBit.Set(d, 32, 18, (ulong)(c.DemandCurrent_A / 0.001));

        return d;
    }
}

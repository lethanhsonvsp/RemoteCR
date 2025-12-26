namespace RemoteCR.Services.Can;

public static class ControlModuleEncoder
{
    /// <summary>
    /// Encode ControlModule – CAN ID 0x190 / 0x191
    /// Delta Wireless Charger CAN v1.14
    /// </summary>
    public static byte[] Encode(ControlModuleCommand c)
    {
        var d = new byte[8];

        // Demand_Voltage [0..19] – 0.001 V
        ulong v = (ulong)Math.Clamp(
            c.Demand_Voltage * 1000.0,
            0,
            (1UL << 20) - 1
        );
        CanBit.Set(d, 0, 20, v);

        // Demand_PowerStage1 [20]
        CanBit.Set(d, 20, 1, c.Demand_PowerStage1 ? 1UL : 0UL);

        // Demand_ClearFaults [21]
        CanBit.Set(d, 21, 1, c.Demand_ClearFaults ? 1UL : 0UL);

        // Demand_PowerStage2~10 [22..30]
        for (int i = 0; i < c.Demand_PowerStages.Length; i++)
        {
            CanBit.Set(d, 22 + i, 1,
                c.Demand_PowerStages[i] ? 1UL : 0UL);
        }

        // Demand_Current [32..49] – 0.001 A
        ulong iA = (ulong)Math.Clamp(
            c.Demand_Current * 1000.0,
            0,
            (1UL << 18) - 1
        );
        CanBit.Set(d, 32, 18, iA);

        return d;
    }
}

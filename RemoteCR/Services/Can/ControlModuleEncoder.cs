namespace RemoteCR.Services.Can;

public static class ControlModuleEncoder
{
    /// <summary>
    /// Encode Control Module Command (CAN ID 0x191)
    /// </summary>
    public static byte[] Encode(ControlModuleCommand c)
    {
        var d = new byte[8];

        /* ============================================================
         * DC VOLTAGE DEMAND
         * Bits [0..19], scale 0.001 V
         * ============================================================ */
        ulong voltage = (ulong)Math.Clamp(
            c.DemandVoltage_V * 1000.0,
            0,
            (1UL << 20) - 1
        );
        CanBit.Set(d, 0, 20, voltage);

        /* ============================================================
         * MASTER POWER ENABLE
         * Bit [20]
         * ============================================================ */
        CanBit.Set(d, 20, 1, c.PowerEnable ? 1UL : 0UL);

        /* ============================================================
         * CLEAR FAULT REQUEST
         * Bit [21]
         * ============================================================ */
        CanBit.Set(d, 21, 1, c.ClearFaults ? 1UL : 0UL);

        /* ============================================================
         * POWER STAGES
         * Bits [22..30]  (Stage 1 → bit 22)
         * ============================================================ */
        if (c.PowerStages != null)
        {
            int count = Math.Min(c.PowerStages.Length, 9);
            for (int i = 0; i < count; i++)
            {
                CanBit.Set(d, 22 + i, 1, c.PowerStages[i] ? 1UL : 0UL);
            }
        }

        /* ============================================================
         * DC CURRENT DEMAND
         * Bits [32..49], scale 0.001 A
         * ============================================================ */
        ulong current = (ulong)Math.Clamp(
            c.DemandCurrent_A * 1000.0,
            0,
            (1UL << 18) - 1
        );
        CanBit.Set(d, 32, 18, current);

        return d;
    }
}

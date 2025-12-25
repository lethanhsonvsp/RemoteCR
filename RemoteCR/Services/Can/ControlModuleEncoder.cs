namespace RemoteCR.Services.Can;

public static class ControlModuleEncoder
{
    /// <summary>
    /// Encode Control Module Command (CAN ID 0x191)
    /// Delta Wireless Charger – CAN v1.14 (Standalone)
    /// </summary>
    public static byte[] Encode(ControlModuleCommand c)
    {
        var d = new byte[8];

        /* ============================================================
         * Demand Voltage [0..19] – scale 0.001 V
         * ============================================================ */
        ulong voltage = (ulong)Math.Clamp(
            c.DemandVoltage_V * 1000.0,
            0,
            (1UL << 20) - 1
        );
        CanBit.Set(d, 0, 20, voltage);

        /* ============================================================
         * MASTER POWER ENABLE – BIT 20
         * ============================================================ */
        CanBit.Set(d, 20, 1, c.PowerEnable ? 1UL : 0UL);

        /* ============================================================
         * CLEAR FAULT – BIT 21
         * ============================================================ */
        CanBit.Set(d, 21, 1, c.ClearFaults ? 1UL : 0UL);

        /* ============================================================
         * POWER STAGE 1 – BIT 22 (QUAN TRỌNG)
         * ============================================================ */
        CanBit.Set(d, 22, 1, c.PowerStage1 ? 1UL : 0UL);

        /* ============================================================
         * Demand Current [32..49] – scale 0.001 A
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

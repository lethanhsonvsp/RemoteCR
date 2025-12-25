namespace RemoteCR.Services.Can;

/// <summary>
/// Mirror of Control Module Command (CAN ID 0x191)
/// Dùng để hiển thị TX command thực sự đang chạy trên bus
/// (không phải giá trị UI input)
/// </summary>
public sealed class ControlModuleCommandReport
{
    /// <summary>
    /// DC voltage demand (V)
    /// From bits [0..19], scale 0.001
    /// </summary>
    public double DemandVoltage_V { get; init; }

    /// <summary>
    /// DC current demand (A)
    /// From bits [32..49], scale 0.001
    /// </summary>
    public double DemandCurrent_A { get; init; }

    /// <summary>
    /// Global power enable
    /// Bit 20
    /// </summary>
    public bool PowerEnable { get; init; }

    /// <summary>
    /// Clear fault request
    /// Bit 21
    /// </summary>
    public bool ClearFaults { get; init; }

    /// <summary>
    /// Power stage 1 enable
    /// Bit 22
    /// </summary>
    public bool PowerStage1 { get; init; }

    /// <summary>
    /// Timestamp when this command was seen on CAN
    /// (UTC)
    /// </summary>
    public DateTime Timestamp { get; init; }

    /* ==================== HELPER ==================== */

    /// <summary>
    /// True nếu command đang yêu cầu bật công suất
    /// </summary>
    public bool IsPowerRequested =>
        PowerEnable && PowerStage1 && DemandCurrent_A > 0;

    /// <summary>
    /// True nếu command là OFF / idle
    /// </summary>
    public bool IsPowerOff =>
        !PowerEnable || DemandCurrent_A <= 0;

    /// <summary>
    /// Command đã stale (quá hạn) – không còn hợp lệ
    /// </summary>
    public bool IsStale(TimeSpan timeout) =>
        DateTime.UtcNow - Timestamp > timeout;
}

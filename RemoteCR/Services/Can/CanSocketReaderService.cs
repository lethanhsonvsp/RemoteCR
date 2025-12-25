namespace RemoteCR.Services.Can;

public class CanSocketReaderService
{
    private readonly SocketCan _can;
    private readonly object _lock = new();

    public ChargingSummaryModel Model { get; } = new();

    // ===== Events =====
    public event Action? OnChange;

    // ===== UI debounce =====
    private DateTime _lastNotify = DateTime.MinValue;

    public CanSocketReaderService(SocketCan can)
    {
        _can = can;
        _can.OnFrameReceived += OnFrame;
    }

    // ============================================================
    // RX HANDLER
    // ============================================================
    private void OnFrame(SocketCan.CanFrame frame)
    {
        lock (_lock)
        {
            CanMessageDecoder.Decode(frame.Id, frame.Data, Model);

            // 🔎 DEBUG: log TX mirror 0x191
            if (frame.Id == 0x191 && Model.ControlCmd != null)
            {
                Log191(Model.ControlCmd);
            }
        }

        // debounce UI update (max ~10 Hz)
        var now = DateTime.UtcNow;
        if ((now - _lastNotify).TotalMilliseconds < 100)
            return;

        _lastNotify = now;
        OnChange?.Invoke();
    }
    private static void Log191(ControlModuleCommandReport cmd)
    {
        Console.WriteLine(
            $"[CAN RX 0x191] " +
            $"V={cmd.DemandVoltage_V:F1}V, " +
            $"I={cmd.DemandCurrent_A:F1}A, " +
            $"PE={(cmd.PowerEnable ? 1 : 0)}, " +
            $"ST1={(cmd.PowerStage1 ? 1 : 0)}, " +
            $"CLR={(cmd.ClearFaults ? 1 : 0)}, " +
            $"@ {cmd.Timestamp:HH:mm:ss.fff} UTC"
        );
    }


    // ============================================================
    // TX MIRROR HELPERS (CHO UI)
    // ============================================================

    /// <summary>
    /// TX 0x191 còn alive không
    /// </summary>
    public bool IsTx191Alive(TimeSpan timeout)
    {
        var cmd = Model.ControlCmd;
        if (cmd == null) return false;

        return DateTime.UtcNow - cmd.Timestamp <= timeout;
    }

    /// <summary>
    /// TX đang yêu cầu bật công suất
    /// </summary>
    public bool IsPowerRequested(TimeSpan timeout)
    {
        var cmd = Model.ControlCmd;
        if (cmd == null) return false;

        if (DateTime.UtcNow - cmd.Timestamp > timeout)
            return false;

        return
            cmd.PowerEnable &&
            cmd.PowerStage1 &&
            cmd.DemandCurrent_A > 0;
    }

    /// <summary>
    /// TX đang ở trạng thái OFF / idle
    /// </summary>
    public bool IsPowerOff(TimeSpan timeout)
    {
        var cmd = Model.ControlCmd;
        if (cmd == null) return true;

        if (DateTime.UtcNow - cmd.Timestamp > timeout)
            return true;

        return !cmd.PowerEnable || cmd.DemandCurrent_A <= 0;
    }
}

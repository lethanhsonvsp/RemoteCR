namespace RemoteCR.Services.Can;

public class CanSocketReaderService
{
    private readonly SocketCan _can;
    private readonly object _lock = new();

    public ChargingSummaryModel Model { get; } = new();

    // Mirror TX command
    public ControlModuleCommandReport? ControlCmd => Model.ControlCmd;

    public event Action? OnChange;

    private DateTime _lastNotify = DateTime.MinValue;

    public CanSocketReaderService(SocketCan can)
    {
        _can = can;
        _can.OnFrameReceived += OnFrame;
    }

    private void OnFrame(SocketCan.CanFrame frame)
    {
        lock (_lock)
        {
            CanMessageDecoder.Decode(frame.Id, frame.Data, Model);
        }

        var now = DateTime.UtcNow;
        if ((now - _lastNotify).TotalMilliseconds < 100)
            return;

        _lastNotify = now;
        OnChange?.Invoke();
    }

    /* =====================================================
     * HELPERS – RẤT QUAN TRỌNG CHO UI
     * ===================================================== */

    /// <summary>
    /// TX 0x191 còn sống hay không (watchdog UI)
    /// </summary>
    public bool IsTx191Alive(TimeSpan timeout)
    {
        var cmd = Model.ControlCmd;
        return cmd != null && !cmd.IsStale(timeout);
    }

    /// <summary>
    /// Thực sự đang yêu cầu bật công suất
    /// (PowerEnable + Stage1 + Current > 0 + TX còn sống)
    /// </summary>
    public bool IsPowerRequested(TimeSpan timeout)
    {
        var cmd = Model.ControlCmd;
        return cmd != null
               && !cmd.IsStale(timeout)
               && cmd.IsPowerRequested;
    }
}

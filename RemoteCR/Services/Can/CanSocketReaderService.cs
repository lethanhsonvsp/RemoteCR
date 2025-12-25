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
        }

        // debounce UI update (max ~10 Hz)
        var now = DateTime.UtcNow;
        if ((now - _lastNotify).TotalMilliseconds < 100)
            return;

        _lastNotify = now;
        OnChange?.Invoke();
    }

}

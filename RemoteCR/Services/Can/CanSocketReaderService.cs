namespace RemoteCR.Services.Can;

public class CanSocketReaderService
{
    private readonly SocketCan _can;
    private readonly object _lock = new();

    public ChargingSummaryModel Model { get; } = new();

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
            var baseId = frame.Id & 0x7F0;
            CanMessageDecoder.Decode(baseId, frame.Data, Model);
        }

        // 🔒 debounce UI update (max 10 Hz)
        var now = DateTime.UtcNow;
        if ((now - _lastNotify).TotalMilliseconds < 100)
            return;

        _lastNotify = now;
        OnChange?.Invoke();
    }
}

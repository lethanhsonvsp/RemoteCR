namespace RemoteCR.Services.Can;

public class CanSocketReaderService
{
    private readonly SocketCan _can;
    private readonly object _lock = new();

    public ChargingSummaryModel Model { get; } = new();

    // ✅ Mirror TX command (0x191)
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
            // ✅ Decode chỉ update Model
            CanMessageDecoder.Decode(frame.Id, frame.Data, Model);
        }

        // 🔒 debounce UI update (max 10 Hz)
        var now = DateTime.UtcNow;
        if ((now - _lastNotify).TotalMilliseconds < 100)
            return;

        _lastNotify = now;
        OnChange?.Invoke();
    }
}

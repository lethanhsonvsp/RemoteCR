namespace RemoteCR.Services.Can;

public class CanSocketWriterService : IDisposable
{
    private readonly SocketCan _can;
    private readonly Timer _timer;

    // 🔒 Command state duy nhất
    private readonly ControlModuleCommand _cmd = new();
    private readonly object _lock = new();

    public CanSocketWriterService(SocketCan can)
    {
        _can = can;

        // ⏱ TX 100ms – đúng theo protocol
        _timer = new Timer(_ => Send(), null, 0, 100);
    }

    // ✅ CÁCH DUY NHẤT ĐỂ UPDATE COMMAND
    public void Update(Action<ControlModuleCommand> update)
    {
        lock (_lock)
        {
            update(_cmd);
        }
    }

    private void Send()
    {
        lock (_lock)
        {
            // TX Control Module Command – 0x191
            _can.Send(0x191, ControlModuleEncoder.Encode(_cmd));
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}

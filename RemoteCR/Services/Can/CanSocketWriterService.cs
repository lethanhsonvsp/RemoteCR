namespace RemoteCR.Services.Can;

public class CanSocketWriterService : IDisposable
{
    private readonly SocketCan _can;
    private readonly Timer _timer;
    private ControlModuleCommand _cmd = new();

    public CanSocketWriterService(SocketCan can)
    {
        _can = can;
        _timer = new Timer(_ => Send(), null, 0, 100);
    }

    public void SetCommand(ControlModuleCommand cmd) => _cmd = cmd;

    private void Send()
    {
        _can.Send(0x190, ControlModuleEncoder.Encode(_cmd));
    }

    public void Dispose() => _timer.Dispose();
}

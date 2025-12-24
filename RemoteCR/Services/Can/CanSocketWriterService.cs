namespace RemoteCR.Services.Can;

public class CanSocketWriterService : IDisposable
{
    private readonly SocketCan _can;
    private readonly Timer _timer;
    private ControlModuleCommand _cmd = new();

    public CanSocketWriterService(SocketCan can)
    {
        _can = can;

        // 10 Hz là chuẩn cho Delta
        _timer = new Timer(_ => Send(), null, 0, 100);
    }

    public void SetCommand(ControlModuleCommand cmd)
    {
        _cmd = cmd;
    }

    private void Send()
    {
        // 🔴 BẮT BUỘC: 0x191
        _can.Send(0x191, ControlModuleEncoder.Encode(_cmd));
    }

    public void Dispose() => _timer.Dispose();
}

namespace RemoteCR.Services.Can;

public class CanSocketWriterService : IDisposable
{
    private readonly SocketCan _can;
    private readonly Timer _timer;
    private ControlModuleCommand _cmd = new();

    public CanSocketWriterService(SocketCan can)
    {
        _can = can;

        // Gửi lệnh mỗi 100 ms (bắt buộc theo tài liệu)
        _timer = new Timer(_ => Send(), null, 0, 100);
    }

    public void SetCommand(ControlModuleCommand cmd)
    {
        _cmd = cmd;
    }

    private void Send()
    {
        // TX Control Frame = 0x191
        _can.Send(0x191, ControlModuleEncoder.Encode(_cmd));
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}

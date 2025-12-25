namespace RemoteCR.Services.Can;

public sealed class CanSocketWriterService : IDisposable
{
    private readonly SocketCan _can;
    private readonly Timer _timer;
    private readonly object _lock = new();

    private readonly ControlModuleCommand _cmd = new();

    private bool _txEnabled = false;

    public CanSocketWriterService(SocketCan can)
    {
        _can = can;

        // 🔒 KHỞI TẠO TIMER NHƯNG KHÔNG CHẠY
        _timer = new Timer(_ => OnTick(), null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    /* ================= PUBLIC API ================= */

    /// <summary>
    /// Update nội dung frame 0x191
    /// </summary>
    public void Update(Action<ControlModuleCommand> update)
    {
        lock (_lock)
        {
            update(_cmd);
        }
    }

    /// <summary>
    /// ▶️ BẬT vòng lặp gửi 0x191 (100 ms)
    /// </summary>
    public void StartTx()
    {
        if (!_can.IsConnected)
            return;

        _txEnabled = true;

        // resume timer
        _timer.Change(0, 100);
    }

    /// <summary>
    /// ⛔ HỦY vòng lặp gửi 0x191 (KHÔNG gửi nữa)
    /// </summary>
    public void StopTx()
    {
        _txEnabled = false;

        // pause timer
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public bool IsTxActive => _txEnabled;

    /* ================= CORE LOOP ================= */

    private void OnTick()
    {
        if (!_txEnabled)
            return;

        if (!_can.IsConnected)
            return;

        lock (_lock)
        {
            _can.Send(0x191, ControlModuleEncoder.Encode(_cmd));
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}

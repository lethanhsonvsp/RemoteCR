namespace RemoteCR.Services.Can;

public sealed class CanSocketWriterService : IDisposable
{
    private readonly SocketCan _can;
    private readonly Timer _timer;

    private readonly ControlModuleCommand _cmd = new();
    private readonly object _lock = new();

    private TxState _state = TxState.Idle;

    private enum TxState
    {
        Idle,
        Active,
        Stopping
    }

    public CanSocketWriterService(SocketCan can)
    {
        _can = can;

        // TX tick 100 ms theo protocol
        _timer = new Timer(_ => OnTick(), null, 100, 100);
    }

    /* ================= PUBLIC API ================= */

    /// <summary>
    /// Update nội dung command (Voltage / Current / Flags)
    /// </summary>
    public void Update(Action<ControlModuleCommand> update)
    {
        lock (_lock)
        {
            update(_cmd);
        }
    }

    /// <summary>
    /// Bắt đầu gửi 0x191
    /// </summary>
    public void StartTx()
    {
        if (!_can.IsConnected)
            return;

        _state = TxState.Active;
    }

    /// <summary>
    /// Dừng TX – gửi 1 frame OFF cuối
    /// </summary>
    public void StopTx()
    {
        if (_state == TxState.Active)
            _state = TxState.Stopping;
    }

    public bool IsTxActive => _state == TxState.Active;

    /* ================= CORE LOOP ================= */

    private void OnTick()
    {
        if (!_can.IsConnected)
        {
            _state = TxState.Idle;
            return;
        }

        switch (_state)
        {
            case TxState.Idle:
                return;

            case TxState.Active:
                SendCurrentCommand();
                return;

            case TxState.Stopping:
                SendOffCommand();
                _state = TxState.Idle;
                return;
        }
    }

    /* ================= SEND HELPERS ================= */

    private void SendCurrentCommand()
    {
        lock (_lock)
        {
            // Guard: không gửi command rỗng
            if (_cmd.DemandVoltage_V <= 0 || _cmd.DemandCurrent_A <= 0)
                return;

            _can.Send(0x191, ControlModuleEncoder.Encode(_cmd));
        }
    }

    private void SendOffCommand()
    {
        var off = new ControlModuleCommand
        {
            DemandVoltage_V = _cmd.DemandVoltage_V,
            DemandCurrent_A = 0,
            PowerEnable = false,
            PowerStage1 = false
        };

        _can.Send(0x191, ControlModuleEncoder.Encode(off));
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}

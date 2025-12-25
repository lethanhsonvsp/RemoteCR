using System;
using System.Threading;
using RemoteCR.Services.Can;

namespace RemoteCR.Services.Can;

public sealed class CanSocketWriterService : IDisposable
{
    private readonly SocketCan _can;
    private readonly Timer _timer;
    private readonly object _lock = new();

    private readonly ControlModuleCommand _cmd = new();

    private TxState _state = TxState.Idle;

    private enum TxState
    {
        Idle,       // chưa gửi gì
        Active,     // gửi đều 0x191
        Stopping    // gửi OFF frame cuối
    }

    public CanSocketWriterService(SocketCan can)
    {
        _can = can;

        // Timer 100 ms theo CAN spec
        _timer = new Timer(_ => OnTick(), null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    /* ============================================================
     * PUBLIC API
     * ============================================================ */

    /// <summary>
    /// Update nội dung frame 0x191 (thread-safe)
    /// </summary>
    public void Update(Action<ControlModuleCommand> update)
    {
        lock (_lock)
        {
            update(_cmd);
        }
    }

    /// <summary>
    /// ▶️ Bắt đầu gửi 0x191 định kỳ (100 ms)
    /// </summary>
    public void StartTx()
    {
        if (!_can.IsConnected)
            return;

        lock (_lock)
        {
            _state = TxState.Active;
        }

        _timer.Change(0, 100);
    }

    /// <summary>
    /// ⛔ Dừng TX – gửi 1 frame OFF CUỐI (rất quan trọng)
    /// </summary>
    public void StopTx()
    {
        lock (_lock)
        {
            if (_state == TxState.Active)
                _state = TxState.Stopping;
        }
    }

    public bool IsTxActive => _state == TxState.Active;

    /* ============================================================
     * CORE LOOP
     * ============================================================ */

    private void OnTick()
    {
        if (!_can.IsConnected)
        {
            _state = TxState.Idle;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
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
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                return;
        }
    }

    /* ============================================================
     * SEND HELPERS
     * ============================================================ */

    /// <summary>
    /// Gửi frame 0x191 hiện tại
    /// (đã encode đúng bit 20 + 22)
    /// </summary>
    private void SendCurrentCommand()
    {
        ControlModuleCommand snapshot;

        lock (_lock)
        {
            snapshot = Clone(_cmd);
        }

        // Guard: không gửi frame rỗng
        if (snapshot.DemandVoltage_V <= 0 ||
            snapshot.DemandCurrent_A <= 0)
            return;

        _can.Send(0x191, ControlModuleEncoder.Encode(snapshot));
    }

    /// <summary>
    /// Gửi 1 frame OFF cuối cùng
    /// → tránh watchdog / sạc treo
    /// </summary>
    private void SendOffCommand()
    {
        ControlModuleCommand off;

        lock (_lock)
        {
            off = new ControlModuleCommand
            {
                DemandVoltage_V = _cmd.DemandVoltage_V,
                DemandCurrent_A = 0,

                PowerEnable = false,   // 🔴 MASTER OFF
                PowerStage1 = false,   // 🟢 STAGE OFF
                ClearFaults = false
            };
        }

        _can.Send(0x191, ControlModuleEncoder.Encode(off));
    }

    private static ControlModuleCommand Clone(ControlModuleCommand c)
    {
        return new ControlModuleCommand
        {
            DemandVoltage_V = c.DemandVoltage_V,
            DemandCurrent_A = c.DemandCurrent_A,
            PowerEnable = c.PowerEnable,
            PowerStage1 = c.PowerStage1,
            ClearFaults = c.ClearFaults
        };
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}

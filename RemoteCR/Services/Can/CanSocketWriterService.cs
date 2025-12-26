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
        Active,     // gửi 0x191 định kỳ
        Stopping    // gửi 1 frame OFF cuối
    }

    public CanSocketWriterService(SocketCan can)
    {
        _can = can;

        // KHÔNG auto start – chỉ chạy khi StartTx()
        _timer = new Timer(_ => OnTick(),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    /* ============================================================
     * PUBLIC API
     * ============================================================ */

    /// <summary>
    /// Update nội dung ControlModuleCommand (thread-safe)
    /// </summary>
    public void Update(Action<ControlModuleCommand> update)
    {
        lock (_lock)
        {
            update(_cmd);
        }
    }

    /// <summary>
    /// ▶️ Bắt đầu gửi 0x191 định kỳ (100 ms – watchdog)
    /// </summary>
    public void StartTx()
    {
        if (!_can.IsConnected)
            return;

        lock (_lock)
        {
            _state = TxState.Active;
        }

        // tick ngay + 100ms
        _timer.Change(0, 100);
    }

    /// <summary>
    /// ⛔ Dừng TX – gửi 1 frame OFF CUỐI
    /// (rất quan trọng để tránh sạc treo)
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
    /// Gửi frame ControlModule (0x191)
    /// – đúng Demand_PowerStage1 + watchdog
    /// </summary>
    private void SendCurrentCommand()
    {
        ControlModuleCommand snapshot;

        lock (_lock)
        {
            snapshot = Clone(_cmd);
        }

        // Guard an toàn
        if (snapshot.Demand_Voltage <= 0 ||
            snapshot.Demand_Current <= 0)
            return;

        // ❗ BẮT BUỘC bật Stage1
        snapshot.Demand_PowerStage1 = true;

        _can.Send(0x191, ControlModuleEncoder.Encode(snapshot));
    }

    /// <summary>
    /// Gửi 1 frame OFF CUỐI
    /// – Stage1 = 0
    /// – Current = 0
    /// </summary>
    private void SendOffCommand()
    {
        ControlModuleCommand off;

        lock (_lock)
        {
            off = new ControlModuleCommand
            {
                Demand_Voltage = _cmd.Demand_Voltage,
                Demand_Current = 0,

                Demand_PowerStage1 = false,
                Demand_ClearFaults = false
            };
        }

        _can.Send(0x191, ControlModuleEncoder.Encode(off));
    }

    private static ControlModuleCommand Clone(ControlModuleCommand c)
    {
        var copy = new ControlModuleCommand
        {
            Demand_Voltage = c.Demand_Voltage,
            Demand_Current = c.Demand_Current,
            Demand_PowerStage1 = c.Demand_PowerStage1,
            Demand_ClearFaults = c.Demand_ClearFaults
        };

        for (int i = 0; i < copy.Demand_PowerStages.Length; i++)
            copy.Demand_PowerStages[i] = c.Demand_PowerStages[i];

        return copy;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
